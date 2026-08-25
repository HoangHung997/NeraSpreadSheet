# NeraSpreadSheet current implementation status

This file is the source of truth for the current development branch. A capability is implemented only when executable source, automated tests and the applicable build/runtime gate exist.

## Product rules

- Independent spreadsheet SDK; no Excel, LibreOffice or DevExpress runtime dependency.
- Formula, dynamic-array, editing, layout, scrolling and printing semantics remain platform-neutral.
- Extension functions pass API, capability, state and resource validation before registration.
- Numerical solvers, schedule loops and special-function primitives are deterministic, bounded and fail closed.
- Built-ins use one authoritative aggregation path.
- Financial date/basis semantics live in `FinancialDateMath`; regular and odd-period securities reuse that service.

## Whole-project implementation snapshot

### Core workbook and editing

- Excel-size sparse worksheets, values/formulas/styles/dimensions/merges, immutable snapshots and bounded caches.
- Multi-range selection, spill-aware clipboard, cell editor, commands, sort and Undo/Redo.
- Atomic structural operations with formula/rule/Table/filter/spill mapping.
- Conditional Formatting, Data Validation, stable Tables, AutoFilter, totals and paged native presenters.

### Formula engine and SDK

- Parser/AST, A1/cross-sheet references, dependency graph, circular detection and affected-only recalculation.
- Shared/structured formulas and Table formula rewrite/projection.
- Function Extension SDK v1.0 with identity/version/API/capability/state/dependency/conflict contracts.
- Built-in eager/versioned registry: **228 names**.
- AST/reference-aware built-ins: **18 names**.
- Dynamic-array built-ins: **5 names**.
- Complete built-in subsystem: **251 names**.
- Formula test registry count uses one shared test constant rather than repeated literals.
- Current formula suite: **219/219 passing tests**.

### Formula families

- Logical, aggregate, math, text/Unicode, date/time and lookup/reference foundations.
- Conditional aggregates, descriptive/order statistics, covariance/regression and advanced distributions.
- Fifty-five financial functions through F005.
- Nineteen engineering functions and twelve database aggregate functions.
- Dynamic arrays: `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE`.

### Financial Functions Foundation

F005 adds:

- `AMORLINC` — French-accounting linear depreciation with prorated first period, full periods and bounded final residual;
- `AMORDEGRC` — accelerated French depreciation with useful-life coefficient, whole-unit rounding and a 100,000-period traversal cap;
- `ODDFPRICE` — odd-first clean price over bounded quasi-coupon ratios;
- `ODDFYIELD` — bounded inverse of the exact `ODDFPRICE` equation;
- `ODDLPRICE` — odd-last clean price using the next theoretical coupon boundary.

Key F005 contracts:

- AMOR dates are normalized to whole dates; period and basis are truncated toward zero.
- AMOR supports basis `0`, `1`, `3`, `4`; invalid useful-life intervals and nonpositive financial domains return `#NUM!`.
- `ODDFPRICE` and `ODDFYIELD` share one strict `issue < settlement < first_coupon < maturity` state.
- The regular tail after `first_coupon` must align to the coupon frequency; quasi-coupon traversal is capped at 100,000 periods.
- `ODDFYIELD` uses log-domain bisection capped at 256 iterations rather than an unbounded Newton loop.
- `ODDLPRICE` requires `last_coupon < settlement < maturity` and derives period ratios from a theoretical coupon boundary on or after maturity.
- All F005 functions are scalar-only, deterministic/pure and logical-argument-counted.
- Unsupported argument kinds/coercion return `#VALUE!`; invalid domains, non-finite results or exhausted budgets return `#NUM!`.

Earlier annuity/root, cash-flow, payment, depreciation, scalar-rate, calendar and F001–F004 contracts remain unchanged.

Full contract: `docs/financial-functions-foundation-contract.md`.

### Rendering, hosts and scrolling

- Fractional pixel scrolling, freeze/split panes and a shared display-list pipeline.
- WPF Direct2D, WinForms/GDI+ and .NET MAUI Skia GPU hosts.
- Hosted validation for Windows desktop rendering, Android, iOS, Mac Catalyst and MAUI Windows loaded contexts.

### XLSX, data exchange, printing and PDF

- XLSX cells, formulas, styles, panes, current rules, Tables/filters and print settings.
- Unknown package-part preservation.
- Streaming CSV/TSV.
- Deterministic pagination, print preview, staged Skia PDF export and desktop print adapters.

## Conservative limitations

- Formula surface is not complete Excel/OpenFormula compatibility.
- `ODDLYIELD`, business-day/holiday conventions and broader financial differential/fuzz corpora remain pending.
- Advanced lookup/reference projection, LET/LAMBDA, higher-order arrays and complete spill syntax/UX remain pending.
- Drawings/charts, pivots/slicers, complete theme/style semantics and independent visual corpus remain pending.
- Plugin discovery/trust/isolation, packaging/API compatibility, localization/accessibility, security/fuzzing, recovery and release hardening remain pending.
- Current project is an engineering-complete MVP foundation, not a production release.

## Weighted progress estimate

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `96–98%`.
- Complete professional roadmap: approximately `81–82%`.
- Production release readiness: approximately `59–62%`.

These are engineering-weighted estimates, not checkbox counts.

## Next implementation work

1. F006: `ODDLYIELD`, `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM`.
2. Continue automatically through `docs/formula-completion-master-schedule.md` in exact five-function milestones.
3. Keep PR #1 Draft until formula catalog, differential/fuzz, provider-isolation and release gates are complete.

## Validation state

F005 implementation head `bbd4e7c70e7d8426ad79843373cc3aff744d9466` built with zero warnings/errors, passed architecture verification and **219/219 formula tests** in CI #872. The public F005 milestone requires the documentation/handoff exact-head hosted matrix to remain fully green. PR #1 remains Draft and unmerged.
