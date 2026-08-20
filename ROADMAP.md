# NeraSpreadSheet roadmap

`docs/current-status.md` là nguồn sự thật về triển khai. Một mục chỉ được đánh dấu hoàn thành khi có source chạy được, automated tests và runtime gate phù hợp.

## A. Independent spreadsheet engine

- [x] Sparse workbook/worksheet trên kích thước logic cỡ Excel.
- [x] Values, formulas, merges, dimensions và immutable snapshots.
- [x] Selection, clipboard, reusable editor, commands và data undo/redo.
- [x] Structural insert/delete có formula mapping và rollback nguyên tử.
- [x] Model-safe row/column reorder theo logical cell identity.
- [x] Sparse whole-row/column/sheet styles và effective composition.
- [x] Split-view changes có history/undo/redo riêng, tách khỏi data history.
- [x] Conditional-formatting Core model, differential styles, structural history và conservative reorder proof.
- [ ] Sparse hide/group/outline metadata và complete axis property model.

## B. Viewport và rendering

- [x] Continuous pixel scrolling bằng `double` offsets.
- [x] Freeze panes, split panes và independent pane scrolling.
- [x] Integrated/optional pane-local scrollbars.
- [x] Shared headers, resize, selection, editor và drag reorder.
- [x] Drag-edge auto-scroll cho split và unsplit hosts.
- [x] Snapshot/tile caching và split-aware dirty-region projection.
- [x] WPF DrawingContext và D3DImage shared-texture backend.
- [x] WinForms GDI+, Direct2D/DirectWrite HWND và D3D11/DXGI backend.
- [x] Production Skia display-list renderer cùng MAUI native GPU host.
- [x] Conditional formatting tham gia shared display-list composer và conservative dependent-range invalidation.
- [x] Injected device/front-buffer/context recreation stress gates.
- [ ] 60/120 Hz, 4K target-hardware latency, FPS, memory và power baselines.

## C. Formula engine

- [x] Tokenizer, parser, AST, dependency graph và circular-reference policy.
- [x] Arithmetic, comparison, concatenation, A1 references/ranges và basic cross-sheet references.
- [x] `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT` và `IF`.
- [x] Shared-formula import và mixed/absolute A1 translation không materialize range.
- [x] Shared-formula export grouping, stable worksheet-order indexes, bidirectional proof và fallback.
- [x] Shared Core A1 translator và structural-reference rewriter dùng cho cell formulas và conditional rules.
- [ ] Dynamic arrays, spill ranges và array calculation contracts.
- [ ] Complete math, text, date/time, lookup, statistical và financial function surface.
- [ ] Tables/structured references và formula rewrite integration.
- [ ] Plugin function SDK cho nghiệp vụ dự toán.

## D. XLSX, printing và interoperability

- [x] Values, cached formulas, multiple sheets, dimensions và merged ranges.
- [x] Standard pane metadata cùng Nera custom state cho bốn pane offsets.
- [x] Current Nera style table và direct-cell style round-trip.
- [x] Sparse row/column style round-trip không flatten logical axis.
- [x] Unknown package-part preservation theo copy-and-patch.
- [x] Nested opaque graph, drawing/image, custom XML/properties và package-root relationship gates.
- [x] Package graph preflight cho URI, relationship ID/type/target và size/count limits.
- [x] Shared-formula import/export, schema, structural/fallback và repeated-save gates.
- [x] Conditional-formatting `dxfs/dxf`, `conditionalFormatting`, `cfRule`, `formula`, priority và `stopIfTrue` round-trip.
- [x] Conditional-formatting malformed-input, multiple-`sqref`, schema và opaque repeated-save gates.
- [ ] External compatibility corpus và differential tests với nhiều trình tạo XLSX thực tế.
- [ ] Data validation và tables.
- [ ] First-class drawings, images và charts model/editor.
- [ ] Print areas, page setup, page breaks, preview và PDF export.

## E. Data và analysis

- [x] Basic in-memory sort có safety limits.
- [ ] Data-validation model, editor commit gate và invalid-cell diagnostics.
- [ ] AutoFilter model và desktop filter UI.
- [ ] Advanced/multi-key sort, custom lists và stable large-data path.
- [ ] Tables, subtotals, grouping và outlines.
- [ ] Pivot tables, slicers và calculated fields.
- [ ] External/virtualized data sources và incremental loading.

## F. Cross-platform control suite

- [x] Platform-neutral command, Ribbon Core, Bars Core và DataGrid Core contracts.
- [x] Public WPF và WinForms spreadsheet hosts/samples.
- [x] MAUI native handler, production touch state machine và pinch zoom.
- [x] Loaded Windows context recreation và logical/raw pixel gates.
- [ ] MAUI virtual keyboard, IME và mobile editor lifecycle.
- [ ] Responsive command surface cho mobile/tablet.
- [ ] Production Ribbon, toolbar, menu và context-menu controls.
- [ ] Standalone DataGrid control dùng chung hạ tầng nhưng không dùng workbook semantics.
- [ ] Theme, localization, accessibility và designer support.

## G. Product hardening

- [ ] API compatibility và package-version checks.
- [ ] NuGet packaging, symbols và source link.
- [ ] Crash recovery, safe-mode startup và support bundle.
- [ ] Security review và fuzzing cho formulas/XLSX/clipboard.
- [ ] Performance budgets được thực thi trong CI.
- [ ] Alpha → Beta → RC → Production release gates.

## Immediate execution order

1. Data validation Core model, list/custom rules, editor gate và XLSX round-trip.
2. Tables, structured references và AutoFilter integration.
3. Formula/function surface, dynamic arrays và plugin function SDK.
4. Advanced sort, grouping, virtualized data và subtotals.
5. Printing/PDF, drawings/charts và pivot/slicers.
6. Accessibility, packaging, fuzzing, performance budgets và release hardening.

## Trạng thái ước tính sau conditional formatting

- Nền móng engine/viewport/renderer: khoảng `86%`.
- MVP bảng tính cơ bản: khoảng `70–74%`.
- Toàn bộ roadmap chuyên nghiệp: khoảng `46%`.
- Production release readiness: khoảng `22–26%`.

Conditional formatting end-to-end nâng tổng thể khoảng `1` điểm phần trăm so với mốc shared-formula. Chưa chấm cao hơn vì data validation, tables/structured references, dynamic arrays, hệ thống hàm lớn, printing/PDF, charts và pivot vẫn còn.

Các tỷ lệ trên là ước lượng theo độ khó và khối lượng còn lại, không phải tỷ lệ số checkbox.
