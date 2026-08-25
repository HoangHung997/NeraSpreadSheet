# NeraSpreadSheet feature matrix

Excel, LibreOffice and DevExpress are behavior/coverage references only.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, merges, dimensions, snapshots and atomic transforms | hide/group/outline metadata and names |
| Selection / editing | Multi-range selection, editor, spill-aware clipboard, commands and Undo/Redo | mobile IME and richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches and exact history | named/theme styles and complete format semantics |
| Formula syntax/dependencies | Parser, AST, shared/structured formulas, graph and affected recalculation | volatile scheduling, spill references and vectorized expressions |
| Function SDK | API 1.0 identity/version/aliases/capabilities/state/dependency/conflict; one registry path | package discovery, publisher trust and isolation |
| Formula surface | 208 eager/versioned + 18 special + 5 dynamic = 231 names | F002 maturity/rate functions, then fixed-coupon functions |
| Conditional aggregates | COUNTIF(S), SUMIF(S), AVERAGEIF(S), criteria parser and positional dependencies | locale-specific criteria compatibility |
| Statistics | Descriptive/order statistics, covariance/regression and 30 transformation/distribution functions | hypothesis tests, confidence intervals and additional distributions |
| Finance | 35 functions: roots, dated schedules, payments, depreciation, calendar and first five maturity securities | YIELDDISC/PRICEMAT/YIELDMAT/ACCRINT/FVSCHEDULE |
| Financial calendar | Basis 0–4, YEARFRAC, PCD/NCD/day/count helpers, maturity anchor and EOM preservation | odd coupons and business-day conventions |
| Maturity securities | ACCRINTM, DISC, INTRATE, RECEIVED, PRICEDISC with common equations and inverse test | maturity interest price/yield and full accrued interest |
| Engineering | 19 deterministic bit/shift/radix/comparison functions | complex numbers, CONVERT, Bessel/error functions |
| Database | 12 criteria-table aggregates with dependencies and budgets | expression criteria, locale parsing and indexing |
| Dynamic arrays | Immutable spills and SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE | A1#, @, advanced helpers and LET/LAMBDA |
| Rules / Tables / AutoFilter | CF, validation, stable Tables, compressed filters and paged presenters | full managers, rich markup and sort state |
| Rendering | Fractional scrolling and shared WPF/WinForms/MAUI GPU display lists | spill UX and hardware performance/accessibility budgets |
| Page setup/PDF | Deterministic pagination, preview, staged PDF and native print adapters | remaining XLSX semantics, font/visual corpus and printers |
| XLSX | Cells, styles, panes, formulas, rules, Tables/filters, printing and unknown-part preservation | dynamic metadata, breaks, custom paper and external corpus |
| Product hardening | Multi-platform CI, atomic export limits and repository validation runner | packaging, API compatibility, security/fuzzing and recovery |

## F001 validation

- Implementation commit: `3ea2ae2d576e40b72e91c02ab493f1e244ffe0bd`.
- Core build, architecture and **198/198 formula tests** passed.
- Windows/Android jobs passed or were progressing normally in CI #859; the Apple runner failed before checkout because DNS could not resolve `github.com`.
- A fresh exact-head full matrix is required before the public milestone is marked complete.

## Weighted progress

- Engine/viewport/renderer foundation: approximately **92%**.
- Basic spreadsheet MVP: approximately **96–98%**.
- Complete professional roadmap: approximately **79%**.
- Production release readiness: approximately **56–59%**.

These are engineering-weighted estimates, not checkbox counts.
