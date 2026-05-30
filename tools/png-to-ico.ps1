<#
  png-to-ico.ps1 — convert a square PNG into a multi-size .ico (PNG-encoded frames).

  Usage:
    powershell.exe -ExecutionPolicy Bypass -File tools\png-to-ico.ps1 -Source <in.png> -Dest <out.ico>

  Produces 16/24/32/48/64/128/256 frames. The source is resized with high-quality
  bicubic interpolation onto a transparent canvas, preserving the alpha channel.
#>
param(
    [Parameter(Mandatory=$true)][string]$Source,
    [Parameter(Mandatory=$true)][string]$Dest
)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$src = [System.Drawing.Image]::FromFile((Resolve-Path $Source))

function Resize-To([int]$S) {
    $bmp = New-Object System.Drawing.Bitmap($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($src, (New-Object System.Drawing.Rectangle(0, 0, $S, $S)))
    $g.Dispose()
    return $bmp
}

$sizes = @(16,24,32,48,64,128,256)
$frames = @()
foreach ($s in $sizes) {
    $bmp = Resize-To $s
    $ms  = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += [pscustomobject]@{ Size = $s; Bytes = $ms.ToArray() }
    $ms.Dispose(); $bmp.Dispose()
}
$src.Dispose()

$fs = [System.IO.File]::Create((New-Item -ItemType File -Path $Dest -Force).FullName)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$frames.Count)
$offset = 6 + (16 * $frames.Count)
foreach ($fr in $frames) {
    $dim = if ($fr.Size -ge 256) { 0 } else { $fr.Size }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim)
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$fr.Bytes.Length); $bw.Write([uint32]$offset)
    $offset += $fr.Bytes.Length
}
foreach ($fr in $frames) { $bw.Write($fr.Bytes) }
$bw.Flush(); $bw.Close(); $fs.Dispose()
Write-Output "wrote $Dest ($($frames.Count) frames)"
