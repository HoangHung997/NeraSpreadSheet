## Mục tiêu

Khởi tạo kiến trúc M0 cho NeraSpreadSheet theo hướng Core đa nền tảng, continuous pixel scrolling, display list, Direct2D/DirectWrite trên Windows và Skia GPU trên MAUI.

## Thay đổi

- Tạo solution/project/module nền.
- Tạo workbook sparse, metric index, scroll controller và display-list contract.
- Tạo host shell WPF/WinForms/MAUI.
- Thêm test, benchmark, CI, ADR và tài liệu Codex handoff.

## Test

Môi trường tạo artifact không có .NET SDK nên chưa chạy được restore/build/test. PR phải được CI và máy Windows xác minh trước khi merge.

## Rủi ro

- Có thể có compile/analyzer issue cần sửa sau lần build đầu tiên.
- Direct2D, Skia GPU, formula engine và XLSX serializer mới ở mức contract, chưa được giả định là hoàn chỉnh.

## Rollback

Revert commit bootstrap hoặc đóng PR; `main` không bị thay đổi trực tiếp.
