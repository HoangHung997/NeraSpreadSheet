# NeraSpreadSheet current implementation status

## Formula snapshot

| Counter | Value |
|---|---:|
| Eager/versioned | 242 |
| AST/reference-aware | 34 |
| Dynamic-array unique | 20 |
| **Total** | **296 / at least 538** |
| Tests | 264/264 |
| Complete batches | F001–F012 |

F012 adds `TOCOL`, `TOROW`, `TRIMRANGE`, `VSTACK`, `WRAPCOLS`, `WRAPROWS`, `XMATCH`, `IFERROR`, `IFNA` and `SWITCH`.

The workbook/editing, dependency graph, rules/Tables/AutoFilter, WPF/WinForms/MAUI render hosts, XLSX preservation and print/PDF foundations remain validated. Production blockers still include catalog breadth, charts/pivots UI, packaging/API compatibility, plugin trust/isolation, security/fuzzing, recovery, localization/accessibility and broad differential/visual corpora.

Full F012 contract: `docs/flatten-wrap-xmatch-lazy-errors-contract.md`.

Next: F013 with `ACOT`, `ACOTH`, `COT`, `COTH`, `CSC`, `CSCH`, `DEGREES`, `RADIANS`, `SEC`, `SECH`.
