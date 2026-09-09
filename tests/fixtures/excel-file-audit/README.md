# Đầu vào audit file XLSX đổi đuôi

Người dùng đã xác nhận rõ trong cuộc trò chuyện ngày 09/09/2026 cho phép công khai
nguyên file `Excel_CV_ThachBich - Copy.dlda` để kiểm tra SDK. Xác nhận mới thay giới
hạn không-public-file trước đó, nhưng không cấp quyền sửa file gốc, H2 hoặc các
lane SDK khác. Không thực thi macro, gọi external provider hoặc xuất dữ liệu ô
vào log khi chỉ cần số liệu/địa chỉ để chẩn đoán.

**Trạng thái tại lúc tạo harness: binary CHƯA được upload.** Môi trường thực thi
attachment bị TransportTimeoutError; GitHub connector hiện nhận text/base64,
không có file-path upload. Không tạo file giả mang tên file thật, không dựng lại
workbook từ extracted text. Không nhận self-test tổng hợp làm kết quả file thật.

Đích nhận file nguyên byte trên CHÍNH nhánh `feature/excel-file-audit-20260909`:

`tests/fixtures/excel-file-audit/Excel_CV_ThachBich - Copy.dlda`

Sau khi thêm file vào thư mục này, push kích hoạt workflow `Excel file audit`.
CI ghi SHA256 của binary đang kiểm và SHA nguồn SDK/harness. Hash này phải được
đối chiếu với `Get-FileHash -Algorithm SHA256` trên file gốc nếu attachment chưa
đọc được byte để lấy hash. Không chỉ dựa vào tên file để tuyên bố byte-identical.

Phần công khai có thể tồn tại trong Git history kể cả xóa file khỏi nhánh sau này.
Không có thay đổi source SDK trong nhánh audit này.
