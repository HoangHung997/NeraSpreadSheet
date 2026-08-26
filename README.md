# NeraSpreadSheet

> M2 engineering foundation; not a production release.

NeraSpreadSheet is an independent spreadsheet SDK for WPF, WinForms and .NET MAUI with sparse workbook storage, continuous pixel scrolling, dynamic arrays, XLSX preservation, printing/PDF and Function Extension SDK v1.0.

## Current validated snapshot

| Item | Value |
|---|---:|
| Eager/versioned functions | 342 |
| AST/reference-aware functions | 34 |
| Dynamic-array unique functions | 20 |
| **Total functions** | **396 / at least 538** |
| Formula tests | 364/364 |
| Completed formula cycles | F001–F016 |
| Pull request | #1 Draft, unmerged |

F016 is the first locked 60-function cycle: three sequential commits A/B/C of 20 names each, one manifest and 60 separately named regressions. A compile-time definite-assignment error was isolated to Group A, repaired by replacing the owning commit, and the final history remains exactly three commits.

Build and validation:

```powershell
dotnet restore .\NeraSpreadSheet.slnx
dotnet build .\NeraSpreadSheet.slnx -c Release
dotnet test .\NeraSpreadSheet.Core.slnx -c Release --no-build
./scripts/verify-architecture.ps1
```

F017 will use the same 60-function A/B/C process after duplicate and catalog audit.
