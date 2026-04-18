# MoneroShameList

A Monero-focused web application for tracking and exposing bad actors in the Monero ecosystem.

## Prerequisites

- .NET SDK (matching the version specified in `MoneroShameList.csproj`)
- PostgreSQL
- PowerShell (for deployment)
- PuTTY tools (`plink.exe` and `pscp.exe`) — for Windows-based deployment

## Local Setup

### 1. Clone the repository

```bash
git clone https://github.com/YOURUSER/MoneroShameList.git
cd MoneroShameList
```

### 2. Create `appsettings.json` files

You must create an `appsettings.json` file in **both** of the following folders:

- `MoneroShameList/`
- `MoneroShameList.Data/`

Use the following format, filling in your own values:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=YOURDB;Username=YOURUSERNAME;Password=YOURPASSWORD"
  },
  "Admin": {
    "Username": "YOURUSERNAME",
    "Password": "YOURPASSWORD"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

Replace the placeholder values:

- `YOURDB` — the name of your PostgreSQL database
- `YOURUSERNAME` / `YOURPASSWORD` (ConnectionStrings) — PostgreSQL credentials
- `YOURUSERNAME` / `YOURPASSWORD` (Admin) — credentials used to log into the admin area

> **Do not commit these files.** They should be listed in `.gitignore`.

### 3. Apply database migrations

From the solution root, run the EF Core migrations against your PostgreSQL database to create the schema.

### 4. Run the application

```bash
dotnet run --project MoneroShameList/MoneroShameList.csproj
```

## Deployment

Deployment is handled by a PowerShell-based CI/CD pipeline that uses PuTTY (`plink`/`pscp`) to push the published build to a Linux VPS running `systemd`.

### 1. Create `deploy-config.ps1` in the `CI` folder

Create a file at `CI/deploy-config.ps1` with the following contents, filling in every value for your environment:

```powershell
# deploy-config.ps1
# Fill in every value. Do NOT commit this file - add it to .gitignore.

# -- PuTTY tools ---------------------------------------------------------------
$PLINK = "C:\Windows\System32\plink.exe"
$PSCP  = "C:\Windows\System32\pscp.exe"

# -- Server --------------------------------------------------------------------
$SSH_HOST     = "IPADDRESS"
$SSH_USER     = "USERNAME"
$SSH_PASSWORD = "PASSWORD"

# -- App -----------------------------------------------------------------------
$DOMAIN       = "DOMAINNAME"
$APP_NAME     = "APPNAME"
$APP_PORT     = PORTNUMBER
$DEPLOY_PATH  = "/var/www/SITEPATH"
$WEB_PROJECT  = Join-Path $PSScriptRoot "..\MoneroShameList\MoneroShameList\MoneroShameList.csproj"

# -- Database ------------------------------------------------------------------
$DB_NAME      = "YOURNAME"
$DB_USER      = "YOURUSERNAME"
$DB_PASSWORD  = "YOURPASSWORD"

# -- Admin login ---------------------------------------------------------------
$ADMIN_USERNAME = "YOURUSERNAME"
$ADMIN_PASSWORD = "YOURPASSWORD"
```

Field reference:

- **PuTTY tools** — paths to `plink.exe` and `pscp.exe` on your local machine
- **Server** — SSH host/IP, username, and password for the target VPS
- **App** — public domain, systemd service name, port the app listens on, deploy path on the server, and the path to the web project's `.csproj`
- **Database** — PostgreSQL database name and credentials on the server
- **Admin login** — credentials seeded into the app's admin account

> **Do not commit `deploy-config.ps1`.** Add it to `.gitignore`. It contains plaintext credentials.

### 2. Run the deployment script

From PowerShell, run the deployment script in the `CI` folder. It will:

1. Publish the .NET project
2. Upload the build to `$DEPLOY_PATH` on the server via `pscp`
3. Restart the `systemd` service for `$APP_NAME` via `plink`

## .gitignore

Make sure at minimum the following are ignored:

```
**/appsettings.json
CI/deploy-config.ps1
```

## License

See `LICENSE` file.
