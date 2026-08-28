# Contract lưu tùy biến Ribbon và Bars

## Phạm vi RIBBON-002

RIBBON-002 bổ sung persistence JSON có version cho các customization headless đã
được định nghĩa ở RIBBON-001. Mỗi module tự sở hữu codec để không tạo dependency
giữa `Ribbon.Core` và `Bars.Core`:

- `RibbonCustomizationJsonSerializer`;
- `BarCustomizationJsonSerializer`.

Codec chỉ lưu override của ứng dụng/người dùng, không lưu definition gốc, caption,
icon hay trạng thái command lúc chạy.

## Schema hiện hành

- Ribbon: `neraspreadsheet.ribbon-customization`, version `1`.
- Bars: `neraspreadsheet.bar-customization`, version `1`.
- Tên thuộc tính JSON phân biệt hoa thường.
- Serializer luôn xuất UTF-8 JSON gọn, theo thứ tự thuộc tính cố định và sắp target
  theo stable ID để cùng một customization logic tạo ra cùng nội dung.
- Trường chưa biết được bỏ qua trong cùng version để ứng dụng mới có thể bổ sung
  metadata mà ứng dụng cũ không cần hiểu.

`isVisible` mặc định là `true`; `order` và `isLarge` mặc định là không override;
collection con không có mặt được hiểu là rỗng. `tabs` ở Ribbon root, và `barId` cùng
`items` ở Bars root là bắt buộc.

## Migration

Cấu hình prototype legacy-v0 là JSON chưa có cả `schema` lẫn `version`, nhưng dùng
cùng payload field với version 1. Codec đọc legacy-v0 trong bộ nhớ; phương thức
`MigrateToCurrent` xác minh rồi ghi lại thành canonical version 1. Codec không bao giờ
ghi legacy-v0. Vì đây là thao tác chuẩn hóa, các field chưa biết sẽ không được ghi lại;
ứng dụng muốn bảo toàn metadata riêng phải lưu metadata đó ngoài payload Nera.

Nếu chỉ có một phần header, schema sai hoặc version khác `1`, thao tác bị từ chối bằng
`InvalidDataException`. Không đoán cấu trúc của version tương lai và không âm thầm hạ
version.

## Validation và giới hạn

- tối đa 1 MiB UTF-8 cho mỗi document;
- tối đa 10.000 tab/group/item hoặc bar item lồng nhau;
- JSON depth tối đa 64;
- không chấp nhận comment, trailing comma hoặc property trùng tên;
- stable ID trùng không phân biệt hoa thường vẫn bị model RIBBON-001 từ chối;
- lỗi JSON/model được chuyển thành `InvalidDataException` và giữ inner exception.

Các giới hạn ngăn file cấu hình nhỏ trở thành đường cấp phát không giới hạn. Việc đọc
và ghi xảy ra khi tải/lưu layout, không thuộc render/input hot path nên milestone này
không cần benchmark hay runtime UI smoke.

## Ngoài phạm vi

- tự đọc/ghi file, registry, roaming profile, cloud sync hoặc database;
- mã hóa, chữ ký và giải quyết xung đột nhiều thiết bị;
- presenter WPF/WinForms/MAUI và giao diện kéo-thả;
- migration sang version chưa tồn tại;
- cập nhật status/roadmap/workflow dùng chung; INTEGRATOR thực hiện khi tích hợp.
