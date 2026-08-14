# NeraSpreadSheet

> Trạng thái: **M0 — Bootstrap kiến trúc, chưa phải bản phát hành sử dụng thực tế**.

NeraSpreadSheet là bộ SDK spreadsheet mô-đun cho **WPF, WinForms và .NET MAUI**. Mục tiêu dài hạn là cung cấp trải nghiệm làm việc kiểu bảng tính chuyên nghiệp, ưu tiên tốc độ cuộn theo từng pixel, khả năng mở rộng theo mô-đun và nghiệp vụ dự toán.

## Mục tiêu kỹ thuật đã khóa

- Không tạo một control giao diện cho từng ô.
- Viewport lưu `ScrollX` và `ScrollY` bằng `double`; cho phép dừng giữa hàng hoặc cột.
- Input được gom theo frame; không render lại toàn bộ theo từng sự kiện chuột/touchpad.
- Workbook, công thức, layout và command không phụ thuộc WPF, WinForms hoặc MAUI.
- Windows dùng backend **Direct2D + DirectWrite + composition**.
- Android, iOS và Mac Catalyst dùng backend **Skia GPU**.
- Renderer tiêu thụ một **display list** dùng chung; host nền tảng không chứa nghiệp vụ workbook.
- Cuộn không kích hoạt tính lại toàn workbook, AutoFit hoặc page layout.
- Spreadsheet và DataGrid dùng chung hạ tầng thấp, nhưng không dùng chung mô hình dữ liệu.

## Kiến trúc tổng quát

```text
Workbook / Formula Engine
          |
      Layout Engine
          |
Continuous Pixel Scroll
          |
       Display List
       /          \
Direct2D/DirectWrite   Skia GPU
   /         \             \
 WPF       WinForms         MAUI
```

## Các mô-đun hiện có

| Mô-đun | Vai trò |
|---|---|
| `NeraSpreadSheet.Foundation` | Kiểu hình học và primitive dùng chung |
| `NeraSpreadSheet.Core` | Workbook, worksheet, ô, range và kích thước hàng/cột dạng sparse |
| `NeraSpreadSheet.Formulas` | Hợp đồng engine công thức, dependency và registry hàm |
| `NeraSpreadSheet.Layout` | Chuyển offset pixel thành hàng/cột nhìn thấy; hỗ trợ kích thước biến đổi |
| `NeraSpreadSheet.Scrolling` | Bộ điều khiển cuộn liên tục, precision input và animation theo frame |
| `NeraSpreadSheet.Commands` | Command ID, metadata, registry và handler dùng chung |
| `NeraSpreadSheet.Ribbon.Core` | Schema ribbon trung lập nền tảng |
| `NeraSpreadSheet.Bars.Core` | Schema toolbar, menu và context menu trung lập nền tảng |
| `NeraSpreadSheet.DataGrid.Core` | Hợp đồng DataGrid riêng, không dùng mô hình ô của Spreadsheet |
| `NeraSpreadSheet.Rendering.Abstractions` | Display list và hợp đồng backend render |
| `NeraSpreadSheet.Rendering.Direct2D` | Ranh giới backend Direct2D/DirectWrite trên Windows |
| `NeraSpreadSheet.Rendering.Skia` | Ranh giới backend Skia GPU đa nền tảng |
| `NeraSpreadSheet.OpenXml` | Hợp đồng nhập/xuất XLSX bằng Open XML |
| `NeraSpreadSheet.Wpf` | Host WPF; không render từng ô bằng `FrameworkElement` |
| `NeraSpreadSheet.WinForms` | Host WinForms; không render từng ô bằng `Control` |
| `NeraSpreadSheet.Maui` | Host MAUI, sẽ nối với Skia GPU bằng handler riêng |

## Yêu cầu môi trường

- .NET SDK `10.0.302` hoặc feature band mới hơn của .NET 10.
- Visual Studio 2026/18.x trên Windows để build WPF, WinForms và MAUI Windows.
- Workload MAUI khi build `NeraSpreadSheet.Maui.slnx`.

## Build

```powershell
dotnet restore .\NeraSpreadSheet.slnx
dotnet build .\NeraSpreadSheet.slnx -c Release
dotnet test .\NeraSpreadSheet.Core.slnx -c Release --no-build
```

MAUI được tách khỏi solution mặc định để CI lõi không buộc cài workload di động:

```powershell
dotnet workload install maui
dotnet build .\NeraSpreadSheet.Maui.slnx -c Release
```

## Quy trình nhánh

```text
main       : bản ổn định
  └─ develop
       └─ feature/<ten-tinh-nang>
```

Mọi thay đổi đi qua pull request. Không commit trực tiếp vào `main`. Xem thêm `CONTRIBUTING.md` và `AGENTS.md`.

## Mốc tiếp theo

M1 tập trung vào snapshot workbook, selection model, dirty region, tile/display-list cache và prototype viewport WPF. Chi tiết tại `ROADMAP.md`.

## Giấy phép

Repository chưa công bố giấy phép mã nguồn mở. Việc repository ở trạng thái public không mặc nhiên cấp quyền sao chép, sửa đổi hoặc phân phối.
