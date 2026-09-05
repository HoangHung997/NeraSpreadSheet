# RELEASE-009 Split Editor — Handoff lane A

## Bổ sung grant — giữ direction khi echo bản nháp nguyên trạng

- Root cấp thêm riêng Control.EditorDraft.cs và SplitAdorner.EditorDraft.cs.
  Guard native Select bằng actual selection start/length sau text update; không
  normalize selection đã đúng. Giữ validation-before-mutation, notification
  de-duplication, focus/history/canonical controller. Không thêm direction API.
- Regression backward selection nay yêu cầu Shift+Left mở rộng selection sau
  identical echo; changed range vẫn phải áp dụng. CaretIndex là WPF public caret,
  không phải contract moving edge. Chưa khẳng định arbitrary direction transfer.
- Split/standalone dùng cùng existing Vietnamese help formatter trong owner
  FormulaEditing partial; không thêm resource key hoặc hai bản chuỗi khác nhau.
- Source47faa24c đã dispatch follow-up build/test alias fix; source kế tiếp chứa
  grant này vẫn cần đủ năm workflows riêng và native screenshots được review.

## Checkpoint implementation 7950acb9

- Source `7950acb90714e7e3789ea4be70b56bc386074fdb` đã push và dispatch
  full33989178209, iOS33989179836, Q003C33989181142, packages33989182733,
  demo33989184174. Q003C/packages đã success; các run còn lại chưa xác nhận xanh.
- Windows job101368191810 dừng tại build do `KeyEventArgs` ambiguous giữa WPF và
  WinForms trong test mới. Follow-up thêm explicit WPF alias; không sửa product,
  nới analyzer hoặc bỏ test. Native7950 chưa chạy.
- Primary WPF TextBox source xác nhận CaretIndex getter là SelectionStart.
  Backward regression dùng native Shift+Left sau round-trip để quan sát direction;
  snapshot range/text/caret giống nhau không chứng minh moving edge được giữ.
  Root đã nhận báo cáo gap; không thêm API hoặc sửa ngoài granted files.

- Branch `feature/release-009-split-editor-routing`, base chính xác
  `7a378ca133a517820a3e9425423e841513e8d07d`. PR #1 Draft/open/unmerged;
  slice chưa release hoặc được tích hợp. Commit implementation là HEAD chứa
  checkpoint này; SHA và CI sẽ được bổ sung sau khi run có kết quả.
- Root grant desktop-only cho phép bắt đầu trước khi baseline iOS hoàn tất.
  Source base có Windows job101365914847, Core/Q, packages33988335138 và
  demo33988360802 success do coordinator xác minh; đây không phải all-platform
  accepted baseline và không thay cổng exact-final-HEAD của slice mới.
- Giữ nhánh navigation đã release ở
  `bed515b7250e165936c5b59e975abbf9923285b4`, artifacts cũ nguyên trạng.
  Không reset/rebase/cleanup, build/native nặng hoặc lấy desktop lease local.

## Implementation đang chờ kiểm chứng

- Owner ScrollCellIntoView route active split pane; native MoveActiveCell dùng
  cùng helper, existing frame/metric/ScrollPaneTo, giữ frozen axes và pane khác.
  Ô quá lớn giữ cạnh đầu để reveal lặp không dao động; không thêm history entry.
- Owner metadata getter route state split popup. Thêm nested argument help qua
  Session.FormulaEditing; compositor và getter dùng cùng reference projection,
  giữ opt-out, stable Table identity và canonical cancel/sheet-switch cleanup.
- Test mới `Release009SplitEditorRoutingSmokeTests` cho actual loaded standalone/
  split, owner input flags false, metadata/help/Table rename, fractional/frozen/
  merged/oversized visibility, Enter/Tab vượt hidden axes và history. Capture PNG
  tại subfolder `split-editor-routing` trong existing Ribbon matrix artifact.
- `Table007WpfEditorDraftSmokeTests` thêm backward-selection round-trip regression,
  ghi rõ ba-argument setter không biểu diễn direction; chưa sửa/mở rộng API.
  Toàn bộ root tests full-cell measure/queued scroll/highlight opt-out giữ nguyên.
- Paths trọng tâm: năm SDK files Control, Control.FormulaEditing,
  SplitController, SplitAdorner.KeyboardEditor, SplitAdorner.FormulaEditing;
  hai test files nêu trên và contract/worklog riêng này. Shared docs, sample,
  Commands, resources, CI, parser helpers và các B paths khác thuộc root.

## Verification và giới hạn

- Local `scripts/verify-architecture.ps1` PASS; `git diff --check` PASS.
  Chưa local build/runtime vì grant cấm heavy local/native và dung lượng thấp.
  Build/analyzers/native screenshots phải được kiểm chứng qua existing CI.
- Chưa có exact implementation CI xanh, chưa review ảnh mới. Không dùng các
  source runs cũ để xác nhận source mới. Năm final workflows vẫn bắt buộc.
- Formula bar, paged filter split routing, hướng caret khi round-trip và whole
  MAUI/hardware/performance acceptance vẫn OPEN theo phạm vi riêng.
- Rollback: revert riêng commit slice này; workbook/history không cần migration.
- Một bước tiếp theo: push implementation và dispatch đủ full/iOS/Q003C/packages/
  demo tại đúng SHA, xử lý build/native failures trước khi release ownership.
