# Database

## Ownership

Each service owns exactly one SQL Server database and its own migrations.
No service reads another service's database and there are no cross-database foreign keys.

| Service | Database | DbContext |
|---|---|---|
| Auth | `HotelAuthDb` | `AuthDbContext` |
| Room | `HotelRoomDb` | `RoomDbContext` |
| Guest | `HotelGuestDb` | `GuestDbContext` |
| Reservation | `HotelReservationDb` | `ReservationDbContext` |
| Operations | `HotelOperationsDb` | `OperationsDbContext` |

All databases run on the same SQL Server instance (Docker, host port 1434), but remain
logically independent.

## Conventions

- EF Core Fluent API with `IEntityTypeConfiguration<T>` per entity.
- Primary keys: `int` identity for business entities, `string` (GUID) for Identity users.
- Money: `decimal(18,2)`.
- Tax rate: `decimal(5,4)` (for example `0.1300` = 13%).
- Strings: explicit maximum lengths; required fields are non-nullable.
- Enums stored as strings for readability.
- Indexes on lookup columns (email, dates, status) and unique constraints where required.
- `DeleteBehavior.Restrict` for referenced data; no cascading deletes across aggregates.
- Migrations live in each service's `DataAccess/Migrations` folder.

## Auth database (`HotelAuthDb`)

ASP.NET Core Identity schema plus:

| Table | Purpose |
|---|---|
| `AspNetUsers` | Users; extra columns `FullName`, `IsActive`, `CreatedAtUtc`; Identity `SecurityStamp` is the token-revocation version |
| `AspNetRoles` | Roles: Owner, Manager, Receptionist |
| `AspNetUserRoles` | User-to-role assignments |

Indexes: unique normalized email (Identity default), unique normalized username.

## Planned databases

### `HotelRoomDb` (implemented in Phase 2)

| Entity | Key fields | Relationships |
|---|---|---|
| `RoomType` | Name, Description, BasePrice | 1 → many `Room`, 1 → many `RoomRate` |
| `Room` | Number (unique), Floor, Status | many → 1 `RoomType`; 1 → many `RoomBlock` |
| `RoomRate` | Amount, ValidFrom, ValidTo | many → 1 `RoomType` |
| `RoomBlock` | FromDate, ToDate, Reason | many → 1 `Room` |

Implemented migration: `20261028205518_InitialCreate`. Unique indexes on `RoomTypes.Name`
and `Rooms.Number`; room blocks cascade on room deletion. Rate periods for a room type cannot
overlap: writes are serialized per room type with a transaction-scoped SQL Server application
lock (`sp_getapplock`) before the overlap check, so concurrent requests cannot create
overlapping periods. Availability is physical: room status and blocks only, reservations live
in `HotelReservationDb`.

### `HotelGuestDb` (Phase 3)

| Entity | Key fields |
|---|---|
| `Guest` | FirstName, LastName, Email, Phone, Address, IsActive, CreatedAtUtc |

Unique index on email; index on last name and phone for search.

### `HotelReservationDb` (Phase 4)

| Entity | Key fields | Notes |
|---|---|---|
| `Reservation` | GuestId, GuestName, GuestPhone, RoomId, RoomNumber, CheckIn, CheckOut, NightlyRate, Status, CreatedAtUtc | Aggregate root; IDs are references only, no cross-DB FK |

Index on `(RoomId, CheckIn, CheckOut)` and on `Status`.

### `HotelOperationsDb` (Phase 5)

| Entity | Key fields | Notes |
|---|---|---|
| `Staff` | FullName, Position, Phone, HireDate, Salary, UserId (nullable), IsActive | `UserId` is an Auth reference only |
| `InventoryItem` | Name, Category, Unit, QuantityInStock, ReorderLevel, UnitCost | |
| `StockMovement` | InventoryItemId, Quantity, Type, Note, CreatedAtUtc | |
| `Bill` | ReservationId, GuestId, GuestName, Subtotal, TaxRate, TaxAmount, Total, Status, CreatedAtUtc | Aggregate root |
| `BillItem` | BillId, Description, Quantity, UnitPrice, LineTotal | Price snapshot |
| `Payment` | BillId, Amount, Method, Status, Reference, IdempotencyKey, PaidAtUtc | Unique `IdempotencyKey` |

## Migrations

Apply migrations per service with the environment loaded:

```bash
set -a; source .env.local; set +a
dotnet ef database update \
  --project src/Services/AuthService/AuthService.DataAccess \
  --startup-project src/Services/AuthService/AuthService.API
```

Destructive operations (drop database, reset migrations) require explicit approval.
