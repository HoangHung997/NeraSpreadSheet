# Performance Budget

Đây là mục tiêu nghiệm thu, chưa phải số liệu đã đạt ở M0.

## Frame budget

| Màn hình | Tổng ngân sách/frame |
|---|---:|
| 60 Hz | 16,67 ms |
| 120 Hz | 8,33 ms |

Mục tiêu 60 Hz cho viewport 4K:

- xử lý input: dưới 1 ms;
- tra row/column và layout visible region: dưới 2 ms;
- tạo/cập nhật display list: dưới 3 ms;
- backend render + present: dưới 8 ms;
- phần dự phòng: tối thiểu 2 ms.

## Bộ nhớ

- Không có object UI cho từng ô.
- Cell storage là sparse.
- Style dùng interning/ID.
- Text layout, glyph, tile và GPU resource có giới hạn cache và cơ chế eviction.

## Dataset bắt buộc

- 1.048.576 hàng × 16.384 cột logical, dữ liệu sparse.
- 100.000 hàng có dữ liệu.
- chiều cao hàng và độ rộng cột không đều;
- hidden rows/columns;
- merged cells;
- freeze panes;
- conditional formatting;
- zoom và DPI 100/125/150/200%;
- 60 Hz và 120 Hz;
- regular mouse, precision touchpad và touch.

## Metric cần báo cáo

- median, P95 và P99 frame time;
- dropped frames;
- allocated bytes/frame;
- working set;
- số tile cache hit/miss;
- số cell layout/render thực tế mỗi frame.
