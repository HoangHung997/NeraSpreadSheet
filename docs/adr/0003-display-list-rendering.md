# ADR 0003: Render qua display list

- Trạng thái: Accepted
- Ngày: 2026-08-14

## Quyết định

Layout/painter tạo display list trung lập nền tảng. Backend Direct2D hoặc Skia thực thi các lệnh fill, line, text và clip.

## Lý do

- Tái sử dụng layout giữa WPF, WinForms và MAUI.
- Dễ benchmark và snapshot-test.
- Có thể cache display list/tile.
- Backend không truy cập workbook, giảm coupling.

## Hệ quả

Text measurement phải có contract rõ ràng; cache glyph và font thuộc backend nhưng kết quả đo phải được version hóa theo DPI, zoom và font.
