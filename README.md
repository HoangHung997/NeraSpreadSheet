# NeraSpreadSheet

> Engineering SDK; chưa phải bản phát hành production hoặc tương đương toàn bộ Excel.

## Hướng phát triển hiện hành — Avalonia-first

**Tất cả ứng dụng mới trong hệ sinh thái dùng Avalonia.** `NeraSpreadSheet.Avalonia`
là bộ SDK UI chính. Engine bảng tính vẫn độc lập với UI: workbook sparse, công thức,
editing/history, formatting, viewport/layout và OpenXML dùng chung.

**WPF, WinForms và MAUI tạm dừng phát triển tính năng UI mới**, giữ làm các gói
bảo trì/tích hợp tùy chọn cho ứng dụng cũ. Không xóa các host/package/API hoặc bỏ
CI; các sửa lỗi dữ liệu/bảo mật và regression vẫn theo ownership. Không cần viết
một app WPF rồi một app MAUI riêng để đưa app mới lên nhiều nền tảng.

AI và contributor phải đọc [quyết định Avalonia-first](docs/avalonia-first-ui-strategy.md)
và `AGENTS.md`. Định hướng không đồng nghĩa mọi feature/platform đã nghiệm thu;
đọc [CURRENT](docs/worklog/CURRENT.md) và worklog của task ở exact branch được dùng.
Root Codex sở hữu shared status/worklog. Không khởi động lại task đang pause chỉ
vì README mô tả capability.

## Build engine và chạy Avalonia

```powershell
dotnet restore NeraSpreadSheet.Core.slnx
dotnet build NeraSpreadSheet.Core.slnx -c Release --no-restore
dotnet test NeraSpreadSheet.Core.slnx -c Release --no-build
./scripts/verify-architecture.ps1

dotnet build NeraSpreadSheet.Avalonia.slnx -c Release
dotnet run --project samples/NeraSpreadSheet.Avalonia.Sample -c Release
```

Dùng SDK đã pin trong `global.json`. Sample desktop không phải bằng chứng Android,
iOS hay browser đã chạy; những nền tảng đó cần packaging/runtime gates riêng.
Theme/control thuộc SDK, ứng dụng cấu hình command và nghiệp vụ của mình.

## Lịch sử kiểm chứng — không thay trạng thái HEAD hiện tại

| Snapshot khóa catalog công thức | Số lượng |
|---|---:|
| Eager/versioned | 468 |
| AST/reference-aware | 40 |
| Dynamic-array unique | 38 |
| Tổng tên trong catalog đã khóa | 546/546 |

Con số catalog không phải tỷ lệ tương thích Excel. Báo cáo M2/F001–F019/Q001 trước
đây là checkpoint lịch sử; không dùng số test hoặc CI cũ làm evidence cho HEAD mới.
Các đặc tính dùng chung gồm pixel scrolling, dynamic arrays, XLSX preservation,
printing/PDF và Function Extension SDK theo contract của từng module.

## Mốc UI để đối chiếu

WPF được giữ làm tham chiếu visual và consumer regression, không phải UI chính
cho app mới:

```powershell
dotnet run --project samples/NeraSpreadSheet.Wpf.Sample -- --ribbon-preview
./scripts/capture-ribbon-visual.ps1
```

Xem [Ribbon visual contract](docs/ribbon-visual-contract.md),
[responsive contract](docs/ribbon-responsive-layout-contract.md) và
[locale/initial view requirements](docs/excel-locale-initial-view-requirements.md).
Mở được XLSX, render đúng và bảo toàn khi lưu là ba mức nghiệm thu riêng.
