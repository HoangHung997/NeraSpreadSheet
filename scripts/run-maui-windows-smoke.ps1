param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,

    [ValidateRange(10, 180)]
    [int]$TimeoutSeconds = 75,

    [ValidateRange(1, 3)]
    [int]$MaximumAttempts = 2,

    [string]$ResultPath,
    [string]$MarkerPrefix,
    [ValidateSet('marker', 'app-file-v1')]
    [string]$ResultProtocol = 'marker'
)

$ErrorActionPreference = "Stop"

if ($ResultProtocol -eq 'app-file-v1') {
    # Package mode owns one process and fresh bounded evidence; legacy behavior stays below.
    $packageProcess = $null
    $packageCancellation = [Threading.CancellationTokenSource]::new()
    $packagePipes = @()
    $packageStage = 'configuration'
    try {
        if (-not $IsWindows -or $env:CI -ne 'true' -or -not $env:RUNNER_TEMP -or
            $MaximumAttempts -ne 1 -or $MarkerPrefix -cnotmatch '^[A-Z0-9_]+:$' -or
            -not [IO.Path]::IsPathFullyQualified($ResultPath)) {
            throw 'Invalid isolated package launch configuration.'
        }
        $packageTemp = [IO.Path]::GetFullPath($env:RUNNER_TEMP).TrimEnd([IO.Path]::DirectorySeparatorChar)
        $packageOutput = [IO.Path]::GetFullPath($ResultPath)
        if (-not $packageOutput.StartsWith($packageTemp + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
            (Test-Path -LiteralPath $packageOutput) -or -not (Test-Path -LiteralPath (Split-Path -Parent $packageOutput) -PathType Container)) {
            throw 'Package output must be new and private to this runner.'
        }
        $packageExecutable = Get-Item -LiteralPath $ExecutablePath
        if ($packageExecutable.PSIsContainer -or $packageExecutable.Extension -cne '.exe') { throw 'Missing package executable.' }
        $packageDirectory = Join-Path $packageTemp ('nera-windows-launch-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $packageDirectory | Out-Null
        $packagePayload = Join-Path $packageDirectory 'payload.json'
        $packageContext = Join-Path $packageDirectory 'context.json'
        $packageNonce = [Guid]::NewGuid().ToString('N')
        $packageContextBytes = [Text.UTF8Encoding]::new($false).GetBytes((@{
            schema = 'native-result-file-context-v1'; path = $packagePayload; transportNonce = $packageNonce
        } | ConvertTo-Json -Compress))
        $packageContextStream = [IO.File]::Open($packageContext, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try { $packageContextStream.Write($packageContextBytes, 0, $packageContextBytes.Length); $packageContextStream.Flush($true) }
        finally { $packageContextStream.Dispose() }
        $packageStart = [Diagnostics.ProcessStartInfo]::new()
        $packageStart.FileName = $packageExecutable.FullName
        $packageStart.WorkingDirectory = $packageExecutable.DirectoryName
        $packageStart.UseShellExecute = $false
        $packageStart.RedirectStandardOutput = $true
        $packageStart.RedirectStandardError = $true
        $packageStart.Environment['NERA_MAUI_SMOKE_RESULT'] = $packagePayload
        $packageStart.Environment['NERA_MAUI_SMOKE_PROTOCOL'] = 'native-result-file-v1'
        $packageStart.Environment['NERA_MAUI_SMOKE_NONCE'] = $packageNonce
        $packageStart.Environment['NERA_MAUI_SMOKE_ATTEMPT'] = '1'
        $packageStage = 'process-start'
        $packageProcess = [Diagnostics.Process]::Start($packageStart)
        if ($null -eq $packageProcess) { throw 'Package process did not start.' }
        $packageClock = [Diagnostics.Stopwatch]::StartNew()
        foreach ($packagePipe in @(
            @{ Input = $packageProcess.StandardOutput.BaseStream; Name = 'console.log' },
            @{ Input = $packageProcess.StandardError.BaseStream; Name = 'stderr.log' }
        )) {
            $packageState = [pscustomobject]@{
                Input = $packagePipe.Input
                Output = [IO.File]::Open((Join-Path $packageDirectory $packagePipe.Name), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
                Buffer = [byte[]]::new(8192); Total = 0L; Done = $false; Read = $null
            }
            $packagePipes += $packageState
            $packageState.Read = $packageState.Input.ReadAsync($packageState.Buffer, 0, $packageState.Buffer.Length, $packageCancellation.Token)
        }
        $packageStage = 'bounded-process-and-pipes'
        while (-not $packageProcess.HasExited -or @($packagePipes | Where-Object { -not $_.Done }).Count -gt 0) {
            if ($packageClock.Elapsed.TotalSeconds -ge $TimeoutSeconds) { throw 'Package process or pipe capture timed out.' }
            foreach ($packagePipe in $packagePipes) {
                if ($packagePipe.Done -or -not $packagePipe.Read.IsCompleted) { continue }
                $packageLength = $packagePipe.Read.GetAwaiter().GetResult()
                if ($packageLength -eq 0) { $packagePipe.Done = $true; continue }
                $packagePipe.Total += $packageLength
                if ($packagePipe.Total -gt 2MB) { throw 'Package process output exceeded its evidence bound.' }
                $packagePipe.Output.Write($packagePipe.Buffer, 0, $packageLength)
                $packagePipe.Read = $packagePipe.Input.ReadAsync($packagePipe.Buffer, 0, $packagePipe.Buffer.Length, $packageCancellation.Token)
            }
            [Threading.Thread]::Sleep(10)
        }
        $packageStage = 'child-exit'
        if ($packageProcess.ExitCode -ne 0) { throw 'Package child exited unsuccessfully.' }
        foreach ($packagePipe in $packagePipes) { $packagePipe.Output.Dispose() }
        $packageStage = 'strict-result'
        & python -B (Join-Path $PSScriptRoot 'verify-native-smoke-result.py') --log (Join-Path $packageDirectory 'console.log') --log (Join-Path $packageDirectory 'stderr.log') --prefix $MarkerPrefix --file-context $packageContext --minimum-frames 3 --output $packageOutput
        if ($LASTEXITCODE -ne 0) { throw 'Package result did not pass the shared strict verifier.' }
        Write-Output 'Loaded Windows package transport passed with child exit zero and a complete bound result.'
        exit 0
    } catch {
        # Report only a fixed stage at the process boundary; raw pipe/error text remains private.
        [Console]::Error.WriteLine("Windows package transport rejected at stage=$packageStage.")
        exit 1
    } finally {
        $packageCancellation.Cancel()
        if ($null -ne $packageProcess) {
            if (-not $packageProcess.HasExited) {
                $packageProcess.Kill($true)
                [void]$packageProcess.WaitForExit(5000)
            }
            foreach ($packagePipe in $packagePipes) { $packagePipe.Input.Dispose(); $packagePipe.Output.Dispose() }
            $packageProcess.Dispose()
        }
        $packageCancellation.Dispose()
    }
}
if ($ResultPath -or $MarkerPrefix) { throw 'Package result options require app-file-v1.' }

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
        "smoke-after-surface-reinsert", "smoke-surface-handler-changed",
        "smoke-surface-loaded", "smoke-surface-loaded-invalidate-returned",
        "smoke-recreated-frame-enter", "smoke-native-surface-loaded",
        "smoke-native-surface-unloaded", "smoke-native-focus-none",
        "smoke-native-focus-surface", "smoke-native-focus-editor", "smoke-native-focus-other",
        "smoke-native-unhandled-exception"
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
