# Current Work Handoff

- Ngày cập nhật: 2026-08-18
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Implementation commit đã xác minh: `d498d6ed7c9eab04fd2a0d8edc6ceae9f62e59b9`
- GitHub Actions implementation gate: run `32158925404`, CI `#338`, kết luận `success`
- Tài liệu nguồn sự thật: `docs/current-status.md`
- Contract: `docs/header-reordering-contract.md`

## Đã hoàn thành trong mốc header reorder + edge auto-scroll

### Model và transaction

- `WorksheetAxisMove` cho fixed-length row/column permutation.
- Di chuyển sparse cells, row-height/column-width overrides và merged ranges mà không materialize toàn axis.
- Formula local/cross-sheet theo logical cell identity; giữ `$`, quoted sheet names và string literals.
- Từ chối nguyên tử formula range có image discontiguous và merge bị split/reverse/vượt freeze boundary.
- Map active/anchor, whole-axis/multi-range selection và per-pane split offsets bằng exact sparse metrics.
- Undo/redo và rollback phục hồi exact worksheet/formula/selection/view snapshots rồi recalculate workbook.

### Split hosts

- Shared source/drop/threshold/preview geometry.
- Row source từ left-edge panes; column source từ top-edge panes.
- Priority: scrollbar → split separator → dimension resize → reorder → selection.
- WinForms dùng actual child-HWND message path, pointer capture và shared display-list preview.
- WPF dùng preview routed input, optional capture và `DrawingVisual` preview trên DrawingContext/D3DImage.

### Unsplit public controllers

- WinForms và WPF có `EnableHeaderReordering`, `TryGetHeaderReorderController`, `DisableHeaderReordering`.
- Cả hai tái sử dụng `WorksheetAxisMove`, `SpreadsheetAxisReorderController`, shared geometry và public viewport/session contracts.
- WinForms dùng một preview child hit-transparent.
- WPF dùng một preview adorner dưới `AdornerLayer`.
- Behavior không khởi động khi split controller đang sở hữu control.

### Edge auto-scroll

- Thêm `SpreadsheetHeaderReorderAutoScroll` dùng chung.
- Velocity chỉ trên trục đang kéo, tăng quadratic trong edge zone và bị clamp ở maximum speed.
- Delta giữ pixel lẻ, không snap hàng/cột.
- Unsplit WinForms dùng timer; unsplit WPF dùng `CompositionTarget.Rendering`.
- Split WinForms/WPF tính velocity theo pane nguồn/đích và chỉ cuộn đúng pane đó.
- Sau mỗi scroll step, layout/drop boundary được tính lại tại pointer coordinate không đổi.
- Timer/render subscription được dọn khi complete/cancel/unload/dispose.

### Runtime gates

- Shared velocity, boundary, invalid configuration và elapsed-delta tests.
- Unsplit WinForms commit/preview/formula/selection/undo smoke.
- Unsplit WinForms edge auto-scroll smoke.
- Unsplit WPF commit/undo và post-move D3DImage smoke.
- Unsplit WPF edge auto-scroll smoke.
- Split WinForms/WPF auto-scroll smoke xác minh chỉ pane nguồn/đích dịch chuyển.
- Existing split row/column reorder, GPU lifecycle, scrollbar, dirty-region và sample gates vẫn xanh.

## CI đã xác minh

CI `#338` xanh tại exact head `d498d6ed7c9eab04fd2a0d8edc6ceae9f62e59b9`:

- `Core build and tests`: restore, build, tests, architecture verification thành công.
- `Windows hosts build`: restore, full solution build, tests, mandatory Windows desktop GPU/runtime smoke thành công.

Hosted Windows runner không cung cấp global WPF pointer injection ổn định. Gate WPF mở Window/control/controller thật và gọi cùng production state machine một cách deterministic. Không mô tả gate này là global native pointer injection.

## Giới hạn có chủ ý

- Unsplit behavior là optional controller lifecycle, không được tạo UI control riêng cho từng ô.
- Không sinh union expression cho formula range discontiguous.
- Chưa có structured/table/shared/dynamic-array reference rewrite.
- Chưa có sparse whole-axis style storage.
- Direct split-view changes chưa có standalone undo/redo commands.
- PR tiếp tục Draft; không merge khi exact-head CI đỏ hoặc chưa xác định.

## File trọng tâm vừa hoàn thành

- `src/NeraSpreadSheet.Rendering.Spreadsheet/SpreadsheetHeaderReorderAutoScroll.cs`
- `src/NeraSpreadSheet.WinForms/NeraSpreadsheetHeaderReorderController.cs`
- `src/NeraSpreadSheet.Wpf/NeraSpreadsheetHeaderReorderController.cs`
- `src/NeraSpreadSheet.WinForms/NeraSpreadsheetSplitSurface.HeaderReorder.cs`
- `src/NeraSpreadSheet.Wpf/NeraSpreadsheetSplitAdorner.HeaderReorder.cs`
- `tests/NeraSpreadSheet.Windows.Rendering.Tests/DesktopUnsplitHeaderReorderSmokeTests.cs`
- `tests/NeraSpreadSheet.Windows.Rendering.Tests/DesktopSplitHeaderReorderAutoScrollSmokeTests.cs`

## Bước tiếp theo duy nhất

Triển khai **sparse whole-axis style storage** và effective style composition. Whole-row/whole-column/whole-sheet formatting không được materialize hàng triệu ô. Phải hỗ trợ row/column patch layering, direct-cell override, structural insert/delete/reorder mapping, exact undo/redo, snapshot/render và test hiệu năng/sparsity.
