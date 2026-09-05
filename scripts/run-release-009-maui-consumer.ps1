param(
    [ValidateSet('windows', 'android', 'ios', 'maccatalyst')][string] $Platform = 'windows',
    [string] $FeedDirectory,
    [switch] $RunAndroidNative,
    [switch] $RunIosNative,
    [switch] $PlanOnly
)
$ErrorActionPreference = 'Stop'
$mauiRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $mauiRoot 'eng/release-009-maui/Common.ps1')
if ($RunAndroidNative -and $Platform -ne 'android') { throw 'The released Android transport requires the Android package consumer.' }
if ($RunIosNative -and $Platform -ne 'ios') { throw 'The released iOS transport requires the iOS package consumer.' }
if ($PlanOnly) { Write-Output 'Verify canonical feed; copy public consumer outside checkout; restore into fresh cache; verify TFM/app payload. Optional Android/iOS native execution verifies fresh cohort marker; Windows/Mac native remain OPEN.'; return }
$mauiCohort = Get-MauiCohort $mauiRoot
$mauiConfig = Get-Content -Raw -LiteralPath (Join-Path $mauiRoot 'eng/release-009-maui/cohort.json') | ConvertFrom-Json
$mauiVerifier = Join-Path $mauiRoot 'eng/release-009-maui/package_matrix.py'
& python $mauiVerifier verify-feed --source $FeedDirectory --sha $mauiCohort.sourceSha --version $mauiCohort.version
if ($LASTEXITCODE -ne 0) { throw 'Canonical feed verification failed.' }
$mauiFeed = Get-Content -Raw -LiteralPath (Join-Path $FeedDirectory 'feed-manifest.json') | ConvertFrom-Json
if ($mauiFeed.sdkVersion -ne $mauiCohort.sdkVersion) { throw 'Consumer SDK does not match producer SDK.' }
$mauiScratch = New-MauiScratch $mauiCohort
$mauiConsumer = Join-Path $mauiScratch 'consumer'
$mauiCache = Join-Path $mauiScratch 'cache'
New-Item -ItemType Directory -Path $mauiConsumer | Out-Null
$mauiTemplate = Join-Path $mauiRoot 'tests/NeraSpreadSheet.Packaged.Maui.Smoke'
Get-ChildItem -LiteralPath $mauiTemplate | Where-Object { $_.Name -notin @('bin', 'obj') } |
    Copy-Item -Destination $mauiConsumer -Recurse
foreach ($name in @('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props')) {
    Set-Content -LiteralPath (Join-Path $mauiConsumer $name) -Value '<Project />' -Encoding utf8
}
$mauiNonce = [Guid]::NewGuid().ToString('N')
$mauiBundleId = 'com.neraspreadsheet.packagedmauismoke'
$mauiIdentity = @"
namespace Packaged.Maui.Smoke;
internal static class CohortIdentity
{
    public const string SourceSha = "$($mauiCohort.sourceSha)";
    public const string Version = "$($mauiCohort.version)";
    public const string FeedHash = "$($mauiFeed.feedHash)";
    public const string Nonce = "$mauiNonce";
    public const string Platform = "$Platform";
}
"@
Set-Content -LiteralPath (Join-Path $mauiConsumer 'CohortIdentity.g.cs') -Value $mauiIdentity -Encoding utf8
$mauiNuget = [xml]'<configuration><packageSources><clear/><add key="nera-artifact" value=""/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources><packageSourceMapping><clear/><packageSource key="nera-artifact"><package pattern="NeraSpreadSheet.*"/></packageSource><packageSource key="nuget.org"><package pattern="*"/></packageSource></packageSourceMapping></configuration>'
$mauiNuget.configuration.packageSources.add[0].value = [IO.Path]::GetFullPath((Join-Path $FeedDirectory 'feed'))
$mauiNugetPath = Join-Path $mauiConsumer 'NuGet.Config'
$mauiNuget.Save($mauiNugetPath)
$mauiArm = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq [Runtime.InteropServices.Architecture]::Arm64
$mauiRid = switch ($Platform) { 'windows' { 'win-x64' } 'android' { 'android-x64' } 'ios' { if ($mauiArm) { 'iossimulator-arm64' } else { 'iossimulator-x64' } } 'maccatalyst' { if ($mauiArm) { 'maccatalyst-arm64' } else { 'maccatalyst-x64' } } }
$mauiConfiguration = if ($Platform -eq 'ios') { 'Debug' } else { 'Release' }
$mauiOutput = Join-Path $mauiRoot "artifacts/release-009-maui/consumers/$Platform"
if (Test-Path -LiteralPath $mauiOutput) { throw 'Refusing previous consumer evidence.' }
New-Item -ItemType Directory -Path $mauiOutput | Out-Null
$mauiProperties = @("-p:NeraPackageVersion=$($mauiCohort.version)", "-p:NeraConsumerPlatform=$Platform",
    "-p:NeraMauiControlsVersion=$($mauiFeed.mauiDependencies.'Microsoft.Maui.Controls')", "-p:Configuration=$mauiConfiguration",
    "-p:NeraConsumerTargetFramework=$($mauiConfig.targets.$Platform)", "-p:RuntimeIdentifier=$mauiRid", '-p:RestoreFallbackFolders=')
Push-Location $mauiConsumer
try {
    Assert-MauiSdk $mauiCohort
    Invoke-MauiDotnet (@('restore', '--configfile', $mauiNugetPath, '--packages', $mauiCache) + $mauiProperties)
    & python $mauiVerifier inspect-consumer --source $FeedDirectory --assets (Join-Path $mauiConsumer 'obj/project.assets.json') --cache $mauiCache --platform $Platform --rid $mauiRid --sha $mauiCohort.sourceSha --version $mauiCohort.version --output (Join-Path $mauiOutput 'resolved.json')
    if ($LASTEXITCODE -ne 0) { throw 'Isolated consumer asset verification failed.' }
    if ($Platform -eq 'windows') {
        Invoke-MauiDotnet (@('publish', '-c', $mauiConfiguration, '--no-restore', '--self-contained', 'false', '-o', (Join-Path $mauiScratch 'app'), '-m:1', '/nodeReuse:false') + $mauiProperties)
        $mauiAppPath = Join-Path $mauiScratch 'app/NeraSpreadSheet.Packaged.Maui.Smoke.exe'
    } else {
        Invoke-MauiDotnet (@('build', '-c', $mauiConfiguration, '--no-restore', '-m:1', '/nodeReuse:false') + $mauiProperties)
        $mauiCandidates = @(if ($Platform -eq 'android') {
            Get-ChildItem -LiteralPath (Join-Path $mauiConsumer 'bin') -Recurse -Filter '*-Signed.apk' -File
        } else {
            Get-ChildItem -LiteralPath (Join-Path $mauiConsumer 'bin') -Recurse -Filter '*.app' -Directory |
                Where-Object {
                    $mauiPlist = Join-Path $_.FullName $(if ($Platform -eq 'ios') { 'Info.plist' } else { 'Contents/Info.plist' })
                    if (-not (Test-Path -LiteralPath $mauiPlist -PathType Leaf)) { throw 'Apple app bundle is missing its manifest.' }
                    $mauiActualBundleId = & /usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' $mauiPlist
                    if ($LASTEXITCODE -ne 0) { throw 'Apple app bundle identity could not be read.' }
                    $mauiActualBundleId -ceq $mauiBundleId
                }
        })
        if ($mauiCandidates.Count -ne 1) { throw 'Consumer output is missing or ambiguous.' }
        $mauiAppPath = $mauiCandidates[0].FullName
    }
    # One scanner defines hidden-file and internal-link semantics on every host.
    $mauiPayloadPath = Join-Path $mauiScratch 'app-payload.json'
    & python $mauiVerifier capture-app --app $mauiAppPath --platform $Platform --output $mauiPayloadPath
    if ($LASTEXITCODE -ne 0) { throw 'Consumer app payload capture failed.' }
    $mauiPayload = @(Get-Content -Raw -LiteralPath $mauiPayloadPath | ConvertFrom-Json)
    Write-MauiJson ([ordered]@{ schemaVersion = 1; status = 'built-runtime-open'; sourceSha = $mauiCohort.sourceSha
        version = $mauiCohort.version; sdkVersion = $mauiCohort.sdkVersion; feedHash = $mauiFeed.feedHash
        platform = $Platform; rid = $mauiRid; configuration = $mauiConfiguration; nonce = $mauiNonce
        appName = [IO.Path]::GetFileName($mauiAppPath)
        files = $mauiPayload; runtimeAcceptance = 'OPEN'; nativeEditorCoverage = 'OPEN' }) (Join-Path $mauiOutput 'build-manifest.json')
    # Absolute paths stay exclusively in RUNNER_TEMP.
    Write-MauiJson ([ordered]@{ appPath = $mauiAppPath; evidenceDirectory = $mauiOutput; nonce = $mauiNonce
        sourceSha = $mauiCohort.sourceSha; version = $mauiCohort.version; feedHash = $mauiFeed.feedHash
        bundleId = $mauiBundleId; markerPrefix = 'NERA_PACKAGED_MAUI_SMOKE:' }) (Join-Path $mauiScratch 'launch-inputs.json')
    & python $mauiVerifier verify-app --app $mauiAppPath --build (Join-Path $mauiOutput 'build-manifest.json')
    if ($LASTEXITCODE -ne 0) { throw 'Consumer app payload verification failed.' }
    if ($RunAndroidNative -or $RunIosNative) {
        $mauiNativeResult = Join-Path $mauiScratch ($Platform + '-result-' + [Guid]::NewGuid().ToString('N') + '.json')
        # Shared transport is imported unchanged from the released root source; no retries or fallback.
        if ($RunAndroidNative) {
            & bash (Join-Path $mauiRoot 'scripts/run-maui-android-smoke.sh') $mauiAppPath $mauiBundleId 'NeraPackagedMauiSmoke' 'NERA_PACKAGED_MAUI_SMOKE:' $mauiNativeResult
        } else {
            & bash (Join-Path $mauiRoot 'scripts/run-maui-ios-smoke.sh') $mauiAppPath $mauiBundleId 'NERA_PACKAGED_MAUI_SMOKE:' $mauiNativeResult
        }
        if ($LASTEXITCODE -ne 0) { throw "$Platform native package transport failed." }
        & python $mauiVerifier verify-runtime --result $mauiNativeResult --build (Join-Path $mauiOutput 'build-manifest.json')
        if ($LASTEXITCODE -ne 0) { throw "$Platform runtime cohort or public postconditions failed." }
        Copy-Item -LiteralPath $mauiNativeResult -Destination (Join-Path $mauiOutput 'loaded-consumer.json')
        $mauiPlatformLabel = if ($RunIosNative) { 'iOS' } else { 'Android' }
        $mauiExitEvidence = if ($RunIosNative) { 'simctl-status-zero-and-explicit-completed-marker' } else { 'explicit-completed-marker' }
        Write-MauiJson ([ordered]@{ schemaVersion = 1; status = 'success'; platform = $Platform
            sourceSha = $mauiCohort.sourceSha; version = $mauiCohort.version; feedHash = $mauiFeed.feedHash
            nonce = $mauiNonce; appName = [IO.Path]::GetFileName($mauiAppPath)
            runtimeAcceptance = "$mauiPlatformLabel public package smoke verified"; exitEvidence = $mauiExitEvidence
            nativeEditorCoverage = 'OPEN' }) (Join-Path $mauiOutput 'runtime-verification.json')
    }
} finally { Pop-Location }
if ($RunAndroidNative -or $RunIosNative) { Write-Output "$mauiPlatformLabel PackageReference consumer passed native public postconditions and exact cohort validation; native editor remains OPEN." }
else { Write-Output "$Platform PackageReference app built from the canonical feed; runtime acceptance is OPEN pending shared launcher integration." }
