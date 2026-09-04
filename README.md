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
| Formula/hardening tests | 522/522 |
| Formula implementation | DONE — F001–F019, exact-head CI #922 green |
| Pull request | #1 Draft, unmerged |

Formula implementation is now closed at 546/546 locked catalog names. Q001 starts the post-formula hardening phase with a checked-in differential corpus plus deterministic arithmetic, dependency and malformed-input fuzzing. Q001 passes 518/518 formula/hardening tests, 1,079/1,079 Core-solution tests and architecture verification. The next active item is Q002 workbook/editing state-model fuzz plus an OpenXML round-trip differential corpus.

Build and validation:

```powershell
dotnet restore .\NeraSpreadSheet.Core.slnx
dotnet build .\NeraSpreadSheet.Core.slnx -c Release --no-restore
dotnet test .\NeraSpreadSheet.Core.slnx -c Release --no-build
./scripts/verify-architecture.ps1
```
