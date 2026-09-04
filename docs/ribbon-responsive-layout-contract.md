# Contract responsive layout cho Ribbon

## Phạm vi RIBBON-007

`RibbonResponsiveLayoutEngine` là nguồn quyết định duy nhất cho measurement,
large/small/compact và group overflow của WPF, WinForms và MAUI. Engine chỉ đọc
`RibbonPresentationSnapshot` và tạo `RibbonLayoutSnapshot` bất biến; không sở
hữu workbook, selection hay native control.

## Measurement và DPI

`RibbonLayoutRequest.AvailableWidth` dùng physical pixel. `Scale` là số physical
pixel trên một logical unit. Metrics mặc định dùng logical unit và được nhân scale
đúng một lần. Vì vậy cùng logical width cho cùng kết quả ở 100%, 125%, 150% và
200% DPI. Metrics tùy chỉnh phải hữu hạn và không âm; scale phải hữu hạn và lớn
hơn 0.

Presenter WPF chuyển DIP sang physical pixel bằng DPI của visual. WinForms dùng
client pixel và `DeviceDpi`. MAUI dùng width logical cùng `LayoutScale` do host cập
nhật khi cửa sổ đổi màn hình. Theme/icon refresh và resize dựng lại cây command
chrome nhỏ, không đi qua worksheet scroll/render frame.

## Collapse deterministic

Mỗi group bắt đầu bằng kích thước preferred hiện có (`IsLarge` hoặc small). Khi
không đủ chỗ, engine lần lượt:

1. đổi large thành small;
2. đổi toàn group thành compact;
3. chuyển toàn group vào một overflow surface chung của tab.

Group có `CollapsePriority` thấp bị co trước. Khi priority bằng nhau, group bên
phải bị co trước để giữ ổn định các lệnh bên trái. Một overflow affordance dùng
chung cho mọi group đã overflow; presenter không dùng cuộn ngang làm giải pháp
chính và command vẫn kích hoạt qua `RibbonRuntimeController`.

## Identity và snapshot

Request mang stable selected-tab ID và focused-command ID. Layout giữ identity
nếu target còn tồn tại, chọn tab đầu tiên nếu tab đã biến mất và xóa focus ID nếu
command không còn trong presentation. Presenter capture identity trước rebuild,
khôi phục native focus khi command vẫn inline và tiếp tục giữ logical focus ID khi
command tạm nằm trong overflow để lần resize rộng sau có thể khôi phục.

## Giới hạn

RIBBON-007 chỉ có command button/toggle hiện hữu. Split button, combo, gallery,
color picker và item-specific measurement callback thuộc RIBBON-008. Khi toàn bộ
available width nhỏ hơn chính overflow affordance, nền tảng có thể bị hệ điều hành
clip vì không tồn tại biểu diễn command hữu dụng nhỏ hơn.
