# NeraSpreadSheet feature matrix

Excel, LibreOffice and DevExpress are behavior/coverage references only.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, merges, dimensions, snapshots and atomic transforms | hide/group/outline metadata and names |
| Selection / editing | Multi-range selection, editor, spill-aware clipboard, commands and Undo/Redo | mobile IME and richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches and exact history | named/theme styles and complete format semantics |
| Formula syntax/dependencies | Parser, AST, shared/structured formulas, graph and affected recalculation | volatile scheduling, spill references and vectorized expressions |
| Function SDK | API 1.0 identity/version/aliases/capabilities/state/dependency/conflict; one registry path | package discovery, publisher trust and isolation |
| Formula surface | 203 eager/versioned + 18 special + 5 dynamic = 226 names | maturity securities, hypothesis tests and advanced lookup/arrays |
| Conditional aggregates | COUNTIF(S), SUMIF(S), AVERAGEIF(S), criteria parser and positional dependencies | locale-specific criteria compatibility |
| Statistics | Descriptive/order statistics, covariance/regression and 30 transformation/distribution functions | hypothesis tests, confidence intervals and additional distributions |
| Finance | 30 functions: roots, dated schedules, payments, depreciation, rate helpers and basis/coupon calendar | discount/maturity securities, then fixed-coupon PRICE/YIELD/DURATION |
| Financial calendar | Basis 0–4, YEARFRAC, PCD/NCD/day/count helpers, maturity anchor and EOM preservation | odd coupons, business-day conventions and external corpus |
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

- Implementation commit: `eeb74ad4ee596f7cb56343b8459f2311538c8243`.
- GitHub Actions: CI `#854`, run `32745296544`, success.
- Formula tests: `192/192`.
- Core, architecture, full Windows, desktop GPU, Android, iOS, Mac Catalyst and MAUI Windows loaded-runtime gates passed.

## Weighted progress

- Engine/viewport/renderer foundation: approximately **92%**.
- Basic spreadsheet MVP: approximately **96–98%**.
- Complete professional roadmap: approximately **78%**.
- Production release readiness: approximately **55–58%**.

These are engineering-weighted estimates, not checkbox counts.
