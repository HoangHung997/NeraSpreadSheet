# AVALONIA-001 — implementation handoff

User yêu cầu ChatGPT triển khai trực tiếp, không chỉ giao việc qua comment.
Branch: `feature/avalonia-001-host`.
Base: `30e74befa9984c5fb4ac1ea00701dff85fe6c533` của PR #1.

Đã viết adapter DisplayList, public spreadsheet host và native editor ownership,
sample desktop, MSTest headless regressions, solution riêng và workflow native
Windows/Linux/macOS. Chi tiết/OPEN scope: [contract](../avalonia-host-contract.md).
Đây là source candidate; không gọi toàn bộ AVALONIA-001 DONE.

Môi trường tác giả không có dotnet, không truy cập mạng từ shell để cài SDK;
vì vậy không có local build/test PASS. Phải đọc kết quả GitHub Actions của
exact final SHA, sửa lỗi build/runtime nếu có, không dùng green của parent.
Các file shared status/CURRENT/central dependencies và host cũ giữ nguyên để
không ghi đè công việc coordinator/Formula Bar/iOS/TABLE-007 đang mở.

Bước tiếp theo duy nhất: xác minh workflow Avalonia của exact final branch SHA,
đọc từng job/log và bổ sung kết quả thực vào handoff trước khi review integration.
