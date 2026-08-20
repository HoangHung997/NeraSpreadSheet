# NeraSpreadSheet feature matrix

This is Nera's own capability map. External spreadsheet products are comparison targets only.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size logical space, sparse cells, bulk mutation, multiple worksheets and immutable snapshots | names, table ownership and richer axis metadata |
| Structural editing | Atomic insert/delete/reorder for cells, formulas, dimensions, styles, merges, selection and split state | group/outline mapping and structured references |
| Selection | Cell, range, row, column, full-sheet and multi-range selection across desktop hosts | accessibility semantics and mobile selection handles |
| Undo / Redo | Data history plus isolated per-sheet split-view history; style, merge, clipboard and structural operations | cross-feature transaction grouping and recovery journal |
| Formula parser | Arithmetic, comparison, concatenation, A1 references/ranges, basic cross-sheet references and error literals | structured references, arrays and richer syntax |
| Formula functions | `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT`, `IF` | math, text, date/time, lookup, statistical, financial and plugin SDK |
| Formula dependencies | Direct/transitive graph, circular policy and affected-only recalculation | spatial dependency index, scheduling and dynamic-array propagation |
| Shared formulas | Two-pass safe import; deterministic rectangular export; mixed/absolute translation; cached-value, structural, fallback and preservation gates | external compatibility corpus and dynamic/shared array interactions |
| Pixel scrolling | Fractional `double` offsets, precision input, wheel animation and pane-local scrolling | target-hardware 60/120 Hz latency and power tuning |
| Layout / viewport | Sparse metrics, freeze/split panes, tile cache, hit testing and split-aware dirty projection | group/outline layout and print/page layout |
| Desktop rendering | WPF DrawingContext/D3DImage; WinForms GDI+/Direct2D/D3D11-DXGI | conditional-format overlays, accessibility and production command chrome |
| GPU recovery | HWND, DXGI and WPF device-stack recreation stress with diagnostics | physical driver-removal and target-hardware soak tests |
| Skia / MAUI | Production display-list executor, one `SKGLView`, pan/pinch/tap/wheel, context recreation and logical/raw scale gates | IME/mobile editor lifecycle and physical-device suspend/resume |
| XLSX values/layout | Values, cached formulas, sheets, dimensions, merges and pane state | compatibility corpus and full format-code semantics |
| XLSX styles | Direct styles plus sparse row/column chronological state, schema and malformed-input gates | themes, named styles and differential styles |
| XLSX preservation | Copy-and-patch unknown graph preservation, nested drawing/image/custom XML, URI/relationship preflight and repeated save | topology-changing merge and streaming envelope |
| Conditional formatting | Not yet first-class | Core rule model, differential styles, renderer and XLSX round-trip |
| Data validation | Not yet first-class | list/custom rules, validation UX and XLSX round-trip |
| Tables / filters | Basic bounded sort only | tables, structured references, AutoFilter and advanced sort |
| Printing / PDF | Not implemented | print areas, setup, breaks, preview and PDF export |
| Drawings / charts | Opaque package preservation only | first-class model, editor and rendering |
| Commands | Native registry/state/dispatcher and spreadsheet command catalogs | production Ribbon/Bars presenters and responsive command surface |
| Ribbon / Bars / DataGrid | Platform-neutral schemas and DataGrid contracts | WPF/WinForms/MAUI controls, designer and standalone DataGrid |
| Product hardening | Architecture gates, multi-platform CI and selected performance benchmarks | NuGet, Source Link, API compatibility, fuzzing, budgets and release gates |

## Current weighted progress

- Engine/viewport/renderer foundation: approximately `85%`.
- Basic spreadsheet MVP: approximately `68–72%`.
- Complete professional roadmap: approximately `45%`.
- Production release readiness: approximately `21–25%`.
