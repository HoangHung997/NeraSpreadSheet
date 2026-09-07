# AVALONIA-001 — contract và quyết định triển khai

## Trạng thái

Implementation candidate cho host desktop đầu tiên. Chưa phải toàn bộ A1/A2/A3 DONE.
Chỉ ghi nhận build/test/native runtime khi có run thành công trên exact source SHA.
PR #1 giữ Draft; nhánh Avalonia có Draft PR #4, chưa được tích hợp vào root.

## Quyết định phụ thuộc

Target `net10.0` không hậu tố Windows. Pin Avalonia/Headless/Desktop/Fluent/Inter
`12.1.2` trong `eng/avalonia-packages.props`, import bởi ba project owned.
Không thay `Directory.Packages.props` hoặc phiên bản Skia của các host hiện có.
Avalonia.Skia 12.1.2 khai báo SkiaSharp >=3.119.4; renderer Nera hiện dùng
4.151.1. Vì vậy host dùng Avalonia.DrawingContext để thực thi shared DisplayList,
không reference Nera.Rendering.Skia, không dùng native surface của MAUI/WPF.
Không claim hai major Skia này tương thích trong cùng một consumer process.
Nguồn: https://www.nuget.org/packages/Avalonia.Skia/12.1.2;
https://docs.avaloniaui.net/docs/graphics-animation/custom-rendering.

## Phạm vi mã đầu tiên

- Public NeraSpreadsheetControl gắn vào SpreadsheetSession do caller sở hữu.
- Shared viewport/display list, headers, cells, formatting, frozen-pane projection.
- Nested display lists giữ reference semantics; brush/pen/text caches có giới hạn.
- Host kế thừa Avalonia Control, không override sealed Panel.Render. Đúng một
  native TextBox nằm trong cả logical/visual trees; không tạo control cho mỗi ô.
- Pixel scrolling dùng ContinuousScrollController và frame timer; zoom 25–400%.
- Selection, keyboard navigation, drag range, header resize, hidden-axis navigation
  dùng geometry/model chung. Không thực thi lại công thức trong Render/scroll.
- Editor bắt đầu/commit/cancel qua Session.Editor; một view không chiếm draft của
  view khác. Đổi sheet, rebind, detach/dispose dọn draft do chính view sở hữu;
  không khôi phục địa chỉ cũ sau canonical cancellation. Re-enter draft không
  reset text; commit keys được bắt ở tunnel trước native multiline handling.
- C/Cmd+C/X/V hiện là clipboard nội bộ của Nera, UI ghi rõ giới hạn. Chưa nối
  OS clipboard, rich native clipboard hoặc drag/drop dữ liệu.
- Sample: toolbar command thật, formula-bar bridge tới cùng native draft,
  tabs, scrollbar hai chiều, status/zoom, Open/Save XLSX qua serializer chung.
  Toolbar sample KHÔNG được coi là NeraRibbonControl/Ribbon parity.

## Giới hạn bắt buộc giữ OPEN

Full Ribbon/Bars/QAT/customization/iconography; Table/filter popups; split host;
completion/point-mode/reference highlights; native OS clipboard; accessibility
peer/screen reader; IME tiếng Việt thực tế; touchpad/physical DPI/multi-monitor;
print-preview; isolated PackageReference consumer and complete package feed;
performance baselines và toàn bộ A2/A3. Không claim pixel-perfect WPF/Excel.
Sample save staging không phải transaction/atomic disk recovery; shared recovery
hold chưa được giải quyết. Native smoke gọi API trên cửa sổ thật, KHÔNG giả là
physical mouse/keyboard/IME/GPU latency test. Capture dùng loaded visual tree.

## Kiểm thử và bằng chứng

`NeraSpreadSheet.Avalonia.slnx` là solution riêng cho host/sample/tests cùng các
shared project dependencies khai báo tường minh để chúng cùng build Release.
Không sửa solutions của WPF/WinForms/MAUI. MSTest dùng HeadlessUnitTestSession
để giữ UI thread. Workflow Avalonia chạy build/analyzers, headless regressions,
architecture, loaded native window smoke/capture và pack trên Windows/Linux/macOS.
Linux native dùng Xvfb; không thay bằng headless drawing. Success record có
source SHA, frame count, postconditions, OS và loaded assembly location.
PackageReference provenance chưa được claim chỉ vì pack thành công.

Chạy từ repository với SDK của global.json:

```powershell
dotnet build NeraSpreadSheet.Avalonia.slnx -c Release
dotnet test tests/NeraSpreadSheet.Avalonia.Tests -c Release
dotnet run --project samples/NeraSpreadSheet.Avalonia.Sample -c Release
$env:NERA_SOURCE_SHA = (git rev-parse HEAD)
dotnet run --project samples/NeraSpreadSheet.Avalonia.Sample -c Release -- --smoke
```

Trước integration phải review owned delta trên current root và lấy combined
exact-head gates (existing full/iOS/OpenXML/Windows-package/MAUI-package/demo
và Avalonia). Green source branch không thay combined evidence. Không đổi
roadmap percentage hoặc xóa các hold QAT inheritance, Excel-1900 và recovery.
Rollback: bỏ các paths Avalonia mới; không có workbook schema migration.
