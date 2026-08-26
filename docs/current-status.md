# NeraSpreadSheet current implementation status

## Formula snapshot

| Counter | Value |
|---|---:|
| Eager/versioned | 262 |
| AST/reference-aware | 34 |
| Dynamic-array unique | 20 |
| **Total** | **316 / at least 538** |
| Tests | 284/284 |
| Complete batches | F001–F014 |

F014 adds `ATANH`, `SINH`, `COSH`, `TANH`, `COMBIN`, `COMBINA`, `FACT`, `FACTDOUBLE`, `GCD` and `LCM` through the authoritative eager/versioned registry.

The workbook/editing, dependency graph, rules/Tables/AutoFilter, WPF/WinForms/MAUI render hosts, XLSX preservation and print/PDF foundations remain validated. Production blockers still include catalog breadth, charts/pivots UI, packaging/API compatibility, plugin trust/isolation, security/fuzzing, recovery, localization/accessibility and broad differential/visual corpora.

Full F014 contract: `docs/hyperbolic-combinatorics-and-integer-contract.md`.

Next: F015 with `MROUND`, `CEILING`, `FLOOR`, `CEILING.PRECISE`, `FLOOR.PRECISE`, `ISO.CEILING`, `MULTINOMIAL`, `SERIESSUM`, `SUMSQ`, `SUMPRODUCT`.
