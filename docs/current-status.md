# NeraSpreadSheet current implementation status

## Formula snapshot

| Counter | Value |
|---|---:|
| Eager/versioned | 468 |
| AST/reference-aware | 40 |
| Dynamic-array unique | 38 |
| **Total** | **546 / 546 locked catalog names** |
| Formula tests | 514/514 |
| Complete cycles | F001–F019 local-green; exact-head CI pending |

F019 adds 60 names in three 20-function groups. A covers Calc/date/text compatibility, B covers ETS/regression/matrix/external-data functions, and C adds higher-order lambda functions plus explicit external-state add-in/cube/RTD/Copilot boundaries.

The authoritative eager registry remains `StandardFormulaFunctions.CreateAll()`. `CELL`, `ISFORMULA` and `ISREF` stay AST/reference-aware. `MUNIT` and `FREQUENCY` stay in the dynamic-array engine. Resource caps remain fail-closed.

Workbook/editing, dependency graph, Tables/AutoFilter, WPF/WinForms/MAUI hosts, XLSX preservation and print/PDF foundations remain in the validation matrix. Production blockers still include the catalog delta, charts/pivots UI, packaging/API compatibility, plugin trust/isolation, security/fuzzing, recovery, localization/accessibility and broad differential/visual corpora.

Manifest: `docs/formula-manifests/F019_60_FUNCTION_MANIFEST.md`.
