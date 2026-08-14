# Current Work Handoff

- Ngày cập nhật: 2026-08-14
- Mốc: M0 bootstrap
- Branch dự kiến: `feature/bootstrap-architecture-v0.1`
- Trạng thái: bộ khung được tạo; cần build/restore trên máy có .NET 10 vì môi trường tạo artifact không có .NET SDK.

## Đã hoàn thành

- Module, project reference và solution tách Core/Windows/MAUI.
- Workbook sparse tối thiểu.
- `SparseAxisMetricIndex`.
- `ContinuousScrollController` giữ offset lẻ.
- Display-list contract.
- Host shell WPF/WinForms/MAUI.
- Unit tests, benchmark, CI và ADR.

## Chưa xác minh trong môi trường hiện tại

- `dotnet restore`, `dotnet build`, `dotnet test`.
- Build MAUI workload.
- API analyzer trên Windows.

## Bước tiếp theo duy nhất

Trên Windows có .NET SDK 10.0.302+, chạy:

```powershell
dotnet restore .\NeraSpreadSheet.slnx
dotnet build .\NeraSpreadSheet.slnx -c Release
dotnet test .\NeraSpreadSheet.Core.slnx -c Release --no-build
pwsh .\scripts\verify-architecture.ps1
```

Sửa mọi compile/analyzer failure trước khi bắt đầu Direct2D hoặc formula engine. Sau đó cập nhật file này bằng commit SHA và kết quả test thực tế.
