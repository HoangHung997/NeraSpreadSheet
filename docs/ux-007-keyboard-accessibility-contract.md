# UX-007 — Keyboard, accessibility và customization

## Phạm vi

Base `2e8482c25a44797a479b276ae26f472811a0a81e`, branch
`feature/ux-007-keyboard-a11y`. Lane A theo wave hoàn thiện 05/09/2026.
Tái sử dụng Ribbon/Bars runtime, localization và customization session hiện có;
không thay workbook, Table mutation, calculation, editor hoặc schema persistence.

## Gap map trước implementation

| Gate | Hiện trạng | Delta cần kiểm chứng |
| --- | --- | --- |
| U1 | Runtime có key tips/collision, native popup có navigation/focus tests | Handled event phải chỉ chạy một lần khi nhiều binding cùng owner; WinForms KeyPreview phải sống đến binding cuối; kiểm tra native focus/escape/menu |
| U2 | Native names/IDs và filter state có sẵn | Kiểm tra role/state/focus sau rebuild và shell mới; peer/UIA tests không được ghi thành screen-reader smoke |
| U3 | Host-scoped neutral vi, English partial; bốn palette | Đủ English cho catalog được hỗ trợ, placeholders giữ nguyên; kiểm tra Picker đang mở và contrast |
| U4 | MAUI chỉ có binding/session, chưa có visual shell | Shell MAUI dùng binding hiện hữu, add/remove/reorder/QAT, preview/apply/cancel/JSON, full/narrow và native semantics |
| U5 | Chỉ có raster-scale/logical-layout matrix | Physical DPI/multi-monitor/real touch cần thiết bị thật; chưa có bằng chứng thì OPEN |

## Contract đã triển khai

Một event shortcut đã được claim không được binding tiếp theo chạy lại, kể cả
khi activation đầu tiên còn chờ async. Command disabled vẫn được claim theo map
để không lọt sang surface khác. Chord ngoài surface không bị giữ. Subscription
được tháo khi dispose; nhiều WinForms bindings chia sẻ lifetime KeyPreview.

MAUI shell chỉ chỉnh working profile qua binding. Apply publish thành công rồi
mới nâng rollback point; Cancel quay về lần Apply thành công gần nhất. JSON vẫn
là schema Ribbon v2, host sở hữu lưu file. Không ghi setting process/application.
Nhãn người dùng/command host không bị dịch. Native controls cung cấp focus và
automation identity ổn định; controls chỉ theo cây Ribbon hữu hạn.

`NeraMauiRibbonCustomizationView` là ContentView nhúng có hai panel ở width từ
720 logical px, một cột trong cửa sổ hẹp, body cuộn và footer Apply/Cancel luôn
ở ngoài vùng cuộn. Các thao tác tạo/xóa/đổi tên/sắp xếp tab, group, command và QAT
gọi session qua binding hiện hữu. Host xử lý Applied/CloseRequested và sở hữu
lưu trữ JSON schema v2. Dispose bỏ preview chưa Apply. UI validation giữ exception
gốc ở LastError và báo CustomizationFailed; public LoadJson vẫn throw khi lỗi.

SetPresentation cập nhật labels và palette theo runtime localizer, giữ controls,
stable selection, JSON, bản nháp caption/caret và search query. Runtime đổi culture
trước, host gọi SetPresentation sau trên UI thread. Native Entry/Editor/Picker
nhận theme trong scope control; không thay thread culture/Application.Resources.
English phủ đủ 461 neutral keys, không dịch tên/caption riêng hoặc workbook values.

WPF/WinForms dialog dùng native cancel action cho Escape. WPF catalog item
Automation Name là caption command, tránh đọc record ToString với metadata kỹ
thuật. MAUI Windows shell chỉ nhận Escape sau native control; Picker mở xử lý
Escape trước. CloseRequested cho host đóng shell rồi khôi phục focus origin còn
sống trong cùng Window. Không thêm hook bàn phím toàn process/OS.

## Acceptance

Commands/MAUI regression, native desktop/runtime smoke, architecture verifier
và exact-final-HEAD CI gồm docs là cổng bắt buộc. Synthetic input, UIA/peer tests,
raster captures và screen-reader/hardware evidence được báo riêng. Chưa báo
whole U1–U5 DONE khi còn gate OPEN. Rollback bằng revert các commit lane sau base;
không migration workbook hoặc customization profile.

Source tests gồm exactly-once/disposal/Alt ownership, successful-Apply rollback,
resource completeness/format, native dialog names, MAUI shell add/remove/QAT
reorder/JSON, full/narrow native bounds và draft/caret qua bốn palette. Loaded
Windows Ribbon smoke phải tạo đủ chín PNG không rỗng: bốn shell, bốn native Picker
đang mở, một narrow shell. CI upload `maui-windows-ribbon-ux007` báo lỗi nếu thiếu;
ảnh Picker lấy pixel của popup đang mở, không thay bằng ảnh closed control.
`RenderTargetBitmap` chỉ dùng cho năm ảnh shell: API này không hỗ trợ Popup
sub-window theo [Microsoft](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.media.imaging.rendertargetbitmap?view=windows-app-sdk-1.8).
Smoke chọn popup chứa toàn bộ native item containers của đúng Picker; chụp
rectangle theo native transform/DPI, kiểm tra ownership của smoke process trước
đọc pixels và kiểm tra caption pixels của mỗi row. Capture bị che hoặc thiếu
caption phải fail. Helper chỉ trong test Windows, không thêm package/production API.

U2 actual screen reader và U5 physical multi-monitor DPI/real touch vẫn OPEN.
UIA peers, synthetic input và raster-scale exports không thay các bằng chứng này.
Apple/Android shell compile là cổng build; chưa phải native keyboard/touch smoke.
U1/U3/U4 chỉ được nghiệm thu source sau exact-final-HEAD CI và review artifact;
coordinator vẫn phải chạy combined gates sau khi ghép các lane.
