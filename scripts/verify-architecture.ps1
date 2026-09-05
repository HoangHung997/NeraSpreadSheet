$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$coreProjects = @(
    "src/NeraSpreadSheet.Foundation/NeraSpreadSheet.Foundation.csproj",
    "src/NeraSpreadSheet.Core/NeraSpreadSheet.Core.csproj",
    "src/NeraSpreadSheet.Formulas/NeraSpreadSheet.Formulas.csproj",
    "src/NeraSpreadSheet.Layout/NeraSpreadSheet.Layout.csproj",
    "src/NeraSpreadSheet.Scrolling/NeraSpreadSheet.Scrolling.csproj"
)

$forbiddenPatterns = @(
    "NeraSpreadSheet.Wpf",
    "NeraSpreadSheet.WinForms",
    "NeraSpreadSheet.Maui",
    "NeraSpreadSheet.Rendering.Direct2D",
    "NeraSpreadSheet.Rendering.Skia",
    "DocumentFormat.OpenXml",
    "UseWPF",
    "UseWindowsForms",
    "UseMaui"
)

$violations = New-Object System.Collections.Generic.List[string]

foreach ($relativePath in $coreProjects) {
    $fullPath = Join-Path $root $relativePath
    $content = Get-Content -Raw -LiteralPath $fullPath

    foreach ($pattern in $forbiddenPatterns) {
        if ($content -match [regex]::Escape($pattern)) {
            $violations.Add("$relativePath contains forbidden dependency marker '$pattern'.")
        }
    }
}

$coreSource = Get-ChildItem -Path (Join-Path $root "src") -Recurse -Filter *.cs |
    Where-Object {
        $_.FullName -match "NeraSpreadSheet\.(Foundation|Core|Formulas|Layout|Scrolling)"
    }

foreach ($file in $coreSource) {
    $content = Get-Content -Raw -LiteralPath $file.FullName
    foreach ($pattern in @("System.Windows", "System.Drawing", "Microsoft.Maui", "SkiaSharp")) {
        if ($content -match [regex]::Escape($pattern)) {
            $violations.Add("$($file.FullName) contains forbidden platform namespace '$pattern'.")
        }
    }
}

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Architecture verification passed."
