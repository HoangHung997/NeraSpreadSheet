# RIBBON-DIALOGS-003 — nút mở cài đặt nhóm và hộp thoại Avalonia

Chỉ đạo chủ repository 09/09/2026: tiếp tục Ribbon, thêm nút góc dưới phải nhóm như Excel; Number/Font/Alignment và các nhóm phù hợp. Baseline main `66730639278f4304cf0493aa85c0c499df3e8ab7`. Main vẫn canonical; nhánh review chỉ tạm cho task này, không mở lại queue đã archive.

## Phạm vi implementation

Launcher là metadata của RibbonItemDefinition hiện hữu, không có model Ribbon/dispatcher thứ hai. Layout dành ô trong caption, không lấn command body, giữ overflow/minimized/keytips/customization. Nhiều launcher được thêm vào một custom group được xếp cạnh nhau thay vì chồng lên nhau. Preset chỉ quảng bá capability đã có handler.

SDK Avalonia có Format Cells (Số/Phông chữ/Căn chỉnh/Đường viền/Màu nền), Page Setup (Trang/Lề/Trang tính) và Zoom. Sample chỉ nối sáu command: Number (Ctrl+1), Font, Alignment, Page Setup, Print Options, Zoom. Border/Fill nằm trong Format Cells, không thêm nhóm giả trên Ribbon. Không triển khai UI mới đồng loạt WPF/WinForms/MAUI.

Draft giữ session/worksheet/selection/version, vô hiệu khi đổi target, chặn stale callbacks và double apply. Preview không ghi vào cell/style catalog. OK gọi operation/history hiện hữu; Cancel/Esc/X không ghi. Dialog không đổi kiểu dữ liệu hoặc công thức. Mixed selection chỉ sửa thuộc tính đã thay; quét common values tối đa 4096 ô, vùng lớn hiển thị unknown và không materialize toàn sheet. API explicit style patch xử lý trường hợp whole-column có active cell đã bằng giá trị đích nhưng các ô khác khác định dạng.

Number format dùng code chuẩn không phụ thuộc culture, preview qua ExcelCellValueFormatter với culture host. Validate cấu trúc giới hạn 255 ký tự/4 sections; KHÔNG phải parser đầy đủ mọi custom directive Excel. Cỡ chữ, indent, rotation, màu, giấy, margin, scaling đều có validation. Margin không chỉnh giữ nguyên precision inch trong file. Page Setup giữ metadata vùng in/tiêu đề lặp/ngắt trang chưa chỉnh; Zoom chỉ thuộc view, không tạo workbook Undo.

## Ranh giới chưa nhận

Chưa có mọi dialog và tính năng Excel: border editor mới có preset từng ô (không phải engine vẽ cạnh ngoài của vùng); Fill hỗ trợ màu đặc; chưa có worksheet protection enforcement, complete custom-number rendering/Accounting spacing, header/footer designer, print-area picker, clipboard task pane, full Find/Replace/Sort dialogs. Không bỏ các tính năng source đang có. H1 clipboard, locale editing round-trip, IME/phần cứng/screen-reader và native Mac backlog của consolidation vẫn OPEN.

## Kiểm tra và bàn giao

New shared draft tests, native-control dialog tests và launcher geometry/overflow tests cùng toàn bộ regression cũ. `--dialogs-smoke` chạy registered modal commands, Cancel, invalid input, real formatting/Undo/Redo, stale selection và 36 captures (9 views × 4 themes). Manifest gắn exact SHA/hashes/native window; scripted activation không phải vật lý mouse/keyboard hay Excel oracle. Thêm gate vào build sample và actual self-contained published app. Check out giữ gói/ảnh cùng source đã kiểm; chưa được coi PASS chỉ vì chuẩn bị mã.

Kết quả exact commit/CI và review ảnh ghi trong PR receipt sau khi chạy. Không ghi trước số PASS chưa quan sát. Nếu qualification đỏ, main/latest giữ baseline cũ. Rollback bằng reviewed revert, không force rewrite main hoặc xóa archive. Không migration workbook, không publish nuget.org, không thay lịch ngoài repo.
