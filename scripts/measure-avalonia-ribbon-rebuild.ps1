param(
    [string]$BaselineRoot = 'ribbon-probe-baseline',
    [string]$OutputRoot = 'artifacts/ribbon-performance'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$baseline = (Resolve-Path $BaselineRoot).Path
$output = [IO.Path]::GetFullPath((Join-Path $root $OutputRoot))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$baselineSha = '2c27bd65e2b6022a374560165821921078982241'
$candidateSha = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $candidateSha -cne $env:NERA_SOURCE_SHA) { throw 'Candidate checkout mismatch.' }
$actualBaseline = (& git -C $baseline rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $actualBaseline -cne $baselineSha) { throw 'Baseline checkout mismatch.' }
$relativeHarness = 'tests/NeraSpreadSheet.Avalonia.Tests/RibbonRebuildProbeTests.cs'
$relativeProject = 'tests/NeraSpreadSheet.Avalonia.Tests/NeraSpreadSheet.Avalonia.Tests.csproj'
$sourceHarness = Join-Path $root $relativeHarness
$targetHarness = Join-Path $baseline $relativeHarness
if (Test-Path $targetHarness) { throw 'Baseline unexpectedly already contains the probe.' }
Copy-Item $sourceHarness $targetHarness
$harnessHash = (Get-FileHash $sourceHarness -Algorithm SHA256).Hash
if ((Get-FileHash $targetHarness -Algorithm SHA256).Hash -cne $harnessHash) { throw 'Harness bytes differ.' }
$reports = @{}
try {
    foreach ($variant in @(
        @{ Name = 'baseline'; Root = $baseline; Sha = $baselineSha },
        @{ Name = 'candidate'; Root = $root; Sha = $candidateSha }
    )) {
        $name = $variant.Name
        $env:NERA_SOURCE_SHA = $variant.Sha
        $env:NERA_RIBBON_PROBE_OUTPUT = Join-Path $output "$name.json"
        $results = Join-Path $output $name
        New-Item -ItemType Directory -Path $results -Force | Out-Null
        $project = Join-Path $variant.Root $relativeProject
        dotnet test $project -c Release --filter 'FullyQualifiedName~RibbonRebuildProbeTests' --logger 'trx;LogFileName=probe.trx' --results-directory $results 2>&1 | Tee-Object (Join-Path $results 'run.log')
        if ($LASTEXITCODE -ne 0) { throw "The $name probe failed. No retries." }
        [xml]$trx = Get-Content (Join-Path $results 'probe.trx') -Raw
        $counts = $trx.TestRun.ResultSummary.Counters
        if ([int]$counts.total -ne 1 -or [int]$counts.executed -ne 1 -or [int]$counts.passed -ne 1) { throw 'Probe test missing, failed or skipped.' }
        $report = Get-Content $env:NERA_RIBBON_PROBE_OUTPUT -Raw | ConvertFrom-Json
        if ($report.sha -cne $variant.Sha -or $report.commandCount -ne 720 -or $report.tabCount -ne 9 -or $report.iterations -ne 7 -or $report.results.Count -ne 3) { throw 'Probe provenance/workload mismatch.' }
        $builtAssembly = Join-Path $variant.Root 'src/NeraSpreadSheet.Avalonia/bin/Release/net10.0/NeraSpreadSheet.Avalonia.dll'
        if ((Get-FileHash $builtAssembly -Algorithm SHA256).Hash.ToLowerInvariant() -cne $report.assemblySha256) { throw 'Loaded product assembly does not match the built source.' }
        $reports[$name] = $report
    }
    if ($reports.baseline.runtime -cne $reports.candidate.runtime -or $reports.baseline.os -cne $reports.candidate.os) { throw 'Paired environments differ.' }
    $rows = foreach ($width in @(820, 1280, 1536)) {
        $before = @($reports.baseline.results | Where-Object width -eq $width)
        $after = @($reports.candidate.results | Where-Object width -eq $width)
        if ($before.Count -ne 1 -or $after.Count -ne 1) { throw 'Missing width sample.' }
        $before = $before[0]; $after = $after[0]
        if ($before.milliseconds.Count -ne 7 -or $after.milliseconds.Count -ne 7 -or $before.allocatedBytes.Count -ne 7 -or $after.allocatedBytes.Count -ne 7) { throw 'Incomplete measurement samples.' }
        if ($before.nativeBodies -ne 9 -or $after.nativeBodies -ne 1) { throw 'Unexpected native materialization count.' }
        [double]$beforeMs = @($before.milliseconds | Sort-Object)[3]
        [double]$afterMs = @($after.milliseconds | Sort-Object)[3]
        [double]$beforeBytes = @($before.allocatedBytes | Sort-Object)[3]
        [double]$afterBytes = @($after.allocatedBytes | Sort-Object)[3]
        if ($beforeMs -le 0 -or $afterMs -le 0 -or $beforeBytes -le 0 -or $afterBytes -le 0) { throw 'Invalid measurement.' }
        [ordered]@{ width = $width; baselineMedianMs = $beforeMs; candidateMedianMs = $afterMs;
            baselineMedianBytes = $beforeBytes; candidateMedianBytes = $afterBytes;
            allocationRatio = $afterBytes / $beforeBytes; timeRatio = $afterMs / $beforeMs;
            baselineBodies = 9; candidateBodies = 1 }
    }
    $summary = [ordered]@{ schema = 'nera.ribbon.paired-probe.v1'; baselineSha = $baselineSha;
        candidateSha = $candidateSha; harnessSha256 = $harnessHash.ToLowerInvariant();
        runtime = $reports.candidate.runtime; rows = @($rows);
        releaseBenchmark = $false; measuresWorksheetScrolling = $false }
    $summary | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $output 'comparison.json') -Encoding utf8
    Write-Output ('NERA_RIBBON_PERFORMANCE ' + ($summary | ConvertTo-Json -Depth 10 -Compress))
}
finally {
    $env:NERA_SOURCE_SHA = $candidateSha
    Remove-Item Env:NERA_RIBBON_PROBE_OUTPUT -ErrorAction SilentlyContinue
}
