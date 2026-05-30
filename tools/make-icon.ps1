<#
  AuroraRMG — icon & logo generator
  ----------------------------------
  Draws the brand mark in the app's "Aurora" theme (dark glass + violet→cyan
  neon) and emits:
    * favicon.ico   — multi-size (16/24/32/48/64/128/256), PNG-encoded frames
    * logo-256.png  — square app mark
    * logo-512.png  — square app mark (hi-res)

  The mark = dark rounded tile + aurora ribbons + a four-point compass star
  (map / navigation motif) filled with the violet→cyan accent gradient and a
  soft neon glow.

  Run with Windows PowerShell 5.1 (GDI+ via System.Drawing from .NET Framework):
    powershell.exe -ExecutionPolicy Bypass -File tools\make-icon.ps1
#>

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'

# ── Palette (matches Themes/MedievalTheme.xaml "AURORA") ────────────────────
$cBgTop    = [System.Drawing.Color]::FromArgb(255, 0x12, 0x16, 0x20)  # #121620
$cBgBot    = [System.Drawing.Color]::FromArgb(255, 0x0A, 0x0C, 0x12)  # #0A0C12
$cViolet   = [System.Drawing.Color]::FromArgb(255, 0x8B, 0x7C, 0xFF)  # #8B7CFF
$cBlue     = [System.Drawing.Color]::FromArgb(255, 0x6F, 0x7B, 0xFF)  # #6F7BFF
$cCyan     = [System.Drawing.Color]::FromArgb(255, 0x22, 0xD3, 0xEE)  # #22D3EE
$cWhite    = [System.Drawing.Color]::FromArgb(255, 0xF2, 0xF5, 0xFF)

function New-IconBitmap([int]$S) {
    $bmp = New-Object System.Drawing.Bitmap($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $f = $S / 256.0   # scale factor (design is authored at 256)

    # ── Rounded tile background ─────────────────────────────────────────────
    $pad    = [Math]::Round(8 * $f)
    $radius = [Math]::Round(56 * $f)
    $rect   = New-Object System.Drawing.RectangleF($pad, $pad, ($S - 2*$pad), ($S - 2*$pad))
    $path   = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0, 0)),
        (New-Object System.Drawing.PointF(0, $S)), $cBgTop, $cBgBot)
    $g.FillPath($bgBrush, $path)

    # Clip everything else to the tile so glows stay inside.
    $g.SetClip($path)

    # ── Aurora ribbons (upper area) ─────────────────────────────────────────
    # A few curved strokes drawn several times with low alpha to fake a glow.
    $ribbons = @(
        @{ y =  72; col = $cViolet; amp = 26 },
        @{ y =  98; col = $cBlue;   amp = 34 },
        @{ y = 126; col = $cCyan;   amp = 30 }
    )
    foreach ($r in $ribbons) {
        $y0 = $r.y * $f
        $amp = $r.amp * $f
        $p0 = New-Object System.Drawing.PointF((20*$f),  ($y0 + $amp))
        $p1 = New-Object System.Drawing.PointF((90*$f),  ($y0 - $amp))
        $p2 = New-Object System.Drawing.PointF((170*$f), ($y0 + $amp*0.7))
        $p3 = New-Object System.Drawing.PointF((240*$f), ($y0 - $amp*0.6))
        foreach ($pass in @(@{w=22; a=26}, @{w=12; a=46}, @{w=5; a=120})) {
            $col = [System.Drawing.Color]::FromArgb($pass.a, $r.col.R, $r.col.G, $r.col.B)
            $pen = New-Object System.Drawing.Pen($col, ($pass.w * $f))
            $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
            $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
            $g.DrawBezier($pen, $p0, $p1, $p2, $p3)
            $pen.Dispose()
        }
    }

    # ── Compass rose (centre) — 4 long cardinal + 4 short diagonal points ────
    $cx = $S / 2.0
    $cy = $S / 2.0 + (16 * $f)   # nudged below centre, under the aurora
    $R  = 84 * $f                # long cardinal points
    $Rd = 34 * $f                # short diagonal points
    $r  = 13 * $f                # waist between points
    function Star-Path([double]$sx, [double]$sy, [double]$outR, [double]$diagR, [double]$inR) {
        $dl = $diagR * 0.7071     # diagonal point projected on each axis
        $iw = $inR    * 0.7071    # waist projected on each axis
        $pts = @(
            (New-Object System.Drawing.PointF($sx,          ($sy - $outR))),  # N
            (New-Object System.Drawing.PointF(($sx + $dl),  ($sy - $dl))),    # NE
            (New-Object System.Drawing.PointF(($sx + $outR), $sy)),           # E
            (New-Object System.Drawing.PointF(($sx + $dl),  ($sy + $dl))),    # SE
            (New-Object System.Drawing.PointF($sx,          ($sy + $outR))),  # S
            (New-Object System.Drawing.PointF(($sx - $dl),  ($sy + $dl))),    # SW
            (New-Object System.Drawing.PointF(($sx - $outR), $sy)),           # W
            (New-Object System.Drawing.PointF(($sx - $dl),  ($sy - $dl)))     # NW
        )
        # Interleave waist points so cardinal points read as sharp spikes.
        $waist = @(
            (New-Object System.Drawing.PointF(($sx + $iw), ($sy - $iw))),
            (New-Object System.Drawing.PointF(($sx + $iw), ($sy + $iw))),
            (New-Object System.Drawing.PointF(($sx - $iw), ($sy + $iw))),
            (New-Object System.Drawing.PointF(($sx - $iw), ($sy - $iw)))
        )
        $poly = @(
            $pts[0], $waist[0], $pts[2], $waist[1], $pts[4], $waist[2], $pts[6], $waist[3]
        )
        $sp = New-Object System.Drawing.Drawing2D.GraphicsPath
        $sp.AddPolygon($poly)
        # Diagonal spikes as a second (thinner) star, rotated 45°.
        $dp = @(
            $pts[1],
            (New-Object System.Drawing.PointF(($sx + $iw*0.6), ($sy + $iw*0.6))),
            $pts[3],
            (New-Object System.Drawing.PointF(($sx - $iw*0.6), ($sy + $iw*0.6))),
            $pts[5],
            (New-Object System.Drawing.PointF(($sx - $iw*0.6), ($sy - $iw*0.6))),
            $pts[7],
            (New-Object System.Drawing.PointF(($sx + $iw*0.6), ($sy - $iw*0.6)))
        )
        $sp.AddPolygon($dp)
        return $sp
    }

    # Glow halo: enlarged star, low alpha, compact passes.
    foreach ($glow in @(@{s=1.22; a=34}, @{s=1.10; a=54})) {
        $gp = Star-Path $cx $cy ($R*$glow.s) ($Rd*$glow.s) ($r*$glow.s)
        $gcol = [System.Drawing.Color]::FromArgb($glow.a, $cViolet.R, $cViolet.G, $cViolet.B)
        $gb = New-Object System.Drawing.SolidBrush($gcol)
        $g.FillPath($gb, $gp); $gb.Dispose(); $gp.Dispose()
    }

    # Main star: violet→cyan diagonal gradient.
    $star = Star-Path $cx $cy $R $Rd $r
    $star.FillMode = [System.Drawing.Drawing2D.FillMode]::Winding
    $sBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(($cx - $R), ($cy - $R))),
        (New-Object System.Drawing.PointF(($cx + $R), ($cy + $R))), $cViolet, $cCyan)
    $g.FillPath($sBrush, $star)

    # Inner highlight: a slim vertical lens over the N–S axis for a faceted look.
    $innerPts = @(
        (New-Object System.Drawing.PointF($cx,             ($cy - $R*0.92))),
        (New-Object System.Drawing.PointF(($cx + $r*0.45), $cy)),
        (New-Object System.Drawing.PointF($cx,             ($cy + $R*0.92))),
        (New-Object System.Drawing.PointF(($cx - $r*0.45), $cy))
    )
    $ip = New-Object System.Drawing.Drawing2D.GraphicsPath
    $ip.AddPolygon($innerPts)
    $hi = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(140, $cWhite.R, $cWhite.G, $cWhite.B))
    $g.FillPath($hi, $ip); $hi.Dispose(); $ip.Dispose()

    # Centre gem.
    $gemR = 8 * $f
    $gemRect = New-Object System.Drawing.RectangleF(($cx - $gemR), ($cy - $gemR), ($gemR*2), ($gemR*2))
    $gemBrush = New-Object System.Drawing.SolidBrush($cWhite)
    $g.FillEllipse($gemBrush, $gemRect); $gemBrush.Dispose()

    # ── Subtle top sheen on the tile ────────────────────────────────────────
    $sheen = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0, 0)),
        (New-Object System.Drawing.PointF(0, ($S*0.5))),
        [System.Drawing.Color]::FromArgb(36, 255, 255, 255),
        [System.Drawing.Color]::FromArgb(0, 255, 255, 255))
    $g.FillRectangle($sheen, 0, 0, $S, [int]($S*0.5)); $sheen.Dispose()

    $g.ResetClip()
    # Hairline border for crispness against dark UIs.
    $bpen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(70, 0x2A, 0x31, 0x42), (1.5*$f))
    $g.DrawPath($bpen, $path); $bpen.Dispose()

    $g.Dispose()
    return $bmp
}

function Save-Png([System.Drawing.Bitmap]$bmp, [string]$file) {
    $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
}

# ── Build .ico from PNG-encoded frames ──────────────────────────────────────
function Save-Ico([int[]]$sizes, [string]$file) {
    $frames = @()
    foreach ($s in $sizes) {
        $bmp = New-IconBitmap $s
        $ms  = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $frames += [pscustomobject]@{ Size = $s; Bytes = $ms.ToArray() }
        $ms.Dispose(); $bmp.Dispose()
    }

    $fs = [System.IO.File]::Create($file)
    $bw = New-Object System.IO.BinaryWriter($fs)
    # ICONDIR
    $bw.Write([uint16]0)               # reserved
    $bw.Write([uint16]1)               # type = icon
    $bw.Write([uint16]$frames.Count)   # count

    $offset = 6 + (16 * $frames.Count)
    foreach ($fr in $frames) {
        $dim = if ($fr.Size -ge 256) { 0 } else { $fr.Size }
        $bw.Write([byte]$dim)          # width
        $bw.Write([byte]$dim)          # height
        $bw.Write([byte]0)             # palette
        $bw.Write([byte]0)             # reserved
        $bw.Write([uint16]1)           # planes
        $bw.Write([uint16]32)          # bpp
        $bw.Write([uint32]$fr.Bytes.Length)
        $bw.Write([uint32]$offset)
        $offset += $fr.Bytes.Length
    }
    foreach ($fr in $frames) { $bw.Write($fr.Bytes) }
    $bw.Flush(); $bw.Close(); $fs.Dispose()
}

# ── Outputs ─────────────────────────────────────────────────────────────────
$appDir   = Join-Path $PSScriptRoot '..\Olden Era - Template Editor'
$assets   = Join-Path $appDir 'Assets'
if (-not (Test-Path $assets)) { New-Item -ItemType Directory -Path $assets | Out-Null }

$icoPath  = Join-Path $appDir 'favicon.ico'
Save-Ico @(16,24,32,48,64,128,256) $icoPath
Write-Output "wrote $icoPath"

$p256 = New-IconBitmap 256; Save-Png $p256 (Join-Path $assets 'logo-256.png'); $p256.Dispose()
$p512 = New-IconBitmap 512; Save-Png $p512 (Join-Path $assets 'logo-512.png'); $p512.Dispose()
Write-Output "wrote logo-256.png / logo-512.png in Assets"
Write-Output "done"
