# NeraSpreadSheet current implementation status

## Formula snapshot

| Counter | Value |
|---|---:|
| Eager/versioned | 242 |
| AST/reference-aware | 30 |
| Dynamic-array unique | 14 |
| **Total** | **286 / at least 538** |
| Tests | 254/254 |
| Complete batches | F001–F011 |

F011 adds `LOOKUP`, `OFFSET`, `PERCENTOF`, `PIVOTBY`, `ROW`, `ROWS`, `SHEET`, `SHEETS`, `SORTBY` and `TAKE`.

The workbook/editing, dependency graph, rules/Tables/AutoFilter, WPF/WinForms/MAUI render hosts, XLSX preservation and print/PDF foundations remain validated. Production blockers still include catalog breadth, charts/pivots UI, packaging/API compatibility, plugin trust/isolation, security/fuzzing, recovery, localization/accessibility and broad differential/visual corpora.

Full F011 contract: `docs/lookup-reference-pivot-ordering-contract.md`.

Next: F012 with ten functions: `TOCOL`, `TOROW`, `TRIMRANGE`, `VSTACK`, `WRAPCOLS`, `WRAPROWS`, `XMATCH`, `IFERROR`, `IFNA`, `SWITCH`.
