param(
    [ValidateSet('neutral', 'windows', 'android', 'ios', 'maccatalyst')][string] $Platform = 'neutral',
    [switch] $PlanOnly
)
$ErrorActionPreference = 'Stop'
$mauiRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $mauiRoot 'eng/release-009-maui/Common.ps1')
$mauiConfig = Get-Content -Raw -LiteralPath (Join-Path $mauiRoot 'eng/release-009-maui/cohort.json') | ConvertFrom-Json
$mauiClosure = @(Get-MauiClosure $mauiRoot)
if ($PlanOnly) {
    Write-Output "RELEASE-009 MAUI $Platform producer; SDK $($mauiConfig.sdkVersion); 15 neutral packages plus four isolated MAUI target shards, then one canonical feed."
    $mauiClosure | ForEach-Object { [IO.Path]::GetRelativePath($mauiRoot, $_).Replace('\', '/') }
    return
}
$mauiCohort = Get-MauiCohort $mauiRoot
$mauiScratch = New-MauiScratch $mauiCohort
$mauiOutput = Join-Path $mauiRoot "artifacts/release-009-maui/shards/$Platform"
if (Test-Path -LiteralPath $mauiOutput) { throw 'Refusing to mix a previous shard.' }
New-Item -ItemType Directory -Path $mauiOutput | Out-Null
$mauiProperties = @("-p:Version=$($mauiCohort.version)", "-p:PackageVersion=$($mauiCohort.version)",
    "-p:RepositoryCommit=$($mauiCohort.sourceSha)", "-p:SourceRevisionId=$($mauiCohort.sourceSha)",
    '-p:ContinuousIntegrationBuild=true', '-p:DebugType=None')
$mauiTfm = if ($Platform -eq 'neutral') { 'net10.0' } else { $mauiConfig.targets.$Platform }
Push-Location $mauiScratch
try {
    Assert-MauiSdk $mauiCohort
    $mauiProjects = if ($Platform -eq 'neutral') { @($mauiClosure | Where-Object { $_ -notlike '*NeraSpreadSheet.Maui.csproj' }) } else { @($mauiClosure[-1]) }
    foreach ($mauiProject in $mauiProjects) {
        $mauiTargetProperties = if ($Platform -eq 'neutral') { @() } else { @("-p:NeraMauiTargetFrameworks=$mauiTfm") }
        Invoke-MauiDotnet (@('pack', $mauiProject, '-c', 'Release', '-m:1', '/nodeReuse:false', '-o', $mauiOutput) + $mauiProperties + $mauiTargetProperties)
    }
    $mauiLibraries = @()
    if ($Platform -ne 'neutral') {
        $mauiAssets = Get-Content -Raw -LiteralPath (Join-Path $mauiRoot 'src/NeraSpreadSheet.Maui/obj/project.assets.json') | ConvertFrom-Json -AsHashtable
        $mauiLibraries = @($mauiAssets.libraries.Keys | Where-Object { $_ -like 'Microsoft.Maui.*/*' } | Sort-Object)
        if (-not $mauiLibraries.Count) { throw 'No evaluated MAUI dependency versions were recorded.' }
    }
    $mauiPackages = @(Get-ChildItem -LiteralPath $mauiOutput -Filter '*.nupkg' | Sort-Object Name | ForEach-Object {
        $mauiPackageFile = $_
        $mauiZip = [IO.Compression.ZipFile]::OpenRead($mauiPackageFile.FullName)
        try {
            $mauiAssemblies = @($mauiZip.Entries | Where-Object { $_.Name -like 'NeraSpreadSheet.*.dll' } | ForEach-Object {
                $mauiAssemblyFile = Join-Path $mauiScratch ([Guid]::NewGuid().ToString('N') + '.dll')
                $mauiInput = $_.Open()
                $mauiDestination = [IO.File]::Create($mauiAssemblyFile)
                try { $mauiInput.CopyTo($mauiDestination) } finally { $mauiInput.Dispose(); $mauiDestination.Dispose() }
                $mauiAssemblyVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($mauiAssemblyFile).ProductVersion
                if ($mauiAssemblyVersion -ne "$($mauiCohort.version)+$($mauiCohort.sourceSha)") { throw 'Packed assembly source/version mismatch.' }
                [ordered]@{ file = $_.FullName; informationalVersion = $mauiAssemblyVersion
                    sha256 = (Get-FileHash -LiteralPath $mauiAssemblyFile -Algorithm SHA256).Hash.ToLowerInvariant() }
            })
        } finally { $mauiZip.Dispose() }
        [ordered]@{ file = $mauiPackageFile.Name; assemblies = $mauiAssemblies
            sha256 = (Get-FileHash -LiteralPath $mauiPackageFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    Write-MauiJson ([ordered]@{
        schemaVersion = 1; sourceSha = $mauiCohort.sourceSha; version = $mauiCohort.version
        sdkVersion = $mauiCohort.sdkVersion; platform = $Platform; requestedTfm = $mauiTfm
        mauiDependencies = $mauiLibraries; packages = $mauiPackages
        expectedNeutralIds = @($mauiClosure[0..($mauiClosure.Count - 2)] | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) })
    }) (Join-Path $mauiOutput 'shard.json')
} finally { Pop-Location }
Write-Output "Verified-source $Platform shard produced; runtime acceptance remains OPEN."
