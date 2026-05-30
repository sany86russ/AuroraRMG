<#
  screenshot.ps1 — capture AuroraRMG tabs OFF-SCREEN without stealing focus.

  The app is launched with --offscreen: it renders in the NORMAL window state
  (so WPF actually paints each tab's content — a minimized window does not),
  with ShowActivated=false and positioned far off-screen. We then:
    1. select each tab via UI Automation (no click, no activation),
    2. keep the window off-screen (Reassert) so it never pops over a game,
    3. PrintWindow(PW_RENDERFULLCONTENT) to grab pixels even off-screen.

  Tab headers are Cyrillic; Windows PowerShell 5.1 mis-decodes Cyrillic literals
  inside a BOM-less .ps1, so the tab list is read from tools\tabs.txt (UTF-8).
  Output PNGs go to tools\shots\.
#>
param(
    [string]$Exe = 'E:\Olden-Era-Generator-main\Olden Era - Template Editor\bin\Debug\net10.0-windows\win-x64\OldenEraTemplateGenerator.exe',
    [string]$OutDir = 'E:\Olden-Era-Generator-main\tools\shots',
    [string]$TabsFile = 'E:\Olden-Era-Generator-main\tools\tabs.txt',
    [int]$Width = 1300,
    [int]$Height = 880
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

$tabSpecs = @()
foreach ($line in [System.IO.File]::ReadAllLines($TabsFile, [System.Text.Encoding]::UTF8)) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $parts = $line.Split('|')
    $tabSpecs += [pscustomobject]@{ Name = $parts[0]; File = $parts[1] }
}

$sig = @'
using System;
using System.Runtime.InteropServices;
public static class Win {
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
Add-Type -TypeDefinition $sig

$SWP_NOACTIVATE = 0x0010
$SWP_NOZORDER   = 0x0004
$SW_SHOWNOACTIVATE = 4
$PW_RENDERFULLCONTENT = 2
$OFF_X = -5000
$OFF_Y = 100

$p = Start-Process -FilePath $Exe -ArgumentList '--offscreen' -PassThru
Write-Output "started pid $($p.Id)"

$hwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Milliseconds 250
    $p.Refresh()
    if ($p.MainWindowHandle -ne [IntPtr]::Zero) { $hwnd = $p.MainWindowHandle; break }
}
if ($hwnd -eq [IntPtr]::Zero) { throw "main window not found" }
Write-Output "hwnd = $hwnd"
Start-Sleep -Milliseconds 1500

function Reassert {
    [void][Win]::SetWindowPos($hwnd, [IntPtr]::Zero, $OFF_X, $OFF_Y, $Width, $Height, ($SWP_NOACTIVATE -bor $SWP_NOZORDER))
    [void][Win]::ShowWindow($hwnd, $SW_SHOWNOACTIVATE)
}

function Capture([string]$name) {
    $r = New-Object Win+RECT
    [void][Win]::GetWindowRect($hwnd, [ref]$r)
    $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
    if ($w -le 0 -or $h -le 0) { $w = $Width; $h = $Height }
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    $ok = [Win]::PrintWindow($hwnd, $hdc, $PW_RENDERFULLCONTENT)
    $g.ReleaseHdc($hdc); $g.Dispose()
    $path = Join-Path $OutDir "$name.png"
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output ("captured {0}  ({1}x{2}, PrintWindow={3})" -f $name, $w, $h, $ok)
}

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
$win = $null
for ($i = 0; $i -lt 40; $i++) {
    $win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
    if ($win) { break }
    Start-Sleep -Milliseconds 250
}
if (-not $win) { throw "UIA window not found" }

function Select-Tab([string]$header) {
    $byName = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $header)
    $tab = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $byName)
    if (-not $tab) { Write-Output "  ! tab not found: $header"; return $false }
    try {
        $sip = $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        $sip.Select()
        return $true
    } catch { Write-Output "  ! cannot select: $header"; return $false }
}

Reassert; Start-Sleep -Milliseconds 800
Capture $tabSpecs[0].File

for ($i = 1; $i -lt $tabSpecs.Count; $i++) {
    if (Select-Tab $tabSpecs[$i].Name) {
        Start-Sleep -Milliseconds 1400      # let WPF lay out + paint the new tab
        Reassert; Start-Sleep -Milliseconds 600
        Capture $tabSpecs[$i].File
    }
}

[void](Select-Tab $tabSpecs[0].Name)
Start-Sleep -Milliseconds 300

try { $p.CloseMainWindow() | Out-Null } catch {}
Start-Sleep -Milliseconds 800
if (-not $p.HasExited) { try { $p.Kill() } catch {} }
Write-Output "done"
