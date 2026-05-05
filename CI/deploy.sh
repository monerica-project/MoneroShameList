#!/usr/bin/env bash
# deploy.sh - MoneroShameList deployment for Linux
# Run from the CI folder: ./deploy.sh [flags]
#
# Flags:
#   --skip-build    skip dotnet publish
#   --skip-backup   skip the pre-deploy DB backup
#   --backup-only   make a DB backup and exit (no deploy)
#   --ssl           install Let's Encrypt SSL after deploy
#   --tor           set up Tor hidden service

set -euo pipefail

# -- Flags --------------------------------------------------------------------
SKIP_BUILD=0
SKIP_BACKUP=0
BACKUP_ONLY=0
SSL=0
TOR=0
for a in "$@"; do
    case "$a" in
        --skip-build)  SKIP_BUILD=1 ;;
        --skip-backup) SKIP_BACKUP=1 ;;
        --backup-only) BACKUP_ONLY=1 ;;
        --ssl)         SSL=1 ;;
        --tor)         TOR=1 ;;
        *) echo "Unknown flag: $a"; exit 1 ;;
    esac
done

# -- Load config --------------------------------------------------------------
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
CFG="$SCRIPT_DIR/deploy-config.sh"
if [[ ! -f "$CFG" ]]; then
    echo "deploy-config.sh not found next to deploy.sh" >&2
    exit 1
fi
# shellcheck disable=SC1090
source "$CFG"

for v in SSH_PASSWORD DB_PASSWORD ADMIN_PASSWORD; do
    if [[ -z "${!v:-}" ]]; then
        echo "$v is empty in deploy-config.sh" >&2
        exit 1
    fi
done

# -- Tools --------------------------------------------------------------------
command -v sshpass >/dev/null || { echo "Install sshpass: sudo apt install sshpass"; exit 1; }
command -v dotnet  >/dev/null || { echo "Install .NET SDK first"; exit 1; }
command -v tar     >/dev/null || { echo "Install tar"; exit 1; }

SSH_OPTS=(-o StrictHostKeyChecking=accept-new -o LogLevel=ERROR)

step()  { printf '\n\033[36m==> %s\033[0m\n' "$1"; }
ok()    { printf '    \033[32mOK: %s\033[0m\n' "$1"; }
warn()  { printf '    \033[33mWARN: %s\033[0m\n' "$1"; }
note()  { printf '    %s\n' "$1"; }

ssh_run()    { sshpass -p "$SSH_PASSWORD" ssh "${SSH_OPTS[@]}" "$SSH_USER@$SSH_HOST" "$1"; }
ssh_ignore() { sshpass -p "$SSH_PASSWORD" ssh "${SSH_OPTS[@]}" "$SSH_USER@$SSH_HOST" "$1" || true; }
scp_up()     { sshpass -p "$SSH_PASSWORD" scp "${SSH_OPTS[@]}" -r "$1" "$SSH_USER@$SSH_HOST:$2"; }
scp_down()   { sshpass -p "$SSH_PASSWORD" scp "${SSH_OPTS[@]}" "$SSH_USER@$SSH_HOST:$1" "$2"; }

run_remote_script() {
    local local_path="$1" remote_path="$2"
    scp_up "$local_path" "$remote_path"
    ssh_run "chmod +x '$remote_path' && bash '$remote_path'"
}

TMPDIR_LOCAL="$(mktemp -d)"
trap 'rm -rf "$TMPDIR_LOCAL"' EXIT

# -- DB Backup ---------------------------------------------------------------
backup_db() {
    step "Backing up PostgreSQL"
    local db_exists
    db_exists="$(ssh_ignore "sudo -u postgres psql -tAc \"SELECT 1 FROM pg_database WHERE datname='$DB_NAME'\" 2>/dev/null" | tr -d '[:space:]')"
    if [[ "$db_exists" != "1" ]]; then
        warn "Database $DB_NAME does not exist yet, skipping backup"
        return 0
    fi

    local stamp; stamp=$(date +%Y%m%d-%H%M%S)
    local remote="/tmp/${DB_NAME}-${stamp}.sql.gz"
    local local_dir="$SCRIPT_DIR/../backups"
    mkdir -p "$local_dir"
    local local_file="$local_dir/${DB_NAME}-${stamp}.sql.gz"

    ssh_run "sudo -u postgres pg_dump '$DB_NAME' | gzip > '$remote' && chmod 644 '$remote'"
    scp_down "$remote" "$local_file"
    ssh_ignore "rm -f '$remote'"

    # Keep last 20 local backups, delete older
    ls -1t "$local_dir"/*.sql.gz 2>/dev/null | tail -n +21 | xargs -r rm -f

    ok "Backup saved: $local_file"
}

if [[ $BACKUP_ONLY -eq 1 ]]; then
    backup_db
    echo
    echo "Backup-only run complete."
    exit 0
fi

# -- Step 1: Build ------------------------------------------------------------
PUBLISH_OUT="$SCRIPT_DIR/../publish"
if [[ $SKIP_BUILD -eq 0 ]]; then
    step "Building"
    rm -rf "$PUBLISH_OUT"
    dotnet publish "$WEB_PROJECT" -c Release -r linux-x64 --self-contained false \
        -o "$PUBLISH_OUT" /p:ErrorOnDuplicatePublishOutputFiles=false
    ok "Built"
fi

# -- Step 2: Bootstrap server -------------------------------------------------
step "Bootstrapping server"
ssh_ignore "apt-get update -q"
ssh_run "command -v dotnet >/dev/null 2>&1 || (wget -q https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O /tmp/ms.deb && dpkg -i /tmp/ms.deb && apt-get update -q && apt-get install -y aspnetcore-runtime-10.0)"
ssh_run "command -v psql >/dev/null 2>&1 || (apt-get install -y postgresql postgresql-contrib && systemctl enable postgresql && systemctl start postgresql)"
ssh_ignore "systemctl start postgresql 2>/dev/null || true"
ssh_run "mkdir -p '$DEPLOY_PATH'"
ssh_ignore "ufw allow 80/tcp 2>/dev/null || true"
ssh_ignore "ufw allow 443/tcp 2>/dev/null || true"
ssh_ignore "ufw allow $APP_PORT/tcp 2>/dev/null || true"
ok "Server ready"

# -- Step 3: PostgreSQL -------------------------------------------------------
step "Setting up PostgreSQL"
cat > "$TMPDIR_LOCAL/pg-setup.sh" <<EOF
#!/bin/bash
sudo -u postgres psql -c "CREATE USER $DB_USER WITH PASSWORD '$DB_PASSWORD';" 2>/dev/null || true
sudo -u postgres psql -c "CREATE DATABASE $DB_NAME OWNER $DB_USER;" 2>/dev/null || true
sudo -u postgres psql -c "GRANT ALL PRIVILEGES ON DATABASE $DB_NAME TO $DB_USER;" 2>/dev/null || true
sudo -u postgres psql -d $DB_NAME -c "GRANT ALL ON SCHEMA public TO $DB_USER;" 2>/dev/null || true
sudo -u postgres psql -d $DB_NAME -c "ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO $DB_USER;" 2>/dev/null || true
echo "DB setup complete"
EOF
run_remote_script "$TMPDIR_LOCAL/pg-setup.sh" "/tmp/pg-setup.sh"
ok "Database ready"

# -- Step 3.5: Backup BEFORE we touch the deployed app ------------------------
if [[ $SKIP_BACKUP -eq 0 ]]; then
    backup_db
fi

# -- Step 4: Write appsettings.json -------------------------------------------
step "Writing config"
CONN_STRING="Host=localhost;Port=5432;Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASSWORD}"
ONION_HOST="$(ssh_ignore "cat /var/lib/tor/moneroshamelist/hostname 2>/dev/null" | tr -d '[:space:]')"

if [[ -n "$ONION_HOST" ]]; then
    note "Found onion: $ONION_HOST"
    cat > "$TMPDIR_LOCAL/appsettings.json" <<EOF
{
  "ConnectionStrings": {
    "DefaultConnection": "$CONN_STRING"
  },
  "Admin": {
    "Username": "$ADMIN_USERNAME",
    "Password": "$ADMIN_PASSWORD"
  },
  "Tor": {
    "OnionHost": "$ONION_HOST"
  }
}
EOF
else
    cat > "$TMPDIR_LOCAL/appsettings.json" <<EOF
{
  "ConnectionStrings": {
    "DefaultConnection": "$CONN_STRING"
  },
  "Admin": {
    "Username": "$ADMIN_USERNAME",
    "Password": "$ADMIN_PASSWORD"
  }
}
EOF
fi
ok "Config ready"

# -- Maintenance + nginx builders --------------------------------------------
write_maintenance_html() {
    cat > "$1" <<'HTML'
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <meta http-equiv="refresh" content="15">
  <title>Updating - MoneroShameList</title>
  <style>
    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      min-height: 100vh; display: flex; align-items: center; justify-content: center;
      background: #0f0f0f; color: #e0e0e0;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
    }
    .card { text-align: center; padding: 3rem 2.5rem; max-width: 440px; }
    .icon { font-size: 2.8rem; margin-bottom: 1.25rem; display: inline-block; animation: spin 3s linear infinite; }
    @keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
    h1 { font-size: 1.5rem; font-weight: 600; margin-bottom: 0.75rem; color: #ff6600; }
    p  { font-size: 0.95rem; line-height: 1.6; color: #aaa; }
    .note { margin-top: 1.75rem; font-size: 0.8rem; color: #555; }
  </style>
</head>
<body>
  <div class="card">
    <div class="icon">&#9881;</div>
    <h1>Updating in progress</h1>
    <p>MoneroShameList is being updated and will be back shortly.</p>
    <p class="note">This page refreshes automatically every 15 seconds.</p>
  </div>
</body>
</html>
HTML
}

build_nginx_conf() {
    # $1 = "ssl" or "plain"; $2 = output path
    local mode="$1" out="$2"
    if [[ "$mode" == "ssl" ]]; then
        cat > "$out" <<EOF
server {
    listen 80;
    server_name $DOMAIN www.$DOMAIN;
    return 301 https://\$host\$request_uri;
}

server {
    listen 443 ssl;
    server_name $DOMAIN www.$DOMAIN;
    ssl_certificate /etc/letsencrypt/live/$DOMAIN/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/$DOMAIN/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;

    location / {
        proxy_pass         http://172.17.0.1:$APP_PORT;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade \$http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host \$host;
        proxy_set_header   X-Real-IP \$remote_addr;
        proxy_set_header   X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
    }
}
EOF
    else
        cat > "$out" <<EOF
server {
    listen 80;
    server_name $DOMAIN www.$DOMAIN;

    location / {
        proxy_pass         http://172.17.0.1:$APP_PORT;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade \$http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host \$host;
        proxy_set_header   X-Real-IP \$remote_addr;
        proxy_set_header   X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
    }
}
EOF
    fi
}

build_maintenance_conf() {
    local mode="$1" out="$2"
    if [[ "$mode" == "ssl" ]]; then
        cat > "$out" <<EOF
server {
    listen 80;
    server_name $DOMAIN www.$DOMAIN;
    return 301 https://\$host\$request_uri;
}

server {
    listen 443 ssl;
    server_name $DOMAIN www.$DOMAIN;
    ssl_certificate /etc/letsencrypt/live/$DOMAIN/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/$DOMAIN/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    location / {
        root /var/www;
        try_files /maintenance.html =503;
        add_header Retry-After 30;
    }
}
EOF
    else
        cat > "$out" <<EOF
server {
    listen 80;
    server_name $DOMAIN www.$DOMAIN;
    location / {
        root /var/www;
        try_files /maintenance.html =503;
        add_header Retry-After 30;
    }
}
EOF
    fi
}

enable_maintenance_page() {
    step "Enabling maintenance page"
    local html="$TMPDIR_LOCAL/maintenance.html"
    write_maintenance_html "$html"
    scp_up "$html" "/tmp/maintenance.html"
    ssh_run "docker exec nginx mkdir -p /var/www"
    ssh_run "docker cp /tmp/maintenance.html nginx:/var/www/maintenance.html"

    # If certs exist on the host but not yet in the container, copy them in now.
    # Without this, switching to the SSL maintenance config will fail because
    # nginx (in the container) can't find the cert files.
    local host_cert
    host_cert="$(ssh_ignore "test -f /etc/letsencrypt/live/$DOMAIN/fullchain.pem && echo yes || echo no" | tr -d '[:space:]')"
    if [[ "$host_cert" == "yes" ]]; then
        ssh_ignore "cp -L /etc/letsencrypt/live/$DOMAIN/fullchain.pem /tmp/fullchain.pem && cp -L /etc/letsencrypt/live/$DOMAIN/privkey.pem /tmp/privkey.pem"
        ssh_ignore "docker exec nginx mkdir -p /etc/letsencrypt/live/$DOMAIN"
        ssh_ignore "docker cp /tmp/fullchain.pem nginx:/etc/letsencrypt/live/$DOMAIN/fullchain.pem"
        ssh_ignore "docker cp /tmp/privkey.pem nginx:/etc/letsencrypt/live/$DOMAIN/privkey.pem"
    fi

    # Decide SSL vs plain based on what's actually inside the container,
    # not what's on the host.
    local cert_now
    cert_now="$(ssh_ignore "docker exec nginx test -f /etc/letsencrypt/live/$DOMAIN/fullchain.pem && echo yes || echo no" | tr -d '[:space:]')"
    local mconf="$TMPDIR_LOCAL/$APP_NAME.maint.conf"
    if [[ "$cert_now" == "yes" ]]; then
        build_maintenance_conf ssl   "$mconf"
    else
        build_maintenance_conf plain "$mconf"
    fi
    scp_up "$mconf" "/tmp/$APP_NAME.conf"
    ssh_run "docker cp /tmp/$APP_NAME.conf nginx:/etc/nginx/conf.d/$APP_NAME.conf"
    ssh_run "docker exec nginx nginx -s reload"
    ok "Maintenance page live"
}

wait_for_app() {
    step "Waiting for app to become healthy"
    local max=24 attempt=0 status="000" healthy=0
    while [[ $attempt -lt $max ]]; do
        attempt=$((attempt+1))
        status="$(ssh_ignore "curl -s -o /dev/null -w '%{http_code}' http://localhost:$APP_PORT/ 2>/dev/null || echo 000" | tr -d '[:space:]')"
        if [[ "$status" =~ ^(200|301|302|303)$ ]]; then healthy=1; break; fi
        note "Attempt $attempt/$max - HTTP $status, retrying in 5s..."
        sleep 5
    done
    if [[ $healthy -eq 1 ]]; then
        ok "App is healthy (HTTP $status)"
    else
        warn "App did not respond after $max attempts."
    fi
}

# -- Step 5: Deploy -----------------------------------------------------------
step "Deploying"
enable_maintenance_page
ssh_ignore "systemctl stop $APP_NAME 2>/dev/null || true"

TARFILE="$TMPDIR_LOCAL/web.tar.gz"
( cd "$PUBLISH_OUT" && tar -czf "$TARFILE" . )

scp_up "$TARFILE" "/tmp/web.tar.gz"
ssh_run "mkdir -p '$DEPLOY_PATH' && tar -xzf /tmp/web.tar.gz -C '$DEPLOY_PATH' && rm /tmp/web.tar.gz"
scp_up "$TMPDIR_LOCAL/appsettings.json" "$DEPLOY_PATH/appsettings.json"
ssh_run "chown -R www-data:www-data '$DEPLOY_PATH' && chmod -R 755 '$DEPLOY_PATH'"

cat > "$TMPDIR_LOCAL/$APP_NAME.service" <<EOF
[Unit]
Description=MoneroShameList ($DOMAIN)
After=network.target postgresql.service

[Service]
WorkingDirectory=$DEPLOY_PATH
ExecStart=/usr/bin/dotnet $DEPLOY_PATH/MoneroShameList.dll
Restart=always
RestartSec=10
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:$APP_PORT
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
EOF
scp_up "$TMPDIR_LOCAL/$APP_NAME.service" "/etc/systemd/system/$APP_NAME.service"
ssh_run "systemctl daemon-reload && systemctl enable $APP_NAME && systemctl restart $APP_NAME"

wait_for_app
ok "Deployed on port $APP_PORT"

# -- Step 7: Configure Nginx --------------------------------------------------
step "Configuring Nginx"
CERT_EXISTS="$(ssh_run "test -f /etc/letsencrypt/live/$DOMAIN/fullchain.pem && echo yes || echo no" | tr -d '[:space:]')"
NCONF="$TMPDIR_LOCAL/$APP_NAME.nginx.conf"
if [[ "$CERT_EXISTS" == "yes" ]]; then
    ssh_run "cp -L /etc/letsencrypt/live/$DOMAIN/fullchain.pem /tmp/fullchain.pem && cp -L /etc/letsencrypt/live/$DOMAIN/privkey.pem /tmp/privkey.pem"
    ssh_run "docker exec nginx mkdir -p /etc/letsencrypt/live/$DOMAIN"
    ssh_run "docker cp /tmp/fullchain.pem nginx:/etc/letsencrypt/live/$DOMAIN/fullchain.pem"
    ssh_run "docker cp /tmp/privkey.pem nginx:/etc/letsencrypt/live/$DOMAIN/privkey.pem"
    build_nginx_conf ssl "$NCONF"
    ok "Nginx configured for $DOMAIN (HTTPS)"
else
    build_nginx_conf plain "$NCONF"
    ok "Nginx configured for $DOMAIN (HTTP only - run with --ssl to enable HTTPS)"
fi
scp_up "$NCONF" "/tmp/$APP_NAME.conf"
ssh_run "docker cp /tmp/$APP_NAME.conf nginx:/etc/nginx/conf.d/$APP_NAME.conf"
ssh_run "docker exec nginx nginx -s reload"

# -- Step 8: SSL (first time only) --------------------------------------------
if [[ $SSL -eq 1 ]]; then
    step "Getting SSL certificate"
    ssh_run "apt-get install -y certbot"
    cat > "$TMPDIR_LOCAL/ssl-setup.sh" <<EOF
#!/bin/bash
set -e
docker stop nginx
sleep 2
certbot certonly --standalone -d $DOMAIN -d www.$DOMAIN --non-interactive --agree-tos -m admin@$DOMAIN
docker start nginx
sleep 2
EOF
    run_remote_script "$TMPDIR_LOCAL/ssl-setup.sh" "/tmp/ssl-setup.sh"
    ssh_run "cp -L /etc/letsencrypt/live/$DOMAIN/fullchain.pem /tmp/fullchain.pem && cp -L /etc/letsencrypt/live/$DOMAIN/privkey.pem /tmp/privkey.pem"
    ssh_run "docker exec nginx mkdir -p /etc/letsencrypt/live/$DOMAIN"
    ssh_run "docker cp /tmp/fullchain.pem nginx:/etc/letsencrypt/live/$DOMAIN/fullchain.pem"
    ssh_run "docker cp /tmp/privkey.pem nginx:/etc/letsencrypt/live/$DOMAIN/privkey.pem"

    build_nginx_conf ssl "$NCONF"
    scp_up "$NCONF" "/tmp/$APP_NAME.conf"
    ssh_run "docker cp /tmp/$APP_NAME.conf nginx:/etc/nginx/conf.d/$APP_NAME.conf"
    ssh_run "docker exec nginx nginx -s reload"
    ok "SSL installed for $DOMAIN"
fi

# -- Step 9: Tor (first time only) --------------------------------------------
if [[ $TOR -eq 1 ]]; then
    step "Setting up Tor hidden service"
    cat > "$TMPDIR_LOCAL/tor-setup.sh" <<EOF
#!/bin/bash
set -e
apt-get install -y tor
systemctl enable tor
if ! grep -q 'moneroshamelist' /etc/tor/torrc; then
    echo '' >> /etc/tor/torrc
    echo '# MoneroShameList hidden service' >> /etc/tor/torrc
    echo 'HiddenServiceDir /var/lib/tor/moneroshamelist/' >> /etc/tor/torrc
    echo 'HiddenServicePort 80 127.0.0.1:$APP_PORT' >> /etc/tor/torrc
fi
systemctl restart tor
sleep 5
echo 'Onion address:'
cat /var/lib/tor/moneroshamelist/hostname
EOF
    run_remote_script "$TMPDIR_LOCAL/tor-setup.sh" "/tmp/tor-setup.sh"
    ONION="$(ssh_ignore "cat /var/lib/tor/moneroshamelist/hostname 2>/dev/null" | tr -d '[:space:]')"
    ok "Tor hidden service configured"
    printf '   \033[35mOnion: %s\033[0m\n' "${ONION:-not ready yet}"
fi

# -- Done ---------------------------------------------------------------------
echo
printf '\033[32m==========================================\033[0m\n'
printf '\033[32m Deployment complete!\033[0m\n'
printf '\033[32m==========================================\033[0m\n'
PROTO="http"; [[ $SSL -eq 1 ]] && PROTO="https"
echo " Site: $PROTO://$DOMAIN"
if [[ $TOR -eq 1 ]]; then
    ON="$(ssh_ignore "cat /var/lib/tor/moneroshamelist/hostname 2>/dev/null" | tr -d '[:space:]')"
    echo " Onion: http://${ON:-pending}"
fi
echo
echo " Useful commands:"
echo "   Status:  sshpass -p PASSWORD ssh $SSH_USER@$SSH_HOST 'systemctl status $APP_NAME'"
echo "   Logs:    sshpass -p PASSWORD ssh $SSH_USER@$SSH_HOST 'journalctl -u $APP_NAME -f'"
echo "   Backup:  ./deploy.sh --backup-only"
echo
