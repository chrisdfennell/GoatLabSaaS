#!/usr/bin/env bash
# Generates a locally-trusted TLS cert for the app, output: certs/app.pfx
# Requires mkcert on PATH or at tools/mkcert.exe. Run `mkcert -install` once
# before first use so the CA is in your Windows/macOS trust store.
#
# Password for the .pfx is taken from $CERT_PASSWORD (falls back to "changeme").
# Keep it matched with CERT_PASSWORD in .env so Kestrel can open the file.

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
OUT_DIR="${ROOT}/certs"
OUT_FILE="${OUT_DIR}/app.pfx"
PASSWORD="${CERT_PASSWORD:-changeme}"

if command -v mkcert >/dev/null 2>&1; then
  MKCERT=mkcert
elif [ -x "${ROOT}/tools/mkcert.exe" ]; then
  MKCERT="${ROOT}/tools/mkcert.exe"
else
  echo "mkcert not found on PATH and tools/mkcert.exe missing." >&2
  echo "See tools/certs/README.md for install instructions." >&2
  exit 1
fi

mkdir -p "${OUT_DIR}"

"${MKCERT}" -pkcs12 -p12-file "${OUT_FILE}" \
  goatlab.local localhost 127.0.0.1

# mkcert encrypts .pfx with the default password "changeit"; re-export if the
# caller wants a different one. For now keep "changeit" and document it.
echo
echo "Wrote ${OUT_FILE}"
echo "PFX password: changeit (mkcert default)"
echo "Set CERT_PASSWORD=changeit in .env so Kestrel can load it."
