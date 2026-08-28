# Ribbon và Bars customization contract

## Phạm vi RIBBON-001

RIBBON-001 bổ sung mô hình tùy biến trung lập nền tảng cho `Ribbon.Core` và
`Bars.Core`. Contract cho phép host hoặc ứng dụng tạo một definition gốc rồi áp dụng
các override bất biến trước khi presenter WPF, WinForms hoặc MAUI dựng giao diện.

Milestone này hỗ trợ:

- ẩn/hiện tab, group và command của Ribbon;
- đổi thứ tự tab, group và command;
- đổi kích thước large/small của Ribbon command;
- ẩn/hiện và đổi thứ tự item trong toolbar/menu/context menu;
- áp dụng tùy biến đệ quy cho submenu;
- so khớp ID không phân biệt hoa thường;
- bỏ qua target không còn tồn tại để cấu hình sống được khi module tùy chọn bị gỡ;
- trả về definition mới, không thay đổi definition gốc.

## Stable identity

- Ribbon tab và group dùng `Id` hiện có.
- Ribbon item dùng `CommandId`; một group không được chứa hai command ID trùng nhau.
- Bar command mặc định dùng `CommandId` làm `Id` tùy biến.
- Bar submenu và separator chỉ tùy biến được khi ứng dụng cấp `Id` tường minh.
- Các `Id` anh em trong cùng một bar/submenu phải duy nhất, không phân biệt hoa
  thường.

Các ràng buộc trên ngăn một override khớp nhiều phần tử và làm kết quả phụ thuộc thứ
tự duyệt.

## Ordering

Definition gốc và kết quả tùy biến đều sắp xếp tăng dần theo `Order`. LINQ stable sort
giữ thứ tự khai báo khi nhiều phần tử có cùng `Order`. Override chỉ thay `Order` của
target; phần tử không được nhắc tới giữ nguyên giá trị.

## Unknown targets

Target không tồn tại được bỏ qua có chủ ý. Điều này cho phép một layout dùng chung
giữa các edition có module khác nhau. ID trùng trong chính customization bị từ chối
ngay khi khởi tạo.

## Ranh giới chưa thuộc RIBBON-001

- presenter/native control cho WPF, WinForms và MAUI;
- thao tác kéo-thả trực quan của người dùng cuối;
- serialization, schema version và migration của customization (`RIBBON-002`);
- icon, shortcut, localization resource và command-state presentation;
- chuyển command sang một group hoặc bar khác;
- cập nhật `CURRENT.md`, roadmap hoặc workflow dùng chung; INTEGRATOR thực hiện khi
  tích hợp.

Không có runtime UI smoke trong milestone này vì thay đổi chỉ thuộc schema headless.
Regression tests xác minh visibility, ordering, stable identity, nested submenu,
unknown target và tính bất biến của source. Không cần benchmark vì áp dụng
customization chỉ duyệt cây schema nhỏ khi dựng UI, không nằm trên render/input hot
path.
