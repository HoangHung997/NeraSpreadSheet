# UX-008 — Handoff kiểm thử accessibility bộ lọc phân trang

## Checkpoint implementation — chờ source CI

- Branch `feature/ux-008-paged-filter-accessibility`, base
  `4639767ef8c5f3c44d1e074084d3b7f78dac00fc`; dùng task/worktree hiện có.
- Nhánh trước `feature/release-009-split-autofilter` giữ nguyên tại
  `8c9bdef9a1355646955a6785a5a2a7dd66e506c3`; không xóa ảnh/artifacts.
- PR tích hợp #1 vẫn Draft/open/unmerged; không tạo PR hoặc task riêng.
- Write grant của coordinator đúng bốn paths; tệp test cũ chỉ thêm `partial`.
  Không viết shared CURRENT/status/wave, production/editor, CI hoặc capture helper.

Đã thêm ba nhóm tests với 12 data cases/48 lượt pane, dùng lại private loaded
fixture/presenter và actual header input. Kiểm native WPF peer roles/names,
Value/Toggle/Invoke/Selection/ExpandCollapse, 250 values/page100, page/search
status, bốn filter/sort states sau canonical mutation, history/result count,
detached peers và focus qua close/reopen/worksheet transition, vi/en isolation.
Chi tiết trong [contract](../ux-008-paged-filter-accessibility-contract.md).

## Files trọng tâm

1. `tests/NeraSpreadSheet.Windows.Rendering.Tests/Release009SplitAutoFilterSmokeTests.cs`:
   một modifier `partial`, không thay existing tests/helpers/timing.
2. `tests/NeraSpreadSheet.Windows.Rendering.Tests/Release009SplitAutoFilterSmokeTests.Accessibility.cs`:
   toàn bộ test mới và peer assertion helpers.
3. `docs/ux-008-paged-filter-accessibility-contract.md`.
4. Worklog này.

## Kiểm tra và bằng chứng

- Base đã được root kiểm sáu SUCCESS: full34188043102/iOS34188043132/
  Q34188043118/Windows34188040548/MAUI34188040552/demo34188040545.
  Base có 174 native desktop và 46 MAUI PASS, 9 Picker/shell PNG đã xem;
  28 split PNG khớp hash nguồn đã QA. Đây chỉ là bằng chứng base.
- Local architecture verification và diff check PASS trong lúc implementation.
  Không chạy local build/native; không lấy binary có sẵn làm proof code mới.
- Candidate build/native/5source workflows còn PENDING. Commit chứa checkpoint
  này phải được kiểm tại chính HEAD; CI receipts sẽ gửi coordinator sau khi
  đối chiếu remote SHA và dedupe runs. Chưa gọi UX-008 PASS/released.

## Giới hạn và rollback

WPF native peer states không thay screen-reader/hardware/physical occlusion
evidence. Không thêm live-region hoặc virtual header provider. Bộ test phải
báo lỗi nếu native subtree/peer lifecycle sai; production fix cần amended grant.
U2 manual/U5/wholeB/Mac package/P3 còn OPEN. B/C files tiếp tục frozen/owned
theo coordinator, không import hoặc sửa chồng.

Rollback bằng revert candidate bốn-path change, không workbook migration.
Bước tiếp theo duy nhất: hoàn tất static review, push candidate đúng bốn paths,
kiểm remote HEAD rồi chạy một bộ năm source gates đã được cấp quyền; đọc failure
thật nếu có trước bất kỳ sửa chữa hoặc dispatch tiếp theo.
