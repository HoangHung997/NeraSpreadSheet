$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$violations = New-Object System.Collections.Generic.List[string]

function Read-ProjectXml {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    [xml] (Get-Content -Raw -LiteralPath $Path)
}

function Get-ProjectProperty {
    param(
        [Parameter(Mandatory = $true)]
        [xml] $Document,

        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    foreach ($propertyGroup in $Document.Project.PropertyGroup) {
        $value = $propertyGroup.$Name
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return [string] $value
        }
    }

    return $null
}

function Add-Violation {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Message
    )

    $violations.Add($Message)
}

function Get-RelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    [System.IO.Path]::GetRelativePath($root, $Path).Replace("\", "/")
}

$propsPath = Join-Path $root "Directory.Build.props"
$props = Read-ProjectXml $propsPath

$requiredCommonProperties = [ordered] @{
    Authors = "HoangHung997"
    Company = "NeraSpreadSheet"
    RepositoryUrl = "https://github.com/HoangHung997/NeraSpreadSheet"
    RepositoryType = "git"
    VersionPrefix = "0.1.0"
    PackageProjectUrl = "https://github.com/HoangHung997/NeraSpreadSheet"
    PackageReadmeFile = "README.md"
}

foreach ($entry in $requiredCommonProperties.GetEnumerator()) {
    $actual = Get-ProjectProperty -Document $props -Name $entry.Key
    if ($actual -ne $entry.Value) {
        Add-Violation "Directory.Build.props must set $($entry.Key) to '$($entry.Value)'."
    }
}

$packageTags = Get-ProjectProperty -Document $props -Name "PackageTags"
foreach ($requiredTag in @("spreadsheet", "xlsx", "openxml")) {
    if ([string]::IsNullOrWhiteSpace($packageTags) -or
        -not ($packageTags.Split(";") -contains $requiredTag)) {
        Add-Violation "Directory.Build.props PackageTags must include '$requiredTag'."
    }
}

$readmePackItems = @(
    $props.Project.ItemGroup.None |
        Where-Object {
            $_.Include -eq '$(MSBuildThisFileDirectory)README.md' -and
            $_.Pack -eq "true" -and
            $_.PackagePath -eq "\"
        }
)
if ($readmePackItems.Count -ne 1) {
    Add-Violation "Directory.Build.props must pack the root README.md exactly once at the package root."
}

$srcProjects = Get-ChildItem -Path (Join-Path $root "src") -Recurse -Filter *.csproj |
    Sort-Object FullName

foreach ($projectFile in $srcProjects) {
    $relativePath = Get-RelativePath $projectFile.FullName
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectFile.Name)
    $document = Read-ProjectXml $projectFile.FullName

    $packageId = Get-ProjectProperty -Document $document -Name "PackageId"
    if ($packageId -ne $projectName) {
        Add-Violation "$relativePath must declare PackageId '$projectName'."
    }

    $description = Get-ProjectProperty -Document $document -Name "Description"
    if ([string]::IsNullOrWhiteSpace($description)) {
        Add-Violation "$relativePath must declare a package Description."
    }

    $targetFramework = Get-ProjectProperty -Document $document -Name "TargetFramework"
    $targetFrameworks = Get-ProjectProperty -Document $document -Name "TargetFrameworks"
    if ([string]::IsNullOrWhiteSpace($targetFramework) -and
        [string]::IsNullOrWhiteSpace($targetFrameworks)) {
        Add-Violation "$relativePath must declare TargetFramework or TargetFrameworks."
    }

    $isPackable = Get-ProjectProperty -Document $document -Name "IsPackable"
    if ($isPackable -eq "false") {
        Add-Violation "$relativePath is a source package project and must remain packable."
    }
}

foreach ($folderName in @("tests", "samples", "benchmarks")) {
    $folderPath = Join-Path $root $folderName
    if (-not (Test-Path -LiteralPath $folderPath)) {
        continue
    }

    $nonSdkProjects = Get-ChildItem -Path $folderPath -Recurse -Filter *.csproj |
        Sort-Object FullName

    foreach ($projectFile in $nonSdkProjects) {
        $relativePath = Get-RelativePath $projectFile.FullName
        $document = Read-ProjectXml $projectFile.FullName

        $isPackable = Get-ProjectProperty -Document $document -Name "IsPackable"
        if ($isPackable -ne "false") {
            Add-Violation "$relativePath must set IsPackable=false."
        }

        $packageId = Get-ProjectProperty -Document $document -Name "PackageId"
        if (-not [string]::IsNullOrWhiteSpace($packageId)) {
            Add-Violation "$relativePath must not declare PackageId."
        }
    }
}

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Packaging SDK verification passed."
