# RBAC

## Roles

| Role | Description |
|---|---|
| `Owner` | Full access, including user/role administration and financial reports |
| `Manager` | Operational access: rooms, rates, guests, reservations, staff profiles, inventory, operational reports |
| `Receptionist` | Front desk: availability, guests, reservations, check-in/out, permitted billing and payments |

## Authentication

- `POST /api/auth/login` is anonymous and returns a JWT.
- All other endpoints require `Authorization: Bearer <token>`.
- Missing/invalid token: `401 Unauthorized`.
- Valid token without the required role: `403 Forbidden`.
- Tokens are validated per service (issuer, audience, signature, expiry), not only at the gateway.
- Every authenticated request also checks the user's current status and `SecurityStamp`
  through Auth. Deactivation, reactivation, deletion or a role change immediately invalidates
  previously issued tokens; the user must log in again.

## Endpoint matrix

The matrix is filled in as each service is implemented. Rows marked *(planned)* are not
implemented yet.

### Auth Service (7001)

| Endpoint | Owner | Manager | Receptionist | Anonymous |
|---|---|---|---|---|
| `POST /api/auth/login` | yes | yes | yes | yes |
| `GET /api/auth/me` | yes | yes | yes | no |
| `GET /api/users` | yes | no | no | no |
| `GET /api/users/{id}` | yes | no | no | no |
| `POST /api/users` | yes | no | no | no |
| `PUT /api/users/{id}` | yes | no | no | no |
| `PUT /api/users/{id}/roles` | yes | no | no | no |
| `DELETE /api/users/{id}` | yes | no | no | no |
| `GET /api/roles` | yes | no | no | no |

### Room Service (7002)

| Area | Owner | Manager | Receptionist |
|---|---|---|---|
| Room type / room / rate administration | yes | yes | no |
| Room status update | yes | yes | yes (front desk) |
| Availability search / room reads | yes | yes | yes |
| Maintenance blocks | yes | yes | no |
| Internal `/internal/*` | service key only | — | — |

### Guest Service (7003) *(planned)*

| Area | Owner | Manager | Receptionist |
|---|---|---|---|
| Create / read / update / search guests | yes | yes | yes |
| Deactivate guest | yes | yes | no |

### Reservation Service (7004) *(planned)*

| Area | Owner | Manager | Receptionist |
|---|---|---|---|
| Create / read / cancel reservation | yes | yes | yes |
| Check-in / check-out | yes | yes | yes |
| Availability search | yes | yes | yes |
| Internal report endpoints | service key only | — | — |

### Hotel Operations Service (7005) *(planned)*

| Area | Owner | Manager | Receptionist |
|---|---|---|---|
| Staff profiles | yes | yes | no |
| Staff compensation (salary) | yes | no | no |
| Inventory administration | yes | yes | no |
| Bills and payments | yes | yes | yes |
| Operational reports | yes | yes | no |
| Financial reports | yes | no | no |

## Business-level restrictions

- A user cannot delete or deactivate their own account.
- Only the roles `Owner`, `Manager` and `Receptionist` can be assigned.
- The Owner account is seeded in development from configuration; credentials are never committed.
- Passwords are stored only as ASP.NET Core Identity hashes.
