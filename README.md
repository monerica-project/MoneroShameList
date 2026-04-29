# MoneroShameList

A web application for listing projects which claim to care about privacy but don't accept Monero.

## Prerequisites

**For local development:**
- .NET SDK (matching the version specified in `MoneroShameList.csproj`)
- PostgreSQL

**For deployment from Linux:**
- `bash`, `dotnet`, `tar`, `sshpass` (`sudo apt install sshpass`)

**For deployment from Windows:**
- PowerShell
- PuTTY tools (`plink.exe` and `pscp.exe`)

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

From the solution root, run the EF Core migrations against your PostgreSQL database to create the schema. Or use the built-in `--migrate-only` mode:

```bash
dotnet run --project MoneroShameList/MoneroShameList.csproj -- --migrate-only
```

### 4. Run the application

```bash
dotnet run --project MoneroShameList/MoneroShameList.csproj
```

## Deployment

Two deploy paths are provided. Both target the same Linux VPS running `systemd`, with `nginx` as a reverse proxy in a Docker container named `nginx`. Pick whichever matches your workstation.

- `CI/deploy.sh` + `CI/deploy-config.sh` — for **Linux** workstations
- `CI/deploy.ps1` + `CI/deploy-config.ps1` — for **Windows** workstations

Both scripts:

1. Publish the .NET project
2. Bootstrap the server (install .NET runtime, PostgreSQL)
3. Create the database/user if missing
4. Back up the remote database (Linux script only — see [Database Backups](#database-backups))
5. Show a maintenance page
6. Upload the build via SCP, install a systemd unit, restart the service
7. Configure `nginx`, optionally with Let's Encrypt SSL
8. Optionally configure a Tor hidden service

### Linux deployment

#### 1. Create `CI/deploy-config.sh`

Create a file at `CI/deploy-config.sh` with the following contents, filling in every empty value:

```bash
# deploy-config.sh
# Sourced by deploy.sh. Do NOT commit this file.

# -- Server -------------------------------------------------------------------
SSH_HOST="IPADDRESS"
SSH_USER="USERNAME"
SSH_PASSWORD=""        # FILL IN

# -- App ----------------------------------------------------------------------
DOMAIN="DOMAINNAME"
APP_NAME="APPNAME"
APP_PORT=PORTNUMBER
DEPLOY_PATH="/var/www/SITEPATH"
WEB_PROJECT="$SCRIPT_DIR/../MoneroShameList/MoneroShameList/MoneroShameList.csproj"

# -- Database -----------------------------------------------------------------
DB_NAME="YOURNAME"
DB_USER="YOURUSERNAME"
DB_PASSWORD=""         # FILL IN

# -- Admin login --------------------------------------------------------------
ADMIN_USERNAME="YOURUSERNAME"
ADMIN_PASSWORD=""      # FILL IN
```

> **Quoting note:** if any password contains a `"`, wrap that value in single quotes (`'pa"ssword'`). Validate with `bash -n CI/deploy-config.sh`.

#### 2. Make the script executable

```bash
chmod +x CI/deploy.sh
```

#### 3. Run the deployment

```bash
cd CI
./deploy.sh
```

#### Flags

| Flag             | What it does                                                        |
| ---------------- | ------------------------------------------------------------------- |
| *(none)*         | Build, back up DB, deploy, configure nginx                          |
| `--skip-build`   | Skip `dotnet publish` (deploy whatever is already in `../publish/`) |
| `--skip-backup`  | Skip the pre-deploy DB backup                                       |
| `--backup-only`  | Take a DB backup and exit (no deploy)                               |
| `--ssl`          | Install Let's Encrypt SSL via certbot (first-time setup)            |
| `--tor`          | Set up a Tor hidden service (first-time setup)                      |

Common combinations:

```bash
./deploy.sh                  # routine deploy
./deploy.sh --ssl --tor      # first-ever deploy to a new server
./deploy.sh --skip-build     # config-only redeploy
./deploy.sh --backup-only    # ad-hoc DB snapshot
```

### Windows deployment

#### 1. Create `CI/deploy-config.ps1`

Create a file at `CI/deploy-config.ps1` with the following contents, filling in every value:

```powershell
# deploy-config.ps1
# Fill in every value. Do NOT commit this file.

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

#### 2. Run the deployment

From PowerShell, in the `CI` folder:

```powershell
.\deploy.ps1
```

#### Flags

| Flag           | What it does                                             |
| -------------- | -------------------------------------------------------- |
| *(none)*       | Build, deploy, configure nginx                           |
| `-SkipBuild`   | Skip `dotnet publish`                                    |
| `-SSL`         | Install Let's Encrypt SSL via certbot (first-time setup) |
| `-Tor`         | Set up a Tor hidden service (first-time setup)           |

## Database Backups

The Linux `deploy.sh` script automatically backs up the remote PostgreSQL database before every deploy and pulls the dump down to your local machine.

### Where backups are stored

Local: `backups/<DB_NAME>-<YYYYMMDD-HHMMSS>.sql.gz`, relative to the repo root. The directory is auto-created. Only the **20 most recent** backups are kept; older ones are pruned automatically.

### Take an ad-hoc backup (no deploy)

```bash
cd CI
./deploy.sh --backup-only
```

### Skip the auto-backup on a deploy

```bash
./deploy.sh --skip-backup
```

### Restore a backup to the remote server

```bash
gunzip -c backups/moneroshamelist-20260428-143022.sql.gz | \
  sshpass -p "$SSH_PASSWORD" ssh root@YOUR.SERVER.IP \
  "sudo -u postgres psql YOUR_DB_NAME"
```

> If the database already has data, drop and recreate it first, or restore into a fresh database.

### Restore a backup to your local machine

```bash
gunzip -c backups/moneroshamelist-20260428-143022.sql.gz | \
  psql -h localhost -U YOUR_LOCAL_USER YOUR_LOCAL_DB
```

## Server Operations

These don't require the deploy script — they're plain SSH commands. Replace `PASSWORD` and the IP as needed.

| Task                  | Command                                                                          |
| --------------------- | -------------------------------------------------------------------------------- |
| Service status        | `sshpass -p PASSWORD ssh root@HOST 'systemctl status APP_NAME'`                  |
| Live logs             | `sshpass -p PASSWORD ssh root@HOST 'journalctl -u APP_NAME -f'`                  |
| Restart service       | `sshpass -p PASSWORD ssh root@HOST 'systemctl restart APP_NAME'`                 |
| Reload nginx          | `sshpass -p PASSWORD ssh root@HOST 'docker exec nginx nginx -s reload'`          |
| Show onion address    | `sshpass -p PASSWORD ssh root@HOST 'cat /var/lib/tor/moneroshamelist/hostname'`  |
| Renew SSL certificate | `sshpass -p PASSWORD ssh root@HOST 'certbot renew && docker restart nginx'`      |

## .gitignore

At minimum the following must be ignored:

```
**/appsettings.json
**/appsettings.Development.json
CI/deploy-config.ps1
CI/deploy-config.sh
backups/
publish/
```

## License

See `LICENSE` file.