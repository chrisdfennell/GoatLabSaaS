# Sync local source to the NAS, then (optionally) deploy remotely.
#
# Usage:
#   .\scripts\sync-to-nas.ps1              # sync only
#   .\scripts\sync-to-nas.ps1 -Deploy      # sync + rebuild + restart on NAS
#   .\scripts\sync-to-nas.ps1 -Deploy -FreshDb  # also wipe the DB volume (destructive)
#
# Requires: OpenSSH client (built into Windows 10/11), SSH password or key auth
# to the NAS.

param(
    [string]$NasHost = "admin@fennell-nas",
    [string]$NasPath = "/share/Container/GoatLabSaaS",
    [switch]$Deploy,
    [switch]$FreshDb
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "==> Syncing $repoRoot -> ${NasHost}:${NasPath}" -ForegroundColor Cyan

# Files/folders to push. scp -O forces legacy protocol (QNAP has no SFTP subsystem).
$items = @(
    "src",
    "Dockerfile",
    "docker-compose.yml",
    ".dockerignore",
    "Directory.Build.props",
    "global.json",
    "GoatLab.sln"
)

foreach ($item in $items) {
    $localPath = Join-Path $repoRoot $item
    if (-not (Test-Path $localPath)) {
        Write-Warning "Skipping missing: $item"
        continue
    }
    Write-Host "  -> $item"
    scp -O -r $localPath "${NasHost}:${NasPath}/"
    if ($LASTEXITCODE -ne 0) { throw "scp failed for $item" }
}

# Prune stale files that were deleted locally (scp doesn't do this).
# Safest: nuke Migrations on the NAS and re-push, since a stale migration
# file breaks compilation.
Write-Host "==> Cleaning stale files on NAS"
ssh $NasHost "find $NasPath/src -type d -name bin -o -name obj | xargs rm -rf 2>/dev/null; true"

if (-not $Deploy) {
    Write-Host "`nSync complete. To deploy, run with -Deploy." -ForegroundColor Green
    exit 0
}

# Delegate the actual build/restart to nas-deploy.sh on the NAS. The script
# sets its own PATH so we don't need a login shell (which would trigger
# QNAP's qts-console-mgmt menu for the admin user). scp loses the executable
# bit, so set it explicitly.
$flag = if ($FreshDb) { "--fresh" } else { "" }
Write-Host "==> Deploying on NAS" -ForegroundColor Cyan
ssh $NasHost "sh $NasPath/scripts/nas-deploy.sh $flag"
if ($LASTEXITCODE -ne 0) { throw "Remote deploy failed" }

Write-Host "`nDone. Tail live logs with: ssh $NasHost 'cd $NasPath && docker compose logs -f goatlab'" -ForegroundColor Green
