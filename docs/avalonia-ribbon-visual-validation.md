# Avalonia PRIMARY-002 — contract trình bày và bằng chứng

Áp dụng trên nhánh `feature/avalonia-primary-ribbon-002`, kế thừa PR4. Đây không
phải chứng nhận tương đương toàn bộ Excel hoặc nghiệm thu thị giác tự động.
Định hướng chung ở `avalonia-first-ui-strategy.md`; không sửa các host cũ.

## Native projection

Ribbon.Core vẫn tạo snapshot/geometry cho tất cả tab và xử lý command, key tips,
customization. Presenter chỉ materialize body của tab đang chọn. Tab ẩn không
có native body nhưng command/shortcut hợp lệ của nó vẫn được runtime giữ.
Đổi tab bằng native TabControl cập nhật SelectedTabId của presenter và layout;
body cũ được giải phóng. Minimize/backstage không giữ body không nhìn thấy.
Resize/state refresh vẫn coalesce; không chạm workbook hoặc tính công thức.

Đường phân nhóm nằm trong width shared đã cấp, không cộng một DIP border ngoài
slot. Font/theme/icon fallback và automation caption vẫn giữ. IconRequestResolver
thay đổi sẽ refresh projection; caller sở hữu ảnh từ custom resolver. Các nút
thu gọn/tùy biến dùng catalog icon, không phụ thuộc emoji của hệ điều hành.

Bar và customization có resource dictionary riêng, dùng palette của SDK, không
sửa Application.Resources hoặc palette của control khác. Customization đổi theme
không tạo profile edit; Apply/Cancel và inherited/explicit-empty QAT giữ contract.
Các cột có nhãn, actions wrap, JSON nằm trong vùng mở rộng có giới hạn chiều cao.
ThemeVariant của field controls không chứng minh toàn bộ WCAG/screen-reader.

## Native matrix

`--ribbon-visual-smoke` chạy trong FullShellWindow thật. Nó kiểm seven-tab preset,
84 tổ hợp (7 tab × 3 width 820/1024/1536 DIP × 4 palette), native geometry khớp
shared layout, group không chồng/tràn, caption area, tab density và icon có thật.
Có capture popup font, disabled presentation, keyboard focus, backstage,
customization và full window. Các capture raster 1/1.25/1.5/2 là thử raster scale,
KHÔNG giả monitor-DPI switching hoặc touch/IME/physical-input acceptance.

Manifest chứa exact source SHA, geometry, SHA256 ảnh, kích thước và kiểm tra
không đổi worksheet/selection/command/profile. Script kiểm tra đủ ma trận và
hash. Chụp bitmap từ control gắn trong native window không đồng nghĩa đã so
pixel với WPF/Excel; phải xem ảnh thật và kiểm tra thêm tương tác phần cứng.
Các ba native smokes cơ bản/full-shell/Formula UX trước đó vẫn bắt buộc, không
được thay bằng ma trận mới. Không giảm tests/assertions cũ để đạt gate.

## Phép đo trước/sau

Probe mới là workload tổng hợp cố định 720 command/9 tab, ba width và mỗi width
3 warmup +7 lượt Rebuild/UpdateLayout. Cùng một file harness được chạy đối với
product baseline `2c27bd65e2b6022a374560165821921078982241` và product candidate.
CI chỉ copy file probe test vào checkout baseline biệt lập; không sửa source
baseline, không commit hoặc đổi ref của nhánh khác. So hash harness, Git SHA và
DLL thực nạp với DLL build Release. Toàn bộ test hiện hành vẫn chạy riêng 5
process; filter baseline chỉ phục vụ đo một probe, không coi là full regression.

Báo cáo median thời gian và allocated bytes trên UI test thread, raw samples
được giữ. Assert số body 9→1; không ép thời gian phải xanh hoặc thử lại tới khi
nhanh hơn. Đây là short-run trên runner khác cấu hình giữa OS; chỉ so cặp trên
cùng runner. Không suy thành hiệu năng cuộn worksheet, render GPU, H2 edit hay
ngưỡng release. Chưa có số đo trước khi Actions thực sự chạy.

## Phạm vi chưa thực hiện

Locale/input và initial per-sheet/window state phải phối hợp PR5, không được
sao chép model vào UI. Table/Filter khác tiếp tục theo trạng thái tạm dừng.
Không merge, Ready hoặc publish NuGet; worklog riêng và CI tại exact HEAD cuối
là nguồn bàn giao. Source/theme có thật không thay bằng chứng thị giác.
