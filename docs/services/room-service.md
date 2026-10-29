# Room Service

Status: **implemented and manually verified** (Phase 2).

- Port: `7002`
- Database: `HotelRoomDb`
- Swagger: `http://localhost:7002/swagger`
- Framework: `net10.0`, ASP.NET Core, EF Core 10, SQL Server, JWT

## 1. Purpose and responsibilities

The Room Service owns physical room data and pricing:

- Room types (name, description, base price, max occupancy, active flag).
- Rooms (number, floor, status, room type).
- Room rates per room type and validity period.
- Room maintenance blocks.
- Physical availability search.
- Room status updates (used by the front desk and by Reservation check-in/out).
- Internal endpoints consumed by the Reservation Service.

It does **not** know about reservations. Bookable availability (removing rooms that already
have an active reservation) is computed by the Reservation Service from its own data.

## 2. Folder and project structure

```text
src/Services/RoomService/
├── RoomService.Contracts/
│   ├── DTOs/{RoomType,Room,RoomRate,RoomBlock,RoomAvailability,InternalRoom}ResponseDto.cs
│   ├── Enums/RoomStatus.cs
│   └── Requests/{CreateRoomType,UpdateRoomType,CreateRoom,UpdateRoom,
│                 UpdateRoomStatus,CreateRoomRate,UpdateRoomRate,CreateRoomBlock}RequestDto.cs
├── RoomService.Business/
│   ├── Authorization/Roles.cs
│   ├── Entities/RoomEntities.cs
│   ├── Exceptions/AppException.cs
│   ├── Interfaces/
│   │   ├── IRoomTypeRepository.cs, IRoomRepository.cs
│   │   ├── IRoomRateRepository.cs, IRoomBlockRepository.cs
│   │   └── IRoomTypeService.cs, IRoomManagementService.cs,
│   │       IRoomRateService.cs, IRoomBlockService.cs
│   ├── Mapping/RoomMapper.cs
│   ├── Rules/RoomRules.cs
│   └── Services/{RoomTypeService,RoomManagementService,RoomRateService,RoomBlockService}.cs
├── RoomService.DataAccess/
│   ├── Configurations/{RoomType,Room,RoomRate,RoomBlock}Configuration.cs
│   ├── Context/RoomDbContext.cs
│   ├── Migrations/                    InitialCreate + snapshot
│   └── Repositories/{RoomType,Room,RoomRate,RoomBlock}Repository.cs
└── RoomService.API/
    ├── Configuration/{AuthenticationConfiguration,SwaggerConfiguration,InternalApiSettings}.cs
    ├── Controllers/{RoomTypes,Rooms,Rates,RoomBlocks,InternalRooms}Controller.cs
    ├── Middleware/{ExceptionHandlingMiddleware,InternalApiKeyMiddleware}.cs
    ├── Security/TokenStatusValidator.cs
    ├── Program.cs
    ├── appsettings.json
    └── Properties/launchSettings.json
```

### References

| Project | References | NuGet |
|---|---|---|
| Contracts | — | — |
| Business | Contracts | `Microsoft.Extensions.Logging.Abstractions` 10.0.14 |
| DataAccess | Business, Contracts | EF Core SqlServer 10.0.14, Design 10.0.14, Pkcs 10.0.14 |
| API | Business, DataAccess, Contracts | JwtBearer 10.0.14, Swashbuckle 10.3.1, Design 10.0.14, Pkcs 10.0.14 |

Entities live in `RoomService.Business/Entities` as plain POCOs, so Business has no EF Core
dependency while DataAccess reuses the same types for Fluent API and repositories.

## 3. Configuration

| Variable | Purpose |
|---|---|
| `ConnectionStrings__RoomDb` | `HotelRoomDb` |
| `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey` | token validation, identical to Auth |
| `InternalApi__Key` | shared key for `/internal/*` and for calling Auth, at least 32 characters |
| `Services__AuthBaseUrl` | Auth base URL for token-status checks (for example `http://localhost:7001`) |

`Program.cs` refuses to start when the connection string, JWT settings, internal key or
Auth base URL are missing. `appsettings.json` contains only non-secret defaults.

## 4. Request flow

```text
HTTP request
  -> ExceptionHandlingMiddleware
  -> InternalApiKeyMiddleware (only for /internal/*)
  -> JWT authentication + role authorization
  -> OnTokenValidated -> Auth /internal/users/{id}/token-status (active + stamp)
  -> Controller
  -> Service interface (Business)
  -> RoomRules (date/amount/status validation)
  -> Repository interface
  -> Repository implementation (DataAccess)
  -> RoomDbContext -> SQL Server
  -> entity -> RoomMapper -> response DTO
```

## 5. Contracts

### Enums

`RoomStatus`: `Available`, `Occupied`, `Cleaning`, `OutOfOrder`. Serialized as strings
(`JsonStringEnumConverter`); an unknown value in JSON produces `400`.

### Requests

| DTO | Validation |
|---|---|
| `CreateRoomTypeRequestDto` | Name required ≤100; description ≤500; base price 0.01–1,000,000; occupancy 1–20 |
| `UpdateRoomTypeRequestDto` | same + `IsActive` |
| `CreateRoomRequestDto` | number required ≤10; floor −5..200; room type id ≥1; status |
| `UpdateRoomRequestDto` | number, floor, room type id |
| `UpdateRoomStatusRequestDto` | status |
| `CreateRoomRateRequestDto` | room type id, amount, `ValidFrom`, `ValidTo` |
| `UpdateRoomRateRequestDto` | amount, `ValidFrom`, `ValidTo` |
| `CreateRoomBlockRequestDto` | room id, `FromDate`, `ToDate`, reason required ≤200 |

### Responses

| DTO | Fields |
|---|---|
| `RoomTypeResponseDto` | id, name, description, base price, max occupancy, active, created |
| `RoomResponseDto` | id, number, floor, status, room type id/name, base price, created |
| `RoomRateResponseDto` | id, room type id/name, amount, validity, created |
| `RoomBlockResponseDto` | id, room id/number, period, reason, created |
| `RoomAvailabilityResponseDto` | id, number, floor, status, room type, nightly rate |
| `InternalRoomResponseDto` | the above plus base price, blocked flag, available flag |

## 6. Business layer

### `Entities/RoomEntities.cs`

Four POCOs: `RoomType` (rooms, rates), `Room` (room type, blocks), `RoomRate` (room type),
`RoomBlock` (room). No EF attributes.

### `Rules/RoomRules.cs`

```csharp
EnsureStayDates(checkIn, checkOut)      // checkOut > checkIn
EnsureInclusiveRange(from, to, label)   // to >= from, used for rates and blocks
EnsureStatusDefined(status)             // Enum.IsDefined
EnsureAmount(amount)                    // > 0
```

### `Services/RoomTypeService.cs`

- Create: trims the name, rejects duplicates (409), stores the entity.
- Update: 404 when missing, duplicate name check excluding itself (409), copies fields.
- Delete: 404 when missing, 409 when rooms still reference the type.
- Reads: ordered by name.

### `Services/RoomManagementService.cs`

- `GetAllAsync(roomTypeId?, status?)` filters in the repository.
- `CreateAsync`: validates status, room type exists (404), unique number (409), saves and
  reloads with the room type for the response.
- `UpdateAsync`: 404, room type exists, unique number excluding itself, saves, reloads.
- `UpdateStatusAsync`: validates the enum, saves the new status.
- `DeleteAsync`: 404 when missing; room blocks are removed by cascade.
- `SearchAvailabilityAsync`: validates stay dates, asks the repository for physically
  available rooms, resolves a nightly rate per room type, maps the result.
- `GetInternalRoomAsync`: validates dates, loads the room, checks blocks for the range,
  resolves the rate and returns availability flags.
- `ResolveNightlyRatesAsync`: one rate lookup per distinct room type; falls back to
  `RoomType.BasePrice` when no rate covers the check-in date.

### `Services/RoomRateService.cs`

- Reads by room type (404 when the type is missing) and by id.
- Create/Update: inclusive validity check, amount > 0, room type existence, and overlapping
  periods for the same room type are rejected with 409.
- Delete: 404 when missing.

### `Services/RoomBlockService.cs`

- List by room (404 when the room is missing).
- Create: inclusive range validation, room existence, saves and reloads with the room.
- Delete: 404 when missing.

### `Mapping/RoomMapper.cs`

Explicit mapping from entities to DTOs. `ToInternalDto` computes
`IsAvailable = Status != OutOfOrder && !IsBlocked`.

## 7. Data access layer

### `Context/RoomDbContext.cs`

Four `DbSet`s (`RoomTypes`, `Rooms`, `RoomRates`, `RoomBlocks`) and
`ApplyConfigurationsFromAssembly`.

### Fluent API

| Entity | Configuration |
|---|---|
| `RoomType` | table `RoomTypes`; name required 100 + **unique index**; description 500; base price `decimal(18,2)`; active default `true` |
| `Room` | table `Rooms`; number required 10 + **unique index**; status stored as string ≤20; FK to `RoomType` with `Restrict`; blocks `Cascade`; indexes on room type and status |
| `RoomRate` | table `RoomRates`; amount `decimal(18,2)`; `DateOnly` → `date`; FK to `RoomType` `Cascade`; index `(RoomTypeId, ValidFrom, ValidTo)` |
| `RoomBlock` | table `RoomBlocks`; reason required 200; index `(RoomId, FromDate, ToDate)` |

### Repositories

- `RoomTypeRepository`: list, get, unique-name check, has-rooms check, add/update/delete.
  Unique-index violations (SQL Server 2601/2627) are translated to `409 Conflict`.
- `RoomRepository`: filtered list with room type included; get with room type; unique-number
  check; availability query; add/update/delete. Unique-index violations (SQL Server
  2601/2627) are translated to `409 Conflict`, so a concurrent duplicate insert does not
  surface as `500`.
- `RoomRateRepository`: list by type, get, applicable rate for a date
  (`ValidFrom <= date <= ValidTo`, latest `ValidFrom` wins), CRUD. `TryAddAsync` and
  `TryUpdateAsync` open a transaction, take a transaction-scoped SQL Server application lock
  (`sp_getapplock`) on `RoomRate:{roomTypeId}`, check overlap, write, and commit. The lock
  serializes concurrent writers for the same room type, so no overlapping periods can be
  created and no serializable deadlocks occur. See
  [the rate-concurrency fix record](../fixes/rate-overlap-concurrency.md).
- `RoomBlockRepository`: list by room, get, blocked-range check, add/delete.

Availability query (physical):

```csharp
.Where(room => room.RoomType.IsActive)
.Where(room => room.Status != RoomStatus.OutOfOrder)
.Where(room => !room.Blocks.Any(block => block.FromDate < checkOut && block.ToDate > checkIn))
```

The block overlap uses the same half-open rule as reservations:
`block.FromDate < checkOut && block.ToDate > checkIn`.

Rate overlap uses inclusive validity ranges: `existing.ValidFrom <= requested.ValidTo &&
existing.ValidTo >= requested.ValidFrom`. The app lock makes the check-and-insert atomic.

### Migration

`20261028205518_InitialCreate` creates `RoomTypes`, `Rooms`, `RoomRates`, `RoomBlocks`
with the constraints above. Applied to `HotelRoomDb`.

## 8. API layer

### `Program.cs`

1. Controllers with `JsonStringEnumConverter`; Problem Details.
2. `RoomDbContext` from `ConnectionStrings:RoomDb`.
3. JWT settings validation; internal key validation (≥32 chars); `Services__AuthBaseUrl`
   validation.
4. Registers four repositories and four services as scoped; settings as singletons.
5. Registers a typed `HttpClient` for `TokenStatusValidator` with a 5-second timeout.
6. Pipeline: exception middleware → internal key middleware → Swagger (Development) →
   authentication → authorization → controllers.

### `Middleware/InternalApiKeyMiddleware.cs`

- Applies only to paths starting with `/internal`.
- Reads `X-Internal-Api-Key` and compares it with `InternalApi__Key` using a fixed-time
  comparison.
- Missing or wrong key: `401` Problem Details. Public routes pass through untouched.

### `Middleware/ExceptionHandlingMiddleware.cs`

Maps `AppException` to its status (400/404/409) and anything else to `500`, writing
`application/problem+json`. No stack traces are exposed.

### Controllers

| Controller | Route | Access |
|---|---|---|
| `RoomTypesController` | `/api/room-types` | read: any authenticated; write: Owner/Manager |
| `RoomsController` | `/api/rooms` | read + status: any authenticated; create/update/delete: Owner/Manager |
| `RatesController` | `/api/rates` | Owner/Manager; `roomTypeId` is a required query parameter (`BindRequired`, otherwise 400) |
| `RoomBlocksController` | `/api/room-blocks` | Owner/Manager |
| `InternalRoomsController` | `/internal/rooms` | internal API key only (no JWT) |

Internal endpoints are deliberately excluded from the gateway in Phase 6 and are hidden from
this service's Swagger document with `[ApiExplorerSettings(IgnoreApi = true)]`. They remain
callable with `X-Internal-Api-Key`. See
[the internal-Swagger fix record](../fixes/internal-swagger-exposure.md).

## 9. Dependency injection

```text
RoomDbContext              scoped
IRoomTypeRepository     -> RoomTypeRepository     scoped
IRoomRepository         -> RoomRepository         scoped
IRoomRateRepository     -> RoomRateRepository     scoped
IRoomBlockRepository    -> RoomBlockRepository    scoped
IRoomTypeService        -> RoomTypeService        scoped
IRoomManagementService  -> RoomManagementService  scoped
IRoomRateService        -> RoomRateService        scoped
IRoomBlockService       -> RoomBlockService       scoped
JwtSettings, InternalApiSettings                  singleton
TokenStatusValidator                              typed HttpClient (5 s timeout)
```

Controllers depend only on service interfaces.

## 10. JWT and RBAC

- Every public endpoint requires a valid JWT (issuer, audience, signature, expiry).
- `OnTokenValidated` calls Auth's internal token-status endpoint with the user id and `stamp`
  claim. Missing user, inactive account, changed stamp, Auth error or timeout -> `401`
  (fail closed). This makes deactivation, deletion and role changes effective immediately.
  See [the token-revocation fix record](../fixes/token-revocation.md).
- `Owner` and `Manager` can manage room types, rooms, rates and blocks.
- `Receptionist` can read rooms/room types, search availability and update room status.
- `Receptionist` gets `403` for rate administration and maintenance blocks.
- No token: `401`.

## 11. Business rules

| Rule | Result |
|---|---|
| Check-out after check-in | 400 |
| Rate `ValidTo >= ValidFrom` | 400 |
| Block `ToDate >= FromDate` | 400 |
| Amount > 0 | 400 |
| Room number unique | 409 |
| Room type name unique | 409 |
| Room type cannot be deleted while rooms exist | 409 |
| Rate periods cannot overlap for the same room type | 409 |
| Concurrent rate writes are serialized per room type (`sp_getapplock`) | one 201, rest 409 |
| Unknown room/type/rate/block | 404 |
| Invalid status value | 400 |
| Availability excludes `OutOfOrder` rooms and blocked ranges | — |
| Nightly rate falls back to `BasePrice` when no rate matches | — |

## 12. Endpoints and examples

### Room types

```bash
# create (Owner/Manager)
curl -X POST http://localhost:7002/api/room-types \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"name":"Deluxe","description":"Sea view","basePrice":120.00,"maxOccupancy":2}'

GET    /api/room-types
GET    /api/room-types/{id}
PUT    /api/room-types/{id}
DELETE /api/room-types/{id}
```

### Rooms

```bash
POST /api/rooms
{"number":"101","floor":1,"roomTypeId":1,"status":"Available"}

GET /api/rooms?roomTypeId=1&status=Available
GET /api/rooms/{id}
PUT /api/rooms/{id}
PUT /api/rooms/{id}/status
{"status":"Occupied"}
DELETE /api/rooms/{id}
```

### Availability (public, any role)

```bash
curl "http://localhost:7002/api/rooms/availability?checkIn=2026-11-10&checkOut=2026-11-12" \
  -H "Authorization: Bearer $TOKEN"
```

`200`:

```json
[
  { "id": 1, "number": "101", "floor": 1, "status": "Available",
    "roomTypeId": 1, "roomTypeName": "Deluxe", "nightlyRate": 100.00 }
]
```

### Rates (Owner/Manager)

```bash
POST /api/rates
{"roomTypeId":1,"amount":100.00,"validFrom":"2026-11-01","validTo":"2026-11-30"}

GET /api/rates?roomTypeId=1
GET /api/rates/{id}
PUT /api/rates/{id}
DELETE /api/rates/{id}
```

### Blocks (Owner/Manager)

```bash
POST /api/room-blocks
{"roomId":1,"fromDate":"2026-11-10","toDate":"2026-11-12","reason":"Maintenance"}

GET /api/room-blocks?roomId=1
DELETE /api/room-blocks/{id}
```

### Internal (Reservation Service)

```bash
curl "http://localhost:7002/internal/rooms/availability?checkIn=2026-11-20&checkOut=2026-11-22" \
  -H "X-Internal-Api-Key: $InternalApi__Key"

curl "http://localhost:7002/internal/rooms/1?checkIn=2026-11-20&checkOut=2026-11-22" \
  -H "X-Internal-Api-Key: $InternalApi__Key"

curl -X PUT http://localhost:7002/internal/rooms/1/status \
  -H "X-Internal-Api-Key: $InternalApi__Key" -H 'Content-Type: application/json' \
  -d '{"status":"Occupied"}'
```

Internal room example:

```json
{
  "id": 1, "number": "101", "floor": 1, "status": "Available",
  "roomTypeId": 1, "roomTypeName": "Deluxe",
  "basePrice": 120.00, "nightlyRate": 100.00,
  "isBlocked": false, "isAvailable": true
}
```

## 13. Swagger

1. Run the service in Development.
2. Open `http://localhost:7002/swagger`.
3. Authorize with the Auth Service token (Bearer).
4. Public endpoints can be exercised directly.
5. Internal endpoints need `X-Internal-Api-Key`; use `curl` or the Swagger "InternalApiKey"
   scheme definition as a reference (it is not applied globally).

## 14. Run

```bash
set -a; source .env.local; set +a
printenv ConnectionStrings__RoomDb >/dev/null && echo "environment loaded"

dotnet ef database update \
  --project src/Services/RoomService/RoomService.DataAccess \
  --startup-project src/Services/RoomService/RoomService.API

dotnet run --project src/Services/RoomService/RoomService.API
```

## 15. Manual verification performed

Environment: Auth Service on 7001, Room Service on 7002, SQL Server container on 1434.

| Check | Result |
|---|---|
| Build | succeeded, 0 warnings, 0 errors |
| Migration | applied, 4 tables created |
| `/api/rooms` without token | 401 |
| Create room type | 201 |
| Create room | 201 with type name |
| Duplicate room number | 409 |
| Create rate | 201 |
| Overlapping rate | 409 |
| Invalid rate range | 400 |
| Availability in rate period | 200, nightly rate 100.00 |
| Availability with equal dates | 400 |
| Create block | 201 |
| Availability during block | empty |
| Availability outside block | room returned |
| Internal without/with wrong key | 401 / 401 |
| Internal availability with key | 200 |
| Internal room blocked range | `isBlocked=true`, `isAvailable=false` |
| Internal room free range | `isBlocked=false`, `isAvailable=true` |
| Internal status update | 200, `Occupied` |
| Receptionist create type/rate/block | 403 / 403 / 403 |
| Receptionist availability | 200 |
| Receptionist status update | 200 |
| Receptionist read rates | 403 |
| Delete room type with rooms | 409 |
| Delete room then type | 204 / 204 |
| Unknown room | 404 |
| Invalid status value | 400 |
| Swagger document | 10 public paths, `Bearer` + `InternalApiKey` schemes; `/internal/*` hidden |

## 16. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `Connection string 'RoomDb' is not configured` | `.env.local` not loaded |
| `InternalApi__Key ... required` at startup | key missing or shorter than 32 chars |
| `401` on `/internal/*` | missing/wrong `X-Internal-Api-Key` |
| `403` managing rates/blocks | the token is not Owner/Manager |
| Availability is empty | room is `OutOfOrder`, blocked, or the room type is inactive |
| `409` creating a rate | an existing rate overlaps the period |
| `409` deleting a room type | rooms still reference it |
| Nightly rate equals base price | no rate covers the check-in date |

## 17. Known limitations

- Availability is **physical**: it does not know about reservations. The Reservation Service
  combines it with its own bookings.
- Deleting a room does not check future reservations (owned by another service).
- Room status is not automatically changed by blocks; blocks only affect availability.
- No pagination on list endpoints (small master data set).
- Rate overlap is rejected rather than allowing seasonal priorities.
- Every authenticated request calls Auth for token status; if Auth is unavailable the request
  fails closed with `401`.
- Rate concurrency uses a SQL Server application lock (`sp_getapplock`), which is specific to
  SQL Server.
- No automated tests by sprint scope; verification is manual.
