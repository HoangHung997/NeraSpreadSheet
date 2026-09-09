## 6. Quy trình Git — chỉ đạo hợp nhất ngày 09/09/2026

- `main` là nhánh canonical duy nhất được giữ lâu dài. Không còn `develop` hoặc nhánh lane dài hạn.
- Mọi AI/người dùng bắt đầu đọc `Check out/README.md` và `docs/worklog/CONSOLIDATION_20260909.md`.
- Nhánh cũ đã lưu tại các tag `archive/consolidation-20260909/*`; mapping đầy đủ ở `Check out/branches.json`. Không tự tạo lại nhánh đã lưu trữ hoặc tiếp tục queue/lease lịch sử.
- Làm thay đổi trên local branch/worktree tạm; nhánh remote phục vụ review phải có phạm vi được chủ repo giao và được dọn sau integration. Không commit code trực tiếp trên main, không rewrite/force-push lịch sử.
- Giữ một writer khi cập nhật main. Chỉ fast-forward/integrate source đã kiểm đúng SHA; branch cleanup phải kiểm expected-SHA và archive trước, không xóa commit chưa bảo tồn.
- `Check out` là cửa xem ảnh/tải bản thử nghiệm. Binary nặng nằm ở GitHub Releases, không commit zip/exe vào source tree. Mỗi gói chứa thư mục `Check out` và manifest/hash/source SHA.
- Workflow `Check out — integrated SDK and test applications` điều phối nguyên các gate Core, legacy hosts, OpenXML, package consumers, Avalonia và native smoke. Không bỏ gate để thu gọn danh sách workflow.
- Hợp nhất source không có nghĩa nghiệm thu whole-B/Mac runtime, H1, locale, physical hardware hoặc 100% tương thích Excel. Các trạng thái còn mở phải được ghi riêng.
- Không publish NuGet công khai hoặc thay lịch ngoài phạm vi được giao. Maintenance WPF/WinForms/MAUI vẫn giữ; phát triển UI mới ưu tiên Avalonia.

Mỗi checkpoint ghi exact source, delta, test/smoke/benchmark thực chạy, hạn chế, rollback và ảnh khi đổi UI. Không gọi preservation là tính đúng tính năng.

