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
- [ ] Sparse hide/group/outline metadata và complete axis property model.

## B. Viewport và desktop rendering

- [x] Continuous pixel scrolling bằng `double` offsets.
- [x] Freeze panes, split panes và independent pane scrolling.
- [x] Integrated/optional pane-local scrollbars.
- [x] Shared headers, resize, selection, editor và drag reorder.
- [x] Drag-edge auto-scroll cho split và unsplit hosts.
- [x] Snapshot/tile caching và split-aware dirty-region projection.
- [x] WPF DrawingContext và D3DImage shared-texture backend.
- [x] WinForms GDI+, Direct2D/DirectWrite HWND và D3D11/DXGI backend.
- [x] Injected device/front-buffer lifecycle recreation stress gates.
- [ ] 60/120 Hz, 4K target-hardware latency, FPS, memory và power baselines.

## C. Formula engine

- [x] Tokenizer, parser, AST, dependency graph và circular-reference policy.
- [x] Arithmetic, comparison, concatenation, A1 references/ranges và basic cross-sheet references.
- [x] `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT` và `IF`.
- [x] Shared-formula import, anchor/follower expansion và mixed/absolute A1 translation không materialize range.
- [ ] Shared-formula export grouping, stable shared indexes và normal-formula fallback.
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
- [x] Repeated-save gate cho nested opaque parts, drawing/image, custom XML/properties và package-root relationships.
- [x] Package graph preflight cho URI, relationship ID/type/target và size/count limits.
- [x] Shared-formula import với malformed-group và cached-value gates.
- [ ] Shared-formula export và complete shared-formula round-trip compatibility corpus.
- [ ] Conditional formatting, validation và tables.
- [ ] First-class drawings, images và charts model/editor.
- [ ] Print areas, page setup, page breaks, preview và PDF export.
- [ ] Compatibility corpus và round-trip differential tests với nhiều nguồn tạo XLSX.

## E. Data và analysis

- [x] Basic in-memory sort có safety limits.
- [ ] AutoFilter model và desktop filter UI.
- [ ] Advanced/multi-key sort, custom lists và stable large-data path.
- [ ] Tables, subtotals, grouping và outlines.
- [ ] Pivot tables, slicers và calculated fields.
- [ ] External/virtualized data sources và incremental loading.

## F. Cross-platform control suite

- [x] Platform-neutral command, Ribbon Core, Bars Core và DataGrid Core contracts.
- [x] Public WPF và WinForms spreadsheet hosts/samples.
- [x] Production Skia GPU display-list executor.
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

1. Shared-formula export grouping, reference equivalence proof và repeated-save round-trip.
2. Conditional formatting, validation và tables.
3. Formula/function surface cùng structured references và dynamic arrays.
4. AutoFilter, advanced sort, grouping và virtualized data.
5. Printing/PDF, drawings/charts và pivot/slicers.
6. Accessibility, packaging, fuzzing, performance budgets và release hardening.

## Trạng thái ước tính sau task shared-formula import

- Nền móng engine/viewport/renderer: khoảng `85%`.
- MVP bảng tính cơ bản: khoảng `66–70%`.
- Toàn bộ roadmap chuyên nghiệp: khoảng `44%`.
- Production release readiness: khoảng `20–25%`.

Task shared-formula import nâng tổng thể khoảng `0,5–1` điểm phần trăm. Phần shared-formula export, compatibility corpus và structural round-trip vẫn còn, nên chưa chấm cao hơn.

Các tỷ lệ trên là ước lượng theo độ khó và khối lượng còn lại, không phải tỷ lệ số checkbox.
