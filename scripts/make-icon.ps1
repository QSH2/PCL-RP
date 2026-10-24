# Generates src/PclCeRegionPatch.Gui/app.ico (multi-size Windows icon) with GDI+.
# Design: PCL-CE-inspired layered blue square with a bold white "P".
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/make-icon.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$outIco = Join-Path $repoRoot 'src\PclCeRegionPatch.Gui\app.ico'
$sizes = @(16, 24, 32, 48, 64, 128, 256)

function New-RoundedPath([single]$w, [single]$h, [single]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * [single]$r
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($w - $d, 0, $d, $d, 270, 90)
    $path.AddArc($w - $d, $h - $d, $d, $d, 0, 90)
    $path.AddArc(0, $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconPng([int]$size, [string]$path) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [single]$size

    # Outer dark navy rounded square (the "frame").
    $outer = New-RoundedPath $s $s ([single]($s * 0.20))
    $navy = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(6, 43, 84))
    $g.FillPath($navy, $outer)
    $navy.Dispose()

    # Inner bright blue rounded square with a soft vertical gradient.
    $margin = [single]($s * 0.075)
    $innerSize = $s - 2 * $margin
    $inner = New-RoundedPath $innerSize $innerSize ([single]($s * 0.145))
    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point(0, 0)),
        (New-Object System.Drawing.Point(0, $size)),
        [System.Drawing.Color]::FromArgb(47, 143, 227),
        [System.Drawing.Color]::FromArgb(20, 101, 184))
    $state = $g.Save()
    $g.TranslateTransform($margin, $margin)
    $g.FillPath($gradient, $inner)
    $g.Restore($state)
    $gradient.Dispose()
    $inner.Dispose()

    # Bold white "P" with a subtle drop shadow.
    $font = New-Object System.Drawing.Font('Segoe UI', [single]($s * 0.58), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $centerY = [single]($s * 0.47)
    $textRect = New-Object System.Drawing.RectangleF(0, [single]($centerY - $s * 0.5), $s, $s)
    $shadowRect = New-Object System.Drawing.RectangleF(0, [single]($centerY - $s * 0.5 + $s * 0.012), $s, $s)
    $shadow = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(70, 0, 0, 0))
    $g.DrawString('P', $font, $shadow, $shadowRect, $format)
    $g.DrawString('P', $font, [System.Drawing.Brushes]::White, $textRect, $format)
    $shadow.Dispose()
    $font.Dispose()
    $format.Dispose()
    $outer.Dispose()

    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

# Render each size, then pack them into a PNG-compressed ICO file.
$work = Join-Path $env:TEMP ('pcl-icon-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $work | Out-Null
$entries = @()
foreach ($size in $sizes) {
    $png = Join-Path $work ("icon-{0}.png" -f $size)
    New-IconPng $size $png
    $entries += [pscustomobject]@{
        Size  = $size
        Bytes = [System.IO.File]::ReadAllBytes($png)
    }
}

$stream = [System.IO.File]::Create($outIco)
$writer = New-Object System.IO.BinaryWriter($stream)
try {
    $writer.Write([uint16]0)                 # reserved
    $writer.Write([uint16]1)                 # type: icon
    $writer.Write([uint16]$entries.Count)    # image count

    $offset = 6 + 16 * $entries.Count
    foreach ($entry in $entries) {
        $dim = if ($entry.Size -ge 256) { 0 } else { $entry.Size }
        $writer.Write([byte]$dim)            # width
        $writer.Write([byte]$dim)            # height
        $writer.Write([byte]0)               # color palette
        $writer.Write([byte]0)               # reserved
        $writer.Write([uint16]1)             # planes
        $writer.Write([uint16]32)            # bit count
        $writer.Write([uint32]$entry.Bytes.Length)
        $writer.Write([uint32]$offset)
        $offset += $entry.Bytes.Length
    }

    foreach ($entry in $entries) {
        $writer.Write($entry.Bytes)
    }
}
finally {
    $writer.Dispose()
    $stream.Dispose()
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ("icon written: {0} ({1:N0} bytes, sizes: {2})" -f $outIco, (Get-Item $outIco).Length, ($sizes -join ', '))
