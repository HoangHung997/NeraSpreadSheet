# NeraSpreadSheet

> M2 engineering foundation; not a production release.

NeraSpreadSheet is an independent spreadsheet SDK for WPF, WinForms and .NET MAUI with sparse workbook storage, continuous pixel scrolling, dynamic arrays, XLSX preservation, printing/PDF and Function Extension SDK v1.0.

## Current validated snapshot

| Item | Value |
|---|---:|
| Eager/versioned functions | 427 |
| AST/reference-aware functions | 37 |
| Dynamic-array unique functions | 22 |
| **Total functions** | **486 / at least 538** |
| Formula tests | 454/454 |
| Completed formula cycles | F001–F018 |
| Pull request | #1 Draft, unmerged |

F018 is a 60-function cycle split into A/B/C groups of 20. Every group passed an analyzer-clean local CLI build, its 20 named regressions and the full formula suite before the next group started. The final local gate passed 1,015 Core-solution tests plus architecture verification before the three commits were eligible for one branch update and one exact-head GitHub CI.

Build and validation:

```powershell
dotnet restore .\NeraSpreadSheet.Core.slnx
dotnet build .\NeraSpreadSheet.Core.slnx -c Release --no-restore
dotnet test .\NeraSpreadSheet.Core.slnx -c Release --no-build
./scripts/verify-architecture.ps1
```
