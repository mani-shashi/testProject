# Hotel Management System — Backend

Backend-only Online Hotel Management System built with ASP.NET Core, EF Core and SQL Server.
It is split into five business microservices plus one Ocelot API Gateway. There is no frontend.

## Components

| Component | Port | Database | Responsibility |
|---|---:|---|---|
| Gateway | 7000 | none | Routing and consolidated Swagger |
| Auth Service | 7001 | `HotelAuthDb` | Login, JWT, users, roles |
| Room Service | 7002 | `HotelRoomDb` | Room types, rooms, rates, availability, room status |
| Guest Service | 7003 | `HotelGuestDb` | Guest records and search |
| Reservation Service | 7004 | `HotelReservationDb` | Reservations, check-in/out, cancellation |
| Hotel Operations Service | 7005 | `HotelOperationsDb` | Staff, inventory, billing, payments, reports |
| SQL Server | 1434 (host) | — | Docker container hosting all five databases |

## Prerequisites

- .NET SDK `10.0.101` (pinned in `global.json`)
- Docker Desktop (only for the SQL Server container)
- `sqlcmd` (optional, for inspecting the database)

## Configuration

All configuration is supplied through environment variables loaded from `.env.local`.

```bash
cp .env.example .env.local
chmod 600 .env.local
# edit .env.local and replace every placeholder
```

Load it in every terminal before running any command:

```bash
set -a
source .env.local
set +a
printenv ConnectionStrings__AuthDb >/dev/null && echo "environment loaded"
```

`.env.local` is ignored by Git and must never be committed or pasted into logs.

## Start SQL Server

```bash
set -a; source .env.local; set +a
docker run -d --name cg-hmnext-sql \
  -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD="$HOTEL_SQL_SA_PASSWORD" \
  -p 1434:1433 \
  mcr.microsoft.com/mssql/server:2022-latest
```

## Run a service

```bash
set -a; source .env.local; set +a
dotnet run --project src/Services/AuthService/AuthService.API
```

Swagger is available at `http://localhost:<port>/swagger` when
`ASPNETCORE_ENVIRONMENT=Development`.

## Repository layout

```text
src/
  Services/
    AuthService/            AuthService.API | .Business | .DataAccess | .Contracts
    RoomService/            (Phase 2)
    GuestService/           (Phase 3)
    ReservationService/     (Phase 4)
    HotelOperationsService/ (Phase 5)
  Gateway/HotelManagement.Gateway/  (Phase 6)
docs/
  README.md  architecture.md  rbac.md  database.md  progress.md
  services/
```

## Documentation

- [Architecture](architecture.md)
- [RBAC](rbac.md)
- [Database](database.md)
- [Progress](progress.md)
- [Fix records](fixes/README.md)
- Service documentation: [`docs/services/`](services/)

## Scope

In scope: the hotel workflows listed above, JWT authentication, role-based authorization,
per-service Swagger, gateway Swagger, migrations and documentation.

Out of scope for this sprint: frontend, Docker for the application itself, message brokers,
Redis, CQRS, sagas, event sourcing, automated tests, CI/CD, and real payment providers.
