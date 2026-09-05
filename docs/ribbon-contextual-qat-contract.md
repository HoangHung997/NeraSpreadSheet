# Contract contextual tabs, QAT, backstage và Key Tips

## Phạm vi RIBBON-009

`RibbonDefinition` tiếp tục là model host-neutral duy nhất. Definition có thể gắn
`RibbonContextualTabRule` vào tab hiện hữu, khai báo Quick Access Toolbar (QAT) và
backstage bằng `RibbonCommandSurfaceItem`. Cả tab thường, contextual tab, QAT và
backstage đều resolve và kích hoạt qua cùng `CommandRegistry`,
`RibbonPresentationProjector`, `RibbonRuntimeController` và `CommandDispatcher`.

## Contextual tabs

`RibbonSelectionContext` chỉ mang hai tín hiệu presentation cần biết:
`HasSelection` và `IsInTable`. Runtime không sở hữu workbook hoặc Table model. Host
cập nhật context sau khi selection/table state thay đổi bằng `SetSelectionContext`.
Tab `Selection` chỉ hiện khi có selection; tab `Table` chỉ hiện khi selection hiện tại
nằm trong Table. Definition gốc không bị sửa, và command trong tab đã biến mất không
thể được kích hoạt từ presenter cũ.

`table-design` trong catalog mặc định là contextual `Table`; capability Table Design
thực tế được checkpoint TABLE-005 bổ sung vào tab này qua command identity hiện hữu,
không tạo đường mutation workbook riêng trong Ribbon.

## Minimized state

`RibbonRuntimeController.IsMinimized` là trạng thái view độc lập với customization.
`RibbonViewStateJsonSerializer` lưu payload schema
`neraspreadsheet.ribbon-view-state`, version 1. Restore chỉ phát thay đổi khi giá trị
thực sự đổi. Presenter giữ tab strip/QAT/File khi minimized và ẩn vùng group command.
WPF, WinForms và MAUI phải ẩn cả group content lẫn overflow command surface; trạng
thái minimized không được để command vô hình vẫn chiếm focus hoặc accessibility tree.

## QAT và backstage

Mỗi item QAT/backstage gồm một `CommandId` stable và key tip. Identity trùng trong
cùng surface bị từ chối không phân biệt hoa thường. Command không đăng ký vẫn có
presentation disabled như Ribbon hiện hữu. Activation luôn đi qua runtime; presenter
không gọi handler hoặc file API trực tiếp.

Backstage là surface File do host điền command document phù hợp. SDK hiện không sở
hữu lifecycle/path picker dùng chung giữa WPF, WinForms và MAUI, nên không giả lập
Open/Save bằng một code path workbook thứ hai.

## Command catalog audit

`RibbonProductionCommandCatalog.CommandIds` là manifest chỉ-đọc của command được
`SpreadsheetSession` đăng ký ở checkpoint này. `CreateDefaultDefinition` đặt toàn bộ
identity vào Ribbon. Gate tích hợp dựng `SpreadsheetSession` thật rồi gọi
`RibbonCommandCatalogAudit.ValidateExact`: command thiếu registration/placement hoặc
registration mới chưa có trong manifest đều fail. Vì vậy test không khóa một số lượng
hard-code và tự phát hiện command thêm/bớt. Host có command ứng dụng riêng tiếp tục
dùng `Validate` thay vì exact audit. Các capability mới phải cập nhật manifest và
regression test cùng commit đăng ký command.

## Key Tips, keyboard và focus

Key Tips có scope rõ: Tabs, tab đang chọn, QAT hoặc Backstage. Collision tường minh
trong QAT/backstage bị từ chối khi dựng definition; tip tab/command tự sinh được giải
collision ổn định trong không gian ASCII `A-Z/0-9`, có giới hạn 1–4 ký tự và không
throw do allocator tham lam khi catalog lớn. Mapping tip và reverse lookup được cache
thành snapshot bất biến; presenter không quét lại toàn map cho từng item. Bộ điều
khiển nhận cả tip hoàn chỉnh và chuỗi ký tự 1–4 ký tự; `F`/`Q` ở scope Tabs lần lượt
mở Backstage/QAT bằng cả API tip hoàn chỉnh và API từng ký tự.

Escape từ tab/QAT/backstage quay về scope Tabs; Escape tiếp theo thoát Key Tips.
WPF và WinForms nhận Alt/Escape cùng ký tự chữ/số native từ owner được truyền vào
`BindShortcuts`, nên vẫn hoạt động khi focus đang ở worksheet/editor sibling; phím
nhập thường không bị giữ khi Key Tips chưa bật. MAUI expose cùng API ký tự để host
keyboard accelerator của từng platform chuyển vào và có API tên riêng để truyền
focus origin ngoài Ribbon mà không làm overload cũ thành mơ hồ qua reflection.
Presenter ghi stable automation/control identity cho focus nằm trong Ribbon, giữ
native reference cho focus ngoài Ribbon, rebuild rồi mới khôi phục. MAUI marshal bước
rebuild/restore sau activation bất đồng bộ về UI dispatcher.
Trong khi Key Tips hoạt động, ba presenter gắn badge văn bản tương phản vào caption
tab/command/QAT/backstage; badge không chỉ dựa vào màu, vẫn hiện ở compact item có
icon và được rebuild theo scope. Shortcut map bao gồm command visible chỉ nằm trong
QAT/backstage, không chỉ command trong tab.

Overload CLR công khai `Project(RibbonDefinition, CommandContext)` được giữ nguyên;
contextual projection dùng overload ba tham số riêng để không phá source/binary API.
Selection/customization chỉ publish sau khi toàn bộ projection, shortcut và Key Tips
snapshot tạo thành công, nên lỗi state handler không để runtime ở trạng thái nửa cũ
nửa mới. View-state JSON sai loại root luôn được chuẩn hóa thành `InvalidDataException`.

Automation/Accessible/Semantic name của File, QAT và backstage dùng caption tiếng
Việt hoặc caption command runtime. Không có control nào được tạo theo ô worksheet.

## Giới hạn

- RIBBON-009 không thêm file picker hay command handler Open/Save/Print dùng chung;
  ứng dụng đăng ký các command backstage theo lifecycle nền tảng của mình.
- Styling chuyên sâu cho badge theo theme/high-contrast thuộc UX-006; badge văn bản,
  keyboard state, activation, collision và focus semantics đã hoạt động.
- Deep customization của QAT/tab/group thuộc RIBBON-010.
