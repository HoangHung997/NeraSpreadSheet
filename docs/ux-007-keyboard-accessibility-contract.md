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
Trên Windows, Entry/Editor/Picker dùng brush thuộc từng control cho viền, focus,
selection và popup. HighContrastDark dùng nền đen/viền trắng/accent vàng;
HighContrastLight dùng nền trắng/viền đen/accent xanh. Brush được cập nhật tại
chỗ để đổi palette trong cùng RequestedTheme vẫn có hiệu lực trên template đã
load; không sửa brush mặc định dùng chung của Windows hoặc resource của host.
Dictionary bổ sung được merge riêng trong control, không ghi đè key local do MAUI
quản lý. TextBox selection dùng nền đậm ở Dark/HCD vì WinUI giữ chữ đang chọn
màu trắng; accent sáng vẫn dùng cho focus và dấu chọn popup.
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
Smoke còn kiểm tra pixel của nền popup, hàng được chọn và accent indicator theo
bốn palette, cùng viền native Entry/Picker ở hai chế độ high contrast. Chuyển
Light → HighContrastLight và Dark → HighContrastDark được kiểm tra khi control
đang loaded; file PNG tồn tại không đủ nghiệm thu contrast.

### Correction screen geometry — 08/09, chưa native acceptance

**Correction tiếp theo sau numeric1217:** guard cũ không hợp lệ vì so direct
peer/rendered bounds với full item slot, đồng thời coi RasterizedClient là screen.
Actual1217/job101933021836 có6refs450x33, full layout460x37, scale1. Exact
[WinUI1.7.4 UIA host source](https://github.com/microsoft/microsoft-ui-xaml/blob/5968fc091e07ee77202245dc7f2f36ea08342d51/src/dxaml/xcp/win/shared/UIAHostEnvironmentInfo.cpp)
và UIAWrapper cho thấy directpeer còn cần conversion; nhận xét public-peer-screen
ở checkpoint96 bên dưới đã được thay thế, không tiếp tục dùng làm contract.

Helper mới đo single actual rendered template child của mỗi item, không hardcode
theme margins. Built-in peer RasterizedClient chia RasterizationScale một lần,
đưa vào `popup.XamlRoot.CoordinateConverter.ConvertLocalToScreen(Rect)` rồi
đối chiếu physical result với measured child popup-local bounds. Converter tự
áp dụng rasterization theo [API](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.content.contentcoordinateconverter.convertlocaltoscreen?view=windows-app-sdk-1.7).
[XamlRoot getter](https://github.com/microsoft/microsoft-ui-xaml/blob/5968fc091e07ee77202245dc7f2f36ea08342d51/src/dxaml/xcp/dxaml/lib/XamlRoot_Partial.cpp)
lấy converter của associated island, cùng island mà Popup tạo UIA environment.
Không suy chọn main-window ID hoặc popup HWND để cộng offset thủ công.

Giữ2px finite/dimension/origin guards, actual HWND/PID checks và mọi caption/
palette/9captures; template root identity, XamlRoot và scale phải giữ nguyên
qua capture. Unexpected template/clipping/unsupported converter vẫn FAIL, không
fallback hoặc giảm tolerance. Diagnosticv2 tách peerClientX/Y với converted
screenX/Y. Thêm pure scale-once regression; actual hosted compilation/native
capture còn bắt buộc. Local cached assets là1.7.250909003/MAUI10.0.20; đây chưa
phải attestation của hosted native module. Không production SDK/package đổi.

Cfa96692c full34183383351/job101926823821 thất bại caption-pixel assertion.
Root download/verify artifact10039767660 (ZIP SHA256
`f5062a523a00dc5dd86649315eaf859780d85dcddc2ad9f8c2cbbde80e6f71e9`) và xem
cả hai PNG: ảnh picker462x234 chứa phần nền customization, không phải danh sách
đang mở. Không coi đó là bằng chứng SDK vẽ sai font/palette. Helper cũ cộng
`TransformToVisual(null)` của popup vào owner client origin, dù hai coordinate
roots có thể khác nhau; kiểm cùng PID không loại được nền cùng process.

Root sửa riêng test helper/caller: lấy screen bounds từ actual item
[AutomationPeer.GetBoundingRectangle](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.automation.peers.automationpeer.getboundingrectangle?view=windows-app-sdk-1.8),
đối chiếu với từng item local-to-popup bounds và RasterizationScale. Ít nhất
hai references phải đồng ý origin trong2physical pixels; kích thước/DPI phải
khớp và item nằm trong popup. Không dùng gốc owner làm fallback. Item centers
phải hit cùng visible native window của smoke và nằm trong actual window rect;
giữ chín own-process checks trước đọc pixels. IsOpen/loaded và geometry phải
giữ nguyên qua capture. Diagnostic chỉ fixed schema/numeric geometry và Boolean,
không HWND/PID/path hay raw desktop dump. Same-process background vẫn không được
coi là popup: các caption/palette assertions nguyên vẹn phải kiểm actual pixels.

Pure geometry self-checks trong existing native smoke dùng scale100/125/150/200%,
negative screen origin và rejects cho thiếu references, invalid/mismatched DPI,
inconsistent origins/outside layout. Đây không phải physical DPI certification.
Chín captures/full-narrow/theme/profile/caret checks không bị giảm; actual
Windows native run và ảnh đúng finalHEAD còn bắt buộc. Không SDK, package,
production Ribbon/editor/renderer, launcher, timing/retry hoặc permission change.
Rollback riêng hai test files về trước correction; không migration user data.

Root96f04fe actual34184752596/job101930731588: build,46MAUI tests và native
geometry self-checks PASS; actual Picker FAIL ở guard screen/local dimensions,
trước khi tạo popup PNG. Chưa có numeric rectangles để xác định clipping, DPI
hay nguồn khác. Không giảm guard: bổ sung diagnostic fixed schema trước guard,
tối đa16references, chỉ numeric geometry (non-finite thànhnull), không captions/
HWND/PID/path. [WinUI source](https://github.com/microsoft/microsoft-ui-xaml/blob/main/dxaml/xcp/dxaml/lib/FrameworkElementAutomationPeer_partial.cpp)
cho thấy core peer lấy global bounds có clipping; đây là lý do cần đo, chưa phải
cause proof cho runner. Same-PID nine-point guard không chứng minh popup identity;
caption/palette pixel assertions vẫn bắt buộc. Timing/acceptance không đổi.

U2 actual screen reader và U5 physical multi-monitor DPI/real touch vẫn OPEN.
UIA peers, synthetic input và raster-scale exports không thay các bằng chứng này.
Apple/Android shell compile là cổng build; chưa phải native keyboard/touch smoke.
U1/U3/U4 chỉ được nghiệm thu source sau exact-final-HEAD CI và review artifact;
coordinator vẫn phải chạy combined gates sau khi ghép các lane.
