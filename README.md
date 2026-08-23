# NeraSpreadSheet

> Trạng thái: **M2 — spreadsheet engine, renderer đa host, XLSX/Table/AutoFilter, printing/PDF, Dynamic Arrays, Function Extension SDK v1.0, Conditional Aggregates, Statistical và Financial Functions Foundation đã có automated gates; chưa phải bản phát hành production**.

NeraSpreadSheet là bộ SDK spreadsheet độc lập cho **WPF, WinForms và .NET MAUI**. Mục tiêu dài hạn là trải nghiệm bảng tính chuyên nghiệp, cuộn liên tục theo từng pixel, mô hình dữ liệu sparse, tương thích tài liệu tốt và khả năng mở rộng cho nghiệp vụ dự toán.

## Nguyên tắc kỹ thuật

- Không tạo một control giao diện cho từng ô.
- Viewport và print preview lưu offset bằng `double`, có thể dừng giữa hàng hoặc cột.
- Workbook, công thức, extension functions, dynamic arrays, layout, scrolling và printing không phụ thuộc WPF, WinForms hoặc MAUI.
- Mọi backend tiêu thụ display list dùng chung; host nền tảng không chứa nghiệp vụ workbook.
- Native filter/preview controls chỉ materialize phần đang nhìn thấy hoặc một page dữ liệu có giới hạn.
- Spill children là derived output của một owner formula, không phải công thức độc lập.
- Extension functions phải khai báo identity, version, capabilities, volatility/state và dependency policy trước khi đăng ký.
- Excel, LibreOffice và DevExpress chỉ là nguồn tham khảo hành vi; không phải runtime dependency.

## Kiến trúc tổng quát

```text
Workbook / Rules / Tables / Spill Ownership
                    |
 Formula Parser + Versioned Function Registry
                    |
         Dependency + Recalculation
                    |
          Layout + Page Layout
                    |
 Continuous Scroll / Print Preview
                    |
               Display List
             /       |       \
    Direct2D/GDI+  Skia GPU  Skia PDF
       /      \        |          |
     WPF    WinForms   MAUI       PDF
```

## Những phần đã có automated gates

- Workbook/worksheet sparse trên không gian địa chỉ cỡ Excel.
- Selection, spill-aware clipboard, editor, command và Undo/Redo dữ liệu/view.
- Insert/delete/reorder có formula/rule/Table/filter/spill mapping và rollback nguyên tử.
- Sparse whole-row/column styles, fractional scrolling, freeze/split panes và tile/display-list cache.
- WPF, WinForms và MAUI GPU hosts cùng recovery/context lifecycle diagnostics.
- Formula parser, dependency graph, circular-reference policy, shared và structured references.
- **136 tên hàm built-in được nhận biết**: 113 eager registry, 18 AST/reference-aware và 5 dynamic-array.
- Logical/error, aggregate, math, text/Unicode, date/time, lookup, conditional aggregate, statistical và financial functions.
- Statistical Foundation: `MEDIAN`, `MODE.SNGL`, `PERCENTILE.INC`, `QUARTILE.INC`, `VAR.P`, `VAR.S`, `STDEV.P`, `STDEV.S`, `RANK.EQ`, `LARGE`, `SMALL`.
- Financial Foundation: `PV`, `FV`, `PMT`, `NPER`, `NPV`, `IRR`, `IPMT`, `PPMT`, `SLN`, `SYD`.
- Conditional Aggregates: `COUNTIF(S)`, `SUMIF(S)`, `AVERAGEIF(S)` với criteria/dependency/budget contracts.
- Function Extension SDK API `1.0`: identity, versions, capabilities, volatility/state, dependencies, aliases và compatibility gates.
- Dynamic arrays: `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE`, spill ownership, `#SPILL!`, recalculation, history, clipboard và XLSX boundary.
- Conditional Formatting, Data Validation, Tables, worksheet AutoFilter và paged native presenters.
- XLSX values/formulas/styles/panes/rules/Tables/filters/printing và unknown-part preservation.
- Deterministic pagination, virtualized preview, staged PDF và native printer adapters.
- Streaming CSV/TSV, cancellation, output limits và formula-injection protection.
- Repository validation runner và final Codex acceptance plan.

Chi tiết chính xác:

- `docs/current-status.md`;
- `docs/formula-surface-i-contract.md`;
- `docs/function-extension-sdk-contract.md`;
- `docs/conditional-aggregates-contract.md`;
- `docs/statistical-functions-foundation-contract.md`;
- `docs/financial-functions-foundation-contract.md`;
- `docs/dynamic-arrays-contract.md`;
- `ROADMAP.md`.

## Các mô-đun chính

| Mô-đun | Vai trò |
|---|---|
| `NeraSpreadSheet.Foundation` | Geometry và primitive dùng chung |
| `NeraSpreadSheet.Core` | Workbook, worksheet, Table/rule/style/dimensions, printing và array/spill contracts |
| `NeraSpreadSheet.Formulas` | Parser, scalar/dynamic functions, SDK, criteria, statistics, finance, dependency và recalculation |
| `NeraSpreadSheet.Layout` | Pixel offset, visible rows/columns và compressed hidden spans |
| `NeraSpreadSheet.Scrolling` | Continuous precision scrolling |
| `NeraSpreadSheet.Commands` | Command metadata/registry/handlers |
| `NeraSpreadSheet.Editing` | Session, editor, clipboard, structure, filter paging, CSV/TSV và history |
| `NeraSpreadSheet.Rendering.*` | Shared display list, Direct2D, Skia, spreadsheet và print composition |
| `NeraSpreadSheet.Export.Pdf` | Worksheet/workbook/print-ticket PDF |
| `NeraSpreadSheet.OpenXml` | XLSX, spill cleanup, print settings và package preservation |
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
main
  └─ develop
       └─ feature/<ten-tinh-nang>
```

Mọi thay đổi đi qua pull request. Không commit trực tiếp vào `main`.

## Mốc tiếp theo

Mốc tiếp theo tập trung vào **Engineering và Database Functions Foundation**, sau đó là advanced statistics/lookup/dynamic arrays, plugin packaging/isolation, drawings/charts, advanced data/pivot và release hardening.

## Giấy phép

Repository chưa công bố giấy phép mã nguồn mở. Việc repository public không mặc nhiên cấp quyền sao chép, sửa đổi hoặc phân phối.
