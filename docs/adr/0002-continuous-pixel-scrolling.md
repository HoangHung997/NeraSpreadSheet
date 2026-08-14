# ADR 0002: Viewport cuộn liên tục theo pixel

- Trạng thái: Accepted
- Ngày: 2026-08-14

## Quyết định

Nguồn sự thật của viewport là `ScrollX` và `ScrollY` kiểu `double`. Hàng/cột đầu tiên nhìn thấy chỉ là kết quả tra cứu từ offset.

## Ràng buộc

- Cho phép hàng/cột bị cắt một phần.
- Precision touchpad giữ delta lẻ.
- Không snap về biên ô khi dừng.
- Input được gom và áp dụng theo frame.
- Regular mouse wheel có thể animation tới target; precision input áp dụng trực tiếp ở frame kế tiếp.

## Hệ quả

Cần metric index để ánh xạ offset ↔ row/column và phải xử lý đường lưới theo physical pixel để tránh mờ.
