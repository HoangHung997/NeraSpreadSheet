# NeraSpreadSheet

> Trạng thái: **M2 — spreadsheet engine độc lập, renderer đa host, XLSX/Table/AutoFilter, printing/PDF, Dynamic Arrays và Function Extension SDK v1.0 đã có automated gates; chưa phải production release**.

NeraSpreadSheet là SDK spreadsheet cho **WPF, WinForms và .NET MAUI**, hướng tới cuộn liên tục theo pixel, mô hình sparse và không phụ thuộc runtime Excel, LibreOffice hoặc DevExpress.

## Trạng thái chính

| Khối | Năng lực hiện tại |
|---|---|
| Workbook | Sparse Excel-size, formulas/styles/dimensions/merges, snapshots và atomic structural transforms |
| Formula engine | Parser/AST, A1/cross-sheet, lazy reference selection, dependency graph và affected-only recalculation |
| Built-ins | **271 tên**: 239 eager/versioned, 23 AST/reference-aware và 9 dynamic-array |
| Dynamic arrays | `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE`, `CHOOSECOLS`, `CHOOSEROWS`, `DROP`, `EXPAND` |
| Reference/introspection | `ADDRESS`, `AREAS`, `CHOOSE`, `COLUMN`, `COLUMNS`, `FORMULATEXT` |
| Rendering | Fractional pixel scrolling, freeze/split panes và WPF/WinForms/MAUI GPU hosts |
| File/print | XLSX preservation, CSV/TSV, pagination, preview, PDF và desktop print adapters |
| Validation | Core/Windows/Android/iOS/Mac Catalyst/MAUI Windows exact-head CI |

**Tổng số hàm: 271 / tối thiểu 538 hàm mục tiêu hiện đã khóa.** P11 catalog audit có thể làm tổng mục tiêu tăng thêm.

## Formula milestones gần nhất

- F007: `NETWORKDAYS`, `NETWORKDAYS.INTL`, `WORKDAY`, `WORKDAY.INTL`, `NUMBERVALUE`.
- F008: `ADDRESS`, `AREAS`, `CHOOSE`, `CHOOSECOLS`, `CHOOSEROWS`.
- F009: `COLUMN`, `COLUMNS`, `DROP`, `EXPAND`, `FORMULATEXT`.
- F010 tiếp theo: `GETPIVOTDATA`, `GROUPBY`, `HSTACK`, `HYPERLINK`, `INDIRECT`.

## Build và test

```powershell
dotnet restore .\NeraSpreadSheet.slnx
dotnet build .\NeraSpreadSheet.slnx -c Release
dotnet test .\NeraSpreadSheet.Core.slnx -c Release --no-build
./scripts/run-complete-validation.ps1 -Configuration Release -RequireCleanWorkingTree
```

Mọi thay đổi đi qua pull request; không commit trực tiếp vào `main`.

## Giấy phép

Repository chưa công bố giấy phép mã nguồn mở. Repository public không mặc nhiên cấp quyền sao chép, sửa đổi hoặc phân phối.
