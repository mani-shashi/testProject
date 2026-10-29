# Architecture

## Service boundaries

Five business services, each owning its own database, plus one gateway.

| Service | Owns | Does not own |
|---|---|---|
| Auth | Identity users, roles, JWT issuance | Staff profiles, guests |
| Room | Room types, rooms, rates, maintenance blocks, room status | Reservations, prices charged on bills |
| Guest | Guest records | Reservation lifecycle |
| Reservation | Reservations, stays, check-in/out, cancellation | Money, room master data, guest master data |
| Hotel Operations | Staff, inventory, bills, bill items, payments, reports | Room master data, reservation lifecycle |

The gateway has no database and no business logic.

## Layered structure inside each service

```text
ServiceName.API          Controllers, middleware, configuration, Program.cs
ServiceName.Business     Service interfaces, business rules, validation, JWT generation
ServiceName.DataAccess   DbContext, entities, Fluent API configuration, repositories, migrations, seeding
ServiceName.Contracts    Request/response DTOs
```

Dependencies:

```text
API  -> Business, DataAccess, Contracts
DataAccess -> Business (implements repository interfaces), Contracts
Business -> Contracts
Contracts -> (none)
```

Business and API never reference EF Core. DataAccess never contains HTTP or business workflow logic.

## Request flow

```text
HTTP request
  -> Controller (API)            validates DTO, checks [Authorize]
  -> Service interface (Business) coordinates the use case
  -> Business rules               overlap, status transitions, totals, uniqueness
  -> Repository interface         defined in Business
  -> Repository implementation    (DataAccess) EF Core queries
  -> DbContext -> SQL Server
  -> DTO back through the same layers
```

Controllers do not inject repositories. Services do not inject `DbContext`.

## Repository pattern

Each service defines focused repository interfaces for the use cases it needs.
There is no generic repository and no Unit of Work; `DbContext.SaveChangesAsync` is called
inside repository methods. Repositories contain queries only, never business rules. The one
exception is concurrency control: rate writes take a SQL Server application lock
(`sp_getapplock`) on the room type before checking overlap and inserting, because SQL Server
has no range-exclusion constraint.

## Authentication and authorization

- Auth Service issues JWTs signed with a shared HS256 key.
- Every service validates issuer, audience, signature and expiry independently.
- Tokens carry a `stamp` claim copied from the user's Identity `SecurityStamp`. Every
  authenticated request checks the user's current status and stamp through Auth's internal
  `GET /internal/users/{id}/token-status` endpoint (Room Service does this from its JWT
  `OnTokenValidated` event). Auth checks its own database directly.
- Deactivating, reactivating, deleting a user or changing roles changes the stamp or removes
  the user, so previously issued tokens stop working immediately. Legacy tokens without a
  stamp claim are rejected.
- The check fails closed: if Auth is unreachable, the request is rejected with `401`.
- Role claims (`Owner`, `Manager`, `Receptionist`) drive `[Authorize(Roles = ...)]`.
- The gateway forwards the `Authorization` header but is not trusted for authorization.
- Internal `/internal/*` endpoints are protected by `X-Internal-Api-Key`, are hidden from
  Swagger, and are not routed by the gateway.

## Cross-service communication

Service-to-service calls are HTTP with a short timeout:

```text
Every service -> Auth    token status/security stamp check for each authenticated request
Reservation -> Room      room details, physical availability, status update (best effort)
Reservation -> Guest     guest existence and snapshot (name, phone)
Operations  -> Reservation  read-only report data (arrivals, departures, occupancy)
```

The Auth token-status call is protected by `X-Internal-Api-Key` and is not routed publicly.

No service reads another service's database. No cross-database foreign keys.

## Deliberate simplifications

- JWT validation configuration is repeated per service instead of a shared project.
- Guest name/phone and room nightly rate are snapshotted into reservations; guest name into bills.
- Check-in/out updates room status best-effort; a failure is logged and reported but does not
  roll back the stay.
- Token revocation uses a synchronous Auth lookup rather than a distributed cache or
  denylist. This adds one internal call per authenticated request but keeps revocation
  immediate and consistent.
- Cancelling a reservation does not refund payments automatically.
- The gateway Swagger aggregates per-service documents; it does not redefine endpoints.

## Ports and databases

| Service | Port | Database |
|---|---:|---|
| Gateway | 7000 | — |
| Auth | 7001 | `HotelAuthDb` |
| Room | 7002 | `HotelRoomDb` |
| Guest | 7003 | `HotelGuestDb` |
| Reservation | 7004 | `HotelReservationDb` |
| Operations | 7005 | `HotelOperationsDb` |
