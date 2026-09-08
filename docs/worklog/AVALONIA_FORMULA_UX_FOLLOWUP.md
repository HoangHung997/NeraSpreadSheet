# Formula UX Avalonia — bổ sung sau audit

Base: `b2bd9b3ef8ab2b1cd70134144a4052c1fc1a5a88`, PR #4,
branch `feature/avalonia-001-host`. Chỉ mục 1 trong yêu cầu người dùng.
Mục 2/3 giữ deferred Codex queue tại PR #1 comment 5579073325, không đổi
root CURRENT hoặc source Clipboard/Worksheet view persistence.

## Correction đầu: routing kéo reference đối xứng

Split host nhận capture từ mọi pane khi bắt đầu kéo reference trên lưới,
kể cả pane giữ editor. Mỗi bước move hit-test bằng pane thực dưới con trỏ,
không dùng tọa độ tương đối của pane xuất phát sau khi vượt separator.
Pointer trong TextBox/TextPresenter vẫn giữ native caret/selection handling.
Canonical cancellation dừng capture; không commit draft, đổi active pane,
selection hoặc workbook trong khi kéo reference.

Bổ sung 5 tests qua native routed MouseDown/Move/Up của headless window:
editor→other pane với offsets khác nhau, chiều ngược lại, same-pane range,
TextBox selection và cancel giữa drag. Giữ tests baseline và mọi CI gates.

Đây là source candidate, chưa có local/CI PASS tại lúc soạn. Shell và Python
trong môi trường tác giả báo ClientError; chỉ công nhận kết quả CI trên SHA
mới sau push. Không lấy CI baseline b2bd9b3e làm bằng chứng cho bản sửa.

## Các phần còn mở

Edge autoscroll theo frame, reference của công thức chưa hoàn chỉnh, UI chọn
reference từ sheet khác không hủy canonical draft, hit-test/move/resize viền
reference. Mục 1 chưa DONE chỉ vì correction routing được ghi nhận.

Bước tiếp theo: kiểm exact-source Avalonia matrix; sửa lỗi quan sát được mà
không nới gate rồi triển khai tiếp các gap mục 1. PR vẫn Draft, không merge,
không mark Ready. Rollback riêng correction này không đổi workbook schema.
