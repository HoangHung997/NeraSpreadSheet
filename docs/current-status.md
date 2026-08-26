# NeraSpreadSheet current implementation status

## Formula snapshot

| Counter | Value |
|---|---:|
| Eager/versioned | 342 |
| AST/reference-aware | 34 |
| Dynamic-array unique | 20 |
| **Total** | **396 / at least 538** |
| Formula tests | 364/364 |
| Complete cycles | F001–F016 |

F016 adds exactly 60 new names through three 20-function commits. Group A implements the first 20 complex engineering functions. Group B finishes the complex family and adds 14 legacy statistical names through existing modern numerical primitives. Group C adds 20 descriptive, A-coercion, exclusive-percentile and ranking functions.

The authoritative registry remains `StandardFormulaFunctions.CreateAll()`. Complex parsing/formatting is centralized, legacy names preserve logical range arguments, and statistical collectors remain bounded at 2,000,000 values.

Workbook/editing, dependency graph, rules/Tables/AutoFilter, WPF/WinForms/MAUI hosts, XLSX preservation and print/PDF foundations remain in the validation matrix. Production blockers still include catalog breadth, charts/pivots UI, packaging/API compatibility, plugin trust/isolation, security/fuzzing, recovery, localization/accessibility and broad differential/visual corpora.

Manifest: `docs/formula-manifests/F016_60_FUNCTION_MANIFEST.md`.
