# Fix record: hide internal endpoints from Swagger

Status: implemented and manually verified.

## Problem

The Room Service Swagger document listed its service-to-service routes:

```text
/internal/rooms/availability
/internal/rooms/{id}
/internal/rooms/{id}/status
```

Auth also gained a new internal token-status route. These endpoints are protected by
`X-Internal-Api-Key` and are intended for other services, not public API consumers. Listing
them in the public Swagger document advertises an internal surface and makes the public API
look larger than it is.

## Root cause

Swashbuckle includes every API-explorer-visible action by default. Internal controllers had
no `[ApiExplorerSettings]` attribute, so they were documented like public controllers.

## Design

Mark internal controllers with:

```csharp
[ApiExplorerSettings(IgnoreApi = true)]
```

This removes their actions from the generated OpenAPI document while leaving routing and the
internal key middleware unchanged. The endpoints remain callable by services that know the
route and key.

## Files changed

| File | Change |
|---|---|
| `RoomService.API/Controllers/InternalRoomsController.cs` | added `[ApiExplorerSettings(IgnoreApi = true)]` |
| `AuthService.API/Controllers/InternalUsersController.cs` | new controller, already marked `IgnoreApi` |

## Verified behaviour

| Document | Public paths | Internal paths present |
|---|---:|---|
| Auth `http://localhost:7001/swagger/v1/swagger.json` | 6 | none |
| Room `http://localhost:7002/swagger/v1/swagger.json` | 10 | none |

Security schemes remain visible:

- Auth: `Bearer`.
- Room: `Bearer` and `InternalApiKey`.

Internal calls still work:

| Scenario | Result |
|---|---|
| Room internal availability with correct key | 200 |
| Room internal availability without/wrong key | 401 |
| Auth token-status with correct key | 200 |
| Auth token-status without/wrong key | 401 |

## Operational notes

- Hiding a route from Swagger is documentation control, not authorization. The internal key
  middleware remains the access control.
- The gateway will not route `/internal/*` in Phase 6.
- If internal API documentation is needed later, publish it as a separate internal-only
  Swagger document rather than re-exposing these paths in the public document.
