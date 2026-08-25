# NeraSpreadSheet

> Trạng thái: **M2 — spreadsheet engine độc lập, renderer đa host, XLSX/Table/AutoFilter, printing/PDF, Dynamic Arrays, Function Extension SDK v1.0 và các nền tảng hàm chính đã có automated gates; chưa phải bản phát hành production**.

NeraSpreadSheet là SDK spreadsheet cho **WPF, WinForms và .NET MAUI**, hướng tới cuộn liên tục theo pixel, mô hình sparse, khả năng mở rộng nghiệp vụ và không phụ thuộc runtime Excel, LibreOffice hoặc DevExpress.

## Nguyên tắc kỹ thuật

- Không tạo native control cho từng ô.
- Workbook, formula engine, dynamic arrays, layout, scrolling và printing không phụ thuộc host UI.
- Extension functions khai báo identity, version, capabilities, volatility/state, dependency và argument-count policy.
- Mọi solver, schedule loop và numerical primitive đều deterministic, bounded và fail closed.
- Built-ins chỉ đi qua một đường tổng hợp registry duy nhất.
- Lịch và day-count tài chính dùng một lớp platform-neutral chung cho coupon và security functions.

## Kiến trúc

```text
Workbook / Rules / Tables / Spill Ownership
                    |
 Formula Parser + Versioned Function Registry
                    |
 Criteria / Statistics / Finance / Engineering / Database
                    |
      Financial Calendar + Security Equations
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
- Structural insert/delete/reorder có formula/rule/Table/filter/spill mapping.
- Fractional scrolling, freeze/split panes và multi-host display-list rendering.
- Function Extension SDK API `1.0`.
- **231 built-in function names**: 208 eager/versioned, 18 AST/reference-aware và 5 dynamic-array.
- Conditional aggregates, statistical, advanced statistical, financial, engineering và database foundations.
- Finance hiện có **35 hàm**. F001 bổ sung `ACCRINTM`, `DISC`, `INTRATE`, `RECEIVED`, `PRICEDISC` trên cùng basis `0..4` đã khóa.
- Official/reference values, date/value domains, scalar capabilities và `DISC` ↔ `PRICEDISC` round trip được kiểm thử.
- Dynamic arrays: `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE` cùng spill ownership và `#SPILL!`.
- Conditional Formatting, Data Validation, Tables, AutoFilter, XLSX, pagination, staged PDF và streaming CSV/TSV.

Tài liệu nguồn sự thật:

- `docs/current-status.md`;
- `docs/function-extension-sdk-contract.md`;
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

**F002:** `YIELDDISC`, `PRICEMAT`, `YIELDMAT`, `ACCRINT`, `FVSCHEDULE`. Sau mỗi đúng năm hàm và exact-head CI xanh, một bảng tiến độ được phát ra rồi hàng đợi tự chuyển sang batch kế tiếp.

## Giấy phép

Repository chưa công bố giấy phép mã nguồn mở. Repository public không mặc nhiên cấp quyền sao chép, sửa đổi hoặc phân phối.
