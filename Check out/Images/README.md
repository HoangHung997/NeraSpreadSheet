# Check out / Images — toàn bộ ảnh của bản build

**Tải [gói ảnh và báo cáo](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/Nera-Check-out-evidence.zip), giải nén rồi mở `Check out/Images/index.html`.** Có mục lục offline, ảnh gốc, phân nhóm theo hệ điều hành/host, SHA-256 và commit nguồn ở `index.json`.

Mỗi gói ứng dụng cũng có `Check out/Images` của đúng hệ điều hành đó. Bộ evidence đầy đủ gom cả ảnh từ ứng dụng self-contained, build Avalonia ba nền tảng, SDK legacy, MAUI và demo legacy trong **cùng lượt CI**; không chỉ lấy vài ảnh Windows xem nhanh. Không gom ảnh của commit cha hoặc run khác.

## Bản review và bản main khác nhau

Nếu `CHECKOUT.json` của link latest còn ghi source cũ thì main chưa được thay. Bản đang review nằm ở [Check out workflow của nhánh task](https://github.com/HoangHung997/NeraSpreadSheet/actions/workflows/check-out.yml?query=branch%3Afeature%2Fribbon-dialogs-003), artifact `Check-out-index-<SHA>`. Không dùng ảnh của latest cũ làm bằng chứng bản mới.

Ảnh/gói lớn được giữ ở GitHub Releases và artifact, không thêm EXE/ZIP hay hàng trăm PNG vào lịch sử source. Đây là thư mục điều hướng cố định trong repository; bên trong gói tải cũng giữ đúng cấu trúc `Check out/Images`. Không tạo nhánh ảnh dài hạn hoặc commit build tự lặp CI.

## Ảnh xem nhanh của bản latest đã được công bố

![Mở XLSX tương thích](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/compatibility-open.png)
![Giữ workbook đang hiển thị khi mở file lỗi](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/compatibility-rejected.png)
![Ribbon](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/ribbon-light.png)
![Định dạng số](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/format-number.png)

Các ảnh mới chỉ xuất hiện sau khi CI bản main đạt và công bố; không giả ảnh giữ chỗ. Mở mục lục của artifact review để xem ngay đúng bản review đã chạy. Các ảnh là smoke tự động, không phải chứng nhận 100% giống Excel hoặc đã kiểm hết IME/phần cứng/screen reader.
