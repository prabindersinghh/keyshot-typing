<#
.SYNOPSIS
  Regenerates KeyShot's .ico files (active = orange crosshair, inactive = grey).
  Output: src/KeyShot.App/Assets/keyshot.ico and keyshot-off.ico
#>
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$assets = Join-Path $PSScriptRoot '..\src\KeyShot.App\Assets'
New-Item -ItemType Directory -Force $assets | Out-Null

function New-Frame([int]$size, [System.Drawing.Color]$accent) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Rounded dark tile
    $r = [Math]::Max(2, [int]($size * 0.22))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $w = $size - 1
    $path.AddArc(0, 0, $r * 2, $r * 2, 180, 90)
    $path.AddArc($w - $r * 2, 0, $r * 2, $r * 2, 270, 90)
    $path.AddArc($w - $r * 2, $w - $r * 2, $r * 2, $r * 2, 0, 90)
    $path.AddArc(0, $w - $r * 2, $r * 2, $r * 2, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 17, 19, 24))), $path)

    # Crosshair
    $stroke = [Math]::Max(1.5, $size * 0.085)
    $pen = New-Object System.Drawing.Pen $accent, $stroke
    $c = $size / 2.0
    $ring = $size * 0.27
    $g.DrawEllipse($pen, [float]($c - $ring), [float]($c - $ring), [float]($ring * 2), [float]($ring * 2))
    $inner = $size * 0.13; $outer = $size * 0.42
    $g.DrawLine($pen, [float]$c, [float]($c - $outer), [float]$c, [float]($c - $inner))
    $g.DrawLine($pen, [float]$c, [float]($c + $inner), [float]$c, [float]($c + $outer))
    $g.DrawLine($pen, [float]($c - $outer), [float]$c, [float]($c - $inner), [float]$c)
    $g.DrawLine($pen, [float]($c + $inner), [float]$c, [float]($c + $outer), [float]$c)
    $dot = [Math]::Max(1.5, $size * 0.07)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $accent), [float]($c - $dot), [float]($c - $dot), [float]($dot * 2), [float]($dot * 2))
    $g.Dispose()
    return $bmp
}

# 32bpp DIB entry (XOR bitmap bottom-up + empty AND mask) - works with every Win32/GDI+ icon loader.
function Get-DibBytes([System.Drawing.Bitmap]$bmp) {
    $s = $bmp.Width
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([int]40); $bw.Write([int]$s); $bw.Write([int]($s * 2)); $bw.Write([int16]1); $bw.Write([int16]32)
    $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0)
    for ($y = $s - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $s; $x++) {
            $p = $bmp.GetPixel($x, $y)
            $bw.Write([byte]$p.B); $bw.Write([byte]$p.G); $bw.Write([byte]$p.R); $bw.Write([byte]$p.A)
        }
    }
    $maskRow = [int]([Math]::Ceiling($s / 32.0) * 4)
    $bw.Write((New-Object byte[] ($maskRow * $s)))
    $bw.Flush()
    return $ms.ToArray()
}

function Write-Icon([string]$file, [System.Drawing.Color]$accent) {
    $sizes = 16, 20, 24, 32, 40, 48, 64, 256
    $images = foreach ($s in $sizes) {
        $bmp = New-Frame $s $accent
        if ($s -eq 256) {
            $ms = New-Object System.IO.MemoryStream
            $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
            , $ms.ToArray()
        } else {
            , (Get-DibBytes $bmp)
        }
        $bmp.Dispose()
    }
    $out = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $out
    $bw.Write([int16]0); $bw.Write([int16]1); $bw.Write([int16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]; $len = $images[$i].Length
        $dim = if ($s -ge 256) { 0 } else { $s }
        $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([int16]1); $bw.Write([int16]32); $bw.Write([int]$len); $bw.Write([int]$offset)
        $offset += $len
    }
    foreach ($img in $images) { $bw.Write([byte[]]$img) }
    $bw.Flush()
    [System.IO.File]::WriteAllBytes($file, $out.ToArray())
    Write-Host "Wrote $file"
}

Write-Icon (Join-Path $assets 'keyshot.ico') ([System.Drawing.Color]::FromArgb(255, 255, 106, 26))
Write-Icon (Join-Path $assets 'keyshot-off.ico') ([System.Drawing.Color]::FromArgb(255, 110, 115, 128))

# PNG for README / docs
$logo = New-Frame 256 ([System.Drawing.Color]::FromArgb(255, 255, 106, 26))
$logo.Save((Join-Path $PSScriptRoot '..\docs\logo.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$logo.Dispose()
