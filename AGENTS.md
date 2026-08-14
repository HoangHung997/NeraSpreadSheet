# NeraSpreadSheet — Quy tắc bắt buộc cho Codex và tác nhân lập trình

## 1. Đọc trước khi sửa

Trước mỗi nhiệm vụ phải đọc tối thiểu:

1. `README.md`;
2. `ARCHITECTURE.md`;
3. ADR liên quan trong `docs/adr/`;
4. `docs/worklog/CURRENT.md`;
5. test và benchmark của mô-đun bị tác động.

Không được bắt đầu bằng cách viết lại kiến trúc hoặc tạo một framework song song.

## 2. Ranh giới phụ thuộc

Hướng phụ thuộc hợp lệ:

```text
Foundation
   ↑
Core ← Formulas
   ↑
Layout ← Scrolling
   ↑
Rendering.Abstractions
   ↑
Platform backend / Platform host / OpenXml
```

Các quy tắc cứng:

- `Foundation`, `Core`, `Formulas`, `Layout`, `Scrolling` không được reference WPF, WinForms, MAUI, Direct2D, Skia hoặc Open XML.
- WPF, WinForms và MAUI không được reference lẫn nhau.
- Backend render không được sở hữu workbook hay tính công thức.
- Open XML chỉ chuyển đổi dữ liệu; không trở thành mô hình workbook nội bộ.
- Không thêm package mới nếu chưa có lý do và ADR hoặc ghi chú quyết định tương ứng.

## 3. Luật hiệu năng không được vi phạm

- Tuyệt đối không tạo `FrameworkElement`, `Control`, `View`, `TextBox`, `Label` hoặc `Border` cho từng ô.
- Chỉ ô đang chỉnh sửa mới được dùng editor thật dưới dạng overlay tái sử dụng.
- Offset cuộn phải là `double`; không biến trạng thái viewport thành `FirstVisibleRow`/`FirstVisibleColumn`.
- Không tự snap về biên hàng/cột sau khi precision touchpad dừng.
- Không gọi render đầy đủ theo từng input event; input phải được gom và tiêu thụ bởi frame scheduler.
- Không tính lại công thức, AutoFit, page break hoặc toàn bộ layout khi chỉ cuộn.
- Layout chỉ xử lý vùng nhìn thấy và overscan.
- Khi cache hợp lệ, cuộn ngắn phải tái sử dụng nội dung cũ và chỉ invalid vùng mới lộ ra.
- Đường lưới được snap theo physical pixel; document offset vẫn liên tục.

## 4. Quy ước mã nguồn

- Identifier, API và comment kỹ thuật trong code dùng tiếng Anh.
- Tài liệu người dùng và UI mặc định dùng tiếng Việt có dấu; chuỗi UI phải sẵn sàng đưa vào resource localization.
- Bật nullable, analyzer và warnings-as-errors.
- Không dùng `dynamic` trong Core nếu không có ADR.
- Không nuốt exception. Exception được chuyển đổi tại ranh giới phù hợp và phải giữ inner exception.
- API công khai mới phải có test và mô tả XML tối thiểu khi hành vi không hiển nhiên.
- Tên test theo mẫu `Method_Should_ExpectedResult_When_Condition`.

## 5. Quy trình Git

- `main`: ổn định; cấm commit trực tiếp.
- `develop`: tích hợp.
- Tính năng: `feature/<slug>`.
- Sửa lỗi: `fix/<slug>`.
- Phát hành: `release/<version>`.

Mỗi pull request phải có:

- tóm tắt thay đổi;
- danh sách test/benchmark đã chạy;
- rủi ro và giới hạn còn lại;
- phương án rollback;
- ảnh hoặc video nếu thay đổi UI;
- số liệu trước/sau nếu thay đổi đường render hoặc cuộn.

## 6. Cổng hoàn thành

Một nhiệm vụ chưa hoàn thành nếu thiếu một trong các mục sau:

1. build thành công cho các project bị tác động;
2. test mới hoặc lý do có thể kiểm chứng vì sao không cần test;
3. cập nhật ADR/tài liệu khi thay đổi contract;
4. cập nhật `docs/worklog/CURRENT.md`;
5. không có secret, đường dẫn máy cá nhân, Machine ID, token hoặc dữ liệu nhạy cảm;
6. báo cáo rõ phần chưa triển khai, không dùng stub để giả vờ đã hoàn thiện backend.

## 7. Handoff khi gần hết ngữ cảnh

Trước khi dừng phải cập nhật `docs/worklog/CURRENT.md` gồm:

- branch và commit hiện tại;
- việc đã hoàn thành;
- file đang sửa;
- test đã chạy và kết quả;
- lỗi còn lại;
- bước tiếp theo duy nhất, đủ cụ thể để tác nhân mới tiếp tục mà không đọc lại toàn repo.
