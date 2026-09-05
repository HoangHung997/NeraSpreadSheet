param(
    [Parameter(Mandatory)][string]$BaselineRoot,
    [Parameter(Mandatory)][string]$CandidateRoot,
    [Parameter(Mandatory)][string]$HarnessRoot,
    [Parameter(Mandatory)][string]$OutputRoot
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'Paired timing is restricted to an isolated GitHub runner. Use worker verify for local correctness only.' }
$BaselineRoot = (Resolve-Path -LiteralPath $BaselineRoot).Path
$CandidateRoot = (Resolve-Path -LiteralPath $CandidateRoot).Path
$HarnessRoot = (Resolve-Path -LiteralPath $HarnessRoot).Path
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) { throw 'Use a fresh artifact directory; never overwrite raw evidence or a frozen budget.' }
New-Item -ItemType Directory -Path $OutputRoot | Out-Null

function Get-Sha([string]$Root) {
    $value = git -C $Root rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $value -notmatch '^[a-f0-9]{40}$') { throw 'An exact Git SHA is required.' }
    if (git -C $Root status --porcelain) { throw "Checkout must start clean: $Root" }
    return $value
}
Push-Location $HarnessRoot
try { $sdk = & dotnet --version } finally { Pop-Location }
$manifest = [ordered]@{
    schema = 'perf008-run-v1'; baselineSha = (Get-Sha $BaselineRoot); candidateSha = (Get-Sha $CandidateRoot)
    harnessSha = (Get-Sha $HarnessRoot); sdk = $sdk; configuration = 'Release'
    runId = $env:GITHUB_RUN_ID; runAttempt = $env:GITHUB_RUN_ATTEMPT; workflowSha = $env:GITHUB_SHA
    runnerOs = $env:RUNNER_OS; runnerArch = $env:RUNNER_ARCH; image = $env:ImageOS; imageVersion = $env:ImageVersion
    cpuModel = (Get-CimInstance Win32_Processor | Select-Object -ExpandProperty Name -Unique)
    logicalProcessors = [Environment]::ProcessorCount
    tieredCompilation = '0'; serverGc = '0'; calibrationPairs = 6; candidatePairs = 12
    isolation = 'One hosted Windows job; serial builds complete before timing; sequential fresh processes; no local/parallel desktop work'
    order = 'Calibration baseline/baseline; paired even baseline/candidate, odd candidate/baseline'
    overlay = @()
}
if ($manifest.sdk -ne '10.0.302') { throw 'Canonical SDK 10.0.302 is required.' }
$overlayNames = @('Program.cs', 'RibbonLayoutBenchmarks.cs', 'TableCompatibilityBenchmarks.cs') +
    @(Get-ChildItem -LiteralPath (Join-Path $HarnessRoot 'benchmarks/NeraSpreadSheet.Benchmarks') -Filter 'PERF008*.cs' | Select-Object -ExpandProperty Name)
foreach ($name in $overlayNames) {
    $relative = "benchmarks/NeraSpreadSheet.Benchmarks/$name"
    $source = Join-Path $HarnessRoot $relative
    $manifest.overlay += @{ path = $relative; sha256 = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash }
    foreach ($root in @($BaselineRoot, $CandidateRoot)) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $root $relative) -Force
        if ((Get-FileHash -LiteralPath (Join-Path $root $relative)).Hash -ne (Get-FileHash -LiteralPath $source).Hash) { throw 'Harness overlay hash mismatch.' }
    }
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputRoot 'manifest.json') -Encoding utf8
$env:DOTNET_TieredCompilation = '0'
$env:DOTNET_gcServer = '0'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
foreach ($side in @('baseline', 'candidate')) {
    $root = if ($side -eq 'baseline') { $BaselineRoot } else { $CandidateRoot }
    Push-Location $root
    try {
        & dotnet build benchmarks/NeraSpreadSheet.Benchmarks/NeraSpreadSheet.Benchmarks.csproj -c Release --nologo -m:1 /nodeReuse:false 2>&1 |
            Tee-Object -FilePath (Join-Path $OutputRoot "build-$side.log")
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $side" }
        & dotnet build-server shutdown | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'Build server shutdown failed.' }
    } finally { Pop-Location }
}
$analysis = Join-Path $HarnessRoot 'scripts/run-perf-008-analysis.py'
& python $analysis self-test
if ($LASTEXITCODE -ne 0) { throw 'Statistical gate self-tests failed.' }
function Invoke-Worker([string]$Root, [string]$Mode, [string]$Name) {
    $assembly = Join-Path $Root 'benchmarks/NeraSpreadSheet.Benchmarks/bin/Release/net10.0/NeraSpreadSheet.Benchmarks.dll'
    $started = [DateTime]::UtcNow.ToString('O')
    & dotnet $assembly --perf-008 $Mode (Join-Path $OutputRoot "$Name.json")
    if ($LASTEXITCODE -ne 0) { throw "Worker failed: $Name" }
    @{ name = $Name; mode = $Mode; productionSha = (git -C $Root rev-parse HEAD); startedUtc = $started
       finishedUtc = [DateTime]::UtcNow.ToString('O'); workerAssemblyHash = (Get-FileHash -LiteralPath $assembly).Hash
       evidenceHash = (Get-FileHash -LiteralPath (Join-Path $OutputRoot "$Name.json")).Hash } |
        ConvertTo-Json -Compress | Add-Content -LiteralPath (Join-Path $OutputRoot 'execution.jsonl') -Encoding utf8
}
foreach ($pair in 0..5) {
    foreach ($side in @('baseline', 'candidate')) {
        Invoke-Worker $BaselineRoot measure ('calibration-{0:00}-{1}' -f $pair, $side)
    }
}
& python $analysis calibrate $OutputRoot
if ($LASTEXITCODE -ne 0) { throw 'Baseline calibration failed.' }
$budgetHash = (Get-FileHash -LiteralPath (Join-Path $OutputRoot 'budget.json')).Hash
Write-Host "Frozen baseline-only budget SHA256: $budgetHash"
@{ event = 'baseline-budget-frozen'; sha256 = $budgetHash; utc = [DateTime]::UtcNow.ToString('O') } |
    ConvertTo-Json -Compress | Add-Content -LiteralPath (Join-Path $OutputRoot 'execution.jsonl') -Encoding utf8
foreach ($pair in 0..11) {
    $order = if ($pair % 2 -eq 0) { @('baseline', 'candidate') } else { @('candidate', 'baseline') }
    foreach ($side in $order) {
        $root = if ($side -eq 'baseline') { $BaselineRoot } else { $CandidateRoot }
        Invoke-Worker $root measure ('paired-{0:00}-{1}' -f $pair, $side)
    }
}
Invoke-Worker $BaselineRoot verify 'stress-baseline'
Invoke-Worker $CandidateRoot verify 'stress-candidate'
if ((Get-FileHash -LiteralPath (Join-Path $OutputRoot 'budget.json')).Hash -ne $budgetHash) { throw 'Frozen budget changed.' }
& python $analysis evaluate $OutputRoot
if ($LASTEXITCODE -ne 0) { throw 'PERF-008 comparison is regression or inconclusive. Inspect raw artifacts; do not widen the budget.' }
