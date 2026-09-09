# NeraSpreadSheet — Quy tắc bắt buộc cho Codex và tác nhân lập trình

## 0. Ưu tiên sản phẩm hiện hành — 09/09/2026

Đọc [Avalonia-first](docs/avalonia-first-ui-strategy.md). Mọi app mới của hệ sinh
thái dùng Avalonia; `NeraSpreadSheet.Avalonia` là SDK UI chính. Core/engine vẫn
độc lập và có thể dùng không UI. **Tạm dừng phát triển tính năng UI mới cho WPF,
WinForms và MAUI**; giữ các host/package/API cũ để bảo trì và tích hợp ứng dụng
cũ. Không xóa source/backend hoặc bỏ CI, không tự tuyên bố EOL/migrate app.
Thay đổi shared engine vẫn phải bảo toàn các consumer đang giữ.

Được tiếp tục cải thiện Ribbon Avalonia theo chỉ đạo mới; không tự mở lại các
lane Table/Filter khác, thay lịch, chiếm ownership hoặc sửa source của tác nhân
khác. Root Codex sở hữu shared status/CURRENT/assignment. Task riêng viết own
worklog và handoff; chỉ Root cập nhật tiến độ toàn dự án. Các entry lịch sử
“ba presenter” không supersede định hướng mới, và định hướng mới không tự xóa
correctness/hardware/release holds.

## 1. Đọc trước khi sửa

Trước mỗi nhiệm vụ phải đọc tối thiểu:

1. `README.md`;
2. `ARCHITECTURE.md`;
3. `docs/current-status.md`;
4. `docs/worklog/CURRENT.md`;
5. contract/ADR liên quan trong `docs/`;
6. test và benchmark của mô-đun bị tác động.

Không bắt đầu bằng cách viết lại kiến trúc, tạo model song song hoặc triển khai lại capability đã tồn tại bằng một code path khác.

## 2. Ranh giới phụ thuộc

Hướng phụ thuộc hợp lệ:

```text
Foundation
   ↑
Core ← Formulas
   ↑
Interaction / Editing
   ↑
Layout ← Scrolling
   ↑
Rendering.Abstractions / Rendering.Spreadsheet / Viewport
   ↑
Platform backend / Platform host / OpenXml
```

Quy tắc cứng:

- `Foundation`, `Core`, `Formulas`, `Interaction`, `Layout`, `Scrolling` không reference Avalonia, WPF, WinForms, MAUI, Direct2D, Skia hoặc Open XML.
- Avalonia, WPF, WinForms và MAUI không reference lẫn nhau.
- Backend render không sở hữu workbook, selection hay calculation engine.
- Open XML chỉ chuyển đổi dữ liệu; không trở thành workbook model nội bộ.
- Không thêm package mới nếu chưa có lý do và ADR/decision note tương ứng.

## 3. Luật hiệu năng

- Không tạo `FrameworkElement`, `Control`, `View`, `TextBox`, `Label` hoặc `Border` cho từng ô.
- Chỉ ô đang chỉnh sửa dùng một editor overlay tái sử dụng.
- Offset cuộn là `double`; không biến viewport state thành `FirstVisibleRow`/`FirstVisibleColumn`.
- Không snap về biên hàng/cột sau precision input.
- Input liên tục phải đi qua frame scheduler; không full-render theo từng raw event.
- Không recalculate formula, AutoFit, page break hoặc toàn layout khi chỉ cuộn.
- Layout/render chỉ xử lý visible + overscan.
- Giữ nested display-list reference semantics; không flatten-copy command arrays.
- Dirty-region path phải có conservative full fallback khi correctness không chắc chắn.
- Gridline có thể snap physical pixel; document offset vẫn liên tục.

## 4. Luật workbook và structural identity

- Insert/delete và reorder là hai contract khác nhau; không giả lập reorder bằng clipboard hoặc insert/delete nếu làm thay đổi identity/history semantics.
- Row/column reorder bắt buộc tái sử dụng `WorksheetAxisMove` và `SpreadsheetAxisReorderController`.
- Formula reference phải theo logical cell identity trên toàn workbook.
- Nếu formula range sẽ thành discontiguous hoặc merge sẽ split/reverse, thao tác phải bị từ chối nguyên tử; không tự đổi nghĩa công thức.
- Selection, dimensions, merge, freeze/split state, scroll offsets và undo/redo phải tham gia cùng transaction.
- Không materialize toàn row/column axis để thực hiện sparse operation.

## 5. Quy ước mã nguồn

- Identifier, API và technical comments trong code dùng tiếng Anh.
- Tài liệu người dùng và UI mặc định dùng tiếng Việt có dấu; chuỗi UI phải sẵn sàng đưa vào resource localization.
- Nullable, analyzers và warnings-as-errors luôn bật.
- Không dùng `dynamic` trong Core nếu không có ADR.
- Không nuốt exception; chuyển đổi exception tại ranh giới phù hợp và giữ inner exception.
- API công khai mới phải có tests và XML docs khi behavior không hiển nhiên.
- Tên test không dùng dấu gạch dưới để tránh analyzer violations; dùng dạng `MethodShouldExpectedResultWhenCondition` hoặc tên mô tả tương đương.

## 6. Quy trình Git

- `main`: ổn định; cấm commit trực tiếp.
- `develop`: tích hợp.
- Tính năng: `feature/<slug>`.
- Sửa lỗi: `fix/<slug>`.
- Phát hành: `release/<version>`.

Mỗi pull request phải có:

- tóm tắt thay đổi;
- test/benchmark đã chạy;
- rủi ro và giới hạn còn lại;
- phương án rollback;
- ảnh/video nếu thay đổi UI và môi trường cho phép;
- số liệu trước/sau nếu thay đổi render/scroll performance.

PR #1 hiện là Draft; không merge khi exact-head CI đỏ hoặc chưa xác định.

## 7. Cổng hoàn thành

Một nhiệm vụ chưa hoàn thành nếu thiếu một trong các mục sau:

1. build thành công cho project bị tác động;
2. regression tests hoặc lý do có thể kiểm chứng vì sao không cần test;
3. runtime smoke cho desktop/render/input behavior;
4. architecture verification;
5. cập nhật contract/status/worklog khi behavior thay đổi;
6. không có secret, đường dẫn máy cá nhân, Machine ID, token hoặc dữ liệu nhạy cảm;
7. báo cáo rõ phần chưa triển khai, không dùng stub để giả vờ hoàn thiện backend;
8. GitHub Actions phải xanh tại đúng HEAD cuối, không chỉ ở một commit cha;
9. Ribbon/visual changes phải có ảnh thật và geometry/state checks; functional tests không thay visual review.

## 8. Handoff khi gần hết ngữ cảnh

Root coordinator cập nhật `docs/worklog/CURRENT.md`. Tác nhân task riêng không
ghi đè file đó: cập nhật own task worklog và gửi receipt để Root fold gồm:

- branch, PR và commit hiện tại;
- implementation commit và CI run đã xác minh;
- việc đã hoàn thành;
- file trọng tâm;
- test đã chạy và kết quả;
- giới hạn/lỗi còn lại;
- một bước tiếp theo duy nhất, đủ cụ thể để tác nhân mới tiếp tục mà không đọc lại toàn repo.
