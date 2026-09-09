# Quyết định sản phẩm: Avalonia là SDK UI chính

Ngày quyết định: 09/09/2026. Chủ sản phẩm xác nhận mọi app mới trong hệ sinh thái
sẽ dùng Avalonia để chia sẻ giao diện giữa các nền tảng. Đây là định hướng phát
triển, không phải chứng nhận mọi nền tảng/tính năng đã hoàn thành.

## Phân lớp và phạm vi hỗ trợ

- **Engine SDK độc lập:** Core, Formulas, Editing, Commands, Layout, Scrolling,
  Viewport, rendering contracts và OpenXML giữ các ranh giới hiện hữu. Có thể
  dùng engine không có UI; không được đưa Avalonia hoặc OpenXML vào Core.
- **UI chính cho app mới:** `NeraSpreadSheet.Avalonia`. Các ứng dụng dùng chung
  control, Ribbon, theme và command thay vì mỗi app tự viết lại giao diện.
  Desktop/mobile/browser vẫn cần entry point, packaging và runtime tests riêng.
- **WPF/WinForms/MAUI:** tạm dừng phát triển tính năng UI mới theo mặc định;
  giữ làm gói tích hợp/bảo trì cho ứng dụng cũ. Không tự xóa source, DLL/NuGet,
  public API, backend, smoke hoặc CI. Fix bảo mật/mất dữ liệu/hồi quy được xử lý
  theo ownership. Tạm dừng không đồng nghĩa ngừng hỗ trợ hay công bố EOL.
- Không yêu cầu mọi tính năng UI mới phải có bốn implementation ngang nhau.
  Thay đổi engine dùng chung vẫn phải kiểm regression của các consumer được giữ.

```text
App mới (Avalonia, dùng views/viewmodels chia sẻ)
                ↓
NeraSpreadSheet.Avalonia — control và theme được đóng gói trong SDK
                ↓
Engine Nera dùng chung ← OpenXML/import/export
                ↑
WPF / WinForms / MAUI — adapter tương thích tùy chọn, maintenance
```

## Quy tắc cho mọi AI và contributor

Đọc tài liệu này trước khi lấy một task UI. Các đề cập cũ “một model, ba
presenter”, M0/M2 hoặc roadmap snapshot là lịch sử/kiến trúc, không phải lệnh
mở lại phát triển đồng thời ba host cũ. Mọi task đang pause chỉ được tiếp tục
khi có chỉ đạo mới rõ ràng. Chỉ đạo mới ngày 09/09 mở lại riêng cải thiện Ribbon
Avalonia và tài liệu chiến lược; không tự restart Table/Filter hoặc đổi lịch.

Root Codex tiếp tục sở hữu CURRENT/status và assignment/progress của các lane.
Không sửa nhánh/worktree người khác, không coi branch riêng là được quyền sửa
mọi shared file. Thay đổi chiến lược không tự cấp quyền merge PR, mark Ready,
publish package hoặc bỏ gate. Các PR/CI chỉ có giá trị cho exact source SHA.

## Tiêu chuẩn Ribbon UI

WPF `RibbonChrome.xaml` và `ribbon-visual-contract.md` là mốc thiết kế tham chiếu,
không phải dependency của Avalonia. Tái sử dụng Ribbon.Core/runtime/definitions,
đưa ControlTheme/resources vào SDK Avalonia; không để mỗi app tự patch Fluent
Theme, không nhúng WPF, không tạo model Ribbon thứ hai. Có preset sản phẩm và
host extension rõ ràng; không dùng command giả để đủ số tab giống Excel.

Nghiệm thu phải phân biệt: command hoạt động; bố trí/interaction đạt; review ảnh
thật; native platform/hardware acceptance. Functional CI xanh không thay visual
acceptance. Cùng definition/capability, width/scale/theme/state để so sánh với
mốc WPF. Palette bốn chế độ, icon 16/32, caption, keyboard focus, overflow và
nội dung popup phải được test; không chỉ đếm số Button.

## Locale và trạng thái khi mở XLSX

Quy tắc chi tiết: [locale/view-state requirements](excel-locale-initial-view-requirements.md).
Không lấy lỗi culture, preservation hoặc worksheet metadata làm lý do thay engine
bằng logic riêng trong UI. XLSX numeric values luôn được giải mã độc lập culture;
formatting/input dùng policy rõ ràng của app/session, không thay dấu hàng loạt.

## Chuyển tiếp và rollback

Các docs này có hiệu lực trên nhánh chứa commit; coordinator đưa delta tài liệu
vào root sau review, không suy rằng branch mặc định đã đổi. Không xóa dữ liệu
hoặc migration workbook. Đổi lại định hướng phải có decision mới của chủ sản phẩm;
revert code UI không tự đảo quyết định sản phẩm. Khi công bố SDK, danh sách nền
tảng được hỗ trợ phải bám native evidence, không lấy danh sách Avalonia hỗ trợ
làm chứng nhận Nera đã test trên tất cả nền tảng.
