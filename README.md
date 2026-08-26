# NeraSpreadSheet

> M2 engineering foundation; not a production release.

NeraSpreadSheet is an independent spreadsheet SDK for WPF, WinForms and .NET MAUI with sparse workbook storage, continuous pixel scrolling, dynamic arrays, XLSX preservation, printing/PDF and Function Extension SDK v1.0.

## Current validated snapshot

| Item | Value |
|---|---:|
| Eager/versioned functions | 262 |
| AST/reference-aware functions | 34 |
| Dynamic-array unique functions | 20 |
| **Total functions** | **316 / at least 538** |
| Formula tests | 284/284 |
| Completed formula batches | F001–F014 |
| Pull request | #1 Draft, unmerged |

Latest batches:

- F013: `ACOT`, `ACOTH`, `COT`, `COTH`, `CSC`, `CSCH`, `SEC`, `SECH`, `ASINH`, `ACOSH`.
- F014: `ATANH`, `SINH`, `COSH`, `TANH`, `COMBIN`, `COMBINA`, `FACT`, `FACTDOUBLE`, `GCD`, `LCM`.
- F015 next: `MROUND`, `CEILING`, `FLOOR`, `CEILING.PRECISE`, `FLOOR.PRECISE`, `ISO.CEILING`, `MULTINOMIAL`, `SERIESSUM`, `SUMSQ`, `SUMPRODUCT`.

Build and validation:

```powershell
dotnet restore .\NeraSpreadSheet.slnx
dotnet build .\NeraSpreadSheet.slnx -c Release
dotnet test .\NeraSpreadSheet.Core.slnx -c Release --no-build
./scripts/run-complete-validation.ps1 -Configuration Release -RequireCleanWorkingTree
```
