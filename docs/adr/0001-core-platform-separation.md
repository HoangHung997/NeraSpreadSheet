# ADR 0001: Tách Core khỏi nền tảng giao diện

- Trạng thái: Accepted
- Ngày: 2026-08-14

## Quyết định

Workbook, formula, layout, scrolling và display-list contract không reference WPF, WinForms hoặc MAUI. Mỗi nền tảng chỉ có host/adapter riêng.

## Lý do

Một UI DLL duy nhất không thể kế thừa đồng thời `FrameworkElement`, `Control` và `View`. Tách Core cho phép dùng cùng workbook trong desktop, mobile, service, converter và test headless.

## Hệ quả

Có nhiều assembly hơn, nhưng dependency rõ ràng, test dễ hơn và backend có thể thay thế độc lập.
