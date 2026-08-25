# NeraSpreadSheet

> Trạng thái: **M2 — spreadsheet engine độc lập, renderer đa host, XLSX/Table/AutoFilter, printing/PDF, Dynamic Arrays, Function Extension SDK v1.0 và các nền tảng hàm chính đã có automated gates; chưa phải bản phát hành production**.

NeraSpreadSheet là SDK spreadsheet cho **WPF, WinForms và .NET MAUI**, hướng tới cuộn liên tục theo pixel, mô hình sparse và không phụ thuộc runtime Excel, LibreOffice hoặc DevExpress.

## Nguyên tắc kỹ thuật

- Không tạo native control cho từng ô.
- Workbook, formula engine, dynamic arrays, layout, scrolling, calendar và printing không phụ thuộc host UI.
- Extension functions khai báo identity, version, capabilities, volatility/state, dependency và argument-count policy.
- Solver, schedule loop, calendar traversal và numerical primitive đều deterministic, bounded và fail closed.
- Built-ins chỉ đi qua một đường tổng hợp registry.
- Financial day-count, odd-coupon, business-day và locale-number semantics dùng các service platform-neutral.

## Kiến trúc

```text
Workbook / Rules / Tables / Spill Ownership
                    |
 Formula Parser + Versioned Function Registry
                    |
 Criteria / Statistics / Finance / Engineering / Database
                    |
Financial + Date/Week + Business Calendar + Locale Number
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

## Toàn cảnh dự án đã có automated gates

| Khối | Năng lực hiện tại |
|---|---|
| Workbook lõi | Sparse Excel-size, values/formulas/styles/dimensions/merges, immutable snapshots và bounded caches |
| Editing | Selection đa vùng, editor, clipboard spill-aware, commands, sort và Undo/Redo |
| Structural transforms | Formula/rule/Table/filter/spill mapping nguyên tử khi chèn/xóa/di chuyển cấu trúc |
| Formula engine | Parser/AST, A1/cross-sheet, dependency graph, circular detection và affected-only recalculation |
| Formula SDK | API `1.0`, version/capability/state/security/dependency/conflict contracts và một registration path |
| Built-ins | **261 tên**: 238 eager/versioned, 18 AST/reference-aware và 5 dynamic-array |
| Finance | **56 hàm**, hoàn thành F001–F006 |
| Calendar/locale F007 | `NETWORKDAYS`, `NETWORKDAYS.INTL`, `WORKDAY`, `WORKDAY.INTL`, `NUMBERVALUE` |
| Data/rules | Conditional Formatting, Data Validation, Tables, AutoFilter, totals, sort và paged presenters |
| Dynamic arrays | Immutable spills cùng `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE` |
| Rendering | Fractional pixel scrolling, freeze/split panes, WPF/WinForms/MAUI display-list GPU hosts |
| File/print | XLSX preservation, streaming CSV/TSV, deterministic pagination, preview, staged PDF và desktop print adapters |
| Validation | Core/Windows/Android/iOS/Mac Catalyst/MAUI Windows exact-head CI matrix |

## Formula milestones gần nhất

- F003: `PRICE`, `YIELD`, `DURATION`, `MDURATION`, `MIRR`.
- F004: `TBILLEQ`, `TBILLPRICE`, `TBILLYIELD`, `DOLLARDE`, `DOLLARFR`.
- F005: `AMORLINC`, `AMORDEGRC`, `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE`.
- F006: `ODDLYIELD`, `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM`.
- F007: `NETWORKDAYS`, `NETWORKDAYS.INTL`, `WORKDAY`, `WORKDAY.INTL`, `NUMBERVALUE`.
- Holiday ranges giữ dependency source, loại duplicate/weekend và giới hạn 2.000.000 giá trị.
- Workday shifting dùng week counting và bounded binary search thay vì quét từng ngày.
- `NUMBERVALUE` dùng explicit separator hoặc `IFormulaLocaleEvaluationContext`, không đọc process-global culture.

Tài liệu nguồn sự thật:

- `docs/current-status.md`;
- `docs/feature-matrix.md`;
- `docs/business-calendar-and-numbervalue-contract.md`;
- `docs/formula-completion-master-schedule.md`;
- `ROADMAP.md`.

## Build và test

```powershell
dotnet restore .\NeraSpreadSheet.slnx
dotnet build .\NeraSpreadSheet.slnx -c Release
dotnet test .\NeraSpreadSheet.Core.slnx -c Release --no-build
./scripts/run-complete-validation.ps1 -Configuration Release -RequireCleanWorkingTree
```

## Nhánh

```text
main
  └─ develop
       └─ feature/<ten-tinh-nang>
```

Mọi thay đổi đi qua pull request; không commit trực tiếp vào `main`.

## Mốc tiếp theo

**F008:** `ADDRESS`, `AREAS`, `CHOOSE`, `CHOOSECOLS`, `CHOOSEROWS`. Sau mỗi đúng năm hàm và exact-head CI xanh, hệ thống báo một bảng tổng thể toàn dự án rồi tự tiếp tục batch sau.

## Giấy phép

Repository chưa công bố giấy phép mã nguồn mở. Repository public không mặc nhiên cấp quyền sao chép, sửa đổi hoặc phân phối.
