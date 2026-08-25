# NeraSpreadSheet feature matrix

Excel, LibreOffice và DevExpress là behavior/coverage references, không phải runtime dependencies.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, merges, dimensions, snapshots và atomic transforms | hide/group/outline metadata và complete axis properties |
| Selection / editing | Multi-range selection, editor, spill-aware clipboard, commands và Undo/Redo | mobile IME và richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches và exact history | named/theme styles và complete format semantics |
| Formula syntax/dependencies | Parser, AST, shared/structured formulas, graph và affected recalculation | advanced references, LET/LAMBDA và spill syntax |
| Function SDK | API 1.0 identity/version/capabilities/state/dependency/conflict; one registry path | package discovery, publisher trust và isolation |
| Formula surface | 238 eager/versioned + 18 special + 5 dynamic = **261 names** | F008 lookup/reference/array selection |
| Conditional aggregates | COUNTIF(S), SUMIF(S), AVERAGEIF(S), criteria parser và positional dependencies | locale criteria compatibility |
| Statistics | Descriptive/order, covariance/regression và advanced distributions | hypothesis tests và confidence intervals |
| Finance | **56 functions** qua F006 | broader differential/fuzz corpus |
| Financial calendar | Basis 0–4, YEARFRAC, regular/odd coupon dates/ratios | broader convention corpus |
| Date compatibility | DATEDIF, DAYS360, ISOWEEKNUM, WEEKNUM | remaining date/time catalog |
| Business calendar | NETWORKDAYS(.INTL), WORKDAY(.INTL), weekend codes/masks, holiday range dependencies | richer holiday/provider conventions |
| Locale number | NUMBERVALUE với explicit/context separators và percent suffixes | broader locale corpus và full text conversion family |
| Engineering | 19 bit/shift/radix/comparison functions | complex numbers, CONVERT và special functions |
| Database | 12 criteria-table aggregates với dependencies và budgets | expression criteria và indexing |
| Dynamic arrays | Immutable spills và SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE | A1#, @, projection helpers và LET/LAMBDA |
| Rules / Tables / AutoFilter | CF, validation, stable Tables, compressed filters và paged presenters | complete managers, pivots và slicers |
| Rendering | Fractional scrolling và WPF/WinForms/MAUI GPU display lists | spill UX, hardware budgets và accessibility |
| Page setup/PDF | Deterministic pagination, preview, staged PDF và print adapters | remaining XLSX semantics, font/visual corpus và printers |
| Product hardening | Multi-platform CI, atomic exports, shared formula-count gate và validation runner | packaging, API compatibility, security/fuzzing và recovery |

## F007 validation

- Initial implementation commit: `acc65b46b4aa1d729baacb7768960a1dbecc66e5`.
- Separator/weekend hardening: `7bc9d78c24f172c7c014d510d65e73be9ac4fd0c`.
- Exact analyzer-clean implementation head: `95748373b9dde1f0faffe2c61d2ad1262cff7532`.
- Formula tests: **229/229**.
- Registry: **238** eager/versioned names; complete subsystem **261** names.
- Financial functions remain **56**.
- CI #878: Core/architecture, Windows/GPU, Android, iOS, Mac Catalyst và MAUI Windows matrix.
- Public milestone report remains gated by documentation/handoff exact-head hosted CI.

## Weighted progress

- Engine/viewport/renderer foundation: khoảng **92%**.
- Basic spreadsheet MVP: khoảng **97–98%**.
- Complete professional roadmap: khoảng **83–84%**.
- Production release readiness: khoảng **61–64%**.
