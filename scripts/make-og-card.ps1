# Generates a 1200x630 Open Graph card for GoatLab using System.Drawing.
# Run from repo root:  pwsh -File scripts/make-og-card.ps1
# Output:              src/GoatLab.Client/wwwroot/images/og-card.png

Add-Type -AssemblyName System.Drawing

$W = 1200
$H = 630
$out = Join-Path $PSScriptRoot "..\src\GoatLab.Client\wwwroot\images\og-card.png"
$out = [System.IO.Path]::GetFullPath($out)

$bmp = New-Object System.Drawing.Bitmap $W, $H
$g   = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

# --- background: diagonal green gradient ---
$rect = New-Object System.Drawing.Rectangle 0, 0, $W, $H
$c1 = [System.Drawing.ColorTranslator]::FromHtml("#1b5e20")
$c2 = [System.Drawing.ColorTranslator]::FromHtml("#2e7d32")
$c3 = [System.Drawing.ColorTranslator]::FromHtml("#43a047")
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $rect, $c1, $c3, [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
$g.FillRectangle($brush, $rect)
$brush.Dispose()

# --- decorative soft circles ---
$soft = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(28, 255, 255, 255))
$g.FillEllipse($soft, 880, -120, 520, 520)
$g.FillEllipse($soft, -160, 380, 460, 460)
$soft.Dispose()

# --- logo badge (circle + hoof-ish glyph) ---
$badgeX = 80; $badgeY = 90; $badgeR = 110
$badgeBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
$g.FillEllipse($badgeBrush, $badgeX, $badgeY, $badgeR, $badgeR)
$badgeBrush.Dispose()

# Draw "🐐" — System.Drawing emoji rendering is unreliable, so use a stylized "G" instead.
$gFont   = New-Object System.Drawing.Font("Segoe UI", 64, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$gBrush  = New-Object System.Drawing.SolidBrush $c1
$gFormat = New-Object System.Drawing.StringFormat
$gFormat.Alignment     = [System.Drawing.StringAlignment]::Center
$gFormat.LineAlignment = [System.Drawing.StringAlignment]::Center
$badgeRect = New-Object System.Drawing.RectangleF $badgeX, $badgeY, $badgeR, $badgeR
$g.DrawString("G", $gFont, $gBrush, $badgeRect, $gFormat)
$gFont.Dispose(); $gBrush.Dispose()

# --- wordmark ---
$wordFont  = New-Object System.Drawing.Font("Segoe UI", 56, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$white     = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$g.DrawString("GoatLab", $wordFont, $white, [single]215, [single]120)
$wordFont.Dispose()

# --- headline ---
$headFont = New-Object System.Drawing.Font("Segoe UI", 72, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$headRect = New-Object System.Drawing.RectangleF 80, 260, 1040, 200
$g.DrawString("Herd management for`nmodern goat farmers.", $headFont, $white, $headRect)
$headFont.Dispose()

# --- subline ---
$subFont = New-Object System.Drawing.Font("Segoe UI", 30, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$subBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(230, 255, 255, 255))
$bullet = [char]0x00B7
$subline = "Health  $bullet  Breeding  $bullet  Milk  $bullet  Finances  $bullet  Offline-first"
$g.DrawString($subline, $subFont, $subBrush, [single]80, [single]470)
$subFont.Dispose(); $subBrush.Dispose()

# --- url chip bottom-right ---
$urlFont = New-Object System.Drawing.Font("Segoe UI", 26, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$urlBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
$g.DrawString("goatlab.app", $urlFont, $urlBrush, [single]80, [single]540)
$urlFont.Dispose(); $urlBrush.Dispose()
$white.Dispose()

# --- save ---
$dir = [System.IO.Path]::GetDirectoryName($out)
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()

Write-Host "Wrote $out ($W x $H)"
