# NeraSpreadSheet — Demo Ribbon Win11 x64 thử nghiệm

Giải nén toàn bộ artifact vào một thư mục mới, rồi mở
`app/NeraSpreadSheet.Wpf.Sample.exe`. Giữ nguyên các DLL và thư mục con;
bản self-contained đi kèm .NET, không cần cài runtime riêng. Mã nguồn/version/
SHA-256 của từng file nằm trong `demo-manifest.json`.

Đây là bản thử nghiệm chưa ký số, không phải bản phát hành production.
Không thay đổi thiết lập bảo mật Windows để chạy app. Hãy dùng bản sao file
Excel khi thử; lệnh Tệp → Lưu bản sao cho phép chọn file đích và chỉ thay file
sau khi serialize thành công. Không dùng bản thử nghiệm làm nơi lưu dữ liệu duy nhất.

## Một lượt kiểm thử đề nghị

1. Mở app: Ribbon đầy đủ và workbook bán hàng synthetic; chuyển các tab Ribbon,
   thu nhỏ cửa sổ, thử các palette và Tùy biến Ribbon.
2. Chọn ô trong Table để hiện Thiết kế Bảng; đổi style, totals, mở bộ lọc, thử
   chọn/bỏ chọn/search và Hoàn tác. Lệnh cần tham số phải mở dialog thật.
3. Chuyển sheet bằng hàng tab ở dưới; nhiều sheet cuộn ngang, tab đang chọn được
   đưa vào vùng nhìn thấy. Nhấp đúp ô để sửa, thử Enter, Alt+Enter và Esc.
4. Tệp → Mở workbook: chọn bản sao XLSX; cửa sổ mới vẫn giữ Ribbon và các sheet
   của file. Lưu bản sao với tên khác, mở lại và so sánh dữ liệu/định dạng.
5. Báo lỗi kèm version/SHA, sheet/ô, thao tác, ảnh trước/sau; không gửi dữ liệu
   nhạy cảm nếu có thể tái hiện bằng workbook synthetic.

## Giới hạn phải biết

- App WPF này dùng các control/command thật của SDK; không phải toàn bộ Excel.
  Các surface khác (WinForms, MAUI, split/GPU riêng) không tự trở thành đầy đủ
  chỉ vì xuất hiện trong dự án.
- Thanh công thức phía trên hiện chỉ hiển thị. Hàng tab chưa có nút thêm/
  đổi tên/xóa sheet; có thể dùng API workbook từ app tích hợp riêng.
- Shell Ribbon này chưa gắn thumb scrollbar riêng cho lưới standalone;
  không coi scrollbar của hàng tab là scrollbar worksheet. Ghép scrollbar
  standalone/split và editable formula bar vẫn thuộc nghiệm thu demo tiếp theo.
- Không hỗ trợ Power Query, VBA, add-ins, OLAP; color/icon sort execution và
  một số conditional-format/dxf vẫn có giới hạn preservation đã khai báo.
- UX-007/TABLE-007, native editor lifecycle, screen reader, DPI/multi-monitor/
  touch và performance trên source kết hợp còn phải nghiệm thu. Xem
  `docs/worklog/CURRENT.md` ở đúng source SHA trên GitHub để biết checkpoint.
- Ảnh trong `captures/` được tạo từ app đã publish và dữ liệu synthetic, bằng
  loaded logical-surface capture. Chúng không chứng minh thao tác trực tiếp trên
  màn hình vật lý, mọi màn hình DPI hoặc mọi backend GPU.

Demo này không tự cài đặt, đăng ký file association, cập nhật app khác hoặc
publish các gói NuGet công khai.
