# NeraSpreadSheet feature matrix

Excel, LibreOffice và DevExpress là behavior/coverage references, không phải runtime dependencies.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook | Excel-size sparse sheets, merges, dimensions, snapshots, atomic transforms | hide/group/outline và complete axis properties |
| Editing | Multi-range selection, editor, spill-aware clipboard, commands, Undo/Redo | mobile IME và richer command surfaces |
| Formula syntax/dependencies | Parser/AST, missing args, reference unions, shared/structured formulas, graph | intersection, `A1#`, `@`, LET/LAMBDA |
| Formula surface | 239 eager + 23 AST/reference-aware + 9 dynamic = **271** | F010 lookup/group/stack/indirect |
| Reference introspection | ADDRESS/AREAS/CHOOSE/COLUMN/COLUMNS/FORMULATEXT | ROW/ROWS/SHEET/SHEETS và full reference algebra |
| Dynamic arrays | SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE/CHOOSECOLS/CHOOSEROWS/DROP/EXPAND | HSTACK, TAKE, TOCOL/TOROW, VSTACK, wrap families |
| Finance | **56 functions** | broader differential/fuzz corpus |
| Engineering | **19 functions** | complex numbers, CONVERT và special functions |
| Database | **12 aggregates** | expression criteria và indexing |
| Data/rules | CF, validation, Tables, AutoFilter, totals, paged presenters | pivots/slicers và complete managers |
| Rendering | Fractional scrolling và WPF/WinForms/MAUI GPU display lists | spill UX, hardware budgets, accessibility |
| XLSX/printing/PDF | Preservation, CSV/TSV, pagination, preview, PDF, print adapters | charts/drawings, full metadata và visual corpus |
| Hardening | Multi-platform CI, architecture gates, bounded resources | packaging, isolation, security/fuzzing, recovery |

**Tổng số hàm: 271 / tối thiểu 538 hàm mục tiêu hiện đã khóa.**

## F009 validation

- Implementation head: `bb332e65291776fea05e52ce8433db9e6b1ac810`.
- Build: zero warnings/errors.
- Formula tests: **239/239**.
- CI #882: Core/architecture, Windows/GPU, Android, iOS, Mac Catalyst và MAUI Windows matrix.
