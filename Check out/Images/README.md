# Check out / Images — toàn bộ ảnh của bản build

**Tải [gói ảnh và báo cáo](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/Nera-Check-out-evidence.zip), giải nén rồi mở `Check out/Images/index.html`.** Có mục lục offline, ảnh gốc, phân nhóm theo hệ điều hành/host, SHA-256 và commit nguồn ở `index.json`.

Mỗi gói ứng dụng cũng có `Check out/Images` của đúng hệ điều hành đó. Bộ evidence đầy đủ gom cả ảnh từ ứng dụng self-contained, build Avalonia ba nền tảng, SDK legacy, MAUI và demo legacy trong **cùng lượt CI**; không chỉ lấy vài ảnh Windows xem nhanh. Không gom ảnh của commit cha hoặc run khác.

## Bản review và bản main khác nhau

Nếu `CHECKOUT.json` của link latest còn ghi source cũ thì main chưa được thay. Bản QA-GAPS-005 đang review nằm ở [Check out workflow của nhánh `fix/qa-gaps-005`](https://github.com/HoangHung997/NeraSpreadSheet/actions/workflows/check-out.yml?query=branch%3Afix%2Fqa-gaps-005), artifact `Check-out-index-<SHA>`. Không dùng ảnh của latest cũ làm bằng chứng bản mới.

Trong app package của checkpoint này có thêm `Images/qa-gaps/`: năm cảnh Ribbon **768/820/1024/1366/1920**, Zoom 110%, Table Design contextual tab và cửa sổ Filter native. Manifest/hash của đúng source được kiểm bởi `scripts/verify-avalonia-qa-gaps.py`. Đây là scripted native evidence, không phải physical keyboard/mouse hoặc pixel-parity với Excel.

Ảnh/gói lớn được giữ ở GitHub Releases và artifact, không thêm EXE/ZIP hay hàng trăm PNG vào lịch sử source. Đây là thư mục điều hướng cố định trong repository; bên trong gói tải cũng giữ đúng cấu trúc `Check out/Images`. Không tạo nhánh ảnh dài hạn hoặc commit build tự lặp CI.

## Ảnh xem nhanh của bản latest đã được công bố

![Mở XLSX tương thích](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/compatibility-open.png)
![Giữ workbook đang hiển thị khi mở file lỗi](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/compatibility-rejected.png)
![Ribbon](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/ribbon-light.png)
![Định dạng số](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/format-number.png)

Sau khi QA-GAPS-005 được merge và **fresh main Check out** đạt, Releases mới có thêm preview `qa-ribbon-768.png`, `qa-ribbon-820.png`, `qa-zoom-110.png`, `qa-table-design.png`, `qa-filter-window.png`. Trước thời điểm đó chỉ xem chúng trong artifact review; không giả ảnh giữ chỗ.

Các ảnh là smoke tự động, không phải chứng nhận 100% giống Excel hoặc đã kiểm hết IME/phần cứng/screen reader.