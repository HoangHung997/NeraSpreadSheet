# NeraSpreadSheet

> M2 engineering foundation; not a production release.

NeraSpreadSheet is an independent spreadsheet SDK for WPF, WinForms and .NET MAUI with sparse workbook storage, continuous pixel scrolling, dynamic arrays, XLSX preservation, printing/PDF and Function Extension SDK v1.0.

## Current validated snapshot

| Item | Value |
|---|---:|
| Eager/versioned functions | 468 |
| AST/reference-aware functions | 40 |
| Dynamic-array unique functions | 38 |
| **Total functions** | **546 / 546 locked catalog names** |
| Formula tests | 514/514 |
| Completed formula cycles | F001–F019 (local-green; exact-head CI pending) |
| Pull request | #1 Draft, unmerged |

F019 adds another 60 locked catalog names in A/B/C groups of 20. Every group passed an analyzer-clean CLI build, 20 named regressions and the full formula suite; the final local gate passed 1,075 Core-solution tests plus architecture verification. The locked catalog now contains 546 names; a final catalog-delta audit may still add future compatibility names.

Build and validation:

```powershell
dotnet restore .\NeraSpreadSheet.Core.slnx
dotnet build .\NeraSpreadSheet.Core.slnx -c Release --no-restore
dotnet test .\NeraSpreadSheet.Core.slnx -c Release --no-build
./scripts/verify-architecture.ps1
```
