# NeraSpreadSheet

> Trạng thái: **M2 — spreadsheet engine, renderer đa host, XLSX/Table/AutoFilter, printing/PDF, Formula Surface I và Dynamic Arrays Foundation đã có automated gates; chưa phải bản phát hành production**.

NeraSpreadSheet là bộ SDK spreadsheet độc lập cho **WPF, WinForms và .NET MAUI**. Mục tiêu dài hạn là trải nghiệm bảng tính chuyên nghiệp, cuộn liên tục theo từng pixel, mô hình dữ liệu sparse, tương thích tài liệu tốt và khả năng mở rộng cho nghiệp vụ dự toán.

## Nguyên tắc kỹ thuật

- Không tạo một control giao diện cho từng ô.
- Viewport và print preview lưu offset bằng `double`, có thể dừng giữa hàng hoặc cột.
- Workbook, công thức, dynamic arrays, layout, scrolling, printing và command không phụ thuộc WPF, WinForms hoặc MAUI.
- Mọi backend tiêu thụ display list dùng chung; host nền tảng không chứa nghiệp vụ workbook.
- Native filter/preview controls chỉ materialize phần đang nhìn thấy hoặc một page dữ liệu có giới hạn.
- Spill children là derived output của một owner formula, không phải các công thức độc lập.
- Excel, LibreOffice và DevExpress chỉ là nguồn tham khảo hành vi; không phải runtime dependency.

## Kiến trúc tổng quát

```text
Workbook / Formula / Rules / Tables / Spill Ownership
                        |
            Layout + Page Layout
                        |
Continuous Pixel Scroll / Print Preview
                        |
                   Display List
                 /       |       \
        Direct2D/GDI+  Skia GPU  Skia PDF
           /      \        |          |
         WPF    WinForms   MAUI       PDF
```

## Những phần đã có automated gates

- Workbook/worksheet sparse trên không gian địa chỉ cỡ Excel.
- Selection, clipboard, editor, command và Undo/Redo dữ liệu/view.
- Insert/delete/reorder hàng cột có formula/rule/Table/filter/spill mapping và rollback nguyên tử.
- Whole-row/column/sheet style dạng sparse, không materialize trục logic.
- Cuộn phân số, freeze pane, split pane, pane-local scroll và tile/display-list cache.
- WPF, WinForms và MAUI GPU hosts cùng recovery/context lifecycle diagnostics.
- Formula parser, dependency graph, circular-reference policy, shared formulas và structured references.
- **109 tên hàm được nhận biết**: 92 hàm registry, 12 hàm AST/reference-aware và 5 hàm dynamic-array.
- Logical/error, aggregate, math/rounding/trigonometry, text/Unicode, date/time và basic lookup/reference functions.
- Dynamic arrays: `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE`.
- Immutable array values, owner/child spill identity, collision preflight, `#SPILL!`, affected recalculation và bounded stabilization.
- Spill-aware editing, Undo/Redo, structural operations, snapshots, clipboard và XLSX document serialization.
- Conditional Formatting và Data Validation từ Core model tới renderer, editor gate và XLSX round-trip.
- Table model có stable identity, calculated columns, filter-aware totals và AutoFilter row projection.
- Direct worksheet AutoFilter dùng chung predicate engine và compressed hidden-row spans với Table.
- Generation-guarded paged filter sessions và native WPF/WinForms/MAUI presenter foundations.
- XLSX values/formulas/dimensions/merges, styles, panes, shared formulas, rules, Tables, filters, print settings và unknown-part preservation.
- Core page setup, deterministic pagination, virtualized native preview, staged PDF, WPF paginator và WinForms `PrintDocument`.
- Streaming CSV/TSV import/export, cancellation, output limits và formula-injection protection.
- `scripts/run-complete-validation.ps1` và `docs/CODEX_FINAL_ACCEPTANCE.md` cho validation cuối trên phần cứng, máy in, thiết bị và corpus thực.

Chi tiết chính xác nằm tại `docs/current-status.md`; Formula Surface I nằm tại `docs/formula-surface-i-contract.md`; Dynamic Arrays nằm tại `docs/dynamic-arrays-contract.md`; thứ tự phần việc còn lại nằm tại `ROADMAP.md`.

## Các mô-đun chính

| Mô-đun | Vai trò |
|---|---|
| `NeraSpreadSheet.Foundation` | Geometry và primitive dùng chung |
| `NeraSpreadSheet.Core` | Workbook, worksheet, cell/range, Table, rule, style, dimensions, print settings và array/spill contracts |
| `NeraSpreadSheet.Formulas` | Parser, AST, coercion/error, scalar/dynamic function engines, dependency và recalculation |
| `NeraSpreadSheet.Layout` | Ánh xạ offset pixel sang hàng/cột và compressed hidden spans |
| `NeraSpreadSheet.Scrolling` | Cuộn liên tục, precision input và animation theo frame |
| `NeraSpreadSheet.Commands` | Command ID, metadata, registry và handler |
| `NeraSpreadSheet.Editing` | Session, editor, spill-aware clipboard/structure, filter paging, CSV/TSV và history |
| `NeraSpreadSheet.Rendering.*` | Display list, Direct2D, Skia, spreadsheet và print composition |
| `NeraSpreadSheet.Export.Pdf` | Worksheet/workbook/print-ticket PDF orchestration |
| `NeraSpreadSheet.OpenXml` | XLSX, dynamic spill cleanup, print settings và package preservation |
| `NeraSpreadSheet.Wpf` | WPF spreadsheet, filters, preview và paginator |
| `NeraSpreadSheet.WinForms` | WinForms spreadsheet, filters, preview và `PrintDocument` |
| `NeraSpreadSheet.Maui` | GPU/touch spreadsheet, paged filters và Skia preview |

## Build và test

Yêu cầu .NET 10 SDK theo `global.json`; Visual Studio 2026/18.x và workload MAUI khi build các target Windows/mobile.

```powershell
dotnet restore .\NeraSpreadSheet.slnx
dotnet build .\NeraSpreadSheet.slnx -c Release
dotnet test .\NeraSpreadSheet.Core.slnx -c Release --no-build
./scripts/run-complete-validation.ps1 -Configuration Release -RequireCleanWorkingTree
```

## Quy trình nhánh

```text
main       : bản ổn định
  └─ develop
       └─ feature/<ten-tinh-nang>
```

Mọi thay đổi đi qua pull request. Không commit trực tiếp vào `main`.

## Mốc tiếp theo

Mốc tiếp theo là **Versioned Function Extension SDK + Conditional Aggregate Functions**, sau đó mở rộng statistical/financial functions, advanced dynamic-array syntax, drawings/charts, advanced data/pivot và release hardening.

## Giấy phép

Repository chưa công bố giấy phép mã nguồn mở. Việc repository ở trạng thái public không mặc nhiên cấp quyền sao chép, sửa đổi hoặc phân phối.
