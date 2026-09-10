# RIBBON-DIALOGS-003 — nút mở cài đặt nhóm và hộp thoại Avalonia

Chỉ đạo chủ repository: tiếp tục Ribbon, bổ sung nút góc dưới bên phải nhóm như Excel và các hộp thoại tương ứng. Baseline main: `66730639278f4304cf0493aa85c0c499df3e8ab7`. Nhánh `feature/ribbon-dialogs-003` / PR #9 là nhánh review tạm của task này; không mở lại các hàng đợi đã lưu trữ. Main vẫn là nguồn canonical.

## Cập nhật 10/09: đã xác minh CI của 15518cee, đang sửa đồng bộ tab Số

Run **34362424590** (push, attempt 1) tại **15518ceeb521022d7a7a12f7457f8d6ccdab3d79** đã hoàn tất SUCCESS. Trong 29 job có 28 SUCCESS và một job cleanup SKIPPED đúng điều kiện không phải main. Toàn bộ bảy workflow chất lượng, ba host Avalonia, ba gói ứng dụng self-contained và bước tập hợp download centre đều đạt.

Đã đọc trực tiếp log Windows Avalonia job `102502513289`: build 0 warning / 0 error; đồ thị 18 project Release đầy đủ, 17 DLL trong sample khớp nguồn; 179 test chạy trong năm tiến trình độc lập đều PASS, không skip; kiểm tra kiến trúc PASS. Native smoke: cơ bản 12, full UI 23, Formula UX 23; Ribbon 203 điều kiện / 84 bố cục / 109 ảnh; dialog 80 điều kiện / 36 ảnh.

Đã đọc log actual published Windows job `102504220867`: executable trong `artifacts/app` chạy đủ năm smoke trên đúng source 155. SHA256 của **inner application ZIP** là `84b0f8c4c322eb3023500796af43f4bcba48ef4715a3fd6781551b94f4b4bc20`. **Outer artifact** `10108699906` có 82.585.110 byte và SHA256 `e7ad29908bc04fa8d768d3e4e54e3ab3c39c474d237a8dadda29127069c6fba9`. Không lẫn checksum của hai lớp ZIP.

Artifact index `10108870075` và ứng dụng Windows đã tải qua connector. Local unzip/PNG review vẫn timeout: không nhận đã tự tính lại hash hoặc xem ảnh. Verifier ghi `visualReviewPerformed=false` rõ ràng. Không chạy lại run đỏ đến khi xanh; kết quả chuẩn bị mã không thay cho kết quả của commit sản phẩm.

### Bổ sung trong commit chứa tài liệu này

Có 24 trường hợp regression mới trong `NumberDialogSynchronizationTests`, cùng phần sửa tab Số:

- Khôi phục các trường điều khiển từ mã chính xác do generator hiện có hỗ trợ: số thường, phần trăm, khoa học, ngày/giờ, phân số và văn bản. Ví dụ `#,##0.000` mở đúng loại Số, ba chữ số thập phân và phân nhóm hàng nghìn. Không tự ghi lại mã khi mở hộp thoại.
- Chỉ nhận diện các dạng canonical đã biết. Mã có locale, tiền tệ/custom phức tạp hoặc vùng chọn nhiều định dạng tiếp tục giữ nguyên văn ở Custom. Đây là khôi phục trường UI, không phải parser XLSX hoặc formatting engine thứ hai.
- Chỉ bật tham số phù hợp: số chữ số thập phân cho các loại có phần lẻ; ký hiệu tiền tệ cho Currency/Accounting; số âm trong ngoặc cho Number/Currency. Tham số không liên quan không được chặn một mã hợp lệ.
- Gõ mã trực tiếp chuyển sang Custom, xóa lỗi còn lại của bộ sinh mã và vô hiệu hóa các helper không liên quan. Mã tùy chỉnh sai vẫn bị validator cũ từ chối. Cập nhật từ generator không bị nhận nhầm thành nhập tay hoặc tạo vòng sự kiện.
- `HasPendingChanges` phản ánh cả tham số generator chưa hợp lệ. Trả mã về đúng nội dung ban đầu không tạo Undo hoặc làm mất Redo. Preview và chuyển loại định dạng không chuyển giá trị/công thức của ô.
- Tách phần Number thành partial `NeraFormatCellsDialog.Number.cs`, giữ các tab khác và shared engine. Không sửa workflow, bỏ gate, thay main hoặc tăng quyền runner.

**CI xanh của 155 không áp dụng cho commit chứa bổ sung mới này.** Phải chạy canonical Check out mới tại đúng HEAD. Ảnh và gói main/latest vẫn ở baseline 667 cho đến khi hoàn tất qualification, review và integration.

## Phạm vi implementation

Launcher là metadata `RibbonItemDefinition.IsDialogLauncher` của item hiện hữu, tạo bằng `DialogLauncher(CommandId)`. Không có model Ribbon hoặc dispatcher thứ hai. Shared layout dành vị trí trong hàng caption, không lấn command body, giữ overflow, minimized, keytips và customization. Nhiều launcher trong một nhóm tùy chỉnh được xếp cạnh nhau, không chồng lên nhau. Preset chỉ hiện capability có handler thật.

SDK Avalonia cung cấp:

| Hộp thoại | Các tab/chức năng |
|---|---|
| Format Cells | Số, Phông chữ, Căn chỉnh, Đường viền, Màu nền |
| Page Setup | Trang, Lề, Trang tính |
| Zoom | Tỷ lệ xem; không thay dữ liệu hoặc tỷ lệ in |

Sample nối sáu command: Number (`Ctrl+1`), Font, Alignment, Page Setup, Print Options và Zoom. Border/Fill thuộc Format Cells, không tạo thêm nhóm giả. Không phát triển các hộp thoại mới song song cho WPF/WinForms/MAUI.

Draft giữ session, worksheet, selection và version, vô hiệu khi mục tiêu thay đổi; chặn callback cũ và áp dụng hai lần. Preview không ghi vào ô hoặc style catalog. OK sử dụng operation/history hiện hữu; Cancel/Esc/X không ghi. Vùng chọn nhiều định dạng chỉ nhận thuộc tính người dùng sửa. Việc đọc giá trị chung giới hạn tối đa 4.096 ô; vùng lớn hiển thị unknown, không mở rộng toàn sheet.

`SpreadsheetStyleController.ApplyPatchToSelection` xử lý ý định áp dụng thuộc tính cụ thể cho nguyên cột kể cả khi active cell đã bằng giá trị đích nhưng các ô khác chưa bằng. API transform cũ giữ nguyên mặc định.

Mã định dạng chuẩn không phụ thuộc culture. Preview dùng `ExcelCellValueFormatter` và culture của host. Validator giới hạn 255 ký tự / bốn section và kiểm tra cấu trúc; không phải toàn bộ ngữ pháp format của Excel. Các trường cỡ chữ, thụt lề, góc xoay, màu, giấy, lề và tỷ lệ đều có validation. Lề không chỉnh giữ độ chính xác inch trong file. Page Setup giữ vùng in, tiêu đề lặp và ngắt trang không sửa.

Format/Page dialogs có `IDisposable` trên UI thread để giải phóng draft kể cả khi chưa Show; Closed cũng giải phóng. Sample dùng `using` và `finally`, không giữ đăng ký sự kiện, cửa sổ đã hỏng hoặc trạng thái disabled khi ShowDialog lỗi trước Closed.

## Lịch sử lỗi đã sửa — không hạ tiêu chuẩn kiểm thử

1. Script chuẩn bị tìm sai các đoạn caption, workflow và shell. Đã đối chiếu source thật, giữ kiểm tra số lần thay thế. Bash pipeline phải truyền mã lỗi build/test qua `tee`.
2. Sửa `Watermark` thành `PlaceholderText` theo Avalonia đang dùng; giải quyết CA1001 bằng Dispose, import đúng namespace và sử dụng mảng fixture tĩnh cho CA1861. Không tắt analyzer.
3. Run `34358105484` chuẩn bị source: build 0 warning/error, Editing 431 / Avalonia 170 / Python 20 PASS. Runner không có quyền đưa workflow vào tree, nên chuyển code tree và workflow blobs cho connector có quyền; không tăng quyền runner. Script/workflow tạm được gỡ từ `b3e44e21`.
4. Commit `1d0154efe3847990411a7de771effa57b84246d5`, run `34360093619`: Ubuntu 175 test × năm lượt PASS, nhưng Ribbon visual FAIL. Log `102494551150` cho thấy Light/820/home/editing kết thúc ở 821 trong client 820. Sáu nhóm có độ rộng 96,5 / 349,5 / 92 / 134 / 66 / 72; hai nhóm lẻ bị native làm tròn lên riêng. Không nới dung sai 0,6 hoặc bỏ item.
5. Commit 155 bổ sung `RibbonLayoutRequest.RoundGroupWidthsToPixels` dạng opt-in: ngân sách group/gap/overflow tính theo pixel vật lý trước khi thu gọn. Chỉ sửa chrome, không thay item measurement hoặc vị trí cuộn. Mặc định legacy vẫn false. Có fixture từ số đo thật và native test tại 819/820/821/1.024. Qualification 155 đã xác nhận bản sửa.

## Ranh giới còn mở

Chưa có mọi hộp thoại/tính năng Excel: border editor hiện dùng preset từng ô, chưa là trình sửa cạnh ngoài của cả vùng; Fill chỉ sửa màu đặc; chưa có thực thi bảo vệ worksheet, rendering toàn bộ custom format/Accounting spacing, trình thiết kế đầu/cuối trang, chọn vùng in, clipboard task pane hoặc đầy đủ Find/Replace/Sort dialogs.

H1 clipboard, locale editor/TSV, IME, phần cứng, screen reader và backlog native Mac vẫn OPEN. Không dùng preview số trong dialog để tuyên bố đã sửa toàn bộ locale hoặc tương thích Excel hoàn toàn.

## Kiểm tra và bàn giao

Giữ các test draft/number/page, native dialog/lifecycle/rounding, OpenXML round-trip, launcher geometry/overflow và toàn bộ regression cũ. `--dialogs-smoke` chạy các command modal đã đăng ký, Cancel, dữ liệu sai, định dạng/Undo/Redo, stale target và 36 ảnh (chín trang × bốn theme). Manifest có exact SHA/hash/native window; scripted input không phải kiểm thử chuột/bàn phím vật lý hoặc Excel oracle.

Cả sample build và executable self-contained đều chạy gate. Gói Check out chứa chín ảnh dialog riêng cùng App, Images, Reports và Licenses. Kết quả final HEAD ghi vào PR receipt sau khi chạy; không kế thừa PASS của working tree/commit cha. Connector đọc, ghi và CI hoạt động; timeout local là vấn đề riêng. Không nhận đã xem ảnh khi chỉ đọc log. Nếu qualification đỏ, giữ main/latest cũ.

Rollback bằng reviewed revert, không force-rewrite main hoặc xóa archive. Không migration workbook, không publish nuget.org, không thay lịch ngoài repository. Bước tiếp theo: xác minh canonical Check out ở HEAD mới, review ảnh rồi cập nhật bản bàn giao đúng source.
