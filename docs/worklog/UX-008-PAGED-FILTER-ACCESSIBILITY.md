# UX-008 — Handoff kiểm thử accessibility bộ lọc phân trang

## Checkpoint hiện hành — fresh direct-peer query đã được duyệt, chờ source CI

Coordinator đã đọc actual failure663 và WPF primary source, duyệt delta ba
owned paths trên663: lấy cached snapshot/counts/offscreen trước reset, chỉ gọi
public `ResetChildrenCache` trên actual scroller peer rồi kiểm fresh subtree.
Giữ đủ 12 cases cùng identity/count/name/role/offscreen/focus/old-peer actions.
Không thay production, CI, timing, capture hoặc fixture cũ ngoài `partial`.
Commit chứa checkpoint này là source mới cần năm exact-HEAD workflows sau
remote-SHA verification và dedupe; không rerun663. Khi có outcome, freeze/report
cho coordinator trước mọi fix/probe tiếp theo. Clipboard/per-sheet vẫn queued.

Fresh enumeration không đóng U2 retained-client automatic cache invalidation,
StructureChanged/property/live-region delivery hoặc manual screen reader.
Bốn failure663 dưới đây vẫn FAIL; không kết luận automatic UIA đúng hoặc lỗi.

## Checkpoint 663 — bốn case cached peer FAIL, chưa release

Implementation/source HEAD `66344418b52d577a43ed39b0c438c5e912e63518` đã push
và kiểm remote SHA trước khi dispatch. Full `34189609698` FAIL riêng Windows
job `101944786182`: build 0 warnings/0 errors; native 186 cases, 182 PASS,
4 FAIL, 0 skip. Bốn lỗi là nhóm `NativePeersShouldExposeCurrentPagePatternsNamesAndFocus`
ở cả hai owner/ngôn ngữ, lần `Next` đầu tiên: 100 peer trong subtree đọc lại
không chứa checkbox peer của trang hiện tại (line61/277 tại663). Tám case mới
về header-state/announcements và close/sheet/stale-actions cùng 174 cases cũ PASS.

Bốn source workflow còn lại SUCCESS: iOS `34189611664`, Q003C `34189613773`,
Windows packages `34189615883`, demo `34189617513`. Không retry candidate663,
không dùng bốn gate xanh để gọi source đã nghiệm thu.

Đã báo coordinator failing evidence và giả thuyết direct-peer cache. WPF
`GetChildren` dùng child cache; direct `CreatePeerForElement` không tự đăng ký
peer thành HWND automation root. Public `ResetChildrenCache` gọi
`GetChildrenCore` đồng bộ. [Microsoft API](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.peers.automationpeer.resetchildrencache?view=windowsdesktop-10.0)
và [WPF v10 source](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Automation/Peers/AutomationPeer.cs)
là cơ sở đề xuất fresh direct-peer query. Đây chưa phải kết luận production
notification lỗi hoặc bằng chứng screen reader hoạt động.

Sau failing evidence, lane chuẩn bị local helper/contract patch để coordinator
review. Coordinator đã duyệt phạm vi fresh query như checkpoint phía trên;
automatic cache/StructureChanged delivery cần scope riêng. Diagnostic chỉ ghi
counts, không nội dung workbook, đường dẫn, định danh máy hoặc dữ liệu nhạy cảm.

## Phạm vi implementation

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
- Candidate663 đã có source CI như receipt phía trên. Candidate fresh-query mới
  còn chờ build/native/năm source gates; không lấy663 làm gate xanh kế thừa.

## Giới hạn và rollback

WPF native peer states không thay screen-reader/hardware/physical occlusion
evidence. Không thêm live-region hoặc virtual header provider. Bộ test phải
báo lỗi nếu native subtree/peer lifecycle sai; production fix cần amended grant.
U2 manual/U5/wholeB/Mac package/P3 còn OPEN. B/C files tiếp tục frozen/owned
theo coordinator, không import hoặc sửa chồng.

Rollback bằng revert candidate bốn-path change, không workbook migration.
Bước tiếp theo duy nhất: static/architecture/diff check, commit/push approved
fresh-query delta rồi kiểm năm exact-source gates và actual Windows diagnostics;
freeze/report outcome cho coordinator, không retry663 hoặc tự thêm fix/probe.
