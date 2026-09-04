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

## QAT và backstage

Mỗi item QAT/backstage gồm một `CommandId` stable và key tip. Identity trùng trong
cùng surface bị từ chối không phân biệt hoa thường. Command không đăng ký vẫn có
presentation disabled như Ribbon hiện hữu. Activation luôn đi qua runtime; presenter
không gọi handler hoặc file API trực tiếp.

Backstage là surface File do host điền command document phù hợp. SDK hiện không sở
hữu lifecycle/path picker dùng chung giữa WPF, WinForms và MAUI, nên không giả lập
Open/Save bằng một code path workbook thứ hai.

## Command catalog audit

`RibbonProductionCommandCatalog.CommandIds` khóa 30 command được
`SpreadsheetSession` đăng ký ở checkpoint này. `CreateDefaultDefinition` đặt cả 30
identity vào Ribbon và `RibbonCommandCatalogAudit.Validate` fail-fast nếu capability
được audit chưa đăng ký hoặc chưa reachable từ tab/QAT/backstage. Các capability mới
phải cập nhật manifest và regression test cùng commit đăng ký command.

## Key Tips, keyboard và focus

Key Tips có scope rõ: Tabs, tab đang chọn, QAT hoặc Backstage. Collision tường minh
trong QAT/backstage bị từ chối khi dựng definition; tip tab/command tự sinh được giải
collision ổn định. Bộ điều khiển nhận cả tip hoàn chỉnh và chuỗi ký tự 1–4 ký tự.

Escape từ tab/QAT/backstage quay về scope Tabs; Escape tiếp theo thoát Key Tips.
WPF và WinForms nhận Alt/Escape cùng ký tự chữ/số native. MAUI expose cùng API ký tự
để host keyboard accelerator của từng platform chuyển vào. Presenter ghi lại focus
trước khi vào Key Tips và khôi phục sau activation hoặc khi thoát scope gốc.
Trong khi Key Tips hoạt động, ba presenter gắn badge văn bản tương phản vào caption
tab/command/QAT/backstage; badge không chỉ dựa vào màu và được rebuild theo scope.

Automation/Accessible/Semantic name của File, QAT và backstage dùng caption tiếng
Việt hoặc caption command runtime. Không có control nào được tạo theo ô worksheet.

## Giới hạn

- RIBBON-009 không thêm file picker hay command handler Open/Save/Print dùng chung;
  ứng dụng đăng ký các command backstage theo lifecycle nền tảng của mình.
- Styling chuyên sâu cho badge theo theme/high-contrast thuộc UX-006; badge văn bản,
  keyboard state, activation, collision và focus semantics đã hoạt động.
- Deep customization của QAT/tab/group thuộc RIBBON-010.
