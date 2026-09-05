Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-MauiDotnet([string[]] $Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments[0]) failed (exit $LASTEXITCODE)." }
}

function Get-MauiCohort([string] $Root) {
    if ($env:CI -ne 'true' -or -not $env:RUNNER_TEMP -or -not $env:GITHUB_RUN_ID) {
        throw 'Only isolated CI may build packages/consumers; use -PlanOnly or the light fixture tests locally.'
    }
    if (git -C $Root status --porcelain --untracked-files=no) { throw 'Tracked source must be clean.' }
    $sha = (git -C $Root rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $sha -notmatch '^[a-f0-9]{40}$' -or $sha -ne $env:GITHUB_SHA) {
        throw 'Source does not match the exact workflow SHA.'
    }
    $config = Get-Content -Raw -LiteralPath (Join-Path $Root 'eng/release-009-maui/cohort.json') | ConvertFrom-Json
    [ordered]@{
        schemaVersion = 1; sourceSha = $sha
        version = "0.1.0-ci.$($env:GITHUB_RUN_ID).$($env:GITHUB_RUN_ATTEMPT).g$($sha.Substring(0, 12))"
        sdkVersion = $config.sdkVersion
    }
}

function New-MauiScratch($Cohort) {
    $scratch = Join-Path $env:RUNNER_TEMP ('nera-maui-package-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $scratch | Out-Null
    @{ sdk = @{ version = $Cohort.sdkVersion; rollForward = 'disable'; allowPrerelease = $false } } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $scratch 'global.json') -Encoding utf8
    return $scratch
}

function Assert-MauiSdk($Cohort) {
    $actual = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or $actual -ne $Cohort.sdkVersion) { throw 'Resolved SDK differs from the package cohort.' }
}

function Get-MauiClosure([string] $Root) {
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $ordered = [Collections.Generic.List[string]]::new()
    $prefix = [IO.Path]::GetFullPath((Join-Path $Root 'src')) + [IO.Path]::DirectorySeparatorChar
    function Visit([string] $Path) {
        $resolved = [IO.Path]::GetFullPath($Path)
        if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'SDK reference escaped src.' }
        if (-not $seen.Add($resolved)) { return }
        [xml] $project = Get-Content -Raw -LiteralPath $resolved
        foreach ($reference in $project.SelectNodes('//ProjectReference')) {
            if ($reference.HasAttribute('Condition') -or $reference.ParentNode.HasAttribute('Condition')) {
                throw 'Conditional project references require a packaging decision.'
            }
            Visit (Join-Path (Split-Path -Parent $resolved) $reference.Include)
        }
        $ordered.Add($resolved)
    }
    Visit (Join-Path $Root 'src/NeraSpreadSheet.Maui/NeraSpreadSheet.Maui.csproj')
    return $ordered.ToArray()
}

function Write-MauiJson($Value, [string] $Path) {
    $Value | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $Path -Encoding utf8
}
