# Current Work Handoff

- Ngày cập nhật: 2026-08-18
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Implementation commit đã được xác minh: `44a5f37368dcf41dd89f5c33ba05bb15108d54dc`
- GitHub Actions: run `32125901319`, CI `#311`, kết luận `success`
- Tài liệu nguồn sự thật: `docs/current-status.md`
- Contract mới nhất: `docs/header-reordering-contract.md`

## Đã hoàn thành trong mốc hiện tại

### Model và transaction reorder

- Thêm `WorksheetAxisMove` cho fixed-length row/column permutation.
- Di chuyển sparse cells, row-height/column-width overrides và merged ranges mà không materialize toàn axis.
- Thêm `SpreadsheetAxisReorderController` vào `SpreadsheetSession`.
- Map formula local/cross-sheet theo logical cell identity; giữ `$` markers, quoted sheet names và string literals.
- Từ chối nguyên tử formula range có image discontiguous.
- Từ chối merged range bị split/reverse hoặc vượt freeze boundary.
- Map active cell, anchor, whole-axis/multi-range selection và per-pane split offsets bằng exact sparse metrics.
- Undo/redo và rollback phục hồi exact worksheet/formula/selection/view snapshots rồi recalculate workbook.

### Shared drag geometry và desktop split hosts

- Thêm shared source/drop/threshold/preview geometry.
- Row source lấy từ left-edge panes; column source lấy từ top-edge panes.
- Input priority: scrollbar → split separator → dimension resize → reorder → selection.
- WinForms đọc `MK_LBUTTON` từ `wParam`, dùng pointer capture và shared display-list preview.
- WPF dùng preview routed input, mouse capture và `DrawingVisual` preview trên DrawingContext/D3DImage.
- Một selected contiguous whole-row/whole-column range được kéo như một block; nếu không thì kéo một axis item.

### Runtime gates

- Core permutation/mutation tests.
- Formula identity/discontiguous rejection tests.
- Transaction, split offset, selection, rollback, undo/redo và recalculation tests.
- Shared reorder geometry/preview tests.
- WinForms real-message row drag smoke.
- WinForms real-message column drag smoke.
- WPF native OS pointer row drag smoke và post-move D3DImage render.
- Full Windows build/tests/GPU runtime gate.
- Cross-platform Core tests và architecture verification.

## Kết quả CI đã xác minh

CI `#311` xanh toàn bộ tại implementation commit `44a5f37368dcf41dd89f5c33ba05bb15108d54dc`:

- `Core build and tests`: restore, build, tests và architecture verification thành công.
- `Windows hosts build`: restore, full solution build, tests và mandatory Windows desktop GPU runtime smoke thành công.
- Row/column reorder source, model, formula, selection, split view, preview và desktop input đều nằm trong gate này.

## Giới hạn có chủ ý

- Native header drag hiện được nối vào public split hosts; unsplit public-control drag path chưa nối.
- Chưa auto-scroll khi kéo header tới mép viewport.
- Không sinh union expression khi formula range trở thành discontiguous; thao tác bị từ chối.
- Chưa có structured/table/shared/dynamic-array reference rewrite.
- Chưa có sparse whole-axis style storage.
- PR tiếp tục Draft; không merge khi exact-head CI đỏ hoặc chưa xác định.

## File trọng tâm

- `src/NeraSpreadSheet.Core/WorksheetAxisMove.cs`
- `src/NeraSpreadSheet.Editing/SpreadsheetAxisReorderController.cs`
- `src/NeraSpreadSheet.Editing/FormulaStructuralReferenceRewriter.cs`
- `src/NeraSpreadSheet.Rendering.Spreadsheet/SpreadsheetSplitHeaderReorderGeometry.cs`
- `src/NeraSpreadSheet.WinForms/NeraSpreadsheetSplitSurface.HeaderReorder.cs`
- `src/NeraSpreadSheet.Wpf/NeraSpreadsheetSplitAdorner.HeaderReorder.cs`
- `tests/NeraSpreadSheet.Windows.Rendering.Tests/DesktopSplitHeaderReorderSmokeTests.cs`
- `tests/NeraSpreadSheet.Windows.Rendering.Tests/WinFormsSplitColumnHeaderReorderSmokeTests.cs`

## Bước tiếp theo duy nhất

Nối cùng contract header drag-reorder vào **unsplit** public WPF/WinForms controls, sau đó thêm drag-edge auto-scroll. Không viết một permutation/model thứ hai; bắt buộc tái sử dụng `WorksheetAxisMove`, `SpreadsheetAxisReorderController` và shared header reorder geometry hiện có.
