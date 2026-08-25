# NeraSpreadSheet feature matrix

Excel, LibreOffice and DevExpress are behavior/coverage references only.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, merges, dimensions, snapshots and atomic transforms | hide/group/outline metadata and names |
| Selection / editing | Multi-range selection, editor, spill-aware clipboard, commands and Undo/Redo | mobile IME and richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches and exact history | named/theme styles and complete format semantics |
| Formula syntax/dependencies | Parser, AST, shared/structured formulas, graph and affected recalculation | volatile scheduling, spill references and vectorized expressions |
| Function SDK | API 1.0 identity/version/capabilities/state/dependency/conflict; one registry path | package discovery, publisher trust and isolation |
| Formula surface | 223 eager/versioned + 18 special + 5 dynamic = 246 names | F005 AMOR and odd-first/odd-last coupon functions |
| Conditional aggregates | COUNTIF(S), SUMIF(S), AVERAGEIF(S), criteria parser and positional dependencies | locale criteria compatibility |
| Statistics | Descriptive/order, covariance/regression and 30 transformation/distribution functions | hypothesis tests and confidence intervals |
| Finance | 50 functions through F004 | `AMORLINC`, `AMORDEGRC`, `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE` |
| Financial calendar | Basis 0–4, YEARFRAC, coupon dates/days/count and EOM anchor | odd coupons and business-day conventions |
| Securities | Maturity, regular fixed-coupon and treasury-bill equations; DOLLAR conversions | AMOR and odd-period engines |
| Engineering | 19 bit/shift/radix/comparison functions | complex numbers, CONVERT and special functions |
| Database | 12 criteria-table aggregates with dependencies and budgets | expression criteria and indexing |
| Dynamic arrays | Immutable spills and SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE | A1#, @, advanced helpers and LET/LAMBDA |
| Rules / Tables / AutoFilter | CF, validation, stable Tables, compressed filters and paged presenters | complete managers and rich markup |
| Rendering | Fractional scrolling and shared WPF/WinForms/MAUI GPU display lists | spill UX and hardware/accessibility budgets |
| Page setup/PDF | Deterministic pagination, preview, staged PDF and print adapters | remaining XLSX semantics, font/visual corpus and printers |
| Product hardening | Multi-platform CI, atomic exports, shared formula-count gate and validation runner | packaging, API compatibility, security/fuzzing and recovery |

## F003 validation

- Implementation commit: `aa276e0a560029a3a7af22d948a49f1cad7ec085`.
- Exact correction head: `48012398a3a020bfb12829bee46cfa88bc1c7fed`.
- Formula tests: **209/209**.
- CI #866: Core/architecture, Windows/GPU, Android, iOS, Mac Catalyst and MAUI Windows passed.

## F004 validation

- Implementation commit: `2d05b076cbf59912d52400440ecec422d398f625`.
- Calendar-boundary hardening head and current CI are recorded in `docs/worklog/CURRENT.md`.
- Formula tests: **214/214** at the implementation gate.
- Registry: **223** eager/versioned names; complete subsystem **246** names.
- Public milestone report remains gated by documentation/handoff exact-head hosted CI.

## Weighted progress

- Engine/viewport/renderer foundation: approximately **92%**.
- Basic spreadsheet MVP: approximately **96–98%**.
- Complete professional roadmap: approximately **80–81%**.
- Production release readiness: approximately **58–61%**.
