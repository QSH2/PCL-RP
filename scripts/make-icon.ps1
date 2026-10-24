# Generates src/PclCeRegionPatch.Gui/app.ico (multi-size Windows icon) with GDI+.
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/make-icon.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$outIco = Join-Path $repoRoot 'src\PclCeRegionPatch.Gui\app.ico'
$sizes = @(16, 24, 32, 48, 64, 128, 256)

function New-RoundedPath([single]$w, [single]$h, [single]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
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
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    # Blue rounded square with a diagonal gradient.
    $radius = [single]($size * 0.22)
    $pathGeo = New-RoundedPath ([single]$size) ([single]$size) $radius
    $start = New-Object System.Drawing.Point(0, 0)
    $end = New-Object System.Drawing.Point($size, $size)
    $blue1 = [System.Drawing.Color]::FromArgb(11, 95, 165)
    $blue2 = [System.Drawing.Color]::FromArgb(47, 163, 247)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($start, $end, $blue1, $blue2)
    $g.FillPath($brush, $pathGeo)

    # Wrench glyph (Segoe MDL2 Assets U+E90F) in white.
    $glyph = [string][char]0xE90F
    $font = New-Object System.Drawing.Font('Segoe MDL2 Assets', [single]($size * 0.56), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $glyphRect = New-Object System.Drawing.RectangleF(0, [single](-$size * 0.04), [single]$size, [single]$size)
    $g.DrawString($glyph, $font, [System.Drawing.Brushes]::White, $glyphRect, $fmt)

    # Small green check badge (skipped for the tiniest sizes to stay readable).
    if ($size -ge 24) {
        $badgeD = [single]($size * 0.42)
        $bx = [single]($size - $badgeD - $size * 0.04)
        $by = [single]($size - $badgeD - $size * 0.04)
        $green = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(46, 204, 113))
        $g.FillEllipse($green, $bx, $by, $badgeD, $badgeD)
        $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, [single]([math]::Max(1.2, $size * 0.055)))
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $points = @(
            (New-Object System.Drawing.PointF([single]($bx + $badgeD * 0.22), [single]($by + $badgeD * 0.52))),
            (New-Object System.Drawing.PointF([single]($bx + $badgeD * 0.42), [single]($by + $badgeD * 0.72))),
            (New-Object System.Drawing.PointF([single]($bx + $badgeD * 0.80), [single]($by + $badgeD * 0.28)))
        )
        $g.DrawLines($pen, $points)
        $pen.Dispose()
        $green.Dispose()
    }

    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $font.Dispose()
    $brush.Dispose()
    $pathGeo.Dispose()
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
