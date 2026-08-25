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

## Phần đã có automated gates

- Sparse workbook/worksheet, editing, commands, clipboard và Undo/Redo.
- Structural transforms có formula/rule/Table/filter/spill mapping.
- Fractional scrolling, freeze/split panes và multi-host rendering.
- Function Extension SDK API `1.0`.
- **246 built-in function names**: 223 eager/versioned, 18 AST/reference-aware và 5 dynamic-array.
- Finance hiện có **50 hàm**.
- F003 bổ sung `PRICE`, `YIELD`, `DURATION`, `MDURATION`, `MIRR`.
- F004 bổ sung `TBILLEQ`, `TBILLPRICE`, `TBILLYIELD`, `DOLLARDE`, `DOLLARFR`.
- Fixed-coupon price/yield dùng cùng coupon state; yield solver bị chặn số vòng lặp; duration/modified duration và MIRR có regression đối chiếu/round-trip.
- Treasury-bill functions dùng actual settlement-to-maturity days và giới hạn một năm lịch; DOLLAR functions truncate mẫu số, khóa lỗi và round-trip cả số âm.
- Count regression được gom về một hằng test chung để mỗi batch chỉ cập nhật một vị trí.
- Dynamic arrays, Conditional Formatting, Data Validation, Tables, AutoFilter, XLSX, pagination, staged PDF và streaming CSV/TSV.

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

**F005:** `AMORLINC`, `AMORDEGRC`, `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE`. Sau mỗi đúng năm hàm và exact-head CI xanh, hệ thống báo một bảng rồi tự tiếp tục batch sau.

## Giấy phép

Repository chưa công bố giấy phép mã nguồn mở. Repository public không mặc nhiên cấp quyền sao chép, sửa đổi hoặc phân phối.
