# NeraSpreadSheet

> Trạng thái: **M2 — spreadsheet engine độc lập, renderer đa host, XLSX/Table/AutoFilter, printing/PDF, Dynamic Arrays, Function Extension SDK v1.0 và các nền tảng hàm chính đã có automated gates; chưa phải bản phát hành production**.

NeraSpreadSheet là SDK spreadsheet cho **WPF, WinForms và .NET MAUI**, hướng tới cuộn liên tục theo pixel, mô hình sparse và không phụ thuộc runtime Excel, LibreOffice hoặc DevExpress.

## Nguyên tắc kỹ thuật

- Không tạo native control cho từng ô.
- Workbook, formula engine, dynamic arrays, layout, scrolling và printing không phụ thuộc host UI.
- Extension functions khai báo identity, version, capabilities, volatility/state, dependency và argument-count policy.
- Solver, schedule loop và numerical primitive đều deterministic, bounded và fail closed.
- Built-ins chỉ đi qua một đường tổng hợp registry.
- Lịch/day-count tài chính dùng một lớp platform-neutral chung cho coupon và securities.

## Kiến trúc

```text
Workbook / Rules / Tables / Spill Ownership
                    |
 Formula Parser + Versioned Function Registry
                    |
 Criteria / Statistics / Finance / Engineering / Database
                    |
 Financial Calendar + Security/Coupon Equations
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

| Khối | Trạng thái hiện tại |
|---|---|
| Workbook lõi | Sparse Excel-size, values/formulas/styles/dimensions/merges, immutable snapshots và bounded caches |
| Editing | Selection đa vùng, editor, clipboard spill-aware, commands, sort và Undo/Redo |
| Structural transforms | Formula/rule/Table/filter/spill mapping nguyên tử khi chèn/xóa/di chuyển cấu trúc |
| Formula engine | Parser/AST, A1/cross-sheet, dependency graph, circular detection và affected-only recalculation |
| Formula SDK | API `1.0`, version/capability/state/security/dependency/conflict contracts và một registration path |
| Built-ins | **251 tên**: 228 eager/versioned, 18 AST/reference-aware và 5 dynamic-array |
| Finance | **55 hàm**, hoàn thành F001–F005 |
| Data/rules | Conditional Formatting, Data Validation, Tables, AutoFilter, totals, sort và paged presenters |
| Dynamic arrays | Immutable spills cùng `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE` |
| Rendering | Fractional pixel scrolling, freeze/split panes, WPF/WinForms/MAUI display-list GPU hosts |
| File/print | XLSX preservation, streaming CSV/TSV, deterministic pagination, preview, staged PDF và desktop print adapters |
| Validation | Core/Windows/Android/iOS/Mac Catalyst/MAUI Windows exact-head CI matrix |

## Formula milestones gần nhất

- F003: `PRICE`, `YIELD`, `DURATION`, `MDURATION`, `MIRR`.
- F004: `TBILLEQ`, `TBILLPRICE`, `TBILLYIELD`, `DOLLARDE`, `DOLLARFR`.
- F005: `AMORLINC`, `AMORDEGRC`, `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE`.
- F005 bổ sung khấu hao theo quy ước kế toán Pháp, quasi-coupon ratio dùng lớp ngày chung, odd-first price/yield round trip và odd-last price.
- Registry-count regression được gom về một hằng test chung để mỗi batch chỉ cập nhật một vị trí.

Tài liệu nguồn sự thật:

- `docs/current-status.md`;
- `docs/financial-functions-foundation-contract.md`;
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

**F006:** `ODDLYIELD`, `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM`. Sau mỗi đúng năm hàm và exact-head CI xanh, hệ thống báo một bảng rồi tự tiếp tục batch sau.

## Giấy phép

Repository chưa công bố giấy phép mã nguồn mở. Repository public không mặc nhiên cấp quyền sao chép, sửa đổi hoặc phân phối.
