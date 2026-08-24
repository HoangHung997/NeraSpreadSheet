# NeraSpreadSheet feature matrix

Excel, LibreOffice and DevExpress are behavior/coverage references only.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, merges, dimensions, snapshots and atomic structure transforms | manual hide/group/outline metadata and names |
| Selection / editing | Multi-range selection, reusable editor, spill-aware clipboard, commands and Undo/Redo | mobile IME lifecycle and richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches and exact history | named/theme styles and complete format semantics |
| Formula syntax/dependencies | Parser, AST, shared/structured formulas, dependency graph and affected recalculation | volatile scheduling, spill references and vectorized expressions |
| Function SDK | API 1.0 identity/version/aliases/capabilities/state/dependency/conflict with one authoritative registry path | package discovery, publisher trust and isolation |
| Formula surface | 191 eager/versioned + 18 special + 5 dynamic = 214 names | scalar finance helpers, hypothesis tests, advanced lookup/arrays |
| Conditional aggregates | COUNTIF(S), SUMIF(S), AVERAGEIF(S), criteria parser and positional dependencies | locale-specific criteria compatibility |
| Statistics | Descriptive/order statistics, covariance/regression and 30 transformation/distribution functions | hypothesis tests, confidence intervals and additional distributions |
| Finance | 18 functions including bounded RATE/XIRR, dated XNPV/XIRR, cumulative loan schedules and DB/DDB/VDB depreciation | ISPMT, EFFECT/NOMINAL, RRI/PDURATION, AMOR and bond/day-count families |
| Engineering | 19 deterministic bit/shift/radix/comparison functions | complex numbers, CONVERT, Bessel/error functions |
| Database | 12 criteria-table aggregates with dependencies and budgets | formula-expression criteria, locale parsing and indexing |
| Dynamic arrays | Immutable spills and SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE | A1#, @, advanced helpers and LET/LAMBDA |
| Conditional formatting | CellIs/Expression, differential styles, history, renderer and XLSX | color scales, data bars, icon sets and manager UI |
| Data validation | Current rule types/operators, editor gate, diagnostics, rendering and XLSX | named/cross-sheet lists and native presenters |
| Tables / AutoFilter | Stable IDs, calculated/totals metadata, compressed filters and paged presenters | complete manager UI, rich markup and sort state |
| Rendering | Fractional scrolling and shared display lists across WPF/WinForms/MAUI GPU | spill UX and hardware performance/accessibility budgets |
| Page setup/PDF | Deterministic pagination, preview, staged PDF and native print adapters | remaining XLSX semantics, font/visual corpus and printers |
| XLSX | Cells, styles, panes, formulas, rules, Tables/filters, printing and unknown-part preservation | dynamic metadata, breaks, custom paper and external corpus |
| CSV/TSV | Streaming quotes/newlines, type policy, injection protection and staged output | encoding/delimiter detection and fuzzing |
| Data / analysis | Sort, validation, Tables, filters, arrays and database/statistical foundations | grouping, virtual data, pivots and slicers |
| Product hardening | Multi-platform CI, atomic export limits, validation runner and Codex acceptance plan | packaging, API compatibility, security/fuzzing and recovery |

## Latest validated implementation milestone

- Implementation commit: `ea61fe227919358539355b814d4c2baf5f05b538`.
- GitHub Actions: CI `#844`, run `32734262232`, success.
- 179 formula tests passed.
- Core, architecture, full Windows, desktop GPU, Android, iOS, Mac Catalyst and MAUI Windows loaded-runtime gates passed.

## Weighted progress

- Engine/viewport/renderer foundation: approximately **92%**.
- Basic spreadsheet MVP: approximately **96–98%**.
- Complete professional roadmap: approximately **76%**.
- Production release readiness: approximately **53–56%**.

These are engineering-weighted estimates, not checkbox counts.
