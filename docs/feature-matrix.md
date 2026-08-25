# NeraSpreadSheet feature matrix

Excel, LibreOffice và DevExpress là behavior/coverage references, không phải runtime dependencies.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, merges, dimensions, snapshots và atomic transforms | hide/group/outline metadata và complete axis properties |
| Selection / editing | Multi-range selection, editor, spill-aware clipboard, commands và Undo/Redo | mobile IME và richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches và exact history | named/theme styles và complete format semantics |
| Formula syntax/dependencies | Parser, AST, missing args, reference unions, shared/structured formulas, graph và affected recalculation | intersection, A1#, @, LET/LAMBDA và broader reference syntax |
| Function SDK | API 1.0 identity/version/capabilities/state/dependency/conflict; one registry path | package discovery, publisher trust và isolation |
| Formula surface | 239 eager/versioned + 20 AST/reference-aware + 7 dynamic = **266 names** | F009 reference introspection/projection |
| Reference selection | ADDRESS, AREAS và lazy CHOOSE với selected-range identity | COLUMN/COLUMNS/FORMULATEXT và full reference algebra |
| Conditional aggregates | COUNTIF(S), SUMIF(S), AVERAGEIF(S), criteria parser và positional dependencies | locale criteria compatibility |
| Statistics | Descriptive/order, covariance/regression và advanced distributions | hypothesis tests và confidence intervals |
| Finance | **56 functions** qua F006 | broader differential/fuzz corpus |
| Financial calendar | Basis 0–4, YEARFRAC, regular/odd coupon dates/ratios | broader convention corpus |
| Date/business calendar | DATEDIF/DAYS360/week numbers, NETWORKDAYS/WORKDAY families | remaining date/time catalog và richer holiday providers |
| Locale number | NUMBERVALUE với explicit/context separators và percent suffixes | broader locale corpus và full text conversion family |
| Engineering | 19 bit/shift/radix/comparison functions | complex numbers, CONVERT và special functions |
| Database | 12 criteria-table aggregates với dependencies và budgets | expression criteria và indexing |
| Dynamic arrays | Immutable spills; SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE/CHOOSECOLS/CHOOSEROWS; CHOOSE spill bridge | DROP/EXPAND, A1#, @, stack/wrap/take và LET/LAMBDA |
| Rules / Tables / AutoFilter | CF, validation, stable Tables, compressed filters và paged presenters | complete managers, pivots và slicers |
| Rendering | Fractional scrolling và WPF/WinForms/MAUI GPU display lists | spill UX, hardware budgets và accessibility |
| Page setup/PDF | Deterministic pagination, preview, staged PDF và print adapters | remaining XLSX semantics, font/visual corpus và printers |
| Product hardening | Multi-platform CI, atomic exports, shared formula-count gate và validation runner | packaging, API compatibility, security/fuzzing và recovery |

## F008 validation

- Exact implementation head: `775a24dfa2fa9dc059896d5445179077b4ffe641`.
- Formula tests: **234/234**.
- Registry: **239** eager/versioned; broader subsystem **266** names.
- AST/reference-aware names: **20**.
- Dynamic-array names: **7**.
- Financial functions remain **56**.
- CI #880: Core/architecture, Windows/GPU, Android, iOS, Mac Catalyst và MAUI Windows matrix.
- Public milestone report remains gated by documentation/handoff exact-head hosted CI.

## Weighted progress

- Engine/viewport/renderer foundation: khoảng **92%**.
- Basic spreadsheet MVP: khoảng **97–98%**.
- Complete professional roadmap: khoảng **84–85%**.
- Production release readiness: khoảng **62–65%**.
