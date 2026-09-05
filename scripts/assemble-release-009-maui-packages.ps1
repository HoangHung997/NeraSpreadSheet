param([string] $ShardDirectory, [switch] $PlanOnly)
$ErrorActionPreference = 'Stop'
$mauiRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $mauiRoot 'eng/release-009-maui/Common.ps1')
if ($PlanOnly) { Write-Output 'Verify five same-cohort producers; pack one canonical MAUI package with four framework groups; runtime acceptance stays OPEN.'; return }
$mauiCohort = Get-MauiCohort $mauiRoot
$mauiScratch = New-MauiScratch $mauiCohort
$mauiOutput = Join-Path $mauiRoot 'artifacts/release-009-maui/canonical'
$mauiAssembler = Join-Path $mauiRoot 'eng/release-009-maui/package_matrix.py'
if (-not $ShardDirectory) { throw 'An explicit downloaded shard directory is required.' }
& python $mauiAssembler prepare --source $ShardDirectory --output $mauiOutput --sha $mauiCohort.sourceSha --version $mauiCohort.version --sdk $mauiCohort.sdkVersion
if ($LASTEXITCODE -ne 0) { throw 'Package shard validation failed.' }
Push-Location $mauiScratch
try {
    Assert-MauiSdk $mauiCohort
    # A clean neutral wrapper avoids importing source props or evaluating foreign workloads.
    $mauiPackProject = Join-Path $mauiScratch 'PackMauiArtifacts.csproj'
    Copy-Item -LiteralPath (Join-Path $mauiRoot 'eng/release-009-maui/PackMauiArtifacts.csproj') -Destination $mauiPackProject
    Invoke-MauiDotnet @('restore', $mauiPackProject)
    Invoke-MauiDotnet @('pack', $mauiPackProject, '--no-build', '--no-restore', '-o', (Join-Path $mauiOutput 'feed'),
        "-p:NeraMauiNuspec=$(Join-Path $mauiOutput 'Maui.nuspec')", "-p:NeraMauiPayload=$(Join-Path $mauiOutput 'payload')")
} finally { Pop-Location }
& python $mauiAssembler finalize --output $mauiOutput
if ($LASTEXITCODE -ne 0) { throw 'Canonical package differs from verified shard payload.' }
Write-Output 'Canonical package verified. Four native consumers are still required for runtime acceptance.'
