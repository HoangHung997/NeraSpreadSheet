# NeraSpreadSheet

> Trạng thái: **M2 — spreadsheet engine, renderer đa host, XLSX/Table/AutoFilter, printing/PDF, Dynamic Arrays, Function Extension SDK v1.0, Conditional Aggregates, Statistical, Financial, Engineering và Database Functions Foundation đã có automated gates; chưa phải bản phát hành production**.

NeraSpreadSheet là SDK spreadsheet độc lập cho **WPF, WinForms và .NET MAUI**, hướng tới cuộn liên tục theo pixel, mô hình sparse, tương thích tài liệu tốt và khả năng mở rộng nghiệp vụ dự toán.

## Nguyên tắc kỹ thuật

- Không tạo native control cho từng ô.
- Viewport và print preview dùng offset `double`.
- Workbook, formula engine, extension functions, dynamic arrays, layout, scrolling và printing không phụ thuộc host UI.
- Spill children là derived output của một owner formula.
- Extension functions khai báo identity, version, capabilities, volatility/state, dependency và argument-count policy.
- Mọi formula family có resource budget và fail-closed behavior.
- Numerical solvers và schedule loops phải deterministic, bounded, không trả kết quả chưa hội tụ hoặc vượt ngân sách.
- Excel, LibreOffice và DevExpress chỉ là nguồn tham khảo hành vi, không phải runtime dependency.

## Kiến trúc

```text
Workbook / Rules / Tables / Spill Ownership
                    |
 Formula Parser + Versioned Function Registry
                    |
 Criteria / Statistics / Finance / Engineering / Database
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

- Sparse workbook/worksheet, selection, clipboard, editor, commands và Undo/Redo.
- Structural insert/delete/reorder có formula/rule/Table/filter/spill mapping và rollback.
- Fractional scrolling, freeze/split panes và multi-host display-list rendering.
- Function Extension SDK API `1.0`.
- **214 built-in function names**: 191 eager/versioned, 18 AST/reference-aware và 5 dynamic-array.
- Conditional aggregates, statistical, advanced statistical, financial, engineering và database function foundations.
- Finance: `PV`, `FV`, `PMT`, `NPER`, `RATE`, `NPV`, `IRR`, `XNPV`, `XIRR`, `IPMT`, `PPMT`, `CUMIPMT`, `CUMPRINC`, `SLN`, `SYD`, `DB`, `DDB`, `VDB`.
- `CUMIPMT`/`CUMPRINC` dùng cùng schedule/sign/timing contract với `PMT`/`IPMT`/`PPMT`.
- `DB` khóa fixed declining rate làm tròn ba chữ số, `DDB` khóa declining-factor cap, `VDB` hỗ trợ partial periods và chuyển sang straight-line.
- Các vòng schedule/depreciation bị giới hạn ở 2.000.000 kỳ.
- Advanced statistics gồm covariance/correlation/regression và normal/log-normal/exponential/binomial/Poisson/Weibull/beta/gamma/chi-square/Student-t/F.
- Engineering gồm bitwise/shift, radix conversions, `DELTA`, `GESTEP`.
- Database gồm 12 criteria-table aggregate functions.
- Dynamic arrays: `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE` cùng spill ownership, `#SPILL!`, history, clipboard và XLSX boundary.
- Conditional Formatting, Data Validation, Tables, worksheet AutoFilter và paged native presenters.
- XLSX values/formulas/styles/panes/rules/Tables/filters/printing và unknown-part preservation.
- Deterministic pagination, virtualized preview, staged PDF và native printer adapters.
- Streaming CSV/TSV, cancellation, output limits và formula-injection protection.

Tài liệu nguồn sự thật:

- `docs/current-status.md`;
- `docs/function-extension-sdk-contract.md`;
- `docs/advanced-statistical-functions-foundation-contract.md`;
- `docs/financial-functions-foundation-contract.md`;
- `docs/engineering-functions-foundation-contract.md`;
- `docs/database-functions-foundation-contract.md`;
- `docs/dynamic-arrays-contract.md`;
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

**Financial scalar helpers**: `ISPMT`, `EFFECT`, `NOMINAL`, `RRI`, `PDURATION`; sau đó AMOR/date-basis, bond/coupon/treasury/price/yield/duration, statistical hypothesis tests, advanced lookup/dynamic arrays, plugin isolation, drawings/charts, advanced data/pivot và release hardening.

## Giấy phép

Repository chưa công bố giấy phép mã nguồn mở. Repository public không mặc nhiên cấp quyền sao chép, sửa đổi hoặc phân phối.
