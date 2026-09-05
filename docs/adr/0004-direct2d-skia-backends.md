# ADR 0004: Direct2D/DirectWrite trên Windows và Skia GPU trên MAUI

- Trạng thái: Accepted
- Ngày: 2026-08-14

## Quyết định

- WPF và WinForms dùng backend Direct2D + DirectWrite + Windows composition.
- Android, iOS và Mac Catalyst dùng Skia GPU qua MAUI handler.
- MAUI GraphicsView chỉ có thể là fallback hoặc công cụ thử nghiệm, không phải backend hiệu năng cao duy nhất.

## Lý do

DirectWrite cho chất lượng text và cache tốt trên Windows; Skia cung cấp đường render GPU đa nền tảng.

## Hệ quả

Phải có software fallback, xử lý device loss, resource lifetime và test DPI/refresh-rate riêng cho từng backend.
