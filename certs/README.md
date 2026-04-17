# Local TLS certificates

This directory holds the PFX served by Kestrel for local HTTPS development.
Nothing in here is checked in — all `*.pfx`, `*.pem`, `*.key`, `*.crt` are
`.gitignore`d. Every developer generates their own.

## One-time setup

1. Install mkcert:
   - Via winget (normal PowerShell): `winget install FiloSottile.mkcert`
   - Or download the binary into `tools/mkcert.exe` (already present in this
     repo as an un-tracked file — see `tools/certs/gen-app-cert.sh` for the
     exact URL if it disappears).
2. Register the local CA in your user trust store:
   ```
   mkcert -install
   ```
3. Add the hostname to your hosts file (admin shell):
   ```
   127.0.0.1   goatlab.local
   ```

## Generate the cert

From repo root:

```
bash tools/certs/gen-app-cert.sh
```

This writes `certs/app.pfx`. The password is the mkcert default `changeit`;
`.env` exposes it via `CERT_PASSWORD` for Kestrel.

## Rotating

Delete `certs/app.pfx` and re-run the script. Browsers will pick up the new
cert on the next request (no restart needed on the browser side; Kestrel
restart needed if docker compose has already loaded it).
