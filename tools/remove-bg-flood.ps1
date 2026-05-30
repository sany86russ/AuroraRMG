<#
  remove-bg-flood.ps1 — make the near-white BACKGROUND transparent via edge flood-fill.

  Robust against the GDI+ "opaque PNG loads as 24bpp -> alpha dropped on UnlockBits"
  trap: we first draw the source onto a fresh Format32bppArgb canvas so the bitmap
  genuinely owns an alpha channel.

  Flood-fill from all border pixels through connected near-white pixels and set them
  transparent. Interior highlights (aurora, grid glow) survive because the dark
  shield outline encloses them — the fill can't reach them from the border.

  Edge pixels get a soft alpha so the silhouette isn't jagged.
#>
param(
    [Parameter(Mandatory=$true)][string]$Source,
    [Parameter(Mandatory=$true)][string]$Dest,
    [int]$WhiteThreshold = 200,   # min(R,G,B) >= this AND low saturation => background candidate
    [int]$SatThreshold   = 40
)
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

# Load source then copy onto a true 32bpp ARGB canvas.
$loaded = New-Object System.Drawing.Bitmap((Resolve-Path $Source).Path)
$w = $loaded.Width; $h = $loaded.Height
$bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.DrawImage($loaded, (New-Object System.Drawing.Rectangle(0,0,$w,$h)))
$g.Dispose(); $loaded.Dispose()

$rect = New-Object System.Drawing.Rectangle(0,0,$w,$h)
$data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite,
                      [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$stride = $data.Stride
$bytes  = $stride * $h
$buf = New-Object byte[] $bytes
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buf, 0, $bytes)

# isBg: low-saturation light pixel
function Test-Bg([int]$idx) {
    $b = $buf[$idx]; $g2 = $buf[$idx+1]; $r = $buf[$idx+2]
    $mn = [Math]::Min($r, [Math]::Min($g2, $b))
    $mx = [Math]::Max($r, [Math]::Max($g2, $b))
    return ($mn -ge $WhiteThreshold -and ($mx - $mn) -le $SatThreshold)
}

$visited = New-Object 'bool[]' ($w * $h)
$stack = New-Object System.Collections.Generic.Stack[int]   # pixel index = y*w + x

# Seed from all border pixels.
for ($x = 0; $x -lt $w; $x++) {
    $stack.Push($x)                 # top row
    $stack.Push((($h-1)*$w) + $x)   # bottom row
}
for ($y = 0; $y -lt $h; $y++) {
    $stack.Push($y*$w)              # left col
    $stack.Push($y*$w + ($w-1))     # right col
}

while ($stack.Count -gt 0) {
    $pi = $stack.Pop()
    if ($visited[$pi]) { continue }
    $visited[$pi] = $true
    $px = $pi % $w; $py = [int]($pi / $w)
    $bi = $py*$stride + $px*4
    if (-not (Test-Bg $bi)) { continue }
    $buf[$bi+3] = 0                  # background -> transparent
    if ($px -gt 0)      { $n = $pi-1;  if (-not $visited[$n]) { $stack.Push($n) } }
    if ($px -lt $w-1)   { $n = $pi+1;  if (-not $visited[$n]) { $stack.Push($n) } }
    if ($py -gt 0)      { $n = $pi-$w; if (-not $visited[$n]) { $stack.Push($n) } }
    if ($py -lt $h-1)   { $n = $pi+$w; if (-not $visited[$n]) { $stack.Push($n) } }
}

# Soft edge: any still-opaque pixel touching a transparent one gets reduced alpha
# proportional to its brightness, smoothing the anti-aliased white fringe.
for ($y = 0; $y -lt $h; $y++) {
    for ($x = 0; $x -lt $w; $x++) {
        $bi = $y*$stride + $x*4
        if ($buf[$bi+3] -eq 0) { continue }
        $touch = $false
        if ($x -gt 0     -and $buf[$bi-4+3]      -eq 0) { $touch = $true }
        if (-not $touch -and $x -lt $w-1 -and $buf[$bi+4+3] -eq 0) { $touch = $true }
        if (-not $touch -and $y -gt 0     -and $buf[$bi-$stride+3] -eq 0) { $touch = $true }
        if (-not $touch -and $y -lt $h-1 -and $buf[$bi+$stride+3] -eq 0) { $touch = $true }
        if (-not $touch) { continue }
        $b = $buf[$bi]; $g2 = $buf[$bi+1]; $r = $buf[$bi+2]
        $mn = [Math]::Min($r, [Math]::Min($g2, $b))
        if ($mn -ge 235) { $buf[$bi+3] = 0 }
        elseif ($mn -ge 205) { $buf[$bi+3] = [byte](255 - (($mn-205)*255/30)) }
    }
}

[System.Runtime.InteropServices.Marshal]::Copy($buf, 0, $data.Scan0, $bytes)
$bmp.UnlockBits($data)
$bmp.Save($Dest, [System.Drawing.Imaging.ImageFormat]::Png)

# Report transparency stats.
$trans = 0
for ($i = 3; $i -lt $bytes; $i += 4) { if ($buf[$i] -eq 0) { $trans++ } }
$bmp.Dispose()
Write-Output ("wrote {0}  ({1}x{2}, transparent {3:P0})" -f $Dest, $w, $h, ($trans/($w*$h)))
