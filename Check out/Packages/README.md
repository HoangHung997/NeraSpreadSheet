# Check out / Packages

Đây là **sổ gói Check out chính thức trong repository GitHub**.

## Bản mới nhất

- Ngày giờ tạo: **12/09/2026 09:59:14 (ICT, UTC+7)**
- Source SHA: `36bc29a829b82a67c7f7c92d40087c5733361a06`
- Workflow run: `34668687734`
- Trạng thái: **VALIDATED / PASS**
- Con trỏ mới nhất: [`latest.json`](latest.json)
- Bản ghi build: [`2026-09-12_0959_ICT_36bc29a8.json`](2026-09-12_0959_ICT_36bc29a8.json)

## Quy tắc

Mỗi lượt full Check out thành công phải tạo một file lịch sử riêng dạng:

`YYYY-MM-DD_HHMM_ICT_<short-sha>.json`

và cập nhật `latest.json`.

Mỗi record chứa ngày giờ tạo gói, source SHA, workflow run ID, trạng thái validation, artifact IDs/links và checksum khi có. Vì vậy **không được dùng tên `latest` một mình để xác định gói mới/cũ**; phải đối chiếu ngày giờ + SHA + run ID.

Các binary ZIP lớn nằm ở GitHub Actions/Release để không làm Git history phình, nhưng metadata quản lý và lịch sử của chúng luôn nằm trong thư mục này.
