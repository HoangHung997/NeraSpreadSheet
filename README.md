# NeraSpreadSheet

> M2 engineering foundation; not a production release.

NeraSpreadSheet is an independent spreadsheet SDK for WPF, WinForms and .NET MAUI with sparse workbook storage, continuous pixel scrolling, dynamic arrays, XLSX preservation, printing/PDF and Function Extension SDK v1.0.

## Current validated snapshot

| Item | Value |
|---|---:|
| Eager/versioned functions | 282 |
| AST/reference-aware functions | 34 |
| Dynamic-array unique functions | 20 |
| **Total functions** | **336 / at least 538** |
| Formula tests | 304/304 |
| Completed formula batches | F001–F015 |
| Public batch size from F015 | 20 new names |
| Pull request | #1 Draft, unmerged |

Latest batches:

- F014: `ATANH`, `SINH`, `COSH`, `TANH`, `COMBIN`, `COMBINA`, `FACT`, `FACTDOUBLE`, `GCD`, `LCM`.
- F015: `MROUND`, `CEILING`, `FLOOR`, `CEILING.PRECISE`, `FLOOR.PRECISE`, `ISO.CEILING`, `MULTINOMIAL`, `SERIESSUM`, `SUMPRODUCT`, `SQRTPI`, `SUMX2MY2`, `SUMX2PY2`, `SUMXMY2`, `BASE`, `DECIMAL`, `ARABIC`, `ROMAN`, `ISEVEN`, `ISODD`, `ISNONTEXT`.
- F016 next: 20 new names after duplicate and catalog audit.

`SUMSQ` and `PRODUCT` already existed before F015 and were not counted twice.

Build and validation:

```powershell
dotnet restore .\NeraSpreadSheet.slnx
dotnet build .\NeraSpreadSheet.slnx -c Release
dotnet test .\NeraSpreadSheet.Core.slnx -c Release --no-build
./scripts/run-complete-validation.ps1 -Configuration Release -RequireCleanWorkingTree
```
