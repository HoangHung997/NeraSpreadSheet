# NeraSpreadSheet

> M2 engineering foundation; not a production release.

NeraSpreadSheet is an independent spreadsheet SDK for WPF, WinForms and .NET MAUI with sparse workbook storage, continuous pixel scrolling, dynamic arrays, XLSX preservation, printing/PDF and Function Extension SDK v1.0.

## Current validated snapshot

| Item | Value |
|---|---:|
| Eager/versioned functions | 372 |
| AST/reference-aware functions | 34 |
| Dynamic-array unique functions | 20 |
| **Total functions** | **426 / at least 538** |
| Formula tests | 394/394 |
| Completed formula cycles | F001–F017 |
| Pull request | #1 Draft, unmerged |

F017 establishes the 30-function process: manifest first, groups A/B/C of ten names, a green CLI gate after every group, three commits pushed together, then one exact-head GitHub CI.

Build and validation:

```powershell
dotnet restore .\NeraSpreadSheet.Core.slnx
dotnet build .\NeraSpreadSheet.Core.slnx -c Release --no-restore
dotnet test .\NeraSpreadSheet.Core.slnx -c Release --no-build
./scripts/verify-architecture.ps1
```
