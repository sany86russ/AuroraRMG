[CmdletBinding()]
param(
    [ValidateSet('Build', 'Test', 'BuildAndTest', 'Publish')][string]$Mode = 'BuildAndTest',
    [string]$Filter,
    [string]$OutputDirectory,
    [switch]$Restore
)
$ErrorActionPreference = 'Stop'
$workspacePath = Split-Path -Parent $PSScriptRoot
$sdkPath = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $sdkPath)) { $sdkPath = (Get-Command dotnet -ErrorAction Stop).Source }
$checkProcess = Get-Process -Id $PID
$originalPriority = $checkProcess.PriorityClass
$originalAffinity = $checkProcess.ProcessorAffinity
$savedEnvironment = @{}
foreach ($name in @('DOTNET_PROCESSOR_COUNT','DOTNET_CLI_USE_MSBUILD_SERVER','MSBUILDDISABLENODEREUSE')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$checkLock = [System.Threading.Mutex]::new($false, 'Local\AuroraRMG.LowImpactChecks')
$lockHeld = $false
try {
    $lockHeld = $checkLock.WaitOne(0)
    if (-not $lockHeld) { throw 'Another local AuroraRMG check is running. Run checks sequentially.' }
    $checkProcess.PriorityClass = 'Idle'
    # Select one allowed logical CPU. Children inherit affinity and Idle priority;
    # shared compiler and reusable MSBuild workers are disabled below.
    $allowedMask = $originalAffinity.ToInt64()
    $selectedMask = 0L
    for ($cpuIndex = 0; $cpuIndex -lt 63; $cpuIndex++) {
        $cpuBit = 1L -shl $cpuIndex
        if (($allowedMask -band $cpuBit) -ne 0) { $selectedMask = $cpuBit }
    }
    if ($selectedMask -ne 0) { $checkProcess.ProcessorAffinity = [IntPtr]$selectedMask }
    $env:DOTNET_PROCESSOR_COUNT = '1'
    $env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
    $env:MSBUILDDISABLENODEREUSE = '1'
    Write-Host ('Checks: priority={0}, affinity={1}, workers=1' -f $checkProcess.PriorityClass, $checkProcess.ProcessorAffinity)
    Push-Location $workspacePath
    try {
        if ($Restore) {
            & $sdkPath restore 'Olden Era - Template Editor.slnx' --disable-parallel -p:UseSharedCompilation=false
            if ($LASTEXITCODE -ne 0) { throw "Restore failed: exit $LASTEXITCODE" }
        }
        if ($Mode -in @('Build','BuildAndTest')) {
            & $sdkPath build 'Olden Era - Template Editor.slnx' -c Release --no-restore -m:1 -nodeReuse:false -p:BuildInParallel=false -p:UseSharedCompilation=false
            if ($LASTEXITCODE -ne 0) { throw "Build failed: exit $LASTEXITCODE" }
        }
        if ($Mode -in @('Test','BuildAndTest')) {
            $testArgs = @('test','Olden Era - Template Editor.slnx','-c','Release','--no-build','--no-restore','-m:1','-nodeReuse:false','-p:BuildInParallel=false','--verbosity','minimal')
            if ($Filter) { $testArgs += @('--filter',$Filter) }
            $testArgs += @('--','RunConfiguration.MaxCpuCount=1','xUnit.MaxParallelThreads=1','xUnit.ParallelizeAssembly=false','xUnit.ParallelizeTestCollections=false')
            & $sdkPath @testArgs
            if ($LASTEXITCODE -ne 0) { throw "Tests failed: exit $LASTEXITCODE" }
        }
        if ($Mode -eq 'Publish') {
            if (-not $OutputDirectory) { throw 'Publish requires a new OutputDirectory inside workspace artifacts.' }
            $publishDirectory = [IO.Path]::GetFullPath($OutputDirectory)
            $artifactRoot = [IO.Path]::GetFullPath((Join-Path $workspacePath 'artifacts'))
            if (-not $publishDirectory.StartsWith($artifactRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Publish output must be a new directory inside workspace artifacts.'
            }
            for ($parent = [IO.DirectoryInfo]::new($publishDirectory); $null -ne $parent; $parent = $parent.Parent) {
                if ($parent.Exists -and ($parent.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Publish output must not traverse a directory link.' }
            }
            if ((Test-Path -LiteralPath $publishDirectory) -and (Get-ChildItem -LiteralPath $publishDirectory -Force | Select-Object -First 1)) {
                throw 'Use a new or empty publish output directory.'
            }
            & $sdkPath publish 'Olden Era - Template Editor/Olden Era - Template Editor.csproj' -c Release --no-build --no-restore -o $publishDirectory -m:1 -nodeReuse:false -p:BuildInParallel=false -p:UseSharedCompilation=false
            if ($LASTEXITCODE -ne 0) { throw "Publish failed: exit $LASTEXITCODE" }
        }
    } finally { Pop-Location }
} finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
    $checkProcess.ProcessorAffinity = $originalAffinity
    $checkProcess.PriorityClass = $originalPriority
    if ($lockHeld) { $checkLock.ReleaseMutex() }
    $checkLock.Dispose()
}
