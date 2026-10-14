[CmdletBinding()]
param(
    [ValidateSet('Ui', 'Docs', 'Kit')][string]$Mode = 'Ui',
    [ValidateSet('ru', 'en')][string]$Language = 'ru',
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$GameInstallPath,
    [string]$ExecutablePath
)
$ErrorActionPreference = 'Stop'
$workspacePath = Split-Path -Parent $PSScriptRoot
$applicationPath = Join-Path $workspacePath 'Olden Era - Template Editor\bin\Release\net10.0-windows\win-x64\OldenEraTemplateGenerator.exe'
if ($ExecutablePath) { $applicationPath = [IO.Path]::GetFullPath($ExecutablePath) }
if (-not (Test-Path -LiteralPath $applicationPath)) { throw 'Build Release before running acceptance.' }
$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $resolvedOutput) {
    if (Get-ChildItem -LiteralPath $resolvedOutput -Force | Select-Object -First 1) { throw 'Use a new or empty output directory.' }
}
$verificationDataPath = Join-Path ([IO.Path]::GetTempPath()) ('AuroraRMG-verification-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $verificationDataPath | Out-Null
$verificationSettings = @{ schemaVersion = 2; language = $Language; gameInstallPath = $GameInstallPath; useGameAssets = $false } | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $verificationDataPath 'settings.json'), $verificationSettings, [Text.UTF8Encoding]::new($false))
$savedEnvironment = @{}
foreach ($name in @('AURORARMG_DATA_DIRECTORY', 'DOTNET_PROCESSOR_COUNT')) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$checkProcess = Get-Process -Id $PID
$originalPriority = $checkProcess.PriorityClass
$originalAffinity = $checkProcess.ProcessorAffinity
$checkLock = [System.Threading.Mutex]::new($false, 'Local\AuroraRMG.LowImpactChecks')
$lockHeld = $false
try {
    $lockHeld = $checkLock.WaitOne(0)
    if (-not $lockHeld) { throw 'Another AuroraRMG verification is running. Run checks sequentially.' }
    $checkProcess.PriorityClass = 'Idle'
    $allowedMask = $originalAffinity.ToInt64(); $selectedMask = 0L
    for ($cpuIndex = 0; $cpuIndex -lt 63; $cpuIndex++) { $cpuBit = 1L -shl $cpuIndex; if (($allowedMask -band $cpuBit) -ne 0) { $selectedMask = $cpuBit } }
    if ($selectedMask -ne 0) { $checkProcess.ProcessorAffinity = [IntPtr]$selectedMask }
    $env:AURORARMG_DATA_DIRECTORY = $verificationDataPath; $env:DOTNET_PROCESSOR_COUNT = '1'
    $command = switch ($Mode) { 'Ui' { '--shoot-patch' } 'Docs' { '--shoot-docs' } 'Kit' { '--make-patch-kit' } }
    $argumentList = @($command, ('"' + $resolvedOutput + '"'), '--lang', $Language)
    $verificationProcess = Start-Process -FilePath $applicationPath -ArgumentList $argumentList -WindowStyle Hidden -PassThru
    $verificationProcess.PriorityClass = 'Idle'; $verificationProcess.ProcessorAffinity = $checkProcess.ProcessorAffinity
    Write-Host ('Verification: mode={0}, priority={1}, affinity={2}, data={3}' -f $Mode, $verificationProcess.PriorityClass, $verificationProcess.ProcessorAffinity, $verificationDataPath)
    $verificationProcess.WaitForExit()
    if ($verificationProcess.ExitCode -ne 0) {
        $errorFile = Join-Path $resolvedOutput 'verification-error.txt'
        if (Test-Path -LiteralPath $errorFile) { Get-Content -LiteralPath $errorFile }
        $isolatedError = Join-Path $verificationDataPath 'verification-error.txt'
        if (Test-Path -LiteralPath $isolatedError) { Get-Content -LiteralPath $isolatedError }
        throw ('Verification failed: exit ' + $verificationProcess.ExitCode)
    }
    Write-Host ('Passed: ' + $resolvedOutput)
} finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
    $checkProcess.ProcessorAffinity = $originalAffinity; $checkProcess.PriorityClass = $originalPriority
    if ($lockHeld) { $checkLock.ReleaseMutex() }
    $checkLock.Dispose()
}
