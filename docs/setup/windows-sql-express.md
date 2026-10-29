# Run on Windows with SQL Server Express

This guide is an alternative to the Docker setup in `docs/README.md`. It runs the
application natively on Windows and uses a local **SQL Server Express** instance instead of
the SQL Server Docker container.

Current scope: **Auth Service (7001)** and **Room Service (7002)**. The gateway, Guest,
Reservation and Operations services are not implemented yet.

## 1. What you need

| Item | Notes |
|---|---|
| .NET SDK 10.0.101 | `global.json` uses `rollForward: latestFeature` |
| SQL Server Express | Named instance is usually `SQLEXPRESS` |
| SQL Server Management Studio (SSMS) | Recommended for enabling Mixed Mode and creating logins |
| EF Core CLI 10.0.14 | `dotnet tool install --global dotnet-ef --version 10.0.14` |
| Git | Optional, only to clone/copy the project |
| PowerShell 5.1 or PowerShell 7 | Used to load `.env.local` |

Do **not** install LocalDB for this guide. Use the Express instance so the connection strings
and TCP behaviour match a normal SQL Server.

## 2. Install and configure SQL Server Express

### 2.1 Install

Run the SQL Server Express installer and choose:

- A named instance: `SQLEXPRESS` (default).
- **Mixed Mode (SQL Server authentication and Windows authentication)**.
- Add your Windows user as an administrator.
- Install SSMS separately if the installer does not include it.

If Express is already installed with Windows-only authentication, change it in SSMS:

1. Connect to `.\SQLEXPRESS` with Windows authentication.
2. Right-click the server -> **Properties** -> **Security**.
3. Select **SQL Server and Windows Authentication mode**.
4. Restart the SQL Server service.

Restart from an elevated PowerShell:

```powershell
Restart-Service 'MSSQL$SQLEXPRESS'
```

### 2.2 Enable TCP/IP and SQL Browser

1. Open **SQL Server Configuration Manager**.
2. **SQL Server Network Configuration** -> **Protocols for SQLEXPRESS**.
3. Enable **TCP/IP**.
4. Open TCP/IP properties -> **IPAll**:
   - Leave **TCP Dynamic Ports** empty.
   - Set **TCP Port** to `1433` (or another free port).
5. Enable the **SQL Server Browser** service and set it to Automatic, or use a static port.
6. Restart `MSSQL$SQLEXPRESS` and `SQLBrowser`.

Using a static port avoids depending on SQL Browser. If you use a static port, the connection
string can use `Server=localhost,1433`. If you keep dynamic ports, use
`Server=localhost\SQLEXPRESS` and make sure SQL Browser is running.

### 2.3 Create the application login

Connect with SSMS as a Windows administrator and run one of the following.

**Option A: dedicated login with database creation rights (simplest for migrations)**

```sql
CREATE LOGIN hotel_app
WITH PASSWORD = '<STRONG_PASSWORD>';

ALTER SERVER ROLE dbcreator ADD MEMBER hotel_app;
```

`dbcreator` lets EF Core create the five databases. The login becomes the database owner,
so it can create tables.

**Option B: pre-create databases and grant only `db_owner`**

```sql
CREATE LOGIN hotel_app
WITH PASSWORD = '<STRONG_PASSWORD>';

CREATE DATABASE HotelAuthDb;
CREATE DATABASE HotelRoomDb;
CREATE DATABASE HotelGuestDb;
CREATE DATABASE HotelReservationDb;
CREATE DATABASE HotelOperationsDb;
GO

USE HotelAuthDb;
CREATE USER hotel_app FOR LOGIN hotel_app;
ALTER ROLE db_owner ADD MEMBER hotel_app;
GO

USE HotelRoomDb;
CREATE USER hotel_app FOR LOGIN hotel_app;
ALTER ROLE db_owner ADD MEMBER hotel_app;
GO

-- Repeat for HotelGuestDb, HotelReservationDb and HotelOperationsDb when those
-- services are implemented.
```

Do not use `sa` for the application. A dedicated login is easier to rotate and restrict.

### 2.4 Test the connection

```powershell
sqlcmd -S .\SQLEXPRESS -E -C -Q "SELECT @@VERSION, SERVERPROPERTY('InstanceName'), SERVERPROPERTY('Collation');"
```

Test the SQL login:

```powershell
sqlcmd -S .\SQLEXPRESS -U hotel_app -P '<STRONG_PASSWORD>' -C -Q "SELECT DB_NAME();"
```

If the SQL login fails with error 18456, Mixed Mode is off, the login was not created, or the
password is wrong.

## 3. Get the project and create `.env.local`

`.env.local` is intentionally ignored by Git. It contains the SQL password, JWT signing key,
internal API key and Owner bootstrap password.

If you cloned the repository, create it from the example:

```powershell
Copy-Item .env.example .env.local
```

Edit `.env.local` in VS Code or another editor. Save it as **UTF-8 without BOM**.

### 3.1 Generate development secrets

Run this twice in PowerShell: once for `Jwt__SigningKey`, once for `InternalApi__Key`.

```powershell
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$bytes = New-Object byte[] 48
$rng.GetBytes($bytes)
[Convert]::ToBase64String($bytes)
```

Use a separate strong password for `HOTEL_SQL_SA_PASSWORD` if you keep that variable, and for
`Bootstrap__OwnerPassword`. The Owner password must satisfy the Identity policy: at least 8
characters with upper case, lower case, digit and symbol.

### 3.2 Express connection strings

Replace the Docker connection strings with Express values. **Quote every connection string**
because of the backslash in `.\SQLEXPRESS`:

```dotenv
HOTEL_SQL_SA_PASSWORD='<STRONG_PASSWORD>'
HOTEL_SQL_PORT=1433
HOTEL_SQL_CONNECTION='Server=.\SQLEXPRESS;User Id=hotel_app;Password=<STRONG_PASSWORD>;TrustServerCertificate=True;Encrypt=True'

ConnectionStrings__AuthDb='Server=.\SQLEXPRESS;Database=HotelAuthDb;User Id=hotel_app;Password=<STRONG_PASSWORD>;TrustServerCertificate=True;Encrypt=True'
ConnectionStrings__RoomDb='Server=.\SQLEXPRESS;Database=HotelRoomDb;User Id=hotel_app;Password=<STRONG_PASSWORD>;TrustServerCertificate=True;Encrypt=True'
ConnectionStrings__GuestDb='Server=.\SQLEXPRESS;Database=HotelGuestDb;User Id=hotel_app;Password=<STRONG_PASSWORD>;TrustServerCertificate=True;Encrypt=True'
ConnectionStrings__ReservationDb='Server=.\SQLEXPRESS;Database=HotelReservationDb;User Id=hotel_app;Password=<STRONG_PASSWORD>;TrustServerCertificate=True;Encrypt=True'
ConnectionStrings__OperationsDb='Server=.\SQLEXPRESS;Database=HotelOperationsDb;User Id=hotel_app;Password=<STRONG_PASSWORD>;TrustServerCertificate=True;Encrypt=True'
```

Keep the JWT and internal keys identical across every service:

```dotenv
Jwt__Issuer=HotelManagement
Jwt__Audience=HotelManagementClients
Jwt__SigningKey='<BASE64_48_BYTES>'
Jwt__ExpiryMinutes=120

InternalApi__Key='<BASE64_48_BYTES>'

Bootstrap__OwnerEmail=owner@hotel.local
Bootstrap__OwnerPassword='<STRONG_PASSWORD>'
Bootstrap__OwnerFullName=Hotel Owner

Services__AuthBaseUrl=http://localhost:7001
Services__RoomBaseUrl=http://localhost:7002
Services__GuestBaseUrl=http://localhost:7003
Services__ReservationBaseUrl=http://localhost:7004

ASPNETCORE_ENVIRONMENT=Development
```

Notes:

- `HOTEL_SQL_PORT` and `HOTEL_SQL_CONNECTION` are used by the Docker instructions. The
  application reads the `ConnectionStrings__*` variables, so those two are not critical on
  Express.
- If you use a static port, `Server=localhost,1433` also works. Keep it quoted.
- If you use Windows authentication instead of a SQL login, use
  `Integrated Security=True` and omit `User Id`/`Password`. The service then runs as your
  Windows user.
- `Encrypt=True;TrustServerCertificate=True` is appropriate for a local development
  certificate. Use a trusted certificate for anything shared.

## 4. Load the environment in PowerShell

`dotnet` does **not** read `.env.local` automatically. Load it into the current PowerShell
process before running migrations or services.

```powershell
Get-Content .env.local | ForEach-Object {
    $line = $_.Trim()
    if ($line -eq '' -or $line.StartsWith('#')) { return }

    $parts = $line -split '=', 2
    if ($parts.Count -ne 2) { return }

    $name = $parts[0].Trim()
    $value = $parts[1].Trim()

    if (($value.StartsWith("'") -and $value.EndsWith("'")) -or
        ($value.StartsWith('"') -and $value.EndsWith('"'))) {
        $value = $value.Substring(1, $value.Length - 2)
    }

    [Environment]::SetEnvironmentVariable($name, $value, 'Process')
}
```

Verify:

```powershell
$env:ConnectionStrings__AuthDb
$env:Jwt__SigningKey
$env:InternalApi__Key
$env:Services__AuthBaseUrl
$env:ASPNETCORE_ENVIRONMENT
```

These variables exist only in that PowerShell window. If you open a new window, load the file
again.

### Git Bash alternative

If you prefer Git Bash, use the normal loader:

```bash
set -a
source .env.local
set +a
```

The file must use **LF** line endings for this to work. If it has CRLF, values may contain a
trailing carriage return and connection strings will fail. Convert it with:

```bash
sed -i 's/\r$//' .env.local
```

The PowerShell loader above handles CRLF correctly.

## 5. Restore, build and apply migrations

Run these in the same PowerShell window where the environment is loaded:

```powershell
dotnet restore
dotnet build HotelManagement.sln
```

Expect `0 Warning(s)` and `0 Error(s)`.

Apply the Auth migration:

```powershell
dotnet ef database update --project src\Services\AuthService\AuthService.DataAccess --startup-project src\Services\AuthService\AuthService.API
```

Apply the Room migration:

```powershell
dotnet ef database update --project src\Services\RoomService\RoomService.DataAccess --startup-project src\Services\RoomService\RoomService.API
```

If `dotnet ef` is not recognized, reopen the terminal or add the tools directory to PATH:

```powershell
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"
```

The migrations create `HotelAuthDb` and `HotelRoomDb` if the login has `dbcreator` (Option A)
or if the databases were pre-created (Option B). If a database already exists, the migration
updates it.

## 6. Run the services

Use two PowerShell windows. In both windows, load `.env.local` first.

Window 1 — Auth Service:

```powershell
dotnet run --project src\Services\AuthService\AuthService.API
```

Window 2 — Room Service:

```powershell
dotnet run --project src\Services\RoomService\RoomService.API
```

Start Auth before Room. Room checks token status against Auth on every authenticated request;
if Auth is unavailable, Room returns `401` (fail closed).

The launch profiles use:

- Auth: `http://localhost:7001`
- Room: `http://localhost:7002`

If a port is already in use, override it:

```powershell
dotnet run --project src\Services\AuthService\AuthService.API --urls http://localhost:7001
```

When you change the Auth URL, update `Services__AuthBaseUrl` as well.

Swagger:

- `http://localhost:7001/swagger`
- `http://localhost:7002/swagger`

## 7. Verify the setup

### 7.1 Login and call Auth

```powershell
$body = @{
    email = 'owner@hotel.local'
    password = '<OWNER_PASSWORD>'
} | ConvertTo-Json

$login = Invoke-RestMethod -Method Post `
    -Uri 'http://localhost:7001/api/auth/login' `
    -ContentType 'application/json' `
    -Body $body

$headers = @{ Authorization = "Bearer $($login.accessToken)" }

Invoke-RestMethod 'http://localhost:7001/api/auth/me' -Headers $headers
```

### 7.2 Call Room with the same token

```powershell
Invoke-RestMethod 'http://localhost:7002/api/rooms' -Headers $headers
```

### 7.3 Check authorization

- `http://localhost:7001/api/users` with no token should return `401`.
- A Receptionist token should get `403` on `/api/users` and on rate/room-type writes.
- Room availability and room reads should work for all three roles.

The Owner account is created on Auth startup in Development. If you change
`Bootstrap__OwnerPassword`, the seeder does not reset an existing Owner's password.

## 8. Data and seeding

Only the Owner account is seeded automatically.

The following are **not** in the repository and are **not** created by migrations:

- `reception@hotel.local` or any other test user.
- The `Deluxe` room type, room `101`, and rate `100.00`.
- Any reservations, guests or bills (those services are not implemented yet).

Create room data through Swagger or the API after the services start. Create staff users
through `POST /api/users` as the Owner.

If you need the data from the old machine, take a SQL backup and restore it on Express, or
recreate the data through the APIs. Migrations alone produce empty business tables.

## 9. Windows-specific things to remember

- `.env.local` is not in Git. Copy it securely or recreate it.
- Quote connection strings containing `.\SQLEXPRESS`; an unquoted backslash can be mangled by
  shell parsing.
- PowerShell environment variables apply only to the current window.
- Keep `.env.local` as UTF-8 without BOM. If using Git Bash, use LF line endings.
- Windows Firewall may prompt when the services start. Allow private networks; localhost
  access normally works without an inbound rule.
- Check for port conflicts:

  ```powershell
  netstat -ano | findstr :7001
  netstat -ano | findstr :7002
  netstat -ano | findstr :1433
  ```

- Keep the system clock correct. JWT validation allows 30 seconds of clock skew.
- SQL Server Express is sufficient for this project. Application locks
  (`sp_getapplock`) used by the rate logic are supported by Express.
- The Express 10 GB database limit is far above this project's needs.
- The default SQL collation is case-insensitive. Unique email and room-number checks rely on
  that behaviour; do not use a case-sensitive collation unless you intend case-sensitive
  matching.

## 10. Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| `Login failed for user 'hotel_app'` (18456) | Mixed Mode disabled, login missing, wrong password, or TCP not enabled |
| `A network-related or instance-specific error` | SQL service stopped, wrong instance name, TCP/SQL Browser disabled, wrong port |
| `The certificate chain was issued by an authority that is not trusted` | Add `TrustServerCertificate=True` to the connection string |
| `Cannot open database "HotelAuthDb"` | Migration not applied or login lacks `db_owner`/`dbcreator` |
| `dotnet ef` not recognized | Install the tool and add `%USERPROFILE%\.dotnet\tools` to PATH |
| Migration cannot create a database | Login lacks `dbcreator`; use Option A or pre-create databases |
| `401` on Room with a valid token | Auth is not running, `Services__AuthBaseUrl` is wrong, or `InternalApi__Key` differs between services |
| `403` on an endpoint | The token does not have the required role |
| Connection string contains `localhostSQLEXPRESS` | The backslash was lost; quote the value in `.env.local` |
| Environment variable is empty | `.env.local` was not loaded in that PowerShell window |
| Port already in use | Stop the old process or change the launch profile/`--urls` |
| Tokens stop working after a user change | Expected: deactivation, role changes and deletion revoke old tokens; log in again |
| Room fails after Auth stops | Expected: token validation fails closed with `401` |

## 11. Security reminders

- Never commit `.env.local` or paste its contents into logs or issues.
- Do not use the `sa` account for the application.
- Use a dedicated SQL login with the minimum rights that still allow migrations.
- Rotate the JWT signing key, internal API key and SQL password when moving to a shared
  machine. Changing the JWT key invalidates existing tokens.
- Keep SQL Server and the application ports bound to localhost unless remote access is
  explicitly required.
- `/internal/*` routes require `X-Internal-Api-Key`; do not expose them through a public
  reverse proxy.
