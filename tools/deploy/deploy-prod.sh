#!/usr/bin/env bash
# Deploy GoatLab to prod by pulling a specific image tag from Docker Hub.
#
# First run rewrites docker-compose.prod.yml to use `image:` instead of
# `build:` (one-time conversion). Subsequent runs only update the tag and
# restart the goatlab service. Safe to re-run.
#
# Usage (on the VPS, as root):
#   /opt/goatlab/tools/deploy/deploy-prod.sh           # uses default version below
#   /opt/goatlab/tools/deploy/deploy-prod.sh 1.0.2     # pin to a specific version
#   /opt/goatlab/tools/deploy/deploy-prod.sh latest    # track :latest (not recommended)
#
# Pinning is recommended: prod gets new code only when you bump the tag and
# re-run, which gives you a single rollback by editing one line.

set -euo pipefail

DEFAULT_VERSION="1.0.1"
VERSION="${1:-$DEFAULT_VERSION}"
IMAGE="fennch/goatlab:${VERSION}"
REPO_DIR="/opt/goatlab"
COMPOSE_FILE="docker-compose.prod.yml"

cd "$REPO_DIR"

echo "==> Pulling latest source (compose yaml, Caddyfile, migrations)..."
git pull

echo "==> Verifying ${IMAGE} exists on Docker Hub..."
if ! docker manifest inspect "$IMAGE" >/dev/null 2>&1; then
    echo "ERROR: ${IMAGE} not found on Docker Hub." >&2
    echo "Check https://hub.docker.com/r/fennch/goatlab/tags or wait for the publish workflow to finish." >&2
    exit 1
fi

BACKUP="${COMPOSE_FILE}.bak.$(date +%Y%m%d-%H%M%S)"
echo "==> Backing up ${COMPOSE_FILE} -> ${BACKUP}..."
cp "$COMPOSE_FILE" "$BACKUP"

echo "==> Pointing ${COMPOSE_FILE} at ${IMAGE}..."
python3 - "$COMPOSE_FILE" "$VERSION" <<'PYEOF'
import re, sys

path, version = sys.argv[1], sys.argv[2]
new_image = f"fennch/goatlab:{version}"

with open(path) as f:
    content = f.read()

build_block = """  goatlab:
    build:
      context: .
      dockerfile: Dockerfile"""
new_block = f"""  goatlab:
    image: {new_image}"""

if build_block in content:
    content = content.replace(build_block, new_block)
    print(f"  Switched from build: to image: {new_image} (one-time conversion).")
elif re.search(r'image:\s*fennch/goatlab:\S+', content):
    content, n = re.subn(
        r'image:\s*fennch/goatlab:\S+',
        f"image: {new_image}",
        content,
    )
    if n != 1:
        print(f"ERROR: expected exactly 1 image: line, found {n}", file=sys.stderr)
        sys.exit(1)
    print(f"  Updated tag to {version}.")
else:
    print("ERROR: could not find a goatlab service to update", file=sys.stderr)
    sys.exit(1)

with open(path, "w") as f:
    f.write(content)
PYEOF

echo "==> goatlab service block now reads:"
awk '/^  goatlab:$/,/^  [a-z]/' "$COMPOSE_FILE" | head -n -1

echo ""
echo "==> Pulling ${IMAGE}..."
docker compose -f "$COMPOSE_FILE" pull goatlab

echo "==> Restarting goatlab service..."
docker compose -f "$COMPOSE_FILE" up -d goatlab

echo "==> Waiting 5 s for startup..."
sleep 5

echo "==> Container status:"
docker compose -f "$COMPOSE_FILE" ps goatlab

echo ""
echo "==> Last 50 lines of logs:"
docker compose -f "$COMPOSE_FILE" logs goatlab --tail 50

echo ""
echo "==> Done. Site: https://goatlab.app"
echo "    Roll back: cp ${BACKUP} ${COMPOSE_FILE} && docker compose -f ${COMPOSE_FILE} up -d goatlab"
