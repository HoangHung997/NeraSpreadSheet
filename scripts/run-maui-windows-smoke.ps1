param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,

    [ValidateRange(10, 180)]
    [int]$TimeoutSeconds = 75,

    [ValidateRange(1, 3)]
    [int]$MaximumAttempts = 2
)

$ErrorActionPreference = "Stop"

$executable = Resolve-Path -LiteralPath $ExecutablePath
$tempRoot = if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
    [System.IO.Path]::GetTempPath()
} else {
    $env:RUNNER_TEMP
}

# Windows fast-fail 0xC0000409 has occurred intermittently during unpackaged
# WinUI startup on hosted runners before MAUI creates its first Window or any
# result marker. Retry that one pre-marker startup failure once. Any timeout,
# functional marker failure, nonzero exit after a marker, or repeated fast-fail
# remains a hard failure and cannot be hidden by this runner.
$retryablePreMarkerExitCodes = [System.Collections.Generic.HashSet[int]]::new()
[void]$retryablePreMarkerExitCodes.Add(-1073740791)

for ($attempt = 1; $attempt -le $MaximumAttempts; $attempt++) {
    $resultPath = Join-Path $tempRoot (
        "nera-maui-windows-smoke-" +
        [Guid]::NewGuid().ToString("N") +
        ".json"
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $executable.Path
    $startInfo.WorkingDirectory = Split-Path -Parent $executable.Path
    $startInfo.UseShellExecute = $false
    $startInfo.Environment["NERA_MAUI_SMOKE_RESULT"] = $resultPath
    $startInfo.Environment["NERA_MAUI_SMOKE_ATTEMPT"] =
        $attempt.ToString([Globalization.CultureInfo]::InvariantCulture)

    $process = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) {
        throw "The MAUI Windows smoke process could not be started."
    }

    try {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            throw "The MAUI Windows smoke process exceeded $TimeoutSeconds seconds on attempt $attempt."
        }
        $process.WaitForExit()

        if (-not (Test-Path -LiteralPath $resultPath)) {
            $exitCode = $process.ExitCode
            $mayRetry =
                $attempt -lt $MaximumAttempts -and
                $retryablePreMarkerExitCodes.Contains($exitCode)
            if ($mayRetry) {
                Write-Warning (
                    "The MAUI Windows smoke exited before creating its marker " +
                    "with retryable native startup code $exitCode on attempt " +
                    "$attempt of $MaximumAttempts. Retrying once with a clean " +
                    "result path."
                )
                Start-Sleep -Seconds 2
                continue
            }
            throw (
                "The MAUI Windows smoke did not create its result marker. " +
                "Attempt=$attempt/$MaximumAttempts; ExitCode=$exitCode."
            )
        }

        $rawResult = Get-Content -LiteralPath $resultPath -Raw
        $result = $rawResult | ConvertFrom-Json
        Write-Host $rawResult
        if ($process.ExitCode -ne 0) {
            throw (
                "The MAUI Windows smoke exited with code " +
                "$($process.ExitCode) after creating its marker."
            )
        }
        if ($result.status -ne "success") {
            throw "The MAUI Windows smoke marker did not report success."
        }
        if ([int]$result.frameCount -lt 3) {
            throw "The MAUI Windows smoke completed without the required frame lifecycle."
        }
        return
    } finally {
        if (-not $process.HasExited) {
            $process.Kill($true)
        }
        Remove-Item -LiteralPath $resultPath -Force -ErrorAction SilentlyContinue
    }
}

throw "The MAUI Windows smoke exhausted all configured attempts."
