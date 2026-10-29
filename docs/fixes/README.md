# Fix records

Status: implemented and manually verified (Phase 2 hardening).

This folder records fixes made after the Phase 2 verification pass. Each record explains the
original problem, the chosen design, the files changed, the verified behaviour, and any
remaining operational notes.

| Record | Problem | Status |
|---|---|---|
| [Token revocation](token-revocation.md) | Deactivated, deleted or role-changed users kept working with existing JWTs | Fixed |
| [Rate overlap concurrency](rate-overlap-concurrency.md) | Concurrent rate creates could overlap or return 500 | Fixed |
| [Internal Swagger exposure](internal-swagger-exposure.md) | `/internal/*` routes appeared in public Swagger documents | Fixed |

## Verification summary

| Check | Result |
|---|---|
| Targeted revocation/concurrency suite | 27/27 passed |
| Auth regression suite | 50/50 passed |
| Room regression suite | 90/90 passed |
| Concurrent overlapping rate creates (10 requests) | 1 x 201, 9 x 409, 0 x 500 |
| Solution build | 0 warnings, 0 errors |
| Database migration required | none |
| Services left running after verification | no |

## Configuration added

`Services__AuthBaseUrl` (for example `http://localhost:7001`) is now required by the Room
Service so it can call Auth's token-status endpoint. It is documented in `.env.example`; the
real value lives only in `.env.local`.

## Related documentation

- [Architecture](../architecture.md)
- [RBAC](../rbac.md)
- [Database](../database.md)
- [Progress](../progress.md)
- [Auth Service](../services/auth-service.md)
- [Room Service](../services/room-service.md)
