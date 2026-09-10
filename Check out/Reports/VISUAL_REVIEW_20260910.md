# Review ảnh trước khi tích hợp — 10/09/2026

## Bằng chứng đã tải và kiểm tra độc lập

Source được review: `26950891d2d777968624f09510a5b71e13e4c6fd`, run `34431921468`, artifact `10135169073`. ZIP tải qua connector được đọc bằng môi trường cục bộ (không còn TransportTimeout ở bước này). SHA-256 outer ZIP: `6763af877efa113b47f04a5b0c85c7ce8e28ea56712303f3a33d493b9b3f234a`. CRC cả hai lớp ZIP đạt; đối chiếu hash tất cả ảnh với `Check out/Images/index.json` đạt.

**Đính chính số ảnh:** manifest và byte thực có **1468 PNG**, không phải1608 như PR receipt trước. Phân nhóm: published153×3=459; CI Avalonia459; LegacySDK270; Legacydemo267; MAUIRibbon9; MAUIFilter4. Không nên dùng số ảnh hard-code hoặc suy từ run khác. `CHECKOUT.json` và `Images/index.json` là nguồn số đếm.

Đã trực tiếp xem ảnh Windows compatibility-open và Number nguyên cỡ, cùng contact sheets của đủ9view dialog ở4theme. Nội dung tab, control/input và nút OK/Cancel không chồng lấn trong bộ này; nhiều khoảng trống là thiết kế hiện hành, chưa phải tuyên bố visual parity với Excel. Không nhận đã review trực quan toàn bộ1468ảnh; hash verification khác review hình thức.

## Phát hiện chặn tích hợp và sửa cùng checkpoint

Ảnh `Published/win-x64/compatibility/compatibility-open.png` cho thấy Ribbon/QAT và tab worksheet bị xám dù import đã xong. Đọc actual source phát hiện `RunIo` chỉ trả `_busy=false`, bật split/formula; tabs được dựng trong lúc `_busy=true` và command snapshots được refresh khi còn busy nhưng không refresh lại sau đó. Actual ShellHandler có thể khôi phục một phần command state nhưng không khôi phục native sheet tabs. Không coi CI xanh cũ là bằng chứng giao diện đã đúng.

Sửa tại sample I/O boundary: khi vào/ra I/O, đồng bộ split/formula/tab buttons và refresh cả Ribbon/menu runtime. Giữ native tab identity (không rebuild tabs chỉ để đổi enabled), không chạm workbook/locale/parser, không bỏ opaque metadata restrictions. `finally` áp dụng cho success, picker cancel, I/O exception và cancellation; lệnh I/O lồng nhau vẫn bị chặn. Nếu cửa sổ đã đóng thì không reproject control đã dispose.

Native `--compatibility-smoke` bổ sung checks thật trên handler/projection/native Save, native tab buttons, regular-format command sau mở file và mở lỗi; kiểm busy/empty-operation/exception/cancellation/reentrancy và không thay đổi workbook. Giữ các assert XML, số liệu, source integrity và3captures cũ. Những checks mới phải chạy cả source sample và published executable trên3OS qua pipeline hiện hữu.

## Trạng thái phát hành

File báo cáo này nằm trong source của bản sửa sau review; nó không tự chứng nhận CI của SHA chứa nó. Chờ exact-head qualification mới và review lại ảnh actual mở file đã sửa trước khi fast-forward main. Latest chỉ từ main đã qua toàn bộ gate. Receipt cuối ở PR9 phải ghi source/run/hash/image-count thực tế của bản main, không mượn green của26950891 hoặc screenshot cũ.

Không sửa shared CURRENT/status. Core/SDK legacy giữ nguyên. Phạm vi compatibility chỉ đọc dữ liệu + warning + opaque preservation có giới hạn; inner-border rendering, duplicate/unique evaluation, full locale/structural fidelity, real workbook và physical accessibility vẫn OPEN.
