# Fix record: race-free rate overlap enforcement

Status: implemented and manually verified.

## Problem

Rate creation and update used a check-then-insert/update sequence:

```text
HasOverlapAsync(...)  -> no overlap
AddAsync(...)         -> insert
```

Two concurrent requests could both pass the check before either inserted, producing two
overlapping rate periods for the same room type. The database had no constraint that could
express "these date ranges must not overlap".

During the verification pass, concurrent duplicate room creation also exposed a related
unique-index race, which was fixed separately by translating SQL Server errors 2601/2627 to
`409 Conflict`.

## Attempt 1: serializable transaction (rejected)

The first implementation wrapped the overlap check and write in a `SERIALIZABLE` transaction.
This prevented overlap but caused SQL Server deadlocks under contention:

```text
10 concurrent creates -> 1 x 201, 7 x 409, 2 x 500
Error Number: 1205, "Transaction was deadlocked ... chosen as the deadlock victim"
```

The 500 responses were not acceptable, and retry logic around an explicit transaction would
have required detaching tracked entities and restarting the transaction.

## Final design: application lock per room type

Rate writes are serialized with a transaction-scoped SQL Server application lock:

```sql
EXEC sp_getapplock
  @Resource = 'RoomRate:{roomTypeId}',
  @LockMode = 'Exclusive',
  @LockOwner = 'Transaction',
  @LockTimeout = 10000;
```

The repository method then:

1. opens a transaction,
2. acquires the lock for the room type,
3. checks overlap inside the transaction,
4. inserts or updates,
5. commits (which releases the lock).

If the lock cannot be acquired within 10 seconds, the repository throws `ConflictException`
(`409`) instead of returning a server error.

Because every writer takes the same lock key in the same order, overlapping writes for one
room type are serialized, while writes for different room types proceed independently.

## Repository API

`IRoomRateRepository` now exposes:

```csharp
Task<bool> TryAddAsync(RoomRate rate, CancellationToken cancellationToken);
Task<bool> TryUpdateAsync(RoomRate rate, CancellationToken cancellationToken);
```

Both return `false` when an overlap exists; the service maps that to `409 Conflict`:

```csharp
if (!await _rates.TryAddAsync(rate, cancellationToken))
{
    throw new ConflictException("A rate for this room type already covers part of the requested period.");
}
```

The previous `HasOverlapAsync` / `AddAsync` / `UpdateAsync` combination was removed from the
interface so callers cannot bypass the lock.

## Files changed

| File | Change |
|---|---|
| `RoomService.Business/Interfaces/IRoomRateRepository.cs` | replaced check+write methods with `TryAddAsync` / `TryUpdateAsync` |
| `RoomService.DataAccess/Repositories/RoomRateRepository.cs` | transaction + `sp_getapplock` + atomic overlap check + write |
| `RoomService.Business/Services/RoomRateService.cs` | uses the atomic methods; `false` -> `ConflictException` |

No schema migration was needed.

## Verified behaviour

Sequential checks:

| Scenario | Result |
|---|---|
| Adjacent periods (`ValidTo` + 1 day) | 201 |
| Overlapping create | 409 |
| Overlapping update | 409 |
| Unknown room type | 404 |
| Invalid range / amount <= 0 | 400 |

Concurrency check, 10 simultaneous creates with the same overlapping period:

```text
1 x 201 Created
9 x 409 Conflict
0 x 500 Internal Server Error
1 row stored
```

The full Room regression suite passed 90/90 after the change.

## Operational notes

- The lock is SQL Server-specific (`sp_getapplock`), matching the project's database choice.
- The lock key is `RoomRate:{roomTypeId}`. All writers must use it; the repository interface
  no longer exposes a path that skips it.
- A lock timeout returns `409`, not `500`. The client can retry.
- The lock is transaction-scoped and is released automatically on commit or rollback.
- Availability reads do not take the lock; they only read committed rate rows.
