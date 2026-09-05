# Q003A — Analytics foundation and shared vector rendering

Q003A establishes the host-neutral chart/pivot foundation and shared display-list rendering layer. It does not claim that the complete charts/pivots UI blocker is resolved.

## Locked capabilities

1. Chart model and projection
   - Column, Bar, Line and Pie definitions.
   - Source-range projection with headers, categories and numeric values.
   - Deterministic fallback labels and explicit empty-state behavior.
2. Pivot model and projection
   - Sum, Count, Average, Minimum and Maximum aggregation.
   - First-seen grouping order, blank grouping, headers and source row/numeric counts.
3. Editing integration
   - Per-worksheet chart/pivot collections.
   - Insert/remove/project operations participate in SpreadsheetSession Undo/Redo.
   - Divergent edits invalidate redo history.
   - Commands: Insert.Chart.Column, Insert.Chart.Bar, Insert.Chart.Line, Insert.Chart.Pie and Insert.Pivot.Sum.
4. Shared rendering
   - Column, Bar, Line and Pie charts plus pivot summaries are composed as a platform-neutral DisplayList.
   - Pie sectors use a shared FillPolygon primitive rather than host-specific approximations.
   - FillPolygon is implemented by WPF StreamGeometry, WinForms/GDI+ FillPolygon, Skia paths and Direct2D PathGeometry.
5. Runtime rendering gates
   - Skia raster pixel test verifies polygon interior/exterior behavior.
   - Windows Direct2D runtime smoke exercises polygon rendering on both HWND and SwapChain renderers through resize stress.
   - Direct2D geometry uses the same ID2D1Factory1 that owns each render target/device context; this prevents D2DERR_WRONG_FACTORY.

## Exact-head CI evidence

- Exact HEAD: `2206f8f6081580ebae1907b8888ec0adce331e73`.
- GitHub CI: #957 — success.
- Build/analyzers: 0 warnings, 0 errors.
- Full Core solution: 1104/1104 passed.
- Core tests: 108/108.
- Editing tests: 196/196.
- Spreadsheet rendering tests: 109/109.
- Skia rendering tests: 14/14.
- Formula tests: 518/518.
- OpenXML tests: 56/56.
- Architecture verification: passed.
- Windows build/tests + desktop Direct2D GPU runtime smoke: passed.
- MAUI Android: passed.
- MAUI iOS + Mac Catalyst: passed.
- MAUI Windows build + handler-resolution + loaded Table filter/runtime/scale smokes: passed.

## Defects exposed while hardening

- Analyzer regressions in shared rendering were fixed without suppressing analyzers.
- Obsolete Skia path mutation APIs were replaced with SKPathBuilder.
- WinForms polygon materialization was corrected to avoid ref-parameter capture.
- Direct2D runtime smoke exposed D2DERR_WRONG_FACTORY when geometry came from a different factory than the render target. The executor now borrows the owning renderer/surface factory.
- Pie legend percentage formatting was made deterministic across cultures.

## Next — Q003B

Build the floating analytics placement and interaction layer without breaking the canvas/display-list architecture or pixel scrolling:

- host-neutral placement state and viewport mapping;
- floating chart/pivot overlays shared by WPF, WinForms and MAUI;
- selection, move and resize interactions;
- keyboard, touch and accessibility behavior;
- viewport/scroll/freeze/split integration;
- deterministic interaction and host smoke tests.

Workbook/OpenXML persistence for chart/drawing/pivot state remains a separate follow-on concern after the interaction layer is stable.
