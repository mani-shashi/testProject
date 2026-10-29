# Auth Service

Status: **implemented and manually verified** (Phase 1).

- Port: `7001`
- Database: `HotelAuthDb`
- Swagger: `http://localhost:7001/swagger`
- Framework: `net10.0`, ASP.NET Core Identity, EF Core 10, SQL Server, JWT

## 1. Purpose and responsibilities

The Auth Service is the only service that stores credentials and roles.

It is responsible for:

- Authenticating users with email and password.
- Issuing signed JWT access tokens.
- Exposing the current authenticated user.
- Creating, listing, updating and deleting users.
- Assigning the `Owner`, `Manager` and `Receptionist` roles.
- Seeding the first Owner account in development.

It is not responsible for staff profiles, guests, rooms or reservations. Other services
never read this database; they validate the JWT and call the internal token-status endpoint
to check whether the account is still active and whether its security stamp matches.

## 2. Folder and project structure

```text
src/Services/AuthService/
├── AuthService.Contracts/            DTOs only, no dependencies
│   ├── DTOs/
│   │   ├── RoleResponseDto.cs
│   │   └── UserResponseDto.cs
│   ├── Requests/
│   │   ├── AssignRolesRequestDto.cs
│   │   ├── CreateUserRequestDto.cs
│   │   ├── LoginRequestDto.cs
│   │   └── UpdateUserRequestDto.cs
│   └── Responses/
│       └── LoginResponseDto.cs
├── AuthService.Business/             use cases, rules, JWT generation
│   ├── Authorization/Roles.cs
│   ├── Configuration/{JwtSettings,BootstrapOwnerSettings}.cs
│   ├── Exceptions/AppException.cs
│   ├── Interfaces/
│   │   ├── IAuthenticationService.cs
│   │   ├── IRoleRepository.cs
│   │   ├── IRoleService.cs
│   │   ├── IUserRepository.cs
│   │   └── IUserService.cs
│   ├── Mapping/UserMapper.cs
│   ├── Models/UserAccount.cs
│   ├── Rules/UserRules.cs
│   ├── Security/JwtTokenGenerator.cs
│   └── Services/{AuthenticationService,UserService,RoleService}.cs
├── AuthService.DataAccess/           EF Core, Identity stores, repositories, seeding
│   ├── Configurations/AppUserConfiguration.cs
│   ├── Context/AuthDbContext.cs
│   ├── Entities/AppUser.cs
│   ├── Migrations/                   InitialCreate + snapshot
│   ├── Repositories/{UserRepository,RoleRepository}.cs
│   └── Seeding/AuthDataSeeder.cs
└── AuthService.API/                  HTTP layer
    ├── Configuration/{AuthenticationConfiguration,SwaggerConfiguration,InternalApiSettings}.cs
    ├── Controllers/{AuthController,UsersController,RolesController,InternalUsersController}.cs
    ├── Middleware/{ExceptionHandlingMiddleware,InternalApiKeyMiddleware}.cs
    ├── Program.cs
    ├── appsettings.json
    └── Properties/launchSettings.json
```

### Project references

| Project | References | NuGet |
|---|---|---|
| Contracts | — | — |
| Business | Contracts | `System.IdentityModel.Tokens.Jwt` 8.23.0 |
| DataAccess | Business, Contracts | `Microsoft.EntityFrameworkCore.SqlServer` 10.0.14, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.14, `Microsoft.EntityFrameworkCore.Design` 10.0.14, `System.Security.Cryptography.Pkcs` 10.0.14 |
| API | Business, DataAccess, Contracts | `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.14, `Swashbuckle.AspNetCore` 10.3.1, `Microsoft.EntityFrameworkCore.Design` 10.0.14, `System.Security.Cryptography.Pkcs` 10.0.14 |

`System.Security.Cryptography.Pkcs` is pinned explicitly because `Microsoft.Data.SqlClient`
6.1.7 pulls a vulnerable 9.0.11 transitively.

## 3. Configuration

Environment variables (loaded from `.env.local`):

| Variable | Used for |
|---|---|
| `ConnectionStrings__AuthDb` | SQL Server database |
| `Jwt__Issuer` | token issuer, must match other services |
| `Jwt__Audience` | token audience, must match other services |
| `Jwt__SigningKey` | HS256 key, at least 32 characters |
| `Jwt__ExpiryMinutes` | access-token lifetime |
| `InternalApi__Key` | shared key for `/internal/*` token-status calls (≥32 chars) |
| `Bootstrap__OwnerEmail` | seeded Owner email |
| `Bootstrap__OwnerPassword` | seeded Owner password |
| `Bootstrap__OwnerFullName` | seeded Owner display name |
| `ASPNETCORE_ENVIRONMENT` | `Development` enables Swagger and seeding |

`appsettings.json` contains only non-secret defaults. It never contains a signing key or
password. The environment always wins over the JSON file.

## 4. Request flow

```text
HTTP request
  -> ExceptionHandlingMiddleware
  -> InternalApiKeyMiddleware (only for /internal/*)
  -> JWT authentication (validates signature/issuer/audience/expiry)
  -> OnTokenValidated (active flag + SecurityStamp check)
  -> Controller ([Authorize(Roles=...)])
  -> IUserService / IAuthenticationService (Business)
  -> UserRules (business rules)
  -> IUserRepository (interface in Business)
  -> UserRepository (DataAccess, uses UserManager<AppUser>)
  -> AuthDbContext -> SQL Server
  -> UserAccount -> UserMapper -> UserResponseDto
```

Controllers never touch `AuthDbContext` or repositories.

## 5. Contracts (DTOs)

### `LoginRequestDto`

```csharp
[Required] [EmailAddress] [MaxLength(256)] public string Email
[Required] [MaxLength(100)]                public string Password
```

`[ApiController]` turns annotation failures into `400` with a `ValidationProblemDetails`.
The password is never logged.

### `CreateUserRequestDto`

```csharp
[Required] [EmailAddress] [MaxLength(256)] public string Email
[Required] [MaxLength(150)]                public string FullName
[Required] [MinLength(8)] [MaxLength(100)] public string Password
[Required] [MinLength(1)]                  public List<string> Roles
```

### `UpdateUserRequestDto`

```csharp
[Required] [MaxLength(150)] public string FullName
public bool IsActive
```

There is no password field: password changes are out of scope for this sprint.

### `AssignRolesRequestDto`

```csharp
[Required] [MinLength(1)] public List<string> Roles
```

### `UserResponseDto`

`Id`, `Email`, `FullName`, `IsActive`, `Roles`, `CreatedAtUtc`. No password hash, no
security stamp, no internal Identity fields.

### `LoginResponseDto`

`AccessToken`, `ExpiresAtUtc`, `User` (a `UserResponseDto`).

## 6. Business layer

### `Authorization/Roles.cs`

```csharp
public const string Owner = "Owner";
public const string Manager = "Manager";
public const string Receptionist = "Receptionist";
public static readonly IReadOnlyList<string> All = new[] { Owner, Manager, Receptionist };
public static string? Normalize(string role)
    => All.FirstOrDefault(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
```

One place defines the three role names. `Normalize` makes role input case-insensitive and
returns `null` for unknown roles.

### `Configuration/JwtSettings.cs`

Plain settings class bound from the `Jwt` section: `Issuer`, `Audience`, `SigningKey`,
`ExpiryMinutes` (default 120).

### `Configuration/BootstrapOwnerSettings.cs`

Bound from the `Bootstrap` section: `OwnerEmail`, `OwnerPassword`, `OwnerFullName`.
Only used in Development.

### `Exceptions/AppException.cs`

```csharp
public abstract class AppException : Exception
{
    protected AppException(string message, int statusCode) : base(message) { StatusCode = statusCode; }
    public int StatusCode { get; }
}
```

Subclasses carry the HTTP status: `ValidationException` 400, `UnauthorizedException` 401,
`ForbiddenException` 403, `NotFoundException` 404, `ConflictException` 409. Business code
throws these; the middleware translates them to Problem Details. No stack trace is returned.

### `Models/UserAccount.cs`

`UserAccount` is the business view of a user: `Id`, `Email`, `FullName`, `IsActive`,
`CreatedAtUtc`, `Roles`, `SecurityStamp`. It is not an EF entity, so Business never
references EF Core. `UserTokenStatus(Id, IsActive, SecurityStamp)` is the lightweight
projection used by the token-revocation check.

`UserOperationResult(bool Succeeded, IReadOnlyList<string> Errors, IReadOnlyList<string> ErrorCodes)`
is the small result type returned by repository writes. It lets Identity validation messages
(for example a weak password) travel back to the service without leaking Identity types into
Business. The codes are used to map duplicate-email errors to `409 Conflict` instead of `400`.

### `Interfaces/IUserRepository.cs`

Focused methods for real use cases:

```csharp
GetAllAsync, GetByIdAsync, GetByEmailAsync, GetTokenStatusAsync(id), EmailExistsAsync,
CreateAsync(email, fullName, password, roles), UpdateAsync(id, fullName, isActive),
DeleteAsync(id), ReplaceRolesAsync(id, roles), CheckPasswordAsync(id, password)
```

### `Interfaces/IRoleRepository.cs`

`GetAllAsync` and `CreateAsync(name)`.

### `Interfaces/IAuthenticationService.cs`

`LoginAsync(LoginRequestDto)` and `GetCurrentUserAsync(userId)`.

### `Interfaces/IUserService.cs`

`GetAllAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `AssignRolesAsync`,
`DeleteAsync`. Methods that need self-protection receive the current user id explicitly,
so the business rule does not depend on `HttpContext`.

### `Rules/UserRules.cs`

```csharp
public static IReadOnlyList<string> NormalizeRoles(IReadOnlyCollection<string>? roles)
{
    if (roles is null || roles.Count == 0) throw new ValidationException("At least one role is required.");
    // each role is trimmed, normalized, unknown roles rejected, duplicates removed
}
```

- Empty role list is invalid.
- Unknown roles are rejected with the allowed list in the message.
- `EnsureNotSelf(id, currentUserId, action)` blocks deleting or deactivating yourself.
- `EnsureOwnerRoleRetained` blocks removing your own `Owner` role.

### `Security/JwtTokenGenerator.cs`

```csharp
var issuedAt = DateTime.UtcNow;
var expiresAt = issuedAt.AddMinutes(_settings.ExpiryMinutes);

var claims = new List<Claim>
{
    new(JwtRegisteredClaimNames.Sub, user.Id),
    new(JwtRegisteredClaimNames.Email, user.Email),
    new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
    new(JwtRegisteredClaimNames.Iat,
        new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
        ClaimValueTypes.Integer64),
    new("fullName", user.FullName),
    new(JwtTokenGenerator.SecurityStampClaim, user.SecurityStamp)
};
claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
var token = new JwtSecurityToken(_settings.Issuer, _settings.Audience, claims,
    notBefore: issuedAt, expires: expiresAt, signingCredentials: credentials);
return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
```

Line by line:

1. `issuedAt` / `expiresAt` are UTC timestamps.
2. `sub` identifies the user; `email` is informational; `jti` makes each token unique;
   `iat` records the issue time; `fullName` is a convenience claim.
3. `stamp` carries the Identity `SecurityStamp`. It is the revocation version: changing the
   stamp invalidates every token issued with the old value.
4. Each role becomes a `ClaimTypes.Role` claim. `JwtSecurityTokenHandler` writes it as the
   long .NET claim URI, and the default inbound mapping turns it back into `ClaimTypes.Role`,
   which `[Authorize(Roles=...)]` reads.
5. The signing key is the shared secret from configuration.
6. HS256 signing with issuer/audience claims.
7. The token string and expiry are returned together.

### `Services/AuthenticationService.cs`

`LoginAsync`:

```csharp
var email = request.Email.Trim().ToLowerInvariant();
var user = await _users.GetByEmailAsync(email, cancellationToken);
if (user is null || !user.IsActive) { log warning; throw new UnauthorizedException("Invalid email or password."); }
var passwordValid = await _users.CheckPasswordAsync(user.Id, request.Password, cancellationToken);
if (!passwordValid) { log warning; throw new UnauthorizedException("Invalid email or password."); }
var (token, expiresAtUtc) = _tokenGenerator.Generate(user);
return new LoginResponseDto { AccessToken = token, ExpiresAtUtc = expiresAtUtc, User = UserMapper.ToDto(user) };
```

- Emails are compared normalized.
- Unknown account and wrong password return the same message, so accounts cannot be probed.
- Inactive users cannot log in.
- Only the user id is logged, never the password.

`GetCurrentUserAsync` loads the user by id and returns 404 if it no longer exists.

### `Services/UserService.cs`

`CreateAsync`:

1. Normalize email and trim the full name.
2. Reject an empty full name.
3. Normalize/validate roles.
4. `EmailExistsAsync` -> `ConflictException` (409).
5. Repository create; Identity validation errors become a `ValidationException` (400).
   Duplicate email/user-name errors (including a unique-index race) become
   `ConflictException` (409).
6. Reload the created user and map it to a DTO.

`UpdateAsync` changes the `SecurityStamp` when `IsActive` changes. `AssignRolesAsync` changes
it when the role set actually differs. In both cases every previously issued token for that
user stops working immediately.

`UpdateAsync`:

1. Load the user or throw 404.
2. Trim/validate the full name.
3. If `IsActive` is false and the target is the caller, throw 400.
4. Persist, reload, return the DTO.

`AssignRolesAsync`:

1. Load the user or throw 404.
2. Normalize roles.
3. Block removing your own Owner role.
4. Replace roles, reload, return the DTO.

`DeleteAsync`:

1. Block deleting yourself.
2. Load the user or throw 404.
3. Delete and map Identity errors to 400.

### `Services/RoleService.cs`

Maps repository role names to `RoleResponseDto`.

### `Mapping/UserMapper.cs`

`ToDto(UserAccount)` copies the seven public fields. Mapping is explicit; no mapping library.

## 7. Data access layer

### `Entities/AppUser.cs`

```csharp
public sealed class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}
```

Identity provides `Id`, `UserName`, `NormalizedUserName`, `Email`, `NormalizedEmail`,
`PasswordHash`, `SecurityStamp`, `ConcurrencyStamp` and related fields.

### `Context/AuthDbContext.cs`

```csharp
public sealed class AuthDbContext : IdentityDbContext<AppUser>
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);
    }
}
```

`base.OnModelCreating` builds the Identity schema. `ApplyConfigurationsFromAssembly` picks
up every `IEntityTypeConfiguration<T>` in the DataAccess assembly.

### `Configurations/AppUserConfiguration.cs` (Fluent API)

```csharp
builder.Property(user => user.FullName).IsRequired().HasMaxLength(150);
builder.Property(user => user.IsActive).IsRequired().HasDefaultValue(true);
builder.Property(user => user.CreatedAtUtc).IsRequired();
```

Identity already configures the table names, primary key, unique normalized email index and
role relationships, so nothing is duplicated here.

### `Repositories/UserRepository.cs`

Uses `UserManager<AppUser>` (Identity's data-access component) inside the DataAccess layer:

- `GetAllAsync` loads users with `AsNoTracking()`, ordered by email, then maps each one.
- `GetByIdAsync` / `GetByEmailAsync` use `FindByIdAsync` / `FindByEmailAsync`.
- `EmailExistsAsync` reuses `FindByEmailAsync`.
- `CreateAsync` creates the user with `UserName = Email`, hashes the password through
  `UserManager.CreateAsync`, then adds roles. Identity validation errors are returned, not
  thrown. A `DbUpdateException` caused by the unique email/user-name index (SQL Server
  2601/2627) is translated into a duplicate-email result so concurrent requests still get
  `409 Conflict`.
- `GetTokenStatusAsync` returns only `Id`, `IsActive` and `SecurityStamp`, without loading
  roles; this is the hot path used by token validation.
- `UpdateAsync` loads, sets `FullName`/`IsActive`, calls `UpdateAsync`, and calls
  `UpdateSecurityStampAsync` when the active flag changed.
- `DeleteAsync` loads and deletes; subsequent token-status lookups return 404.
- `ReplaceRolesAsync` removes current roles then adds the new ones, and refreshes the
  security stamp when the role set changed.
- `CheckPasswordAsync` verifies the hash with `UserManager.CheckPasswordAsync`.
- `MapAsync` loads roles and builds the `UserAccount` model.

Note: `GetAllAsync` issues one role query per user. That is acceptable for the small user
count in this sprint and keeps the code readable.

### `Repositories/RoleRepository.cs`

`RoleManager<IdentityRole>` is used to list and create roles.

### `Seeding/AuthDataSeeder.cs`

Called once at startup in Development:

1. Creates `Owner`, `Manager`, `Receptionist` if missing.
2. Looks up the configured Owner email.
3. Creates the Owner with the configured password if absent (password hashed by Identity).
4. Reactivates an existing Owner if it was deactivated and refreshes its security stamp.
5. Ensures the Owner role is assigned.

It is idempotent: restarting the service does not duplicate anything. Errors throw at
startup, so a misconfigured bootstrap fails loudly instead of leaving a half-seeded database.

### Migration

`Migrations/20261028203336_InitialCreate.cs` creates the Identity tables
(`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`,
`AspNetUserTokens`, `AspNetRoleClaims`) plus the `FullName`, `IsActive`, `CreatedAtUtc`
columns. The database `HotelAuthDb` was created by `dotnet ef database update`.

## 8. API layer

### `Program.cs`

1. `AddControllers()` and `AddProblemDetails()`.
2. Reads `ConnectionStrings:AuthDb`; throws a clear error if `.env.local` was not loaded.
3. `AddDbContext<AuthDbContext>` with SQL Server.
4. `AddIdentityCore<AppUser>()` with password policy (8 chars, upper, lower, digit, symbol,
   unique email) and `AddRoles<IdentityRole>()` + `AddEntityFrameworkStores`.
5. Binds `JwtSettings`; validates issuer, audience and a signing key of at least 32 chars.
6. Binds `InternalApiSettings`; requires a key of at least 32 characters.
7. Registers `JwtSettings`, `InternalApiSettings` and `JwtTokenGenerator` as singletons.
8. Registers repositories and services as scoped.
9. Adds JWT authentication and Swagger.
10. Pipeline: exception middleware -> internal key middleware -> Swagger (Development) ->
    authentication -> authorization -> controllers.
11. In Development: binds `BootstrapOwnerSettings`, validates email/password, creates a
    scope and runs the seeder.
12. `app.Run()`.

The app refuses to start if required configuration is missing.

### `Configuration/AuthenticationConfiguration.cs`

```csharp
ValidateIssuer = true, ValidIssuer = settings.Issuer,
ValidateAudience = true, ValidAudience = settings.Audience,
ValidateIssuerSigningKey = true,
IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
ValidateLifetime = true,
ClockSkew = TimeSpan.FromSeconds(30),
RoleClaimType = ClaimTypes.Role,
NameClaimType = ClaimTypes.Email
```

Every validation flag is on. A tampered or expired token produces `401`.

`OnTokenValidated` then:

1. Reads `ClaimTypes.NameIdentifier` (`sub` after inbound mapping) and the `stamp` claim.
2. Rejects the token if either is missing (legacy tokens without a stamp fail).
3. Calls `IUserRepository.GetTokenStatusAsync`.
4. Fails validation when the user is missing, inactive, or the stamp no longer matches.

This is why deactivation, deletion and role changes take effect immediately, and why a
database outage fails closed with `401`.

### `Configuration/SwaggerConfiguration.cs`

Adds the `Bearer` HTTP security scheme and applies it to all operations. Swashbuckle 10 uses
`OpenApiSecuritySchemeReference`, which is why the requirement is created through a document
factory.

### `Middleware/ExceptionHandlingMiddleware.cs`

- `AppException` -> its status code, logged as a warning.
- Any other exception -> `500` with a generic message, logged as an error.
- Writes `application/problem+json` with `status`, `title`, `detail`, `instance`.
- Does nothing if the response already started.

### `Controllers/AuthController.cs`

Controller-level `[Authorize]`; `login` is `[AllowAnonymous]`.

- `POST /api/auth/login` calls `IAuthenticationService.LoginAsync`.
- `GET /api/auth/me` reads `ClaimTypes.NameIdentifier` from the token and calls
  `GetCurrentUserAsync`. It returns `401` if the claim is missing.

### `Controllers/UsersController.cs`

`[Authorize(Roles = Roles.Owner)]` on the whole controller:

- `GET /api/users`
- `GET /api/users/{id}`
- `POST /api/users` (201 + `Location` header)
- `PUT /api/users/{id}`
- `PUT /api/users/{id}/roles`
- `DELETE /api/users/{id}` (204)

`CurrentUserId()` reads the authenticated user id and throws `UnauthorizedException` if it
is absent.

### `Controllers/RolesController.cs`

`GET /api/roles`, Owner only.

### `Controllers/InternalUsersController.cs`

`GET /internal/users/{id}/token-status`, hidden from Swagger with
`[ApiExplorerSettings(IgnoreApi = true)]`.

- Returns `{ "isActive": true, "securityStamp": "..." }` for an existing user.
- Returns `404` for an unknown or deleted user.
- No JWT is required; `InternalApiKeyMiddleware` requires `X-Internal-Api-Key` and compares
  it in fixed time. The gateway will not route this path.

### `Middleware/InternalApiKeyMiddleware.cs`

Applies only to `/internal/*`; public routes pass through. Missing or wrong key returns `401`
Problem Details.

## 9. Dependency injection graph

```text
AuthDbContext           scoped   SQL Server, AuthDb
UserManager<AppUser>    scoped   provided by AddIdentityCore
RoleManager<IdentityRole> scoped provided by AddRoles
IUserRepository  -> UserRepository      scoped
IRoleRepository  -> RoleRepository      scoped
IAuthenticationService -> AuthenticationService scoped
IUserService     -> UserService         scoped
IRoleService     -> RoleService         scoped
JwtSettings, InternalApiSettings        singleton
JwtTokenGenerator                       singleton
```

Controllers resolve only service interfaces. Services resolve only repository interfaces
and `JwtTokenGenerator`.

## 10. JWT and RBAC

- Algorithm: HS256 with the shared `Jwt__SigningKey`.
- Claims: `sub`, `email`, `jti`, `iat`, `fullName`, `stamp`, one `role` claim per role.
- Lifetime: `Jwt__ExpiryMinutes` (120 by default).
- The token is sent as `Authorization: Bearer <token>`.
- `401`: missing, malformed, expired, wrong issuer/audience or wrong signature.
- `403`: valid token without the required role.
- Role authorization is enforced in the service, not at the gateway.

Tokens are revocable: deactivation, reactivation, deletion and role changes invalidate
previously issued tokens through the `stamp` claim and the token-status check. The service
fails closed if the user database is unavailable. See
[the token-revocation fix record](../fixes/token-revocation.md).

## 11. Business rules and validation

| Rule | Where | Result |
|---|---|---|
| Email format and required fields | DataAnnotations | 400 |
| Password minimum 8 chars, upper/lower/digit/symbol | Identity | 400 |
| Email unique (case-insensitive) | `EmailExistsAsync` | 409 |
| Roles limited to Owner/Manager/Receptionist | `UserRules.NormalizeRoles` | 400 |
| At least one role | `UserRules.NormalizeRoles` | 400 |
| Cannot delete yourself | `UserRules.EnsureNotSelf` | 400 |
| Cannot deactivate yourself | `UserRules.EnsureNotSelf` | 400 |
| Cannot remove your own Owner role | `UserRules.EnsureOwnerRoleRetained` | 400 |
| Inactive user cannot log in | `AuthenticationService` | 401 |
| Deactivated/deleted user token rejected | `OnTokenValidated` + `GetTokenStatusAsync` | 401 |
| Role change invalidates old tokens | `ReplaceRolesAsync` stamp refresh | 401 |
| Legacy token without `stamp` | `OnTokenValidated` | 401 |
| Wrong password / unknown email | `AuthenticationService` | 401 (same message) |
| User not found | Services | 404 |

## 12. Endpoints and examples

All examples use `http://localhost:7001`. Tokens are shortened here.

### POST /api/auth/login (anonymous)

```bash
curl -X POST http://localhost:7001/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"owner@hotel.local","password":"<owner password>"}'
```

`200 OK`:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAtUtc": "2026-10-28T22:34:17.300644Z",
  "user": {
    "id": "a3f1...",
    "email": "owner@hotel.local",
    "fullName": "Hotel Owner",
    "isActive": true,
    "roles": ["Owner"],
    "createdAtUtc": "2026-10-28T20:30:00Z"
  }
}
```

Errors: `400` invalid body, `401` invalid credentials.

### GET /api/auth/me

```bash
curl http://localhost:7001/api/auth/me -H "Authorization: Bearer $TOKEN"
```

`200` with the same user object. `401` without a valid token.

### GET /api/users (Owner)

`200` with a JSON array of users. `401` unauthenticated, `403` for Manager/Receptionist.

### GET /api/users/{id} (Owner)

`200` user, `404` unknown id.

### POST /api/users (Owner)

```bash
curl -X POST http://localhost:7001/api/users \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"email":"reception@hotel.local","fullName":"Front Desk",
       "password":"<strong password>","roles":["Receptionist"]}'
```

`201 Created` with the user. `400` validation/weak password/unknown role,
`409` duplicate email.

### PUT /api/users/{id} (Owner)

```json
{ "fullName": "Front Desk Lead", "isActive": true }
```

`200` updated user. `400` self-deactivation, `404` unknown id.

### PUT /api/users/{id}/roles (Owner)

```json
{ "roles": ["Manager"] }
```

`200` updated user. `400` unknown role or removing your own Owner role.

### DELETE /api/users/{id} (Owner)

`204 No Content`. `400` deleting yourself, `404` unknown id.

### GET /api/roles (Owner)

```json
[ { "name": "Manager" }, { "name": "Owner" }, { "name": "Receptionist" } ]
```

## 13. Exploring with Swagger

1. Run the service (Development).
2. Open `http://localhost:7001/swagger`.
3. Call `POST /api/auth/login`, copy `accessToken`.
4. Click **Authorize**, paste the token (without the word `Bearer`), confirm.
5. Call `GET /api/auth/me`, `GET /api/users`, `GET /api/roles`.
6. To see `403`, log in as the Receptionist user and call `/api/users`.

## 14. Database and run instructions

```bash
# 1. load configuration
set -a; source .env.local; set +a
printenv ConnectionStrings__AuthDb >/dev/null && echo "environment loaded"

# 2. start SQL Server once (if not already running)
docker run -d --name cg-hmnext-sql -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD="$HOTEL_SQL_SA_PASSWORD" -p 1434:1433 \
  mcr.microsoft.com/mssql/server:2022-latest

# 3. apply migrations
dotnet ef database update \
  --project src/Services/AuthService/AuthService.DataAccess \
  --startup-project src/Services/AuthService/AuthService.API

# 4. run
dotnet run --project src/Services/AuthService/AuthService.API
```

The Owner is created on the first Development startup from `Bootstrap__OwnerEmail` and
`Bootstrap__OwnerPassword`.

## 15. Manual verification performed

| Check | Result |
|---|---|
| `dotnet build HotelManagement.sln` | succeeded, 0 warnings, 0 errors |
| Migration applied to `HotelAuthDb` | Identity tables present |
| Owner seeded on startup | yes, role `Owner` |
| `POST /api/auth/login` (Owner) | 200, token + roles |
| `GET /api/auth/me` (Owner) | 200 |
| `GET /api/users` without token | 401 |
| `GET /api/users` (Owner) | 200 |
| `POST /api/users` (Owner) | 201 |
| Receptionist login | 200 |
| `GET /api/users` (Receptionist) | 403 |
| `GET /api/roles` (Receptionist) | 403 |
| `GET /api/auth/me` (Receptionist) | 200 |
| Duplicate email | 409 |
| Unknown role | 400 |
| Weak password | 400 |
| Invalid email format | 400 |
| Assign Manager role | 200 |
| Remove own Owner role | 400 |
| Delete own account | 400 |
| Deactivate own account | 400 |
| Unknown user id | 404 |
| Tampered token | 401 |
| Wrong password | 401 |
| Swagger UI and JSON | 200, Bearer scheme present |

## 16. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Startup throws `Connection string 'AuthDb' is not configured` | `.env.local` not loaded in that terminal; run the `set -a; source .env.local; set +a` block |
| Startup throws about `Jwt__SigningKey` | Key missing or shorter than 32 characters |
| `dotnet ef` cannot connect | SQL Server container not running or wrong port; check `docker ps` and `sqlcmd -S localhost,1434` |
| Login returns 401 for a valid password | User is inactive or was created in a different database |
| `403` on `/api/users` | Token does not have the `Owner` role |
| `409` creating a user | Email already exists (case-insensitive) |
| Seeding fails on password policy | `Bootstrap__OwnerPassword` must meet the Identity policy |
| Swagger shows no padlock | `ASPNETCORE_ENVIRONMENT` is not `Development` |

## 17. Known limitations

- No refresh tokens, password reset or lockout.
- Revocation is stamp-based: changing the stamp invalidates all tokens for a user, so a role
  change also forces re-login.
- Legacy tokens issued before the `stamp` claim was added are rejected.
- `GetAllAsync` performs one role query per user (acceptable at this scale).
- Role assignment is not transactional with user creation: a role failure after creation
  leaves the user without the requested roles and returns `400`.
- No automated tests by sprint scope; verification is manual.
