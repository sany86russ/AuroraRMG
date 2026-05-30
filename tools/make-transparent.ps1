<#
  make-transparent.ps1 — strip the near-white background from a PNG.

  The ChatGPT-exported ICO.png has an opaque near-white background. We build an
  alpha matte: low-saturation bright pixels (the white backdrop + its anti-alias
  fringe) fade to transparent, while coloured pixels (the aurora) and dark pixels
  (the shield body/outline) stay fully opaque. Fast via LockBits (BGRA bytes).
#>
param(
    [Parameter(Mandatory=$true)][string]$Source,
    [Parameter(Mandatory=$true)][string]$Dest
)
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$src = New-Object System.Drawing.Bitmap((Resolve-Path $Source).Path)
$w = $src.Width; $h = $src.Height
$rect = New-Object System.Drawing.Rectangle(0,0,$w,$h)
$data = $src.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite,
                      [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$stride = $data.Stride
$bytes = $stride * $h
$buf = New-Object byte[] $bytes
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buf, 0, $bytes)

for ($y = 0; $y -lt $h; $y++) {
    $row = $y * $stride
    for ($x = 0; $x -lt $w; $x++) {
        $i = $row + $x * 4          # BGRA
        $b = $buf[$i]; $g = $buf[$i+1]; $r = $buf[$i+2]
        $mn = [Math]::Min($r, [Math]::Min($g, $b))
        $mx = [Math]::Max($r, [Math]::Max($g, $b))
        $sat = $mx - $mn
        if ($sat -ge 30) { continue }                 # coloured -> keep opaque
        if ($mn -lt 200) { continue }                 # dark (shield body/outline) -> keep
        # low-saturation light pixel -> matte by brightness (245->0a, 200->full)
        $a = [int](255 * (245 - $mn) / 45)
        if ($a -lt 0) { $a = 0 } elseif ($a -gt 255) { $a = 255 }
        $buf[$i+3] = [byte]$a
    }
}

[System.Runtime.InteropServices.Marshal]::Copy($buf, 0, $data.Scan0, $bytes)
$src.UnlockBits($data)
$src.Save($Dest, [System.Drawing.Imaging.ImageFormat]::Png)
$src.Dispose()
Write-Output "wrote $Dest ($w x $h, background made transparent)"
