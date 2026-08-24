# NeraSpreadSheet feature matrix

Excel, LibreOffice and DevExpress are behavior/coverage references only.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, merges, dimensions, snapshots and atomic transforms | hide/group/outline metadata and names |
| Selection / editing | Multi-range selection, editor, spill-aware clipboard, commands and Undo/Redo | mobile IME and richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches and exact history | named/theme styles and complete format semantics |
| Formula syntax/dependencies | Parser, AST, shared/structured formulas, graph and affected recalculation | volatile scheduling, spill references and vectorized expressions |
| Function SDK | API 1.0 identity/version/aliases/capabilities/state/dependency/conflict; one registry path | package discovery, publisher trust and isolation |
| Formula surface | 196 eager/versioned + 18 special + 5 dynamic = 219 names | financial calendar, hypothesis tests and advanced lookup/arrays |
| Conditional aggregates | COUNTIF(S), SUMIF(S), AVERAGEIF(S), criteria parser and positional dependencies | locale-specific criteria compatibility |
| Statistics | Descriptive/order statistics, covariance/regression and 30 transformation/distribution functions | hypothesis tests, confidence intervals and additional distributions |
| Finance | 23 functions: roots, dated schedules, cumulative payments, depreciation and scalar rate/growth helpers | basis 0–4, YEARFRAC, coupon dates, bonds and yields |
| Engineering | 19 deterministic bit/shift/radix/comparison functions | complex numbers, CONVERT, Bessel/error functions |
| Database | 12 criteria-table aggregates with dependencies and budgets | expression criteria, locale parsing and indexing |
| Dynamic arrays | Immutable spills and SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE | A1#, @, advanced helpers and LET/LAMBDA |
| Rules / Tables / AutoFilter | CF, validation, stable Tables, compressed filters and paged presenters | full managers, rich markup and sort state |
| Rendering | Fractional scrolling and shared WPF/WinForms/MAUI GPU display lists | spill UX and hardware performance/accessibility budgets |
| Page setup/PDF | Deterministic pagination, preview, staged PDF and native print adapters | remaining XLSX semantics, font/visual corpus and printers |
| XLSX | Cells, styles, panes, formulas, rules, Tables/filters, printing and unknown-part preservation | dynamic metadata, breaks, custom paper and external corpus |
| CSV/TSV | Streaming quotes/newlines, type policy, injection protection and staged output | encoding/delimiter detection and fuzzing |
| Data / analysis | Sort, validation, Tables, filters, arrays and database/statistical foundations | grouping, virtual data, pivots and slicers |
| Product hardening | Multi-platform CI, atomic export limits and repository validation runner | packaging, API compatibility, security/fuzzing and recovery |

## Latest validated implementation milestone

- Implementation commit: `e2d3bb4b296292ae83dc4c1a5e35a442f6574e4f`.
- GitHub Actions: CI `#849`, run `32740594038`, success.
- Formula tests: `185/185`.
- Core, architecture, full Windows, desktop GPU, Android, iOS, Mac Catalyst and MAUI Windows loaded-runtime gates passed.

## Weighted progress

- Engine/viewport/renderer foundation: approximately **92%**.
- Basic spreadsheet MVP: approximately **96–98%**.
- Complete professional roadmap: approximately **77%**.
- Production release readiness: approximately **54–57%**.

These are engineering-weighted estimates, not checkbox counts.
