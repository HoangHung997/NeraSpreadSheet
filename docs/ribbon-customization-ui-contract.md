# Contract phiên và giao diện tùy biến Ribbon/Bars

## Phạm vi RIBBON-CUSTOMIZE-UI

Milestone này biến customization model đã có thành một luồng chỉnh sửa dùng được:

- `RibbonCustomizationSession` chỉnh ẩn/hiện tab, group, command; đổi thứ tự anh em;
  đổi command giữa kích thước lớn/nhỏ;
- `BarCustomizationSession` chỉnh ẩn/hiện và thứ tự item có stable ID, kể cả submenu;
- `NeraRibbonCustomizationDialog` native cho WPF và WinForms áp dụng thay đổi trực
  tiếp qua `RibbonRuntimeController.SetCustomization`;
- dialog có thể reset về definition của ứng dụng và lưu/nạp JSON schema v1 qua codec
  RIBBON-002;
- target ID được so khớp không phân biệt hoa thường;
- mỗi thao tác tạo customization mới khi áp dụng; definition gốc không bị sửa.

## Identity và phạm vi sắp xếp

Target Ribbon được nhận dạng bằng đường dẫn `tabId/groupId/commandId`. Target Bar dùng
chuỗi stable ID từ bar root tới item. Lệnh di chuyển chỉ đổi vị trí trong danh sách
anh em hiện tại. Milestone không chuyển command sang tab, group, bar hoặc submenu
khác và không giả lập chuyển bằng thao tác xóa/thêm.

Item Bar không có stable ID vẫn được giữ nguyên bởi `BarDefinition`; editor không
hiển thị item đó như một target có thể chỉnh. Vì thế separator ẩn danh không bị gán
identity giả.

## Module tùy chọn

Khi mở một customization có target chưa tồn tại trong definition hiện tại, session
giữ nguyên override đó khi người dùng chỉnh và lưu lại. Điều này cho phép cùng một
profile sống qua việc bật/tắt module. Nút **Mặc định** là thao tác chủ ý xóa toàn bộ
override, bao gồm target module chưa nạp.

## Runtime và UI

Dialog dùng control native theo nền tảng và không tạo control theo từng ô bảng tính.
Các thay đổi chỉ dựng lại cây Ribbon nhỏ, không đi qua đường render/scroll của sheet.
Dialog không sở hữu command dispatcher và không thực thi command; runtime RIBBON-004
vẫn là đường duy nhất cập nhật snapshot/activation.

Chuỗi UI mặc định dùng tiếng Việt và các control có automation name/id. Ứng dụng có
thể dùng session headless để xây editor riêng hoặc dùng dialog có sẵn.

## Giới hạn còn lại

Các giới hạn kéo-thả/chuyển parent/MAUI dưới đây mô tả checkpoint lịch sử
RIBBON-CUSTOMIZE-UI và đã được RIBBON-010 thay thế bằng
[`ribbon-deep-customization-contract.md`](ribbon-deep-customization-contract.md).

- chưa có kéo-thả; bản native dùng chọn target và nút Lên/Xuống;
- chưa chuyển command giữa các parent;
- MAUI presenter/editor thuộc task `RIBBON-MAUI` sau khi vùng Apple được giải phóng;
- integrator cập nhật roadmap, status và worklog dùng chung khi tích hợp stack.

Rollback: revert commit của `RIBBON-CUSTOMIZE-UI`; schema JSON v1 và definition gốc
không thay đổi nên cấu hình đã lưu vẫn đọc được bằng RIBBON-002.
