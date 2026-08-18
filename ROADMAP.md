# Roadmap NeraSpreadSheet

`docs/current-status.md` là nguồn sự thật chi tiết về phần đã chạy được. Roadmap này chỉ thể hiện các mốc sản phẩm và phần còn lại; một checkbox chỉ được đánh dấu hoàn thành khi source, automated tests và runtime gate tương ứng đều tồn tại.

## M0 — Bootstrap kiến trúc

- [x] Khóa module và dependency direction.
- [x] Workbook sparse tối thiểu.
- [x] Metric index cho hàng/cột kích thước biến đổi.
- [x] Continuous pixel scroll controller.
- [x] Shared display-list contract.
- [x] Host shell WPF, WinForms và MAUI boundary.
- [x] Command, Ribbon Core, Bars Core và DataGrid Core contract.
- [x] Unit test, benchmark và CI nền.

## M1 — Workbook, editing và viewport usable

- [x] Versioned worksheet/read snapshot cho renderer.
- [x] Selection model, active cell, extended/multi-range và whole-axis selection.
- [x] Row/column insert, delete và live resize.
- [x] Native merged ranges.
- [x] Undo/redo transaction cho edit, formatting, merge, sort và structural operations.
- [x] Split-aware dirty-region projection cho cell/range changes.
- [x] Display-list builder cho grid, text, style, selection, headers và split chrome.
- [x] Editor overlay dùng lại một instance trên mỗi desktop host.
- [x] Freeze panes và split panes độc lập.
- [x] Per-worksheet split state, per-pane continuous offsets và pane-local scrollbars.
- [x] Model-safe row/column reorder, formula identity mapping và split-host native drag UI.
- [ ] Hidden row/column và outline/group semantics.
- [ ] Sparse whole-axis styles.
- [ ] Header reorder UI trên unsplit controls và drag-edge auto-scroll.

## M2 — Windows renderer hiệu năng cao

- [x] Direct2D/DirectWrite HWND backend.
- [x] D3D11/DXGI `FlipDiscard` swap-chain backend.
- [x] DirectWrite text-layout LRU/cache.
- [x] Nera-owned WPF D3D11 shared texture, D3D9Ex bridge và D3DImage lifecycle.
- [x] Hardware/WARP fallback và one-shot device recovery.
- [x] Translated tile cache và partial dirty-region paths cho backend phù hợp.
- [x] WPF DrawingContext và WinForms GDI+ fallback.
- [x] WPF/WinForms split-host runtime smoke trên Windows CI.
- [ ] Sustained 60 Hz/120 Hz benchmark trên 4K và phần cứng mục tiêu.
- [ ] Injected device-loss/front-buffer-loss stress dài hạn.
- [ ] Tile copy/scroll-blit optimization cho scroll delta nhỏ.

## M3 — Formula engine

- [x] Tokenizer, parser và AST.
- [x] Dependency graph, affected-only recalculation và circular-reference detection.
- [x] Arithmetic, comparison, concatenation, A1 references/ranges và basic cross-sheet references.
- [x] `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT`, `IF`.
- [x] Structural insert/delete và axis-reorder reference rewriting.
- [x] Error literal như `#REF!`.
- [ ] Complete math/text/date-time/lookup/financial function surface.
- [ ] Dynamic array/spill engine.
- [ ] Structured/table references và shared-formula semantics.
- [ ] Plugin hàm nghiệp vụ dự toán.

## M4 — XLSX, page layout và in ấn

- [x] Basic Open XML values, formulas/cached values và multiple worksheets.
- [x] Row heights, column widths và merged ranges.
- [x] Standard + Nera-native per-worksheet split metadata round trip.
- [ ] Full style/number-format fidelity.
- [ ] Shared formulas, conditional formatting, validation và tables.
- [ ] Drawing/image/chart parts.
- [ ] Unknown-part preservation.
- [ ] Print layout, page break, preview và PDF pipeline.
- [ ] Round-trip compatibility corpus.

## M5 — MAUI và Skia GPU

- [ ] Production Skia GPU render backend.
- [ ] MAUI native handler theo nền tảng.
- [ ] Touch selection handle, precision pan, pinch zoom và virtual keyboard.
- [ ] Mobile split/freeze interaction policy.
- [ ] Responsive command surface thay cho ribbon desktop đầy đủ.

## M6 — Control suite

- [x] Command registry/dispatcher và schema boundary.
- [x] Ribbon Core, toolbar/menu Bars Core và DataGrid Core boundaries.
- [x] WPF/WinForms sample apps với formulas, formatting, merge, XLSX, backends, freeze, split và pane scrollbars.
- [ ] Production Ribbon, toolbar, menu và context-menu hosts.
- [ ] DataGrid control với data model riêng.
- [ ] Theme, localization, accessibility và designer support.
- [ ] Chart, conditional formatting, image và shape.
- [ ] Pivot, slicer và module nâng cao.

## M7 — Product hardening

- [ ] Standalone undo/redo commands cho direct split-view changes.
- [ ] API compatibility checks.
- [ ] NuGet packaging, symbols và source-link.
- [ ] Crash recovery, diagnostics bundle và telemetry opt-in.
- [ ] Performance/correctness corpus trên workbook lớn.
- [ ] Alpha → Beta → RC → Production.

## Hàng đợi triển khai hiện tại

1. Header reorder UI trên unsplit WPF/WinForms controls và edge auto-scroll.
2. Sparse whole-axis style storage.
3. Undo/redo command layer cho direct split-view changes.
4. Injected GPU/front-buffer failure stress.
5. Skia GPU + MAUI native handler/touch UX.
6. XLSX fidelity mở rộng và printing/PDF.
