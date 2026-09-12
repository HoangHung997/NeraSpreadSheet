# Check out / Packages

Đây là **sổ gói Check out chính thức trong repository GitHub**.

## Bản mới nhất hiện tại

- Ngày giờ tạo: **12/09/2026 09:59:14 (ICT, UTC+7)**
- Source SHA: `36bc29a829b82a67c7f7c92d40087c5733361a06`
- Workflow run: `34668687734`
- Trạng thái: **VALIDATED / PASS**
- Con trỏ mới nhất: [`latest.json`](latest.json)
- Bản ghi build: [`2026-09-12_0959_ICT_36bc29a8.json`](2026-09-12_0959_ICT_36bc29a8.json)

## Quy tắc

Mỗi lượt full Check out thành công trên `main` phải tạo một file lịch sử riêng dạng:

`YYYY-MM-DD_HHMM_ICT_<short-sha>.json`

và cập nhật `latest.json` cùng block **Bản Check out mới nhất** trong `Check out/README.md`.

Mỗi record chứa ngày giờ tạo gói, source SHA, workflow run ID, trạng thái validation, link tải immutable và checksum. Vì vậy **không dùng tên `latest` một mình để xác định gói mới/cũ**; phải đối chiếu ngày giờ + SHA + run ID.

Các binary ZIP lớn vẫn là tài sản GitHub của cùng repository nhưng nằm ở GitHub Actions/Release thay vì Git history. Lý do: commit trực tiếp các ZIP 50–100 MB mỗi lượt build sẽ làm repository tăng dung lượng vĩnh viễn. Metadata quản lý, lịch sử, SHA và link tải của tất cả gói luôn nằm trong thư mục này.

`scripts/check-out/assemble.py` tự cập nhật registry sau khi full Check out của `main` đã PASS và publication thành công. Các commit cập nhật registry dùng `[skip ci]` để không tạo vòng lặp workflow.
