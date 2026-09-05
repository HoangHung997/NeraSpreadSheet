# Contract presentation Ribbon và Bars

## Phạm vi RIBBON-003

RIBBON-003 nối definition/customization headless với command registry mà không phụ
thuộc WPF, WinForms hay MAUI. Kết quả là snapshot chỉ đọc để presenter nền tảng dựng
native controls hoặc GPU chrome:

- `CommandPresentationResolver` kết hợp `CommandDescriptor` và `CommandState`;
- `RibbonPresentationProjector` tạo cây tab/group/item;
- `BarPresentationProjector` tạo cây toolbar/menu/context-menu đệ quy.

Projector không thực thi command. Khi người dùng kích hoạt item, host vẫn gọi
`CommandDispatcher.TryExecuteAsync` để giữ một đường thực thi duy nhất.

## Command presentation

Một command đã đăng ký cung cấp:

- caption runtime từ `CommandState.DisplayText` khi giá trị này không rỗng, nếu không
  dùng caption của `CommandDescriptor`;
- tooltip, icon key và shortcut từ descriptor;
- enabled và checked từ state của handler.

Command chưa đăng ký vẫn được giữ trong cây bằng caption fallback là `CommandId`,
`IsRegistered = false` và disabled. Quy tắc này giữ stable layout khi module tùy chọn
chưa được nạp, đồng thời cho presenter có thể hiển thị hoặc chẩn đoán item thiếu mà
không tự thay đổi cấu trúc.

## Snapshot consistency

Mỗi lần `Project` tạo cache mới theo `CommandId`. Một command xuất hiện ở nhiều tab,
group, menu hoặc submenu chỉ được hỏi state một lần trong snapshot đó và mọi vị trí
tham chiếu cùng một `CommandPresentation`. Lần `Project` tiếp theo đọc state mới;
snapshot cũ không bị thay đổi.

Ribbon giữ nguyên thứ tự definition đã được chuẩn hóa và giữ cờ large/small. Bars giữ
nguyên kind, stable ID, caption submenu và cây con. Separator luôn disabled. Submenu
enabled khi có ít nhất một hậu duệ trực tiếp có thể mở/thực thi; submenu lồng nhau tự
lan truyền trạng thái này.

## Ranh giới

- không tạo native control, `FrameworkElement`, WinForms `Control` hoặc MAUI `View`;
- không đăng ký event hay tự refresh theo timer/frame;
- không sở hữu `CommandRegistry`, workbook, selection hoặc calculation engine;
- không thay đổi execution/cancellation semantics của `CommandDispatcher`;
- presenter nền tảng, input binding, icon resource và localization resource thuộc
  milestone sau;
- không cập nhật status/roadmap/workflow dùng chung; INTEGRATOR thực hiện khi merge.

Projection chỉ duyệt cây schema nhỏ khi host dựng hoặc refresh chrome, không nằm trên
render/input hot path. Regression tests kiểm tra metadata, unknown command, snapshot
immutability, cache state, submenu đệ quy và enabled aggregation; không cần benchmark
hay runtime UI smoke cho milestone headless này.
