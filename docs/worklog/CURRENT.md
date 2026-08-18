# Current Work Handoff

- Ngày cập nhật: 2026-08-19
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Implementation commit đã xác minh: `56e232ec928cf36db8a9497a78e2986b0b65a818`
- GitHub Actions: run `32197270157`, CI `#377`, kết luận `success`
- Tài liệu nguồn sự thật: `docs/current-status.md`
- Contract mới nhất: `docs/whole-axis-style-contract.md`

## Đã hoàn thành trong mốc hiện tại

### Sparse whole-axis style model

- Thêm `CellStylePatch` cho thay đổi property-level.
- Thêm sparse interval maps riêng cho hàng và cột.
- Ghép row/column patches theo một global sequence của worksheet.
- Giữ direct cell style là complete override và patch direct cells khi whole-axis command phủ lên chúng.
- Whole-row, whole-column và whole-sheet formatting không materialize blank cells.
- Whole-sheet dùng một full row-axis span.
- Finite range vẫn chịu materialization safety limit.

### Merge, structure và reorder

- Whole-axis formatting cắt qua merged range ngoài anchor sẽ cập nhật đúng top-left anchor tối thiểu.
- Inserted axes thừa hưởng style tại insertion index.
- Shifted spans được clip tại fixed worksheet boundary.
- Delete và `WorksheetAxisMove` map style spans theo cùng identity transform với cells/dimensions/merges.
- `WorksheetStructuralState` lưu row spans, column spans và next style sequence.
- Rollback, undo và redo phục hồi exact axis/cell state.

### Snapshot, renderer và hiệu năng

- `WorksheetSnapshot` deep-copy sparse axis styles.
- Effective style cache dùng row/column operation identity.
- Renderer áp dụng fill/border/font cho cả blank visible cells mà không tạo `CellData`.
- Style-only execute/undo/redo không gọi formula calculation engine.
- `UndoRedoManager.TryUndo/TryRedo` trả về operation và bảo toàn stack khi operation ném lỗi.
- Có BenchmarkDotNet coverage cho whole-row style và snapshot lookup.

## Kết quả xác minh

CI `#377` xanh toàn bộ tại implementation commit `56e232ec928cf36db8a9497a78e2986b0b65a818`:

- Core restore/build/tests và architecture verification thành công.
- Full Windows restore/build/test thành công.
- Mandatory Windows desktop GPU/runtime smoke thành công.
- No-materialization, direct override, merged anchor, insert/delete/reorder, exact history, snapshot cache và renderer gates đều xanh.
- Existing split/header-reorder/auto-scroll/dirty-region/GPU lifecycle gates vẫn xanh.

## Giới hạn có chủ ý

- Basic XLSX chưa round-trip full style table hoặc sparse axis-style metadata.
- Direct cell style là complete override, không phải partial style layer.
- Merged anchor có thể trở thành một explicit styled cell khi action chỉ cắt qua phần không chứa anchor.
- PR tiếp tục Draft; không merge nếu exact-head CI đỏ hoặc chưa xác định.

## File trọng tâm

- `src/NeraSpreadSheet.Core/CellStylePatch.cs`
- `src/NeraSpreadSheet.Core/WorksheetAxisStyleMap.cs`
- `src/NeraSpreadSheet.Core/Worksheet.cs`
- `src/NeraSpreadSheet.Core/WorksheetSnapshot.cs`
- `src/NeraSpreadSheet.Core/WorksheetStructuralState.cs`
- `src/NeraSpreadSheet.Editing/SpreadsheetStyleController.cs`
- `src/NeraSpreadSheet.Editing/SetWorksheetStylesOperation.cs`
- `src/NeraSpreadSheet.Rendering.Spreadsheet/SpreadsheetDisplayListComposer.cs`
- `tests/NeraSpreadSheet.Core.Tests/WorksheetAxisStyleTests.cs`
- `tests/NeraSpreadSheet.Editing.Tests/SparseWholeAxisStyleTests.cs`
- `tests/NeraSpreadSheet.Editing.Tests/MergedWholeAxisStyleTests.cs`
- `tests/NeraSpreadSheet.Rendering.Spreadsheet.Tests/SpreadsheetAxisStyleRenderingTests.cs`
- `benchmarks/NeraSpreadSheet.Benchmarks/SparseWholeAxisStyleBenchmarks.cs`

## Bước tiếp theo duy nhất

Triển khai **standalone undo/redo commands cho direct split-view changes**:

1. khóa view-operation contract cho topology, split coordinates, active pane và four-pane offsets;
2. đưa direct split mutations vào history mà không làm scrollbar/wheel frame spam history;
3. hỗ trợ command IDs, `CanExecute`, exact undo/redo và per-worksheet isolation;
4. nối WPF/WinForms public controllers;
5. thêm Core tests và Windows runtime smoke;
6. giữ split state persistence/XLSX hiện hành tương thích.
