#!/bin/sh
# Run this on the NAS in /share/Container/GoatLabSaaS to (re)start the stack
# the right way. Handles container order, shows logs, and has a --fresh
# mode that wipes the DB volume for a clean migration.
#
# Usage:
#   ./scripts/nas-deploy.sh           # normal up: build if source changed, start, show logs
#   ./scripts/nas-deploy.sh --fresh   # destroy DB volume first (wipes ALL data)
#   ./scripts/nas-deploy.sh --logs    # just tail logs on already-running stack

set -e

# QNAP's non-interactive ssh PATH omits docker. Add Container Station's
# binary locations so this script works whether run interactively or via ssh.
export PATH="/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:$PATH"
for p in /share/CACHEDEV1_DATA/.qpkg/container-station/bin \
         /share/ZFS*/.qpkg/container-station/bin \
         /share/*/.qpkg/container-station/bin; do
    [ -d "$p" ] && export PATH="$p:$PATH"
done

if ! command -v docker >/dev/null 2>&1; then
    echo "ERROR: 'docker' not found on PATH." >&2
    echo "Current PATH: $PATH" >&2
    echo "Run on the NAS to locate it:  find /share -name docker -type f 2>/dev/null | head" >&2
    exit 127
fi

cd "$(dirname "$0")/.."

if [ ! -f .env ]; then
    echo "ERROR: .env missing. Copy .env.example to .env and set SA_PASSWORD."
    exit 1
fi

case "${1:-}" in
    --logs)
        exec docker compose logs -f goatlab
        ;;
    --fresh)
        echo "==> Stopping stack"
        docker compose down
        echo "==> Wiping DB volume (this deletes ALL data)"
        docker volume rm goatlabsaas_goatlab-mssql 2>/dev/null || true
        ;;
    "")
        echo "==> Stopping stack"
        docker compose down
        ;;
    *)
        echo "Unknown flag: $1"
        echo "Usage: $0 [--fresh|--logs]"
        exit 1
        ;;
esac

echo "==> Building image"
docker compose build goatlab

echo "==> Starting stack (sqlserver first, then goatlab when healthy)"
docker compose up -d

echo "==> Waiting 5s for containers to settle"
sleep 5

docker compose ps

echo ""
echo "==> Last 40 lines of goatlab logs:"
docker compose logs --tail=40 goatlab

echo ""
echo "Done. Tail live logs with: ./scripts/nas-deploy.sh --logs"
