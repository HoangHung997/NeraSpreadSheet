# NeraSpreadSheet feature matrix

Excel, LibreOffice and DevExpress are behavior/coverage references only.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, merges, dimensions, snapshots and atomic transforms | hide/group/outline metadata and names |
| Selection / editing | Multi-range selection, editor, spill-aware clipboard, commands and Undo/Redo | mobile IME and richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches and exact history | named/theme styles and complete format semantics |
| Formula syntax/dependencies | Parser, AST, shared/structured formulas, graph and affected recalculation | volatile scheduling, spill references and vectorized expressions |
| Function SDK | API 1.0 identity/version/capabilities/state/dependency/conflict; one registry path | package discovery, publisher trust and isolation |
| Formula surface | 213 eager/versioned + 18 special + 5 dynamic = 236 names | F003 fixed-coupon price/yield/duration + MIRR |
| Conditional aggregates | COUNTIF(S), SUMIF(S), AVERAGEIF(S), criteria parser and positional dependencies | locale criteria compatibility |
| Statistics | Descriptive/order, covariance/regression and 30 transformation/distribution functions | hypothesis tests and confidence intervals |
| Finance | 40 functions through F002 | PRICE/YIELD/DURATION/MDURATION/MIRR |
| Financial calendar | Basis 0–4, YEARFRAC, coupon dates/days/count and EOM anchor | odd coupons and business-day conventions |
| Maturity securities | Discount, maturity-interest price/yield, periodic accrual and variable-rate FV | fixed-coupon cash-flow engine |
| Engineering | 19 bit/shift/radix/comparison functions | complex numbers, CONVERT and special functions |
| Database | 12 criteria-table aggregates with dependencies and budgets | expression criteria and indexing |
| Dynamic arrays | Immutable spills and SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE | A1#, @, advanced helpers and LET/LAMBDA |
| Rules / Tables / AutoFilter | CF, validation, stable Tables, compressed filters and paged presenters | complete managers and rich markup |
| Rendering | Fractional scrolling and shared WPF/WinForms/MAUI GPU display lists | spill UX and hardware/accessibility budgets |
| Page setup/PDF | Deterministic pagination, preview, staged PDF and print adapters | remaining XLSX semantics, font/visual corpus and printers |
| Product hardening | Multi-platform CI, atomic exports, shared formula-count gate and validation runner | packaging, API compatibility, security/fuzzing and recovery |

## F002 validation

- Implementation commit: `70051299a1531016ce82df981a49753f09d1d8a6`.
- Build succeeded with zero warnings/errors.
- Formula tests: **204/204**.
- Architecture verification passed.
- Exact documentation/handoff hosted matrix is required before the public milestone report.

## Weighted progress

- Engine/viewport/renderer foundation: approximately **92%**.
- Basic spreadsheet MVP: approximately **96–98%**.
- Complete professional roadmap: approximately **80%**.
- Production release readiness: approximately **57–60%**.
