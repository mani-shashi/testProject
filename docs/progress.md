# Progress

Last updated: Phase 2 revocation and concurrency hardening complete — awaiting approval for Phase 3.

## Status

| Phase | Scope | Status |
|---|---|---|
| 0 | Repository inspection, scaffolding, docs | Complete |
| 1 | Auth Service | Complete |
| 2 | Room Service | Complete |
| 3 | Guest Service | Not started |
| 4 | Reservation Service | Not started |
| 5 | Hotel Operations Service | Not started |
| 6 | API Gateway and integration | Not started |

## Phase 0 — decisions

- Target framework `net10.0`, SDK pinned to `10.0.101`.
- Clean rebuild in `CG-HMNEXT`; the older `hmNext` project is reference only.
- Five services plus gateway, each service with API / Business / DataAccess / Contracts projects.
- No shared BuildingBlocks project; small JWT/exception code is repeated per service.
- No generic repository, no Unit of Work, no mapping library, no validation library, no tests.
- Ports: gateway 7000, Auth 7001, Room 7002, Guest 7003, Reservation 7004, Operations 7005.
- SQL Server runs in Docker on host port 1434; the application runs natively.
- Configuration comes from `.env.local` loaded with `set -a; source .env.local; set +a`.
- ASP.NET Core Identity for Auth; other services validate JWT and check Auth token status.
- Owner account seeded idempotently at Auth startup in Development.
- Guest and rate data are snapshotted into reservations; bills snapshot prices.
- Room status sync from check-in/out is best effort and reported, not transactional.
- Reports: Operations calls read-only Reservation internal endpoints for arrivals/departures/occupancy.

## Phase 1 — Auth Service

### Implemented

- Solution and four layered projects for Auth.
- ASP.NET Core Identity (`AppUser`, `IdentityRole`) with `AuthDbContext`.
- Fluent API configuration for Identity tables and custom user columns.
- Repositories: `IUserRepository`, `IRoleRepository` with EF Core implementations.
- Business services: authentication, user management, role listing.
- JWT generation and validation (issuer, audience, signing key, expiry).
- RBAC: Owner-only user/role administration; authenticated `/me`.
- Idempotent Owner seeding from `Bootstrap__*` settings (Development).
- Exception middleware with Problem Details.
- Swagger with Bearer authentication.

### Verification log

Environment: SQL Server 2022 container `cg-hmnext-sql` on host port 1434; Auth Service on
`http://localhost:7001`; database `HotelAuthDb` migrated with `InitialCreate`.

| Check | Result |
|---|---|
| Solution build | succeeded, 0 warnings, 0 errors |
| Migration | applied, Identity tables + custom user columns created |
| Owner seeding | Owner created with role `Owner` on first startup |
| Owner login | 200 |
| `GET /api/auth/me` (Owner) | 200 |
| `GET /api/users` without token | 401 |
| `GET /api/users` (Owner) | 200 |
| Create Receptionist user | 201 |
| Receptionist login | 200 |
| `GET /api/users` (Receptionist) | 403 |
| `GET /api/roles` (Receptionist) | 403 |
| `GET /api/auth/me` (Receptionist) | 200 |
| Duplicate email | 409 |
| Unknown role | 400 |
| Weak password | 400 |
| Invalid email format | 400 |
| Role reassignment | 200 |
| Self role removal / self delete / self deactivate | 400 |
| Unknown user | 404 |
| Tampered token | 401 |
| Wrong password | 401 |
| Swagger UI + document | 200, Bearer scheme present |

## Phase 2 — Room Service

### Implemented

- Four layered projects for Room Service.
- Room types, rooms, rates and maintenance blocks with Fluent API configuration.
- Physical availability search with nightly-rate resolution.
- Room status management (public and internal).
- Internal API-key-protected endpoints for the Reservation Service.
- RBAC: Owner/Manager manage master data; Receptionist reads and updates status.
- Exception middleware with Problem Details; Swagger with Bearer + internal key schemes.

### Verification log

Environment: Auth Service on 7001, Room Service on 7002, SQL Server on 1434,
database `HotelRoomDb` migrated with `InitialCreate`.

| Check | Result |
|---|---|
| Build | succeeded, 0 warnings, 0 errors |
| Migration | 4 tables created |
| No token on `/api/rooms` | 401 |
| Room type / room / rate creation | 201 |
| Duplicate room number | 409 |
| Overlapping rate | 409 |
| Invalid rate range / stay dates / status | 400 |
| Availability with rate | 200, nightly rate 100.00 |
| Block removes room from availability | yes |
| Internal endpoint without key | 401 |
| Internal endpoint with key | 200 |
| Internal blocked/free flags | correct |
| Internal status update | 200 |
| Receptionist write operations | 403 |
| Receptionist read + status update | 200 |
| Delete room type with rooms | 409 |
| Delete room then type | 204 |
| Swagger | 13 paths, 2 security schemes |

## Phase 2 verification pass

Environment: Auth 7001 and Room 7002 running against SQL Server on 1434; all checks manual
(curl, crafted HS256 tokens, `sqlcmd`).

| Area | Result |
|---|---|
| Auth suite (login, `/me`, RBAC, CRUD, roles, validation, JWT tampering) | 50/50 checks passed |
| Room suite (types, rooms, status, rates, blocks, availability, internal API, RBAC, cascades) | 90/90 checks passed |
| JWT payload | `sub`, `email`, `jti`, `iat`, `nbf`, `exp`, `iss`, `aud`, `fullName`, role present; 120-minute lifetime |
| JWT rejection | expired, wrong issuer/audience/signature, `alg=none`, tampered token -> 401 |
| Password storage | Identity PBKDF2 hashes (`AQAAAA…`, 84 chars); no hashes in API responses |
| Room foreign keys | `RoomBlocks -> Rooms` CASCADE, `RoomRates -> RoomTypes` CASCADE, `Rooms -> RoomTypes` NO ACTION |
| Money columns | `decimal(18,2)` for `RoomTypes.BasePrice` and `RoomRates.Amount` |
| Swagger | Auth 6 paths + Bearer; Room 10 paths + Bearer + InternalApiKey (internal paths hidden) |

Fixes made during the pass:

- JWT now includes an `iat` claim.
- Duplicate-email races in Auth return `409` instead of `400`/`500` (Identity error codes and
  unique-index `DbUpdateException` mapped to conflict).
- Duplicate room-number/room-type-name races in Room return `409` instead of `500`.
- `GET /api/rates` now requires `roomTypeId` and returns `400` when it is missing.

The three limitations found in this pass were then fixed in the hardening pass below.

## Revocation and concurrency hardening

Detailed records: [fix records](fixes/README.md).

| Area | Result |
|---|---|
| Targeted revocation/concurrency suite | 27/27 checks passed |
| Deactivation | old token rejected by Auth and Room immediately (401) |
| Reactivation | old token stays rejected; new login works |
| Role change | old token rejected; new token carries the new role |
| Deletion | old token rejected by Auth and Room |
| Legacy token without `stamp` | rejected (401) |
| Internal token-status | no/wrong key 401, valid key 200, unknown user 404 |
| Concurrent overlapping rate creates (10) | exactly 1 x 201, 9 x 409, 0 x 500 |
| Swagger | internal paths absent (Auth 6, Room 10) |
| Regression | Auth 50/50, Room 90/90 |

Implementation:

- JWT includes a `stamp` claim from Identity `SecurityStamp`.
- Auth validates status/stamp against its database on every authenticated request.
- Room calls Auth `GET /internal/users/{id}/token-status` from `OnTokenValidated`; the call
  uses `X-Internal-Api-Key`, has a 5-second timeout, and fails closed.
- Deactivation/reactivation and role changes refresh the stamp; deletion removes the user.
- Rate writes take a transaction-scoped `sp_getapplock` per room type before the overlap
  check, so concurrent writers serialize instead of racing or deadlocking.
- Internal controllers are hidden from Swagger with `[ApiExplorerSettings(IgnoreApi = true)]`.
- No migration was needed; `SecurityStamp` already exists in the Identity schema.

## Pending / limitations

- Phases 3–6 not implemented; Guest Service is next and requires approval.
- No automated tests by design; verification is manual.
- Gateway and consolidated Swagger arrive in Phase 6.
- Auth: no refresh tokens, password reset or lockout.
- Token revocation is synchronous: each authenticated request in a non-Auth service makes one
  internal call to Auth. Auth downtime therefore rejects requests (fail closed).
- Rate concurrency uses a SQL Server application lock; it is correct across service instances
  sharing the database but is SQL Server-specific.
- Room: availability is physical only; reservations are applied by the Reservation Service.
- Room: deleting a room does not check future reservations (owned by another service).
