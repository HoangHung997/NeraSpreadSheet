param(
    [string]$DotNetPath = "dotnet",
    [string]$OutputDirectory = "artifacts/ribbon-visual-011/captures",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "samples/NeraSpreadSheet.Wpf.Sample/NeraSpreadSheet.Wpf.Sample.csproj"
if (-not $SkipBuild) {
    & $DotNetPath build $project -c Release -m:1
    if ($LASTEXITCODE -ne 0) { throw "Ribbon preview build failed." }
}
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory, $repositoryRoot)
[System.IO.Directory]::CreateDirectory($outputPath) | Out-Null
$application = Join-Path $repositoryRoot "samples/NeraSpreadSheet.Wpf.Sample/bin/Release/net10.0-windows10.0.19041.0/NeraSpreadSheet.Wpf.Sample.dll"
$captureError = Join-Path $outputPath "capture.stderr.log"
$captureProcess = Start-Process -FilePath $DotNetPath -ArgumentList @('"' + $application + '"', '--ribbon-preview', '--capture', '"' + $outputPath + '"') -WindowStyle Hidden -RedirectStandardError $captureError -PassThru
if (-not $captureProcess.WaitForExit(180000)) {
    $captureProcess.Kill()
    throw "Ribbon preview capture timed out."
}
if ($captureProcess.ExitCode -ne 0) {
    Get-Content -LiteralPath $captureError | Write-Output
    throw "Ribbon preview capture failed with exit code $($captureProcess.ExitCode)."
}
$manifest = Get-Content -Raw -LiteralPath (Join-Path $outputPath "manifest.json") | ConvertFrom-Json
if ($manifest.status -ne "success") { throw "Ribbon capture validation failed." }
Write-Output "Ribbon capture passed: $($manifest.images.Count) images, $($manifest.layouts.Count) native layout snapshots."
