# NeraSpreadSheet

SDK bảng tính độc lập; **Avalonia là UI chính cho tất cả ứng dụng mới**. WPF/WinForms/MAUI giữ bảo trì và tích hợp ứng dụng cũ, không tiếp tục ba bộ UI mới song song.

## Bắt đầu tại đây

### [Mở thư mục Check out — ảnh giao diện và tải bản chạy thử](Check%20out/README.md)

Chỉ theo dõi nhánh **`main`**. Bản tải về luôn có source SHA, checksum và liên kết CI; đây là bản engineering preview, không phải chứng nhận tương thích toàn bộ Excel.

| Bạn cần | Nơi xem |
|---|---|
| Ảnh Ribbon/bảng tính và gói chạy thử không cần Visual Studio | [Check out](Check%20out/README.md) |
| Sơ đồ module, engine độc lập UI | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Quy tắc cho AI/người phát triển | [AGENTS.md](AGENTS.md) |
| Nguồn đã hợp nhất và các phần vẫn còn mở | [Báo cáo hợp nhất](docs/worklog/CONSOLIDATION_20260909.md) |
| Khôi phục mã từ nhánh cũ | [Danh mục lưu trữ](Check%20out/branches.md) |
| Chiến lược UI | [Avalonia-first](docs/avalonia-first-ui-strategy.md) |

## Build từ source

Dùng SDK theo `global.json`. Các solution có phạm vi khác nhau, không phải bản sao engine:

```sh
dotnet build NeraSpreadSheet.Core.slnx -c Release
dotnet test NeraSpreadSheet.Core.slnx -c Release --no-build
dotnet build NeraSpreadSheet.Avalonia.slnx -c Release
dotnet run --project samples/NeraSpreadSheet.Avalonia.Sample -c Release
```

`NeraSpreadSheet.slnx` giữ các desktop host Windows và backend tương ứng. Core/OpenXML có thể dùng không khởi tạo UI. Không đưa Avalonia/OpenXML types vào Core/Formulas.

## Phạm vi tương thích

Đọc dữ liệu, tính/vẽ đúng và bảo toàn tính năng chưa hỗ trợ là ba khả năng riêng. Engine có sparse workbook, formula catalog, editing/Undo, layout/viewport và XLSX preservation. Không coi mở được workbook, có đủ nút Ribbon hay CI xanh là tương đương hoàn toàn Excel.

Clipboard/view-state shared được ghép để kiểm thử; H1 host wiring, locale/no-op edit, native Mac diagnostics, hardware/accessibility và kiểm chứng workbook người dùng còn cần các checkpoint riêng. Các báo cáo cũ trong `docs/worklog` là lịch sử, không phải hàng đợi AI đang chạy.
