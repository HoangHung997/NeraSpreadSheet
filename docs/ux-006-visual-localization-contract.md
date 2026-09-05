# UX-006 — Resource localization và visual chrome

## Phạm vi đang triển khai

Lane bắt đầu ở `2bc00eb667da2f2c5afda1024ab753ac638d85d4`. Chỉ thay presentation
của Ribbon/Bars/Table Design/Filter. Không đổi workbook, formula, Table mutation,
filter/sort semantics, IDs, key tips tường minh hoặc customization JSON schema.

Coordinator transfer hai partial `NeraSpreadsheetTableHost.cs` và
`NeraSpreadsheetTableHost.Keyboard.Windows.cs` cho lane B, chỉ presentation.
Keyboard contract, bounded retry và subscriptions/disposal giữ nguyên.

Transfer CI chỉ cho một upload step sau loaded MAUI TableFilterSmoke, chứa
`ux006-*.png` do smoke tạo trong AppContext.BaseDirectory. Không đổi native
success/frame/focus/Undo/Redo gate hoặc runner script. RenderTargetBitmap chỉ
chứng minh WinUI Filter chrome, không chứng minh Skia body hay DPI vật lý.

`PresentationLocalization` dùng ResourceManager của .NET với catalog neutral
tiếng Việt. `Default` cố định `vi-VN`, không phụ thuộc ngôn ngữ của máy nhúng.
Host tạo một instance bằng CultureInfo và callback resource override; callback
trả null để tiếp tục fallback framework. Culture không có resource về neutral;
key không tồn tại trả nguyên key. Catalog `en` là tập override ban đầu, không
được gọi là bản dịch tiếng Anh đầy đủ. Keys là chuỗi nguồn có nghĩa, bao gồm
format message; dữ liệu truyền vào placeholder không bị dịch.

## Quyền của host và state

- `RibbonRuntimeController.SetLocalization` và `BarRuntimeController.SetLocalization`
  tái tạo snapshot trên UI context, cùng command context như Refresh. Không đổi
  culture toàn thread/process hoặc tài nguyên của application. Resource callback
  lỗi trước publish giữ snapshot/localizer cũ, exception tiếp tục đến caller.
- Descriptor mặc định được nhận dạng bằng stable command ID và caption nguồn
  khớp catalog. Caption host khác mặc định thắng resource SDK. Host có thể opt in
  bằng `CommandDescriptor.CaptionResourceKey` / `TooltipResourceKey`.
- Tab/group factory có `CaptionResourceKey`. Custom caption khác caption gốc và
  custom IDs giữ nguyên văn bản. Profile cũ lưu lại đúng caption gốc vẫn được coi
  là nhãn mặc định; không cần sửa hoặc migration JSON. Session chỉ localize rows
  của editor, không ghi bản dịch vào working profile.
- Tab key tips lấy caption gốc từ definition; command tips tiếp tục dựa vào ID.
  Bản dịch không tham gia allocator. QAT, shortcut và activation không thay đổi.
- Totals và built-in Table style có caption riêng với value/selected value giữ
  nguyên. Tên Table/cột, công thức, giá trị ô và tên style do host tạo không bị dịch.
- Paged desktop Filter nhận localizer/palette trước lần mở tiếp theo. Các shell
  Filter standalone nhận chúng qua constructor overload. Hai MAUI spreadsheet
  hosts có SetPresentation trên UI thread: sửa labels/palette tại chỗ, không
  thay ItemsSource hoặc controls đang focus, không đổi query/caret/checked values.
  Shell keys được ghi trước khi gắn workbook values. Binding localize accessibility/blank
  label khi publish, giữ cùng generation, page, value và selection semantics.

## Chrome và layout

Tái sử dụng palette/chrome/Iconography hiện hữu. High-contrast light/dark dùng
bề mặt trắng/đen ở cả ba host. Bars và Filter áp palette trong root của chính
control. WPF không ghi vào Application.Resources. Focus có viền; WPF command
có nét đứt ở phía trong, pressed có viền dày hơn. Checked vẫn có native state,
viền/glyph hoặc chữ trạng thái; disabled vẫn có trạng thái native IsEnabled.

Giữ engine dense ba hàng, caption đáy, responsive collapse/overflow và override
measurement của host. Không đổi viewport/scroll/render algorithm. Nhãn dài phải
còn truy cập qua overflow/tooltip/automation. Hàng 24 logical px mặc định là UI
dense; ứng dụng cảm ứng có thể chọn RowHeight lớn hơn bằng metrics hiện hữu.

## Evidence và giới hạn

Commands regressions kiểm tra resource coverage của 49 command, tab/group/filter
menu, fallback/culture/host override, lỗi callback, custom labels/JSON, key tips,
Table totals/Undo và localized layout trên width/scale matrix. Audit nguồn khóa
resource keys dùng bởi native presenters. MAUI binding kiểm tra blank label,
user value không bị dịch và culture switch không tăng history.

Desktop native tests và sample captures **chỉ chạy trên GitHub runner** trong
wave này. Capture bổ sung nhãn dài ở 1024/1920 và Filter trong bốn palette,
scale 1/1,25/1,5/2, có kiểm tra Cancel/history. Bộ command smoke Table.Style,
totals, dialogs/validation, Convert/Undo hiện hữu được giữ nguyên.

Loaded MAUI smoke kiểm tra culture/theme giữa lúc sheet mở, native focus/caret,
identity của controls/current page, query/selection và không có global resource/
culture/history leak. Capture vi-VN/light và en-GB/high-contrast dark của cả hai
host giữ nguyên original native frame/filter/Undo/Redo gates.

Chưa tuyên bố whole UX-006 DONE. Raster export/logical geometry không thay thế
physical multi-monitor DPI, touch-device acceptance hoặc screen reader. MAUI
customization vẫn là binding cho host xây shell, không giả có native dialog
hoàn chỉnh. CI/head cuối và ảnh đã inspect được ghi trong worklog lane.

Rollback: revert các commit UX-006 sau baseline. Không migration workbook,
resource application hoặc customization profile.
