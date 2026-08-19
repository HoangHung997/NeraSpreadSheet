param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,

    [ValidateRange(10, 180)]
    [int]$TimeoutSeconds = 75
)

$ErrorActionPreference = "Stop"

$executable = Resolve-Path -LiteralPath $ExecutablePath
$tempRoot = if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
    [System.IO.Path]::GetTempPath()
} else {
    $env:RUNNER_TEMP
}
$resultPath = Join-Path $tempRoot (
    "nera-maui-windows-smoke-" + [Guid]::NewGuid().ToString("N") + ".json"
)

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $executable.Path
$startInfo.WorkingDirectory = Split-Path -Parent $executable.Path
$startInfo.UseShellExecute = $false
$startInfo.Environment["NERA_MAUI_SMOKE_RESULT"] = $resultPath

$process = [System.Diagnostics.Process]::Start($startInfo)
if ($null -eq $process) {
    throw "The MAUI Windows smoke process could not be started."
}

try {
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill($true)
        throw "The MAUI Windows smoke process exceeded $TimeoutSeconds seconds."
    }
    $process.WaitForExit()

    if (-not (Test-Path -LiteralPath $resultPath)) {
        throw "The MAUI Windows smoke did not create its result marker. ExitCode=$($process.ExitCode)."
    }

    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    Write-Host (Get-Content -LiteralPath $resultPath -Raw)
    if ($process.ExitCode -ne 0) {
        throw "The MAUI Windows smoke exited with code $($process.ExitCode)."
    }
    if ($result.status -ne "success") {
        throw "The MAUI Windows smoke marker did not report success."
    }
    if ([int]$result.frameCount -lt 3) {
        throw "The MAUI Windows smoke completed without the required frame lifecycle."
    }
} finally {
    if (-not $process.HasExited) {
        $process.Kill($true)
    }
    Remove-Item -LiteralPath $resultPath -Force -ErrorAction SilentlyContinue
}
