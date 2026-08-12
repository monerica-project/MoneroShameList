#!/usr/bin/env bash
#
# Deploys MoneroShameList to its ACTUAL production host, monerica-vps, where
# moneroshamelist.com is served (systemd service `moneroshamelist`, user www-data,
# /var/www/moneroshamelist, Kestrel on 127.0.0.1:5140, host nginx).
#
# NOTE: the legacy CI/deploy.sh points at the old Docker host — that is NOT where the live
# site runs, so deploys there never reach production. Use THIS script instead.
#
# It EXCLUDES appsettings*.json from the sync so it can never overwrite the server's
# production DB connection string (doing so previously took the site down).
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$SCRIPT_DIR/../MoneroShameList/MoneroShameList/MoneroShameList.csproj"
OUT="$SCRIPT_DIR/../publish"
SSH_TARGET="monerica-vps"          # ~/.ssh/config alias (host, port, and user come from your ~/.ssh/config)
SSH_PORT=56777
DEPLOY_PATH="/var/www/moneroshamelist"
SERVICE="moneroshamelist"

echo "==> Publishing (linux-x64, framework-dependent)"
rm -rf "$OUT"
dotnet publish "$PROJECT" -c Release -r linux-x64 --self-contained false \
    -o "$OUT" /p:ErrorOnDuplicatePublishOutputFiles=false

echo "==> Syncing to $SSH_TARGET:$DEPLOY_PATH (server config + data protection keys preserved)"
rsync -rlptDz --rsync-path="sudo rsync" -e "ssh -p $SSH_PORT" \
    --exclude 'appsettings.json' \
    --exclude 'appsettings.Production.json' \
    --exclude 'appsettings.Development.json' \
    --exclude 'wwwroot/uploads/' \
    "$OUT"/ "$SSH_TARGET":"$DEPLOY_PATH"/

echo "==> Restarting service"
ssh -p "$SSH_PORT" "$SSH_TARGET" "sudo chown -R www-data:www-data $DEPLOY_PATH && sudo systemctl restart $SERVICE"
sleep 4
state=$(ssh -p "$SSH_PORT" "$SSH_TARGET" "systemctl is-active $SERVICE" || true)
echo "    service: $state"

echo "==> Health check"
code=$(curl -s -o /dev/null -w "%{http_code}" https://moneroshamelist.com/ || echo 000)
echo "    https://moneroshamelist.com/ → HTTP $code"
[ "$state" = "active" ] && [ "$code" = "200" ] && echo "==> Done." || { echo "!! Check the service (journalctl -u $SERVICE)"; exit 1; }
