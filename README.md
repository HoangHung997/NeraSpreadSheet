# NeraSpreadSheet

> Trạng thái: **M1 — nền tảng spreadsheet engine, renderer đa host và XLSX interoperability đã được CI xác minh; chưa phải bản phát hành production**.

NeraSpreadSheet là bộ SDK spreadsheet độc lập cho **WPF, WinForms và .NET MAUI**. Mục tiêu dài hạn là trải nghiệm bảng tính chuyên nghiệp, cuộn liên tục theo từng pixel, mô hình dữ liệu sparse và khả năng mở rộng cho nghiệp vụ dự toán.

## Nguyên tắc kỹ thuật đã khóa

- Không tạo một control giao diện cho từng ô.
- Viewport lưu `ScrollX` và `ScrollY` bằng `double`, có thể dừng giữa hàng hoặc cột.
- Cuộn không kích hoạt tính lại toàn workbook, AutoFit hoặc page layout.
- Workbook, công thức, layout, scrolling và command không phụ thuộc WPF, WinForms hoặc MAUI.
- Windows có WPF DrawingContext/D3DImage và WinForms GDI+/Direct2D/D3D11-DXGI.
- Android, iOS, Mac Catalyst và MAUI Windows dùng một public `SKGLView` cùng Skia GPU.
- Mọi backend tiêu thụ display list dùng chung; host nền tảng không chứa nghiệp vụ workbook.
- Spreadsheet và DataGrid chia sẻ hạ tầng thấp nhưng không dùng chung mô hình dữ liệu.
- Excel, LibreOffice và DevExpress chỉ là nguồn tham khảo hành vi; không phải runtime dependency.

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

## Những phần đã có gate thực thi

- Workbook/worksheet sparse trên không gian địa chỉ cỡ Excel.
- Selection, clipboard, editor, command và undo/redo dữ liệu/view.
- Insert/delete/reorder hàng cột có formula/rule/Table mapping và rollback nguyên tử.
- Whole-row/column/sheet style dạng sparse, không materialize trục logic.
- Cuộn phân số, freeze pane, split pane, pane-local scroll và tile/display-list cache.
- WPF, WinForms và MAUI GPU hosts cùng recovery/context lifecycle diagnostics.
- Formula parser, dependency graph, circular-reference policy, shared formulas và các hàm nền tảng.
- Conditional Formatting và Data Validation đi từ Core model tới renderer, editor gate và XLSX round-trip.
- Table Core model có stable Table/column identity, structured-reference evaluation/rewrite và AutoFilter row projection.
- Hàng bị lọc được nén thành span trong metric index; viewport, content extent và hit-test bỏ qua hàng đó mà không ghi đè chiều cao gốc.
- XLSX values/formulas/dimensions/merge, style table, sparse style state, panes, conditional formatting, validation và standard table parts.
- `PreserveUnknownParts=true` theo mô hình copy-and-patch, giữ opaque relationship graph và Table `extLst` qua repeated save.
- Drawing + image, custom XML + properties, package-root/nested/external relationships được kiểm tra bằng fixture và `OpenXmlValidator`.
- Package graph preflight chặn URI traversal, duplicate/invalid relationship ID, invalid relationship type và target chứa control character trước workbook restoration hoặc destination mutation.

Chi tiết chính xác nằm tại `docs/current-status.md`; thứ tự phần việc còn lại nằm tại `ROADMAP.md`; contract Table nằm tại `docs/table-structured-reference-contract.md`.

## Các mô-đun chính

| Mô-đun | Vai trò |
|---|---|
| `NeraSpreadSheet.Foundation` | Geometry và primitive dùng chung |
| `NeraSpreadSheet.Core` | Workbook, worksheet, cell/range, Table, rule, merge, style và sparse dimensions |
| `NeraSpreadSheet.Formulas` | Parser, AST, structured-reference expansion, dependency, recalculation và registry hàm |
| `NeraSpreadSheet.Layout` | Ánh xạ offset pixel sang hàng/cột hiển thị và compressed hidden spans |
| `NeraSpreadSheet.Scrolling` | Cuộn liên tục, precision input và animation theo frame |
| `NeraSpreadSheet.Commands` | Command ID, metadata, registry và handler |
| `NeraSpreadSheet.Editing` | Session, editor, clipboard, structural operations và Table/rule history |
| `NeraSpreadSheet.Rendering.Abstractions` | Display list và hợp đồng backend |
| `NeraSpreadSheet.Rendering.Direct2D` | Direct2D/DirectWrite backend cho Windows |
| `NeraSpreadSheet.Rendering.Skia` | Skia display-list renderer đa nền tảng |
| `NeraSpreadSheet.OpenXml` | Biên nhập/xuất XLSX và package preservation |
| `NeraSpreadSheet.Wpf` | WPF host, không render từng ô bằng `FrameworkElement` |
| `NeraSpreadSheet.WinForms` | WinForms host, không render từng ô bằng `Control` |
| `NeraSpreadSheet.Maui` | Native GPU/touch host trên một `SKGLView` |
| `NeraSpreadSheet.Ribbon.Core` | Schema Ribbon trung lập nền tảng |
| `NeraSpreadSheet.Bars.Core` | Schema toolbar/menu/context menu |
| `NeraSpreadSheet.DataGrid.Core` | Hợp đồng DataGrid riêng biệt |

## Build và test

Yêu cầu .NET 10 SDK theo `global.json`; Visual Studio 2026/18.x và workload MAUI khi build các target Windows/mobile.

```powershell
dotnet restore .\NeraSpreadSheet.slnx
dotnet build .\NeraSpreadSheet.slnx -c Release
dotnet test .\NeraSpreadSheet.Core.slnx -c Release --no-build
```

MAUI target matrix:

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

Mọi thay đổi đi qua pull request. Không commit trực tiếp vào `main`.

## Mốc tiếp theo

Mốc kỹ thuật kế tiếp là **calculated-column propagation, totals execution và native Table/AutoFilter UX**. Sau đó mở rộng hệ thống hàm, dynamic arrays, advanced data analysis, printing/PDF, charts/pivot và các gate phát hành production.

## Giấy phép

Repository chưa công bố giấy phép mã nguồn mở. Việc repository ở trạng thái public không mặc nhiên cấp quyền sao chép, sửa đổi hoặc phân phối.
