# Generates src/PclCeRegionPatch.Gui/app.ico (multi-size Windows icon) with GDI+.
# Design: flat Fluent-blue rounded square with a clean white download glyph.
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
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Flat Fluent-blue rounded square (no gradient, no badge).
    $radius = [single]($size * 0.225)
    $pathGeo = New-RoundedPath ([single]$size) ([single]$size) $radius
    $background = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(15, 108, 189))
    $g.FillPath($background, $pathGeo)

    # White download glyph: shaft, chevron head, baseline.
    $stroke = [single]([math]::Max(1.2, $size * 0.075))
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, $stroke)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $centerX = [single]($size / 2)
    $g.DrawLine($pen, $centerX, [single]($size * 0.225), $centerX, [single]($size * 0.575))
    $g.DrawLines($pen, @(
        (New-Object System.Drawing.PointF([single]($size * 0.325), [single]($size * 0.425))),
        (New-Object System.Drawing.PointF($centerX, [single]($size * 0.605))),
        (New-Object System.Drawing.PointF([single]($size * 0.675), [single]($size * 0.425)))
    ))
    $g.DrawLine($pen, [single]($size * 0.285), [single]($size * 0.795), [single]($size * 0.715), [single]($size * 0.795))

    $pen.Dispose()
    $background.Dispose()
    $pathGeo.Dispose()
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
