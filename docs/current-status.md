# NeraSpreadSheet current implementation status

## Formula snapshot

| Counter | Value |
|---|---:|
| Eager/versioned | 372 |
| AST/reference-aware | 34 |
| Dynamic-array unique | 20 |
| **Total** | **426 / at least 538** |
| Formula tests | 394/394 |
| Complete cycles | F001–F017 |

F017 adds 30 statistical names through three ten-function groups. Group A supplies legacy normal, rank, percentile and forecast aliases. Group B adds variance, Student-t, confidence and probability compatibility. Group C adds discrete distributions plus F/Z hypothesis tests and their legacy aliases.

The authoritative registry remains `StandardFormulaFunctions.CreateAll()`. Compatibility names delegate to existing modern numerical primitives where possible; new discrete searches and summations are bounded and fail closed.

Workbook/editing, dependency graph, Tables/AutoFilter, WPF/WinForms/MAUI hosts, XLSX preservation and print/PDF foundations remain in the validation matrix. Production blockers still include catalog breadth, charts/pivots UI, packaging/API compatibility, plugin trust/isolation, security/fuzzing, recovery, localization/accessibility and broad differential/visual corpora.

Manifest: `docs/formula-manifests/F017_30_FUNCTION_MANIFEST.md`.
