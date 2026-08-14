# Đóng góp cho NeraSpreadSheet

## Nhánh

- Tạo `feature/*` hoặc `fix/*` từ `develop`.
- Không push trực tiếp vào `main`.
- Một pull request chỉ giải quyết một mục tiêu chính.

## Kiểm tra trước khi mở PR

```powershell
dotnet restore .\NeraSpreadSheet.slnx
dotnet build .\NeraSpreadSheet.slnx -c Release
dotnet test .\NeraSpreadSheet.Core.slnx -c Release --no-build
pwsh .\scripts\verify-architecture.ps1
```

Thay đổi hiệu năng phải chạy benchmark liên quan và ghi cấu hình máy, dataset, số lần đo, median/P95 và mức cấp phát bộ nhớ.

## Commit

Dùng commit message rõ nghĩa:

```text
feat(scroll): preserve fractional precision input
fix(layout): skip zero-height rows at viewport boundary
test(core): cover XFD maximum column
chore(ci): add Windows host build
```

## Pull request

PR phải nêu:

- mục tiêu;
- kiến trúc bị tác động;
- test đã chạy;
- benchmark trước/sau nếu có;
- rủi ro;
- rollback;
- phần cố ý chưa làm.
