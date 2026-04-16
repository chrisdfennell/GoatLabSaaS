#requires -Version 5
# Generates GoatLab PWA icons (192, 512) + favicon via System.Drawing.
# Run once; outputs land in src/GoatLab.Client/wwwroot/images/.

Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot "..\src\GoatLab.Client\wwwroot\images"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function New-Icon {
    param([int]$Size, [string]$Path)

    $bmp = New-Object System.Drawing.Bitmap $Size, $Size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    $rect = New-Object System.Drawing.Rectangle 0, 0, $Size, $Size
    $c1 = [System.Drawing.Color]::FromArgb(255, 46, 125, 50)
    $c2 = [System.Drawing.Color]::FromArgb(255, 27, 94, 32)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $c1, $c2, 135.0
    $g.FillRectangle($brush, $rect)

    # Soft inner ring
    $ringBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(40, 255, 255, 255))
    $inset = [int]($Size * 0.06)
    $g.FillEllipse($ringBrush, $inset, $inset, $Size - 2*$inset, $Size - 2*$inset)

    # Letter G centered
    $fontSize = [int]($Size * 0.58)
    $font = New-Object System.Drawing.Font "Arial Black", $fontSize, ([System.Drawing.FontStyle]::Bold)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $textBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $g.DrawString("G", $font, $textBrush, ($Size/2), ($Size/2 - $Size*0.03), $sf)

    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $bmp.Dispose()
    $brush.Dispose()
    $ringBrush.Dispose()
    $textBrush.Dispose()
    $font.Dispose()
    Write-Host "Wrote $Path"
}

New-Icon -Size 192 -Path (Join-Path $outDir "icon-192.png")
New-Icon -Size 512 -Path (Join-Path $outDir "icon-512.png")
New-Icon -Size 32  -Path (Join-Path $outDir "favicon-32.png")
New-Icon -Size 180 -Path (Join-Path $outDir "apple-touch-icon.png")

Write-Host "Done."
