param([switch] $PlanOnly)

$ErrorActionPreference = 'Stop'
$demoRoot = Split-Path -Parent $PSScriptRoot
$demoProject = Join-Path $demoRoot 'samples/NeraSpreadSheet.Wpf.Sample/NeraSpreadSheet.Wpf.Sample.csproj'
if ($PlanOnly) {
    Write-Output 'Publish WPF sample, win-x64, self-contained, untrimmed, NeraRibbonDemo=true; run the published executable without --ribbon-preview through its existing complete capture matrix.'
    Write-Output 'Experimental artifact only; no installer, public release, NuGet publish, or user workbook.'
    return
}
if ($env:CI -ne 'true' -or -not $IsWindows -or -not $env:RUNNER_TEMP -or -not $env:GITHUB_RUN_ID) {
    throw 'Demo packaging and native validation run only in isolated Windows CI; use -PlanOnly locally.'
}
if (git -C $demoRoot status --porcelain --untracked-files=no) { throw 'Tracked source must be clean before publishing the demo.' }
$demoSha = (git -C $demoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $demoSha -notmatch '^[0-9a-f]{40}$' -or $demoSha -ne $env:GITHUB_SHA) {
    throw 'Demo source SHA does not match the requested workflow head.'
}
$demoVersion = "0.1.0-ci.$($env:GITHUB_RUN_ID).$($env:GITHUB_RUN_ATTEMPT).g$($demoSha.Substring(0, 12))"
$demoOutput = Join-Path $demoRoot "artifacts/release-009-demo/$demoSha"
if (Test-Path -LiteralPath $demoOutput) { throw 'Refusing to mix an existing demo artifact directory.' }
$demoApplication = Join-Path $demoOutput 'app'
$demoCaptures = Join-Path $demoOutput 'captures'
New-Item -ItemType Directory -Path $demoApplication, $demoCaptures | Out-Null
& dotnet publish $demoProject -c Release -r win-x64 --self-contained true -o $demoApplication -m:1 /nodeReuse:false `
    -p:PublishSingleFile=false -p:PublishTrimmed=false -p:NeraRibbonDemo=true -p:DebugType=None `
    "-p:Version=$demoVersion" "-p:RepositoryCommit=$demoSha" "-p:SourceRevisionId=$demoSha" -p:ContinuousIntegrationBuild=true
if ($LASTEXITCODE -ne 0) { throw 'Self-contained Ribbon demo publish failed.' }
$demoExe = Join-Path $demoApplication 'NeraSpreadSheet.Wpf.Sample.exe'
foreach ($name in @('NeraSpreadSheet.Wpf.Sample.exe', 'coreclr.dll', 'hostpolicy.dll', 'PresentationFramework.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $demoApplication $name) -PathType Leaf)) {
        throw "Required self-contained demo file is missing: $name"
    }
}
$demoHeader = [IO.File]::OpenRead($demoExe)
$demoReader = [IO.BinaryReader]::new($demoHeader)
try {
    if ($demoReader.ReadUInt16() -ne 0x5A4D) { throw 'Demo apphost has no DOS header.' }
    $demoHeader.Position = 0x3C
    $demoPeOffset = $demoReader.ReadInt32()
    if ($demoPeOffset -lt 0 -or $demoPeOffset -gt $demoHeader.Length - 6) { throw 'Demo PE header offset is invalid.' }
    $demoHeader.Position = $demoPeOffset
    if ($demoReader.ReadUInt32() -ne 0x00004550 -or $demoReader.ReadUInt16() -ne 0x8664) {
        throw 'Demo apphost is not an AMD64 Windows executable.'
    }
} finally { $demoReader.Dispose() }
$demoRuntime = Get-Content -Raw -LiteralPath (Join-Path $demoApplication 'NeraSpreadSheet.Wpf.Sample.runtimeconfig.json') | ConvertFrom-Json
if ($demoRuntime.runtimeOptions.configProperties.'NeraSpreadSheet.RibbonDemo' -ne $true) {
    throw 'The published app would not open the Ribbon shell by default.'
}
$demoAssemblies = @(Get-ChildItem -LiteralPath $demoApplication -Filter 'NeraSpreadSheet*.dll' -Recurse | ForEach-Object {
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($_.FullName).ProductVersion
    if ($version -ne "$demoVersion+$demoSha") { throw "Published SDK source/version mismatch: $($_.Name)" }
    [ordered]@{ file = [IO.Path]::GetRelativePath($demoApplication, $_.FullName).Replace('\', '/'); version = $version }
})
if ($demoAssemblies.Count -lt 2) { throw 'The demo did not contain its SDK assemblies.' }

# Use the actual published apphost and its default Ribbon mode, not dotnet run
# or a source test assembly. The existing capture method retains all assertions.
$demoLog = Join-Path $env:RUNNER_TEMP ('nera-demo-' + [Guid]::NewGuid().ToString('N') + '.stderr.log')
$demoProcess = Start-Process -FilePath $demoExe -ArgumentList @('--capture', ('"' + $demoCaptures + '"')) `
    -WorkingDirectory $demoApplication -WindowStyle Hidden -RedirectStandardError $demoLog -PassThru
try {
    if (-not $demoProcess.WaitForExit(180000)) {
        $demoProcess.Kill()
        throw 'Published demo capture timed out; the artifact is not accepted.'
    }
    if ($demoProcess.ExitCode -ne 0) { throw "Published demo capture failed with exit code $($demoProcess.ExitCode)." }
} catch {
    if (Test-Path -LiteralPath $demoLog) {
        Get-Content -LiteralPath $demoLog |
            Where-Object { $_ -match '^Capture: ' } |
            Select-Object -Last 8 | Write-Output
    }
    Write-Output "Completed synthetic capture images: $(@(Get-ChildItem -LiteralPath $demoCaptures -Filter '*.png').Count)"
    throw
} finally { $demoProcess.Dispose() }
$demoManifest = Get-Content -Raw -LiteralPath (Join-Path $demoCaptures 'manifest.json') | ConvertFrom-Json
if ($demoManifest.status -ne 'success' -or $demoManifest.schemaVersion -ne 2 -or
    $demoManifest.layouts.Count -ne 128 -or $demoManifest.images.Count -lt 177) {
    throw 'Published demo did not complete the existing Ribbon command/layout matrix.'
}
foreach ($entry in $demoManifest.images) {
    if ($entry.file -notmatch '^[a-zA-Z0-9_.-]+\.png$' -or
        -not (Test-Path -LiteralPath (Join-Path $demoCaptures $entry.file) -PathType Leaf)) {
        throw 'Published demo capture manifest refers to a missing or unsafe image path.'
    }
}
Copy-Item -LiteralPath (Join-Path $demoRoot 'docs/demo/README-WIN11-VI.md') -Destination (Join-Path $demoOutput 'README.md')
Copy-Item -LiteralPath (Join-Path $demoRoot 'docs/demo/COMMANDS-WIN11-VI.md') -Destination (Join-Path $demoOutput 'COMMANDS.md')
$demoDocuments = @('README.md', 'COMMANDS.md') | ForEach-Object {
    $document = Get-Item -LiteralPath (Join-Path $demoOutput $_)
    [ordered]@{ file = $_; bytes = $document.Length; sha256 = (Get-FileHash -LiteralPath $document.FullName -Algorithm SHA256).Hash }
}
$demoFiles = @(Get-ChildItem -LiteralPath $demoApplication -File -Recurse | Sort-Object FullName | ForEach-Object {
    [ordered]@{
        file = [IO.Path]::GetRelativePath($demoOutput, $_.FullName).Replace('\', '/')
        bytes = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
})
[ordered]@{
    schemaVersion = 1; status = 'success'; sourceSha = $demoSha; version = $demoVersion
    target = 'win-x64'; selfContained = $true; defaultRibbonShell = $true
    captureImages = $demoManifest.images.Count; nativeLayoutSnapshots = $demoManifest.layouts.Count
    limitation = 'Experimental WPF demo; this does not close combined UX/Table, physical-device, MAUI, GPU or full Excel acceptance.'
    sdkAssemblies = $demoAssemblies; files = $demoFiles; documents = @($demoDocuments)
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $demoOutput 'demo-manifest.json') -Encoding utf8
Write-Output "Experimental published demo passed at $demoSha; $($demoManifest.images.Count) captures, $($demoManifest.layouts.Count) layouts."
