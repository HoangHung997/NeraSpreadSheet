# Contract runtime Ribbon và Bars

## Phạm vi RIBBON-004

RIBBON-004 bổ sung lớp điều phối runtime trung lập nền tảng giữa definition,
customization, presentation snapshot và command dispatcher:

- `RibbonRuntimeController` quản lý một Ribbon;
- `BarRuntimeController` quản lý một toolbar, menu hoặc context menu.

Controller không tạo native control. Presenter WPF, WinForms và MAUI chỉ đọc snapshot,
gửi activation và dựng lại chrome khi nhận `SnapshotChanged`.

## Definition và customization

`Definition` luôn là cây gốc bất biến do ứng dụng cung cấp. `EffectiveDefinition` là
kết quả áp dụng `Customization`. Gọi `SetCustomization(null)` khôi phục đúng instance
definition gốc; không sửa cây nguồn. Mỗi lần đổi customization tạo snapshot mới và
phát đúng một sự kiện `SnapshotChanged` đồng bộ.

Ứng dụng chịu trách nhiệm lưu JSON bằng serializer RIBBON-002. Runtime không tự đọc
file, registry, roaming profile hoặc cloud để giữ Core độc lập với môi trường host.

## Refresh và activation

`Refresh` hỏi lại command state và thay snapshot bất biến hiện hành. Controller không
tự tạo timer hoặc đăng ký frame callback; host gọi refresh sau khi workbook, selection
hoặc module thay đổi.

`TryActivateAsync` chỉ chấp nhận command hiện diện trong `EffectiveDefinition`. Vì vậy
presenter cũ không thể kích hoạt command đã bị ẩn sau khi customization đổi. Command
hợp lệ luôn chạy qua `CommandDispatcher.TryExecuteAsync`; runtime không gọi handler
trực tiếp và dispatcher vẫn kiểm tra `CanExecute` tại thời điểm thực thi.

Sau một execution thành công, controller refresh snapshot rồi phát
`SnapshotChanged`, cho phép checked/enabled/caption phản ánh state mới. Command bị
từ chối không phát refresh vì runtime không thực hiện mutation. Cancellation và lỗi
handler được truyền nguyên trạng; runtime không nuốt exception.

## Ranh giới luồng và hiệu năng

Controller không đồng bộ hóa callback giữa nhiều thread. Platform host phải gọi và
nhận sự kiện trên UI dispatcher của chính host. Projection và thu thập command ID chỉ
duyệt cây chrome nhỏ khi khởi tạo, refresh hoặc đổi customization; không nằm trên
đường scroll/render/input liên tục nên không cần benchmark.

RIBBON-004 là runtime headless nên không có desktop smoke. Regression tests xác minh
customization, command visibility, nested submenu, dispatcher execution, snapshot
refresh, sự kiện và source immutability. Native presenter và runtime smoke thuộc các
milestone `RIBBON-DESKTOP` và `RIBBON-MAUI`.
