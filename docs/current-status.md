# NeraSpreadSheet current implementation status

## Formula snapshot

| Counter | Value |
|---|---:|
| Eager/versioned | 252 |
| AST/reference-aware | 34 |
| Dynamic-array unique | 20 |
| **Total** | **306 / at least 538** |
| Tests | 274/274 |
| Complete batches | F001–F013 |

F013 adds `ACOT`, `ACOTH`, `COT`, `COTH`, `CSC`, `CSCH`, `SEC`, `SECH`, `ASINH` and `ACOSH`. `DEGREES` and `RADIANS` were already present and were not counted twice.

The workbook/editing, dependency graph, rules/Tables/AutoFilter, WPF/WinForms/MAUI render hosts, XLSX preservation and print/PDF foundations remain validated. Production blockers still include catalog breadth, charts/pivots UI, packaging/API compatibility, plugin trust/isolation, security/fuzzing, recovery, localization/accessibility and broad differential/visual corpora.

Full F013 contract: `docs/advanced-trigonometric-and-hyperbolic-contract.md`.

Next: F014 with `ATANH`, `SINH`, `COSH`, `TANH`, `COMBIN`, `COMBINA`, `FACT`, `FACTDOUBLE`, `GCD`, `LCM`.
