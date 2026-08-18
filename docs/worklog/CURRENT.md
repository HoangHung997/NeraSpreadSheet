# Current Work Handoff

- Ngày cập nhật: 2026-08-18
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Implementation commit đã xác minh: `16e034189328385b16e7c6b567c4b4b2a094c974`
- GitHub Actions implementation gate: run `32129937837`, CI `#322`, kết luận `success`
- Tài liệu nguồn sự thật: `docs/current-status.md`
- Contract: `docs/header-reordering-contract.md`

## Đã hoàn thành trong mốc header reordering

### Model và transaction

- `WorksheetAxisMove` cho fixed-length row/column permutation.
- Di chuyển sparse cells, row-height/column-width overrides và merged ranges mà không materialize toàn axis.
- `SpreadsheetAxisReorderController` trong `SpreadsheetSession`.
- Formula local/cross-sheet theo logical cell identity; giữ `$`, quoted sheet names và string literals.
- Từ chối nguyên tử formula range có image discontiguous.
- Từ chối merge bị split/reverse hoặc vượt freeze boundary.
- Map active/anchor, whole-axis/multi-range selection và per-pane split offsets bằng exact sparse metrics.
- Undo/redo và rollback phục hồi exact worksheet/formula/selection/view snapshots rồi recalculate workbook.

### Shared drag geometry và split hosts

- Shared source/drop/threshold/preview geometry.
- Row source từ left-edge panes; column source từ top-edge panes.
- Priority: scrollbar → split separator → dimension resize → reorder → selection.
- WinForms đọc `MK_LBUTTON` từ `wParam`, dùng pointer capture và shared display-list preview.
- WPF dùng preview routed handlers, physical-button-gated optional capture và `DrawingVisual` preview trên DrawingContext/D3DImage.
- WPF không hủy transaction chỉ vì capture unavailable; lost capture khi vẫn đang giữ nút trái vẫn cancel an toàn.
- Selected contiguous whole-row/whole-column range được kéo như một block; nếu không thì kéo một axis item.

### Runtime gates

- Core permutation/mutation tests.
- Formula identity/discontiguous rejection tests.
- Transaction, split-offset, selection, rollback, undo/redo và recalculation tests.
- Shared reorder geometry/preview tests.
- WinForms actual surface-message row drag smoke.
- WinForms actual surface-message column drag smoke.
- WPF real loaded-window production state-machine smoke: source, threshold, preview lifecycle, selection, commit/undo và post-move D3DImage render.
- Full Windows build/tests/GPU runtime gate.
- Cross-platform Core tests và architecture verification.

## CI đã xác minh

CI `#322` xanh tại implementation commit `16e034189328385b16e7c6b567c4b4b2a094c974`:

- `Core build and tests`: restore, build, tests, architecture verification thành công.
- `Windows hosts build`: restore, full solution build, tests, mandatory Windows desktop GPU/runtime smoke thành công.

Hosted Windows runner không cung cấp global WPF pointer injection ổn định. Gate WPF vì vậy mở Window/control/controller thật và gọi cùng production drag state machine một cách deterministic. Không được mô tả gate này là global native pointer injection.

## Giới hạn có chủ ý

- Native drag UI hiện chỉ nối vào public split hosts.
- Chưa có unsplit-control header drag.
- Chưa auto-scroll khi kéo tới mép viewport.
- Không sinh union expression cho formula range discontiguous.
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

Nối cùng contract header drag-reorder vào **unsplit** public WPF/WinForms controls, sau đó bổ sung drag-edge auto-scroll. Không tạo permutation/model thứ hai; bắt buộc tái sử dụng `WorksheetAxisMove`, `SpreadsheetAxisReorderController` và shared reorder geometry hiện có.
