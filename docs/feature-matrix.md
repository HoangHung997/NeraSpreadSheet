# NeraSpreadSheet feature matrix

Excel, LibreOffice and DevExpress are behavior/coverage references only.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, merges, dimensions, snapshots and atomic transforms | hide/group/outline metadata and names |
| Selection / editing | Multi-range selection, editor, spill-aware clipboard, commands and Undo/Redo | mobile IME and richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches and exact history | named/theme styles and complete format semantics |
| Formula syntax/dependencies | Parser, AST, shared/structured formulas, graph and affected recalculation | volatile scheduling, spill references and vectorized expressions |
| Function SDK | API 1.0 identity/version/capabilities/state/dependency/conflict; one registry path | package discovery, publisher trust and isolation |
| Formula surface | 228 eager/versioned + 18 special + 5 dynamic = 251 names | F006 odd-last yield and date/week compatibility |
| Conditional aggregates | COUNTIF(S), SUMIF(S), AVERAGEIF(S), criteria parser and positional dependencies | locale criteria compatibility |
| Statistics | Descriptive/order, covariance/regression and 30 transformation/distribution functions | hypothesis tests and confidence intervals |
| Finance | 55 functions through F005 | `ODDLYIELD`, then business-day/holiday coverage |
| Financial calendar | Basis 0–4, YEARFRAC, coupon dates/days/count, EOM anchor and bounded quasi-coupon ratios | business-day conventions and differential corpus |
| Securities | Maturity, regular fixed-coupon, treasury-bill, French depreciation, odd-first price/yield and odd-last price | odd-last yield and broader odd-period corpus |
| Engineering | 19 bit/shift/radix/comparison functions | complex numbers, CONVERT and special functions |
| Database | 12 criteria-table aggregates with dependencies and budgets | expression criteria and indexing |
| Dynamic arrays | Immutable spills and SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE | A1#, @, advanced helpers and LET/LAMBDA |
| Rules / Tables / AutoFilter | CF, validation, stable Tables, compressed filters and paged presenters | complete managers and rich markup |
| Rendering | Fractional scrolling and shared WPF/WinForms/MAUI GPU display lists | spill UX and hardware/accessibility budgets |
| XLSX / data exchange | Cells, formulas, styles, panes, rules, Tables/filters, package preservation and streaming CSV/TSV | dynamic-array metadata, drawings/charts and locale corpus |
| Page setup/PDF | Deterministic pagination, preview, staged PDF and print adapters | remaining XLSX semantics, custom paper, font/visual corpus and printers |
| Product hardening | Multi-platform CI, atomic exports, shared formula-count gate and validation runner | packaging, API compatibility, security/fuzzing and recovery |

## F005 validation

- Implementation commit: `bbd4e7c70e7d8426ad79843373cc3aff744d9466`.
- Functions: `AMORLINC`, `AMORDEGRC`, `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE`.
- Build: zero warnings and zero errors.
- Formula tests: **219/219**.
- Registry: **228** eager/versioned names; complete subsystem **251** names.
- Financial functions: **55**.
- Architecture verification: passed.
- Hosted implementation matrix: CI #872.
- Public milestone report remains gated by documentation/handoff exact-head hosted CI.

## Weighted progress

- Engine/viewport/renderer foundation: approximately **92%**.
- Basic spreadsheet MVP: approximately **96–98%**.
- Complete professional roadmap: approximately **81–82%**.
- Production release readiness: approximately **59–62%**.
