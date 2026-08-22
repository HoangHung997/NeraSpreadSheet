[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [switch]$SkipRestore,
    [switch]$SkipMaui,
    [switch]$SkipLoadedRuntime,
    [switch]$RequireCleanWorkingTree,

    [string]$OutputPath = 'artifacts/final-validation/result.json'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-Step {
    param(
        [Parameter(Mandatory)]
        [string]$Name,
        [Parameter(Mandatory)]
        [scriptblock]$Action,
        [switch]$Optional
    )

    $started = Get-Date
    Write-Host "`n=== $Name ===" -ForegroundColor Cyan
    try {
        & $Action
        $script:results.Add([ordered]@{
            name = $Name
            status = 'success'
            optional = [bool]$Optional
            startedUtc = $started.ToUniversalTime().ToString('O')
            durationSeconds = [math]::Round(((Get-Date) - $started).TotalSeconds, 3)
            error = $null
        })
    }
    catch {
        $entry = [ordered]@{
            name = $Name
            status = if ($Optional) { 'warning' } else { 'failure' }
            optional = [bool]$Optional
            startedUtc = $started.ToUniversalTime().ToString('O')
            durationSeconds = [math]::Round(((Get-Date) - $started).TotalSeconds, 3)
            error = $_.Exception.ToString()
        }
        $script:results.Add($entry)
        if (-not $Optional) {
            throw
        }
        Write-Warning "$Name failed but was marked optional: $($_.Exception.Message)"
    }
}

function Invoke-DotNet {
    param([Parameter(ValueFromRemainingArguments)] [string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') exited with code $LASTEXITCODE."
    }
}

function Test-ProjectExists {
    param([Parameter(Mandatory)] [string]$Path)
    return Test-Path -LiteralPath $Path -PathType Leaf
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $repoRoot
try {
    $results = [System.Collections.Generic.List[object]]::new()
    $head = (& git rev-parse HEAD).Trim()
    $branch = (& git branch --show-current).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to read the Git repository state.'
    }

    if ($RequireCleanWorkingTree) {
        $dirty = & git status --porcelain
        if ($LASTEXITCODE -ne 0) {
            throw 'Unable to inspect the Git working tree.'
        }
        if ($dirty) {
            throw 'The working tree is not clean.'
        }
    }

    if (-not $SkipRestore) {
        Invoke-Step 'Restore Core solution' {
            Invoke-DotNet restore NeraSpreadSheet.Core.slnx
        }
    }

    Invoke-Step 'Build Core solution' {
        $args = @('build', 'NeraSpreadSheet.Core.slnx', '-c', $Configuration)
        if ($SkipRestore) { $args += '--no-restore' }
        Invoke-DotNet @args
    }

    Invoke-Step 'Test Core solution' {
        Invoke-DotNet test NeraSpreadSheet.Core.slnx `
            -c $Configuration `
            --no-build `
            --logger "trx;LogFileName=final-core-tests.trx"
    }

    Invoke-Step 'Verify architecture' {
        & ./scripts/verify-architecture.ps1
        if ($LASTEXITCODE -ne 0) {
            throw "Architecture verification exited with code $LASTEXITCODE."
        }
    }

    if ($IsWindows) {
        if (-not $SkipRestore) {
            Invoke-Step 'Restore full Windows solution' {
                Invoke-DotNet restore NeraSpreadSheet.slnx
            }
        }

        Invoke-Step 'Build full Windows solution' {
            $args = @('build', 'NeraSpreadSheet.slnx', '-c', $Configuration)
            if ($SkipRestore) { $args += '--no-restore' }
            Invoke-DotNet @args
        }

        Invoke-Step 'Test Windows solution' {
            Invoke-DotNet test NeraSpreadSheet.Core.slnx `
                -c $Configuration `
                --no-build `
                --logger "trx;LogFileName=final-windows-tests.trx"
        }

        Invoke-Step 'Desktop GPU and native presenter tests' {
            Invoke-DotNet test `
                tests/NeraSpreadSheet.Windows.Rendering.Tests/NeraSpreadSheet.Windows.Rendering.Tests.csproj `
                -c $Configuration `
                --no-build `
                --logger "trx;LogFileName=final-windows-renderer-tests.trx"
        }
    }

    if (-not $SkipMaui) {
        if ($IsWindows) {
            Invoke-Step 'Build MAUI Windows target' {
                Invoke-DotNet build `
                    src/NeraSpreadSheet.Maui/NeraSpreadSheet.Maui.csproj `
                    -c $Configuration `
                    -f net10.0-windows10.0.19041.0 `
                    -p:NeraMauiTargetFrameworks=net10.0-windows10.0.19041.0
            }

            Invoke-Step 'Test MAUI Windows contracts' {
                Invoke-DotNet test `
                    tests/NeraSpreadSheet.Maui.Tests/NeraSpreadSheet.Maui.Tests.csproj `
                    -c $Configuration `
                    -p:NeraMauiTargetFrameworks=net10.0-windows10.0.19041.0 `
                    --logger "trx;LogFileName=final-maui-windows-tests.trx"
            }

            if (-not $SkipLoadedRuntime) {
                $loadedProjects = @(
                    'tests/NeraSpreadSheet.Maui.Windows.TableFilterSmoke/NeraSpreadSheet.Maui.Windows.TableFilterSmoke.csproj',
                    'tests/NeraSpreadSheet.Maui.Windows.Smoke/NeraSpreadSheet.Maui.Windows.Smoke.csproj',
                    'tests/NeraSpreadSheet.Maui.Windows.ScaleSmoke/NeraSpreadSheet.Maui.Windows.ScaleSmoke.csproj'
                )
                foreach ($project in $loadedProjects) {
                    if (-not (Test-ProjectExists $project)) { continue }
                    $name = [IO.Path]::GetFileNameWithoutExtension($project)
                    $publishPath = "artifacts/final-validation/$name"
                    Invoke-Step "Publish loaded runtime: $name" {
                        Invoke-DotNet publish $project `
                            -c $Configuration `
                            -f net10.0-windows10.0.19041.0 `
                            -r win-x64 `
                            --self-contained false `
                            -p:NeraMauiTargetFrameworks=net10.0-windows10.0.19041.0 `
                            -o $publishPath
                    }
                    $executable = Join-Path $publishPath "$name.exe"
                    Invoke-Step "Run loaded runtime: $name" {
                        & ./scripts/run-maui-windows-smoke.ps1 -ExecutablePath $executable
                        if ($LASTEXITCODE -ne 0) {
                            throw "$name exited with code $LASTEXITCODE."
                        }
                    }
                }
            }
        }
        elseif ($IsMacOS) {
            Invoke-Step 'Build MAUI iOS target' {
                Invoke-DotNet build `
                    src/NeraSpreadSheet.Maui/NeraSpreadSheet.Maui.csproj `
                    -c $Configuration `
                    -f net10.0-ios `
                    -p:NeraMauiTargetFrameworks=net10.0-ios
            }
            Invoke-Step 'Build MAUI Mac Catalyst target' {
                Invoke-DotNet build `
                    src/NeraSpreadSheet.Maui/NeraSpreadSheet.Maui.csproj `
                    -c $Configuration `
                    -f net10.0-maccatalyst `
                    -p:NeraMauiTargetFrameworks=net10.0-maccatalyst
            }
        }
        elseif ($IsLinux) {
            Invoke-Step 'Build MAUI Android target' {
                Invoke-DotNet build `
                    src/NeraSpreadSheet.Maui/NeraSpreadSheet.Maui.csproj `
                    -c $Configuration `
                    -f net10.0-android `
                    -p:NeraMauiTargetFrameworks=net10.0-android
            } -Optional
        }
    }

    $failed = @($results | Where-Object status -eq 'failure')
    $report = [ordered]@{
        schemaVersion = 1
        generatedUtc = (Get-Date).ToUniversalTime().ToString('O')
        repositoryRoot = $repoRoot.Path
        branch = $branch
        head = $head
        configuration = $Configuration
        operatingSystem = [Environment]::OSVersion.ToString()
        success = $failed.Count -eq 0
        steps = $results
        codexFollowUp = @(
            'Run target-hardware 4K/120-Hz scrolling, latency and memory tests.',
            'Run physical multi-monitor DPI transitions.',
            'Run Android/iOS device IME, virtual-keyboard, suspend/resume and accessibility tests.',
            'Run screen-reader, high-contrast, localization and theme certification.',
            'Run external Excel/LibreOffice XLSX compatibility corpus and differential checks.',
            'Run fuzzing for formulas, XLSX, clipboard and malformed package graphs.'
        )
    }

    $outputFile = Join-Path $repoRoot $OutputPath
    New-Item -ItemType Directory -Force -Path (Split-Path $outputFile) | Out-Null
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputFile -Encoding utf8
    Write-Host "`nValidation report: $outputFile" -ForegroundColor Green

    if (-not $report.success) {
        exit 1
    }
}
finally {
    Pop-Location
}
