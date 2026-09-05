param([switch] $PlanOnly)

$ErrorActionPreference = 'Stop'
$releaseRoot = Split-Path -Parent $PSScriptRoot
$releaseProjects = [System.Collections.Generic.List[string]]::new()
$releaseVisited = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

function Add-PackageClosure([string] $Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    $sourcePrefix = [IO.Path]::GetFullPath((Join-Path $releaseRoot 'src')) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($sourcePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Package closure escaped the SDK source directory.'
    }
    if (-not $releaseVisited.Add($resolved)) { return }
    [xml] $project = Get-Content -Raw -LiteralPath $resolved
    foreach ($reference in $project.SelectNodes('//ProjectReference')) {
        if ($reference.Condition) { throw 'Conditional project reference requires an explicit packaging decision.' }
        Add-PackageClosure (Join-Path (Split-Path -Parent $resolved) $reference.Include)
    }
    $releaseProjects.Add($resolved)
}

$releaseRoots = @('NeraSpreadSheet.Wpf', 'NeraSpreadSheet.WinForms', 'NeraSpreadSheet.OpenXml')
foreach ($id in $releaseRoots) { Add-PackageClosure (Join-Path $releaseRoot "src/$id/$id.csproj") }
if ($PlanOnly) {
    $releaseProjects | ForEach-Object { [IO.Path]::GetRelativePath($releaseRoot, $_).Replace('\', '/') }
    return
}
if ($env:CI -ne 'true' -or -not $IsWindows -or -not $env:RUNNER_TEMP -or -not $env:GITHUB_RUN_ID) {
    throw 'Native package acceptance runs only in the isolated Windows CI job; use -PlanOnly locally.'
}
if (git -C $releaseRoot status --porcelain --untracked-files=no) { throw 'Tracked source tree must be clean before packaging.' }
$releaseSha = (git -C $releaseRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $releaseSha -notmatch '^[0-9a-f]{40}$') { throw 'Could not resolve the source SHA.' }
$releaseVersion = "0.1.0-ci.$($env:GITHUB_RUN_ID).$($env:GITHUB_RUN_ATTEMPT).g$($releaseSha.Substring(0, 12))"
$releaseOutput = Join-Path $releaseRoot "artifacts/release-009/$releaseSha"
$releaseFeed = Join-Path $releaseOutput 'packages'
New-Item -ItemType Directory -Path $releaseFeed -Force | Out-Null
if (Get-ChildItem -LiteralPath $releaseFeed -Filter '*.nupkg') { throw 'Refusing to mix an existing package artifact set.' }
$releaseScratch = Join-Path $env:RUNNER_TEMP ('nera-consumer-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $releaseScratch | Out-Null
$releaseCache = Join-Path $releaseScratch 'packages'
$releaseConsumer = Join-Path $releaseScratch 'consumer'
New-Item -ItemType Directory -Path $releaseConsumer | Out-Null

function Invoke-ReleaseDotnet([string[]] $Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE." }
}

$releaseBuildProperties = @("-p:Version=$releaseVersion", "-p:RepositoryCommit=$releaseSha", "-p:SourceRevisionId=$releaseSha", '-p:ContinuousIntegrationBuild=true')
Push-Location $releaseRoot
try {
    foreach ($id in $releaseRoots) {
        Invoke-ReleaseDotnet (@('build', "src/$id/$id.csproj", '-c', 'Release', '-m:1', '/nodeReuse:false') + $releaseBuildProperties)
    }
    foreach ($project in $releaseProjects) {
        Invoke-ReleaseDotnet (@('pack', $project, '-c', 'Release', '--no-build', '--no-restore', '-o', $releaseFeed) + $releaseBuildProperties)
    }
} finally { Pop-Location }

$releaseManifest = foreach ($package in Get-ChildItem -LiteralPath $releaseFeed -Filter '*.nupkg' | Sort-Object Name) {
    $zip = [IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $nuspecs = @($zip.Entries | Where-Object { $_.FullName.EndsWith('.nuspec', [StringComparison]::OrdinalIgnoreCase) })
        if ($nuspecs.Count -ne 1) { throw 'Package must contain exactly one nuspec.' }
        $reader = [IO.StreamReader]::new($nuspecs[0].Open())
        try { [xml] $spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $spec.package.metadata
        if ($metadata.version -ne $releaseVersion -or $metadata.repository.commit -ne $releaseSha) { throw 'Package source/version mismatch.' }
        [ordered]@{
            id = [string]$metadata.id; version = [string]$metadata.version; sourceSha = $releaseSha
            sha256 = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash
            libraries = @($zip.Entries.FullName | Where-Object { $_ -like 'lib/*/*.dll' })
        }
    } finally { $zip.Dispose() }
}
if (@($releaseManifest).Count -ne $releaseProjects.Count) { throw 'Incomplete SDK package closure.' }
foreach ($id in $releaseRoots + @('NeraSpreadSheet.Rendering.Direct2D')) {
    if (-not ($releaseManifest | Where-Object { $_.id -eq $id })) { throw "Required package missing: $id" }
}
$releaseManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $releaseOutput 'package-manifest.json') -Encoding utf8

Copy-Item -LiteralPath (Join-Path $releaseRoot 'tests/NeraSpreadSheet.Packaged.Windows.Smoke/Program.cs') -Destination $releaseConsumer
Copy-Item -LiteralPath (Join-Path $releaseRoot 'tests/NeraSpreadSheet.Packaged.Windows.Smoke/NeraSpreadSheet.Packaged.Windows.Smoke.csproj') -Destination $releaseConsumer
$releaseConfig = Join-Path $releaseConsumer 'NuGet.Config'
$releaseXml = [xml]'<configuration><packageSources><clear/><add key="nera-artifact" value=""/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources><packageSourceMapping><clear/><packageSource key="nera-artifact"><package pattern="NeraSpreadSheet.*"/></packageSource><packageSource key="nuget.org"><package pattern="*"/></packageSource></packageSourceMapping></configuration>'
$releaseXml.configuration.packageSources.add[0].value = $releaseFeed
$releaseXml.Save($releaseConfig)
Push-Location $releaseConsumer
try {
    Invoke-ReleaseDotnet @('restore', '--configfile', $releaseConfig, '--packages', $releaseCache, "-p:NeraPackageVersion=$releaseVersion")
    $assets = Get-Content -Raw -LiteralPath 'obj/project.assets.json' | ConvertFrom-Json -AsHashtable
    if (@($assets.libraries.Values | Where-Object { $_.type -ne 'package' }).Count) { throw 'Consumer assets contain a source project instead of packages.' }
    $resolvedFolders = @($assets.packageFolders.Keys | ForEach-Object { [IO.Path]::GetFullPath($_).TrimEnd('\', '/') })
    if ($resolvedFolders.Count -ne 1 -or $resolvedFolders[0] -ne [IO.Path]::GetFullPath($releaseCache).TrimEnd('\', '/')) {
        throw 'Consumer restore used a package cache outside the isolated directory.'
    }
    $resolvedSdk = @($assets.libraries.Keys | Where-Object { $_.StartsWith('NeraSpreadSheet.', [StringComparison]::OrdinalIgnoreCase) })
    foreach ($library in $resolvedSdk) {
        $parts = $library.Split('/')
        if ($parts[1] -ne $releaseVersion -or -not ($releaseManifest | Where-Object { $_.id -eq $parts[0] })) { throw 'Consumer resolved a foreign SDK package.' }
    }
    $resolvedSdk | Sort-Object | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releaseOutput 'resolved-sdk-packages.json') -Encoding utf8
    Invoke-ReleaseDotnet @('build', '-c', 'Release', '--no-restore', "-p:NeraPackageVersion=$releaseVersion", '-m:1', '/nodeReuse:false')
    $dll = Join-Path $releaseConsumer 'bin/Release/net10.0-windows10.0.19041.0/NeraSpreadSheet.Packaged.Windows.Smoke.dll'
    Invoke-ReleaseDotnet @($dll, $releaseVersion, $releaseSha, (Join-Path $releaseOutput 'loaded-consumer.json'))
} finally { Pop-Location }
Write-Output "RELEASE-009 Windows package consumer passed at $releaseSha ($releaseVersion). MAUI package acceptance remains separate."
