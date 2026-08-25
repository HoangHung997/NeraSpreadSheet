# NeraSpreadSheet feature matrix

Excel, LibreOffice và DevExpress chỉ là behavior/coverage references; không phải runtime dependencies.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, merges, dimensions, snapshots và atomic transforms | hide/group/outline metadata và names |
| Selection / editing | Multi-range selection, editor, spill-aware clipboard, commands và Undo/Redo | mobile IME và richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches và exact history | named/theme styles và complete format semantics |
| Formula syntax/dependencies | Parser, AST, shared/structured formulas, graph và affected recalculation | volatile scheduling, spill references và vectorized expressions |
| Function SDK | API 1.0 identity/version/capabilities/state/dependency/conflict; một registry path | package discovery, publisher trust và isolation |
| Formula surface | 233 eager/versioned + 18 special + 5 dynamic = **256 names** | F007 business-day và locale-number functions |
| Date compatibility | `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM` | business-day calendars và locale parsing |
| Conditional aggregates | COUNTIF(S), SUMIF(S), AVERAGEIF(S), criteria parser và positional dependencies | locale criteria compatibility |
| Statistics | Descriptive/order, covariance/regression và transformation/distribution functions | hypothesis tests và confidence intervals |
| Finance | **56 functions** qua F006 | business-day/holiday integration và broader differential corpus |
| Financial calendar | Basis 0–4, YEARFRAC, coupon dates/days/count, EOM anchor và bounded quasi-coupon ratios | holiday/business-day conventions |
| Securities | Maturity, regular coupon, treasury bill, AMOR và odd-first/odd-last price/yield equations | broader compatibility/fuzz corpus |
| Engineering | 19 bit/shift/radix/comparison functions | complex numbers, CONVERT và special functions |
| Database | 12 criteria-table aggregates với dependencies và budgets | expression criteria và indexing |
| Dynamic arrays | Immutable spills và SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE | A1#, @, advanced helpers và LET/LAMBDA |
| Rules / Tables / AutoFilter | CF, validation, stable Tables, compressed filters và paged presenters | complete managers và rich markup |
| Rendering | Fractional scrolling và shared WPF/WinForms/MAUI GPU display lists | spill UX và hardware/accessibility budgets |
| Page setup/PDF | Deterministic pagination, preview, staged PDF và print adapters | remaining XLSX semantics, font/visual corpus và printers |
| Product hardening | Multi-platform CI, atomic exports, shared formula-count gate và validation runner | packaging, API compatibility, security/fuzzing và recovery |

## F005 validation

- Implementation commit: `bbd4e7c70e7d8426ad79843373cc3aff744d9466`.
- Formula tests: **219/219**.
- Registry: **228** eager/versioned; complete subsystem **251**.
- CI #872: Core/architecture, Windows/GPU, Android, iOS, Mac Catalyst và MAUI Windows passed.

## F006 validation

- Implementation commit: `c43bf362054110940f149a144546c4bba13387e3`.
- Functions: `ODDLYIELD`, `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM`.
- Formula tests: **224/224**.
- Registry: **233** eager/versioned; complete subsystem **256**.
- Build/analyzers: zero warnings và zero errors.
- Core/architecture, Windows/GPU, Android, iOS và Mac Catalyst gates passed at implementation validation; public milestone remains gated by the complete exact-head hosted matrix.

## Weighted progress

- Engine/viewport/renderer foundation: khoảng **92%**.
- Basic spreadsheet MVP: khoảng **96–98%**.
- Complete professional roadmap: khoảng **82–83%**.
- Production release readiness: khoảng **60–63%**.
