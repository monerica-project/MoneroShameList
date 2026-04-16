# deploy.ps1
# Run from the CI folder: .\deploy.ps1
#
# Flags:
#   -SkipBuild    skip dotnet publish
#   -SSL          install Let's Encrypt SSL after deploy

param(
    [switch]$SkipBuild,
    [switch]$SSL
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# -- Load config ---------------------------------------------------------------
$configPath = Join-Path $PSScriptRoot "deploy-config.ps1"
if (-not (Test-Path $configPath)) {
    Write-Error "deploy-config.ps1 not found next to deploy.ps1"
    exit 1
}
. $configPath

# -- Helpers -------------------------------------------------------------------
function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "    OK: $msg" -ForegroundColor Green }

function SSH($cmd) {
    & $PLINK -ssh -pw $SSH_PASSWORD -batch "$SSH_USER@$SSH_HOST" $cmd
    if ($LASTEXITCODE -ne 0) { Write-Error "SSH command failed: $cmd"; exit 1 }
}

function SSH-Ignore($cmd) {
    & $PLINK -ssh -pw $SSH_PASSWORD -batch "$SSH_USER@$SSH_HOST" $cmd
}

function SCP($local, $remote) {
    & $PSCP -pw $SSH_PASSWORD -r -batch $local "${SSH_USER}@${SSH_HOST}:${remote}"
    if ($LASTEXITCODE -ne 0) { Write-Error "SCP failed: $local -> $remote"; exit 1 }
}

function Save-UnixFile([string]$path, [string]$content) {
    $clean = $content -replace "`r`n", "`n" -replace "`r", "`n"
    [System.IO.File]::WriteAllText($path, $clean, [System.Text.UTF8Encoding]::new($false))
}

function Run-RemoteScript([string]$localPath, [string]$remotePath) {
    SCP $localPath $remotePath
    SSH "chmod +x $remotePath && bash $remotePath"
}

function Build-NginxConf([bool]$ssl) {
    if ($ssl) {
        $conf  = "server {`n"
        $conf += "    listen 80;`n"
        $conf += "    server_name $DOMAIN www.$DOMAIN;`n"
        $conf += "    return 301 https://`$host`$request_uri;`n"
        $conf += "}`n`n"
        $conf += "server {`n"
        $conf += "    listen 443 ssl;`n"
        $conf += "    server_name $DOMAIN www.$DOMAIN;`n"
        $conf += "    ssl_certificate /etc/letsencrypt/live/$DOMAIN/fullchain.pem;`n"
        $conf += "    ssl_certificate_key /etc/letsencrypt/live/$DOMAIN/privkey.pem;`n"
        $conf += "    ssl_protocols TLSv1.2 TLSv1.3;`n"
        $conf += "    ssl_ciphers HIGH:!aNULL:!MD5;`n`n"
        $conf += "    location / {`n"
        $conf += "        proxy_pass         http://172.17.0.1:$APP_PORT;`n"
        $conf += "        proxy_http_version 1.1;`n"
        $conf += "        proxy_set_header   Upgrade `$http_upgrade;`n"
        $conf += "        proxy_set_header   Connection keep-alive;`n"
        $conf += "        proxy_set_header   Host `$host;`n"
        $conf += "        proxy_set_header   X-Real-IP `$remote_addr;`n"
        $conf += "        proxy_set_header   X-Forwarded-For `$proxy_add_x_forwarded_for;`n"
        $conf += "        proxy_set_header   X-Forwarded-Proto `$scheme;`n"
        $conf += "        proxy_cache_bypass `$http_upgrade;`n"
        $conf += "    }`n"
        $conf += "}`n"
    } else {
        $conf  = "server {`n"
        $conf += "    listen 80;`n"
        $conf += "    server_name $DOMAIN www.$DOMAIN;`n`n"
        $conf += "    location / {`n"
        $conf += "        proxy_pass         http://172.17.0.1:$APP_PORT;`n"
        $conf += "        proxy_http_version 1.1;`n"
        $conf += "        proxy_set_header   Upgrade `$http_upgrade;`n"
        $conf += "        proxy_set_header   Connection keep-alive;`n"
        $conf += "        proxy_set_header   Host `$host;`n"
        $conf += "        proxy_set_header   X-Real-IP `$remote_addr;`n"
        $conf += "        proxy_set_header   X-Forwarded-For `$proxy_add_x_forwarded_for;`n"
        $conf += "        proxy_set_header   X-Forwarded-Proto `$scheme;`n"
        $conf += "        proxy_cache_bypass `$http_upgrade;`n"
        $conf += "    }`n"
        $conf += "}`n"
    }
    return $conf
}

function Wait-ForApp {
    Write-Step "Waiting for app to become healthy"
    $maxAttempts = 24
    $attempt = 0
    $healthy = $false
    while ($attempt -lt $maxAttempts) {
        $attempt++
        $status = (& $PLINK -ssh -pw $SSH_PASSWORD -batch "$SSH_USER@$SSH_HOST" `
            "curl -s -o /dev/null -w '%{http_code}' http://localhost:$APP_PORT/ 2>/dev/null || echo 000").Trim()
        if ($status -match "^(200|301|302|303)$") { $healthy = $true; break }
        Write-Host "    Attempt $attempt/$maxAttempts - HTTP $status, retrying in 5s..." -ForegroundColor Yellow
        Start-Sleep -Seconds 5
    }
    if (-not $healthy) {
        Write-Host "    WARNING: App did not respond after $maxAttempts attempts." -ForegroundColor Red
    } else {
        Write-Ok "App is healthy (HTTP $status)"
    }
}

# -- Step 1: Build -------------------------------------------------------------
if (-not $SkipBuild) {
    Write-Step "Building"
    $publishOut = Join-Path $PSScriptRoot "..\publish"
    if (Test-Path $publishOut) { Remove-Item $publishOut -Recurse -Force }
    dotnet publish $WEB_PROJECT -c Release -r linux-x64 --self-contained false -o $publishOut /p:ErrorOnDuplicatePublishOutputFiles=false
    if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }
    Write-Ok "Built"
}

# -- Step 2: Bootstrap server --------------------------------------------------
Write-Step "Bootstrapping server"
SSH-Ignore "apt-get update -q"
SSH "command -v dotnet > /dev/null 2>&1 || (wget -q https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O /tmp/ms.deb && dpkg -i /tmp/ms.deb && apt-get update -q && apt-get install -y aspnetcore-runtime-10.0)"
SSH "command -v psql > /dev/null 2>&1 || (apt-get install -y postgresql postgresql-contrib && systemctl enable postgresql && systemctl start postgresql)"
SSH-Ignore "systemctl start postgresql 2>/dev/null || true"
SSH "mkdir -p $DEPLOY_PATH"
SSH-Ignore "ufw allow 80/tcp 2>/dev/null || true"
SSH-Ignore "ufw allow 443/tcp 2>/dev/null || true"
SSH-Ignore "ufw allow $APP_PORT/tcp 2>/dev/null || true"
Write-Ok "Server ready"

# -- Step 3: PostgreSQL --------------------------------------------------------
Write-Step "Setting up PostgreSQL"
$pgScript = "#!/bin/bash`n" +
    "sudo -u postgres psql -c `"CREATE USER $DB_USER WITH PASSWORD '$DB_PASSWORD';`" 2>/dev/null || true`n" +
    "sudo -u postgres psql -c `"CREATE DATABASE $DB_NAME OWNER $DB_USER;`" 2>/dev/null || true`n" +
    "sudo -u postgres psql -c `"GRANT ALL PRIVILEGES ON DATABASE $DB_NAME TO $DB_USER;`" 2>/dev/null || true`n" +
    "sudo -u postgres psql -d $DB_NAME -c `"GRANT ALL ON SCHEMA public TO $DB_USER;`" 2>/dev/null || true`n" +
    "sudo -u postgres psql -d $DB_NAME -c `"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO $DB_USER;`" 2>/dev/null || true`n" +
    "echo `"DB setup complete`"`n"
$pgScriptFile = Join-Path $env:TEMP "pg-setup.sh"
Save-UnixFile $pgScriptFile $pgScript
Run-RemoteScript $pgScriptFile "/tmp/pg-setup.sh"
Write-Ok "Database ready"

# -- Step 4: Write appsettings.json --------------------------------------------
Write-Step "Writing config"
$connString = "Host=localhost;Port=5432;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD"
$appSettings = "{`n  `"ConnectionStrings`": {`n    `"DefaultConnection`": `"$connString`"`n  },`n  `"Admin`": {`n    `"Username`": `"$ADMIN_USERNAME`",`n    `"Password`": `"$ADMIN_PASSWORD`"`n  }`n}`n"
$appSettingsFile = Join-Path $env:TEMP "appsettings.json"
Save-UnixFile $appSettingsFile $appSettings
Write-Ok "Config ready"

# -- Step 5: Deploy ------------------------------------------------------------
Write-Step "Deploying"
SSH-Ignore "systemctl stop $APP_NAME 2>/dev/null || true"

$publishOut = Join-Path $PSScriptRoot "..\publish"
$tarFile    = Join-Path $env:TEMP "web.tar.gz"
Push-Location $publishOut
& tar -czf $tarFile .
Pop-Location

SCP $tarFile "/tmp/web.tar.gz"
SSH "mkdir -p $DEPLOY_PATH && tar -xzf /tmp/web.tar.gz -C $DEPLOY_PATH && rm /tmp/web.tar.gz"
SCP $appSettingsFile "$DEPLOY_PATH/appsettings.json"
SSH "chown -R www-data:www-data $DEPLOY_PATH && chmod -R 755 $DEPLOY_PATH"

$svcContent  = "[Unit]`n"
$svcContent += "Description=MoneroShameList ($DOMAIN)`n"
$svcContent += "After=network.target postgresql.service`n`n"
$svcContent += "[Service]`n"
$svcContent += "WorkingDirectory=$DEPLOY_PATH`n"
$svcContent += "ExecStart=/usr/bin/dotnet $DEPLOY_PATH/MoneroShameList.dll`n"
$svcContent += "Restart=always`n"
$svcContent += "RestartSec=10`n"
$svcContent += "User=www-data`n"
$svcContent += "Environment=ASPNETCORE_ENVIRONMENT=Production`n"
$svcContent += "Environment=ASPNETCORE_URLS=http://0.0.0.0:$APP_PORT`n"
$svcContent += "Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false`n`n"
$svcContent += "[Install]`n"
$svcContent += "WantedBy=multi-user.target`n"

$svcFile = Join-Path $env:TEMP "$APP_NAME.service"
Save-UnixFile $svcFile $svcContent
SCP $svcFile "/etc/systemd/system/$APP_NAME.service"
SSH "systemctl daemon-reload && systemctl enable $APP_NAME && systemctl restart $APP_NAME"

Wait-ForApp
Write-Ok "Deployed on port $APP_PORT"

# -- Step 7: Configure Nginx ---------------------------------------------------
Write-Step "Configuring Nginx"
$certExists = (& $PLINK -ssh -pw $SSH_PASSWORD -batch "$SSH_USER@$SSH_HOST" `
    "test -f /etc/letsencrypt/live/$DOMAIN/fullchain.pem && echo yes || echo no").Trim()

if ($certExists -eq "yes") {
    SSH "cp -L /etc/letsencrypt/live/$DOMAIN/fullchain.pem /tmp/fullchain.pem && cp -L /etc/letsencrypt/live/$DOMAIN/privkey.pem /tmp/privkey.pem"
    SSH "docker exec nginx mkdir -p /etc/letsencrypt/live/$DOMAIN"
    SSH "docker cp /tmp/fullchain.pem nginx:/etc/letsencrypt/live/$DOMAIN/fullchain.pem"
    SSH "docker cp /tmp/privkey.pem nginx:/etc/letsencrypt/live/$DOMAIN/privkey.pem"
    $conf = Build-NginxConf $true
    Write-Ok "Nginx configured for $DOMAIN (HTTPS)"
} else {
    $conf = Build-NginxConf $false
    Write-Ok "Nginx configured for $DOMAIN (HTTP only - run with -SSL to enable HTTPS)"
}
$confFile = Join-Path $env:TEMP "$APP_NAME.nginx.conf"
Save-UnixFile $confFile $conf
SCP $confFile "/tmp/$APP_NAME.conf"
SSH "docker cp /tmp/$APP_NAME.conf nginx:/etc/nginx/conf.d/$APP_NAME.conf"
SSH "docker exec nginx nginx -s reload"

# -- Step 8: SSL (first time only) ---------------------------------------------
if ($SSL) {
    Write-Step "Getting SSL certificate"
    SSH "apt-get install -y certbot"

    $sslScript  = "#!/bin/bash`n"
    $sslScript += "set -e`n"
    $sslScript += "docker stop nginx`n"
    $sslScript += "sleep 2`n"
    $sslScript += "certbot certonly --standalone -d $DOMAIN -d www.$DOMAIN --non-interactive --agree-tos -m admin@$DOMAIN`n"
    $sslScript += "docker start nginx`n"
    $sslScript += "sleep 2`n"
    $sslScriptFile = Join-Path $env:TEMP "ssl-setup.sh"
    Save-UnixFile $sslScriptFile $sslScript
    Run-RemoteScript $sslScriptFile "/tmp/ssl-setup.sh"

    SSH "cp -L /etc/letsencrypt/live/$DOMAIN/fullchain.pem /tmp/fullchain.pem && cp -L /etc/letsencrypt/live/$DOMAIN/privkey.pem /tmp/privkey.pem"
    SSH "docker exec nginx mkdir -p /etc/letsencrypt/live/$DOMAIN"
    SSH "docker cp /tmp/fullchain.pem nginx:/etc/letsencrypt/live/$DOMAIN/fullchain.pem"
    SSH "docker cp /tmp/privkey.pem nginx:/etc/letsencrypt/live/$DOMAIN/privkey.pem"

    $conf = Build-NginxConf $true
    $confFile = Join-Path $env:TEMP "$APP_NAME.ssl.nginx.conf"
    Save-UnixFile $confFile $conf
    SCP $confFile "/tmp/$APP_NAME.conf"
    SSH "docker cp /tmp/$APP_NAME.conf nginx:/etc/nginx/conf.d/$APP_NAME.conf"
    SSH "docker exec nginx nginx -s reload"
    Write-Ok "SSL installed for $DOMAIN"
}

# -- Done ----------------------------------------------------------------------
Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host " Deployment complete!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host " Site: http$(if ($SSL) {'s'})://$DOMAIN" -ForegroundColor White
Write-Host ""
Write-Host " Useful commands:" -ForegroundColor Gray
Write-Host "   Status: plink -ssh -pw PASSWORD root@$SSH_HOST systemctl status $APP_NAME" -ForegroundColor Gray
Write-Host "   Logs:   plink -ssh -pw PASSWORD root@$SSH_HOST journalctl -u $APP_NAME -f" -ForegroundColor Gray
Write-Host ""