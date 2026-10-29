# Fix record: immediate JWT revocation

Status: implemented and manually verified.

## Problem

Access tokens were stateless. Once issued, a token remained valid until `exp`, even if the
user was:

- deactivated (`IsActive = false`),
- deleted,
- given a different role set.

Deactivation blocked new logins, but an already-issued token still passed signature, issuer,
audience and expiry validation in Auth and in every other service. Deleting a user made
`GET /api/auth/me` return 404, but other JWT-only endpoints still accepted the token.

## Root cause

- The JWT had no user-version claim.
- JWT validation only checked token integrity and lifetime.
- Other services had no way to ask Auth whether the user still existed or was active.
- User updates and role changes did not change any value that tokens depended on.

## Design

### Revocation version

The user's ASP.NET Core Identity `SecurityStamp` is used as the revocation version.

- Auth includes it in the JWT as the `stamp` claim.
- Auth refreshes the stamp when:
  - `IsActive` changes (deactivate or reactivate),
  - the role set actually changes,
  - the bootstrap Owner is reactivated by the seeder.
- Deletion removes the user entirely.
- Legacy tokens without a `stamp` claim are rejected.

Changing the stamp invalidates every token issued with the old value, for that user only.

### Validation flow

Auth Service:

```text
JWT signature/issuer/audience/expiry
  -> OnTokenValidated
  -> read sub (ClaimTypes.NameIdentifier) and stamp
  -> IUserRepository.GetTokenStatusAsync(userId)
  -> reject if user is missing, inactive, or stamp differs
```

Room Service (and the same pattern for future services):

```text
JWT signature/issuer/audience/expiry
  -> OnTokenValidated
  -> GET Auth /internal/users/{id}/token-status
       header: X-Internal-Api-Key
  -> reject if 404, non-2xx, timeout, inactive, or stamp differs
```

The Auth call has a 5-second timeout and fails closed: if Auth cannot be reached, the request
is rejected with `401`.

### Internal endpoint

`GET /internal/users/{id}/token-status`

Response for an existing user:

```json
{ "isActive": true, "securityStamp": "<current stamp>" }
```

- Unknown or deleted user: `404`.
- Missing or wrong `X-Internal-Api-Key`: `401`.
- No JWT is required.
- Hidden from Swagger with `[ApiExplorerSettings(IgnoreApi = true)]`.
- Not routed by the gateway in Phase 6.

## Files changed

Auth Service:

| File | Change |
|---|---|
| `AuthService.Business/Models/UserAccount.cs` | added `SecurityStamp`; added `UserTokenStatus` |
| `AuthService.Business/Interfaces/IUserRepository.cs` | added `GetTokenStatusAsync` |
| `AuthService.Business/Security/JwtTokenGenerator.cs` | added `stamp` claim and claim-name constant |
| `AuthService.Business/Services/UserService.cs` | documents stamp refresh on update/role change |
| `AuthService.DataAccess/Repositories/UserRepository.cs` | maps stamp; refreshes stamp on active/role changes; implements token-status lookup |
| `AuthService.DataAccess/Seeding/AuthDataSeeder.cs` | refreshes stamp when reactivating the Owner |
| `AuthService.API/Configuration/AuthenticationConfiguration.cs` | `OnTokenValidated` status/stamp check |
| `AuthService.API/Configuration/InternalApiSettings.cs` | new settings type |
| `AuthService.API/Middleware/InternalApiKeyMiddleware.cs` | new fixed-time key check for `/internal/*` |
| `AuthService.API/Controllers/InternalUsersController.cs` | new token-status endpoint, hidden from Swagger |
| `AuthService.API/Program.cs` | binds/validates `InternalApi__Key`, registers middleware |

Room Service:

| File | Change |
|---|---|
| `RoomService.API/Security/TokenStatusValidator.cs` | new typed HttpClient for Auth token-status |
| `RoomService.API/Configuration/AuthenticationConfiguration.cs` | `OnTokenValidated` calls the validator |
| `RoomService.API/Program.cs` | validates `Services__AuthBaseUrl`, registers typed client with 5 s timeout |

Configuration:

| Variable | Purpose |
|---|---|
| `Services__AuthBaseUrl` | Auth base URL used by Room for token-status checks |
| `InternalApi__Key` | already existed; now also used by Auth's internal endpoint |

No database migration was needed: `SecurityStamp` already exists in the Identity schema.

## Verified behaviour

| Scenario | Result |
|---|---|
| Normal Owner login, Auth `/me`, Room `/api/rooms` | 200 |
| Deactivated user, existing token, Auth `/me` | 401 |
| Deactivated user, existing token, Room `/api/rooms` | 401 |
| Deactivated user, new login | 401 |
| Reactivated user, old token | 401 |
| Reactivated user, new login | 200 |
| Role changed, old token | 401 |
| Role changed, new token | carries the new role |
| Deleted user, old token, Auth and Room | 401 |
| Crafted token without `stamp` | 401 |
| Token-status without/wrong key | 401 |
| Token-status with key, unknown user | 404 |
| Legacy token for unknown user | 401 |

The targeted suite passed 27/27 checks; Auth regression passed 50/50 and Room regression
passed 90/90.

## Operational notes

- Every authenticated request in a non-Auth service now performs one internal Auth call.
- Auth downtime rejects requests (fail closed) rather than accepting unverifiable tokens.
- A role change forces the affected user to log in again.
- Tokens issued before this change do not contain `stamp` and are rejected; users must log in
  again after deployment.
- A future distributed cache or denylist could reduce the per-request call, but would add
  invalidation complexity. The synchronous lookup keeps revocation immediate.
