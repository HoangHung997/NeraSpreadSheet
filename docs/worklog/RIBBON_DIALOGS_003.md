# RIBBON-DIALOGS-003 — nút mở cài đặt nhóm và hộp thoại Avalonia

Chỉ đạo chủ repository 09/09/2026: tiếp tục Ribbon, thêm nút góc dưới phải nhóm như Excel; Number/Font/Alignment và các nhóm phù hợp. Baseline main `66730639278f4304cf0493aa85c0c499df3e8ab7`. Main vẫn canonical; PR #9 / `feature/ribbon-dialogs-003` chỉ là nhánh review tạm cho task này, không mở lại queue đã archive.

## Phạm vi implementation

Launcher là metadata `RibbonItemDefinition.IsDialogLauncher` của item hiện hữu, tạo bằng `DialogLauncher(CommandId)`. Không có model Ribbon/dispatcher thứ hai. Layout dành ô trong caption, không lấn command body, giữ overflow/minimized/keytips/customization. Nhiều launcher trong một custom group được xếp cạnh nhau thay vì chồng lên nhau. Preset chỉ quảng bá capability đã có handler.

SDK Avalonia có Format Cells (Số/Phông chữ/Căn chỉnh/Đường viền/Màu nền), Page Setup (Trang/Lề/Trang tính) và Zoom. Sample nối sáu command: Number (`Ctrl+1`), Font, Alignment, Page Setup, Print Options, Zoom. Border/Fill nằm trong Format Cells, không thêm nhóm giả trên Ribbon. Không triển khai UI mới đồng loạt WPF/WinForms/MAUI.

Draft giữ session/worksheet/selection/version, vô hiệu khi đổi target, chặn stale callbacks và double apply. Preview không ghi vào cell/style catalog. OK gọi operation/history hiện hữu; Cancel/Esc/X không ghi. Dialog không đổi kiểu dữ liệu hoặc công thức. Mixed selection chỉ sửa thuộc tính đã thay; quét common values tối đa 4096 ô, vùng lớn hiển thị unknown và không materialize toàn sheet. API `SpreadsheetStyleController.ApplyPatchToSelection` xử lý ý định explicit whole-column ngay cả khi active cell đã bằng giá trị đích nhưng các ô khác khác định dạng. API transform cũ giữ nguyên mặc định.

Number format dùng code chuẩn không phụ thuộc culture; preview qua `ExcelCellValueFormatter` với culture host. Validate cấu trúc giới hạn 255 ký tự/4 sections; KHÔNG phải parser đầy đủ mọi custom directive Excel. Cỡ chữ, indent, rotation, màu, giấy, margin, scaling đều có validation. Thông số helper số không hợp lệ phải chặn OK, không lén áp dụng code hợp lệ trước đó. Khi trả trường nhập về nguyên nội dung ban đầu thì không tạo edit/không mất Redo. Margin không chỉnh giữ nguyên precision inch trong file. Page Setup giữ metadata vùng in/tiêu đề lặp/ngắt trang chưa chỉnh; Zoom chỉ thuộc view, không tạo workbook Undo.

Format/Page dialogs có `IDisposable` trên UI thread để giải phóng draft kể cả chưa Show; Closed cũng giải phóng. Sample dùng `using` và finally, không để lại subscription, tracked window hay trạng thái disabled khi ShowDialog lỗi trước Closed. Các API dialog vẫn trả kết quả riêng, không nạp workbook/engine thứ hai.

## Đã sửa sau khi thực thi, không bỏ gate

1. Temporary preparer tìm sai anchor caption/workflow/shell. Đã đối chiếu source, giữ replacement-count guards. Bash pipeline phải truyền mã lỗi build/test qua `tee`.
2. Avalonia 12.1.2 yêu cầu `PlaceholderText` thay `Watermark`; sửa ownership CA1001 bằng Dispose, import extension đúng namespace và dùng fixture arrays tĩnh theo CA1861. Không tắt analyzer.
3. Run `34358105484` chuẩn bị source đã build 0 warning/error, Editing431/Avalonia170/Python20 PASS. Runner token không có quyền đưa workflow vào tree: code tree và workflow blobs được handoff cho connector có quyền, không tăng quyền runner hay gọi CI chuẩn bị là CI của final source. Temp preparer/workflow đã gỡ khỏi tree từ `b3e44e21`.
4. Bản actual `1d0154efe3847990411a7de771effa57b84246d5`, run `34360093619`: Ubuntu build0warning/error, Avalonia175 ×5 PASS/0skip, architecture và native basic/full/formula PASS, nhưng **Ribbon visual FAIL**. Log job102494551150 chỉ rõ Light/820/home/editing right821 > client820; shared widths 96.5+349.5+92+134+66+72 + gaps =820 nhưng hai native desired widths bị làm tròn lên riêng. Đây là lỗi geometry thật; không nới ngưỡng0.6 hoặc bỏ item.
5. Sửa bằng `RibbonLayoutRequest.RoundGroupWidthsToPixels` opt-in: ngân sách tính từng group và dự phòng gaps/overflow theo physical pixels trước khi chọn compact/overflow. Chỉ width chrome bị làm tròn, không thay item measurement hay document scroll. Avalonia chọn theo `UseLayoutRounding`; request mặc định false giữ hành vi legacy. Bổ sung fixture từ sáu width đã đo và native tests tại819/820/821/1024; giữ đầy đủ command/launcher trong snapshot, kể cả overflow.

## Ranh giới chưa nhận

Chưa có mọi dialog/tính năng Excel: border editor mới có preset từng ô (không phải engine vẽ cạnh ngoài của vùng); Fill hỗ trợ màu đặc; chưa có worksheet protection enforcement, complete custom-number rendering/Accounting spacing, header/footer designer, print-area picker, clipboard task pane, full Find/Replace/Sort dialogs. Không bỏ các tính năng source đang có. H1 clipboard, locale editing round-trip, IME/phần cứng/screen-reader và native Mac backlog của consolidation vẫn OPEN. Không lấy preview number format của dialog để tuyên bố đã sửa tất cả locale editor/TSV.

## Kiểm tra và bàn giao

Shared draft/number/page tests, native-control dialog/rounding/lifecycle tests, OpenXML formatting round-trip, launcher geometry/overflow tests cùng toàn bộ regression cũ. `--dialogs-smoke` chạy registered modal commands, Cancel, invalid input, real formatting/Undo/Redo, stale selection và 36 captures (9 views ×4 themes). Manifest gắn exact SHA/hashes/native window; scripted activation không phải vật lý mouse/keyboard hay Excel oracle. Gate chạy cả build sample và actual self-contained published app. `Check out` nhận9ảnh dialog riêng cùng app/images/reports/licenses, không bỏ số liệu nguồn hoặc dùng ảnh cũ giả làm bản mới.

**Bản chứa thay đổi này vẫn cần qualification tại đúng SHA cuối.** Kết quả actual run/test/image review ghi trong PR receipt sau khi chạy; không kế thừa PASS của working tree chuẩn bị/commit cha. Môi trường shell/Python local đang timeout; không dùng điều đó để nói connector không ghi được, và không nhận đã xem ảnh mới nếu chỉ đọc log. Nếu qualification đỏ, main/latest giữ baseline cũ.

Rollback bằng reviewed revert, không force rewrite main hoặc xóa archive. Không migration workbook, không publish nuget.org, không thay lịch ngoài repo. Một bước tiếp theo: chạy canonical Check out trên HEAD có pixel-budget fix, đọc actual native Ribbon/dialog results rồi kiểm ảnh trước integration/delivery.
