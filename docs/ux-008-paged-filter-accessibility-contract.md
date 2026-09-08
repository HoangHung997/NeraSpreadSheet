# UX-008 — Accessibility tự động của popup bộ lọc phân trang

## Phạm vi

Base `4639767ef8c5f3c44d1e074084d3b7f78dac00fc`, đã được coordinator xác minh
sáu workflow SUCCESS. Đây là phần kiểm thử U2 của Table/Filter/Ribbon/UX;
không triển khai workbook, editor, filter presenter hay automation provider mới.

Dùng cùng partial class `Release009SplitAutoFilterSmokeTests` và private
`RunLoaded`/`Host`/`ClickHeader`/`Ready`/`Drain`/`Flush` hiện có. Fixture thật
gồm WPF sample đã Loaded, canonical session, một Table hoặc worksheet AutoFilter,
250 giá trị phân biệt, trang mặc định 100 và bốn split panes với offset lẻ.
Tệp test cũ chỉ thêm modifier `partial`; mọi assertions, helpers, timing và ảnh
của RELEASE-009 giữ nguyên.

## Những điều phải kiểm chứng

Ba nhóm test, mỗi nhóm chạy hai owner và hai ngôn ngữ vi-VN/en-US. Mỗi case
đi qua cả bốn panes: tổng 12 test cases, 48 lượt kiểm pane.

- Mở bằng actual header input và xác minh placement target/anchor/pane từ frame
  đã trình bày. Native WPF peers phải có role Edit/ComboBox/CheckBox/Button/Text,
  tên đúng ngôn ngữ và native Value/Selection/ExpandCollapse/Toggle/Invoke patterns.
  Việc đọc/tác động peer chạy trên chính WPF UI thread của fixture.
- Trang 0/100/200 lần lượt có 100/100/50 checkbox peers trong native subtree;
  tên phải tương ứng dữ liệu hiện hành. Previous/Next đổi trạng thái enabled;
  Invoke trên nút disabled phải bị từ chối. Search `Value 249` còn đúng một
  peer, trang reset về 0. Toggle đổi draft selection nhưng chưa có workbook Undo.
- Status peer có owner, tên cột, trạng thái filter/sort và số dòng kết quả;
  thay trang/search cập nhật mô tả trang mà không giả thay kết quả đã Apply.
  Invoke Sort/Apply/ClearSort/Clear đi qua controller thật, kiểm một Undo cho
  mỗi mutation và đếm visible rows độc lập từ worksheet snapshot.
- Sau Sort, Apply, ClearSort và Clear, header geometry và native status phải
  lần lượt phản ánh Sorted, FilteredAndSorted, Filtered và None. Popup được mở
  lại tại cùng pane để đọc trạng thái mới. Tên của popup root được kiểm như
  attached metadata; không giả rằng Border root có một peer riêng.
- Peer của trang/popup cũ phải rời current native subtree, offscreen và mất
  focus. Native patterns giữ từ popup cũ không được sửa binding/popup mới.
  Cancel trả focus về actual split surface; chuyển worksheet và quay lại không
  cho control/peer cũ giành focus, đổi selection ngầm hoặc thêm workbook history.
- Localization chỉ thay tài nguyên của presenter, không đổi thread culture,
  tên owner/cột hoặc dữ liệu của workbook.

## Ranh giới bằng chứng

Đây là direct native WPF peer và loaded UI regression. Không thêm external UIA
COM client, không giả screen-reader smoke bằng việc đọc Name/role/state.
Trước khi kiểm current subtree, helper lưu counts của cached snapshot rồi gọi
public `ResetChildrenCache` trên actual ScrollViewer peer để lấy children mới
qua `GetChildrenCore`. Nếu cached/current khác nhau, ghi bounded counts trước/sau;
không bỏ peer identity/names/count/role/offscreen/focus assertions. Tối đa ba
page/search changes mỗi pane của fixture cố định; không ghi nội dung workbook.
Đây là fresh direct-peer query, không chứng minh tự invalidating cache hoặc
UIA StructureChanged/property-change/live-region events đến connected client.
[Microsoft API](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.peers.automationpeer.resetchildrencache?view=windowsdesktop-10.0)
giải thích thao tác reset đồng bộ; lượt cached query FAIL tại663 được giữ trong
worklog, không diễn giải lại thành PASS hoặc kết luận production cause.
`IsOffscreen` không chứng minh phần tử không bị một cửa sổ khác che. Không dùng
kết quả này để đóng U2 manual Narrator/NVDA, U5 physical DPI/multi-monitor/touch,
Apple hardware keyboard, whole MAUI hoặc P3 performance.

Nếu native test chứng minh production gap hoặc cần sửa fixture cũ ngoài
modifier, lane báo failing evidence và exact path cho coordinator. Không sửa
SDK/editor/renderer hoặc giảm assertions để tạo PASS trong phạm vi này.

## Cổng bàn giao

Build/analyzers và toàn bộ Windows runtime suite phải PASS tại source cuối;
các tests/captures cũ vẫn chạy. Kiểm architecture, diff và bốn-path ownership.
Source final cần full CI, iOS, Q003C, Windows package và published demo cùng HEAD;
coordinator kiểm sáu workflow riêng sau integration. Không chạy local heavy
build/native, không retry mù hoặc lấy kết quả commit cha làm final acceptance.

Rollback bằng revert bốn-path test/contract/worklog change. Không migration,
package mới, thay public API hoặc thay hành vi workbook.
