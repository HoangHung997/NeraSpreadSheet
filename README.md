# NeraSpreadSheet

> M2 engineering foundation; not a production release.

NeraSpreadSheet is an independent spreadsheet SDK for WPF, WinForms and .NET MAUI with sparse workbook storage, continuous pixel scrolling, dynamic arrays, XLSX preservation, printing/PDF and Function Extension SDK v1.0.

## Current validated snapshot

| Item | Value |
|---|---:|
| Eager/versioned functions | 242 |
| AST/reference-aware functions | 30 |
| Dynamic-array unique functions | 14 |
| **Total functions** | **286 / at least 538** |
| Formula tests | 254/254 |
| Completed formula batches | F001–F011 |
| Pull request | #1 Draft, unmerged |

Latest batches:

- F010: `GETPIVOTDATA`, `GROUPBY`, `HSTACK`, `HYPERLINK`, `INDIRECT`.
- F011: `LOOKUP`, `OFFSET`, `PERCENTOF`, `PIVOTBY`, `ROW`, `ROWS`, `SHEET`, `SHEETS`, `SORTBY`, `TAKE`.
- F012 next: `TOCOL`, `TOROW`, `TRIMRANGE`, `VSTACK`, `WRAPCOLS`, `WRAPROWS`, `XMATCH`, `IFERROR`, `IFNA`, `SWITCH`.

Build and validation:

```powershell
dotnet restore .\NeraSpreadSheet.slnx
dotnet build .\NeraSpreadSheet.slnx -c Release
dotnet test .\NeraSpreadSheet.Core.slnx -c Release --no-build
./scripts/run-complete-validation.ps1 -Configuration Release -RequireCleanWorkingTree
```
