# NeraSpreadSheet

> M2 engineering foundation; not a production release.

NeraSpreadSheet is an independent spreadsheet SDK for WPF, WinForms and .NET MAUI with sparse workbook storage, continuous pixel scrolling, dynamic arrays, XLSX preservation, printing/PDF and Function Extension SDK v1.0.

## Current validated snapshot

| Item | Value |
|---|---:|
| Eager/versioned functions | 252 |
| AST/reference-aware functions | 34 |
| Dynamic-array unique functions | 20 |
| **Total functions** | **306 / at least 538** |
| Formula tests | 274/274 |
| Completed formula batches | F001–F013 |
| Pull request | #1 Draft, unmerged |

Latest batches:

- F012: `TOCOL`, `TOROW`, `TRIMRANGE`, `VSTACK`, `WRAPCOLS`, `WRAPROWS`, `XMATCH`, `IFERROR`, `IFNA`, `SWITCH`.
- F013: `ACOT`, `ACOTH`, `COT`, `COTH`, `CSC`, `CSCH`, `SEC`, `SECH`, `ASINH`, `ACOSH`.
- F014 next: `ATANH`, `SINH`, `COSH`, `TANH`, `COMBIN`, `COMBINA`, `FACT`, `FACTDOUBLE`, `GCD`, `LCM`.

Build and validation:

```powershell
dotnet restore .\NeraSpreadSheet.slnx
dotnet build .\NeraSpreadSheet.slnx -c Release
dotnet test .\NeraSpreadSheet.Core.slnx -c Release --no-build
./scripts/run-complete-validation.ps1 -Configuration Release -RequireCleanWorkingTree
```
