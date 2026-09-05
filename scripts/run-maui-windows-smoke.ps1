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

function Write-FailureStageTrace {
    param([string]$ResultPath)

    $tracePath = $ResultPath + ".trace"
    if (-not (Test-Path -LiteralPath $tracePath -PathType Leaf)) { return }
    # The app only emits these labels. Never echo arbitrary sidecar content or
    # paths, and bound diagnostic work independently of the smoke success gate.
    $allowedStages = @(
        "smoke-page-constructor", "smoke-page-loaded", "smoke-view-created",
        "smoke-editor-host-created", "smoke-editor-host-attached", "smoke-first-frame",
        "smoke-editor-verified", "table-editor-enter", "table-editor-opened",
        "table-editor-candidate-accepted", "table-editor-enter-returned",
        "table-editor-history-returned", "table-editor-stale-caret-cancel-returned",
        "table-editor-alt-enter-returned", "table-editor-column-resized",
        "table-editor-zoomed", "table-editor-geometry-settled",
        "table-editor-escape-returned", "table-editor-complete",
        "smoke-pinch-returned", "smoke-pan-returned", "smoke-tap-returned",
        "smoke-primary-input-complete", "smoke-primary-input-verified",
        "smoke-wheel-queued", "smoke-resize-queued", "smoke-resize-applied",
        "smoke-recreation-queued", "smoke-before-surface-remove",
        "smoke-after-surface-remove", "smoke-after-handler-disconnect",
        "smoke-after-surface-reinsert"
    )
    $stream = [IO.File]::OpenRead($tracePath)
    try {
        $buffer = [byte[]]::new(8192)
        $length = $stream.Read($buffer, 0, $buffer.Length)
        $labels = [Text.Encoding]::UTF8.GetString($buffer, 0, $length) -split '\r?\n'
        Write-Host "MAUI Windows failure stage trace:"
        $labels | Select-Object -First 64 | ForEach-Object {
            if ($allowedStages -ccontains $_) { Write-Host $_ }
        }
    } finally {
        $stream.Dispose()
    }
}

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
            Write-FailureStageTrace -ResultPath $resultPath
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
            Write-FailureStageTrace -ResultPath $resultPath
            throw (
                "The MAUI Windows smoke exited with code " +
                "$($process.ExitCode) after creating its marker."
            )
        }
        if ($result.status -ne "success") {
            Write-FailureStageTrace -ResultPath $resultPath
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
