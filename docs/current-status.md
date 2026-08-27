# NeraSpreadSheet current implementation status

## Formula snapshot

| Counter | Value |
|---|---:|
| Eager/versioned | 427 |
| AST/reference-aware | 37 |
| Dynamic-array unique | 22 |
| **Total** | **486 / at least 538** |
| Formula tests | 454/454 |
| Complete cycles | F001–F018 |

F018 adds 60 names in three 20-function groups. A covers DBCS/legacy and regex/text compatibility. B adds information/reference introspection plus Gamma, permutation and hypothesis-test compatibility. C adds unit conversion, error/Bessel functions, classic/modern lookup, aggregate/volatile math, determinant, identity matrix and frequency spill output.

The authoritative eager registry remains `StandardFormulaFunctions.CreateAll()`. `CELL`, `ISFORMULA` and `ISREF` stay AST/reference-aware. `MUNIT` and `FREQUENCY` stay in the dynamic-array engine. Resource caps remain fail-closed.

Workbook/editing, dependency graph, Tables/AutoFilter, WPF/WinForms/MAUI hosts, XLSX preservation and print/PDF foundations remain in the validation matrix. Production blockers still include the catalog delta, charts/pivots UI, packaging/API compatibility, plugin trust/isolation, security/fuzzing, recovery, localization/accessibility and broad differential/visual corpora.

Manifest: `docs/formula-manifests/F018_60_FUNCTION_MANIFEST.md`.
