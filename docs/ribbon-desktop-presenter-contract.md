# Contract presenter Ribbon/Bars cho desktop

## Phạm vi RIBBON-DESKTOP

Milestone này cung cấp native chrome có thể nhúng trực tiếp vào ứng dụng:

- WPF `NeraRibbonControl` và `NeraBarPresenter`;
- WinForms `NeraRibbonControl` và `NeraBarPresenter`.

Ribbon dùng tab, group và button/toggle button native. Bars dùng toolbar, main menu
hoặc context menu native theo `BarKind`. Đây là control/presenter độc lập; chúng không
sở hữu workbook và không buộc ứng dụng phải dùng `NeraSpreadsheetControl`.

## Runtime và command

Presenter chỉ đọc snapshot của RIBBON-004. Click luôn gọi
`TryActivateAsync` trên runtime; presenter không resolve hoặc gọi handler trực tiếp.
Sau execution thành công, runtime phát snapshot mới và presenter dựng lại chrome để
caption, enabled và checked nhất quán.

`CommandContextFactory` cho phép ứng dụng cung cấp service, parameter và cancellation
token tại thời điểm click. Lỗi command được chuyển qua `CommandActivationFailed` và
giữ nguyên exception. Nếu ứng dụng không đăng ký boundary handler, exception tiếp tục
đi qua UI dispatcher thay vì bị nuốt.

Snapshot có thể được phát sau continuation bất đồng bộ; presenter marshal rebuild về
WPF Dispatcher hoặc WinForms UI thread. Presenter unsubscribe khi dispose.

## Native mapping

- Ribbon tab/group: WPF `TabItem`/`GroupBox`; WinForms `TabPage`/`GroupBox`.
- Ribbon command: `Button` hoặc toggle/check button khi command có checked state.
- Toolbar: WPF `ToolBar`; WinForms `ToolStrip`.
- Main/context menu: WPF `Menu`/`ContextMenu`; WinForms
  `MenuStrip`/`ContextMenuStrip`.
- Submenu và separator giữ nguyên cây/thứ tự từ snapshot.
- Tooltip, shortcut display, enabled, checked và Automation/Accessible name được ánh
  xạ từ `CommandPresentation`.
- `IconResolver` do ứng dụng cung cấp, chuyển `IconKey` độc lập nền tảng thành
  `ImageSource` hoặc `Image`. Core không sở hữu resource nền tảng.

Control chỉ được tạo cho command chrome, không tạo control theo ô và không tham gia
scroll/render hot path. Snapshot đổi sẽ rebuild toàn cây Ribbon/Bars nhỏ; không chạy
theo raw input/frame nên milestone không cần benchmark.

## Validation và giới hạn

Loaded STA smoke phải mở WPF Window và WinForms Form thật, xác minh native control,
accessibility metadata, click qua runtime và refresh enabled/checked. Build/analyzer
phải sạch trên cả hai host.

Milestone này chưa cung cấp trình chỉnh sửa kéo-thả trực quan hoặc bộ theme giống một
sản phẩm bên thứ ba. Ứng dụng có thể tùy biến bằng model/JSON RIBBON-001/002 và style
native control theo hệ theme riêng. MAUI presenter thuộc `RIBBON-MAUI`.
