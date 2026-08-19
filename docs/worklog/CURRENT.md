# Current Work Handoff

- Ngày cập nhật: 2026-08-19
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Implementation commit đã xác minh: `4dd4068d7bf0cc319f9862d8a85082d2c7dce980`
- GitHub Actions: run `32234154347`, CI `#420`, kết luận `success`
- Tài liệu nguồn sự thật: `docs/current-status.md`
- Contract sparse styles: `docs/whole-axis-style-contract.md`

## Mốc đã xác minh

### XLSX style-table chuẩn

- Ghi và đọc font, fill, border, alignment, number format cùng direct cell style ID qua style table SpreadsheetML có deduplication ổn định.
- Cell, row và column dùng style index chuẩn để tệp có khả năng tương tác với phần mềm XLSX bên ngoài.
- Generated package được kiểm tra bằng `OpenXmlValidator`; thứ tự child element của font đã được sửa đúng schema.

### Exact sparse row/column style round-trip

- Custom XML part có version bảo toàn chính xác catalog Nera, row/column style spans và worksheet-global chronological sequence.
- Không materialize các ô trống trên dải hàng/cột rất lớn; bài kiểm thử giữ `UsedCellCount` không đổi và chặn kích thước tệp bất thường.
- Direct-cell complete override và composition giao nhau giữa row/column được khôi phục chính xác.

### Xác minh exact-head đã xanh

CI `#420` tại `4dd4068d7bf0cc319f9862d8a85082d2c7dce980` thành công toàn bộ:

- Core build/tests và architecture verification.
- Windows hosts build/tests cùng desktop GPU runtime smoke.
- MAUI Windows GPU host build.
- OpenXml schema validation, direct style round-trip và huge sparse-axis no-flattening.

## Lát cắt mới hơn đang chốt exact-head

### Gia cố malformed exact style-state

- Từ chối catalog thiếu/default sai hoặc chứa style trùng.
- Từ chối `nextSequence` không hợp lệ, operation sequence vượt biên, patch rỗng, span sai hoặc chồng lấn.
- Từ chối package chứa nhiều Nera style-state part cùng loại.
- XML, base64 và JSON lỗi được quy về `InvalidDataException` trước khi workbook bị restore.
- Có giới hạn kích thước payload, số style, worksheet và span để tránh dữ liệu bất thường gây tiêu thụ bộ nhớ vô hạn.
- Bổ sung test package lỗi thực, không chỉ gọi trực tiếp helper nội bộ.

Lát cắt này chỉ được chuyển sang “đã xác minh” sau khi Core/Windows/MAUI exact-head CI đều xanh và commit/run được ghi lại trong tài liệu.

## Giới hạn còn lại

- Chưa preserve unknown OpenXml parts khi load rồi save sang workbook mới.
- Chưa hỗ trợ theme, named style, differential style, conditional formatting và toàn bộ semantics format-code của Excel.
- MAUI mới có compile gate; chưa có loaded native Window/device smoke và GL-context recreation gate.
- Chưa có Android/iOS/Mac Catalyst build/lifecycle matrix.
- PR tiếp tục Draft; không merge nếu exact-head CI đỏ hoặc chưa xác định.

## File trọng tâm

- `src/NeraSpreadSheet.OpenXml/OpenXmlStyleTableCodec.cs`
- `src/NeraSpreadSheet.OpenXml/NeraOpenXmlStyleStateCodec.cs`
- `src/NeraSpreadSheet.OpenXml/NeraOpenXmlWorkbookSerializer.cs`
- `tests/NeraSpreadSheet.OpenXml.Tests/StyleRoundTripTests.cs`
- `tests/NeraSpreadSheet.OpenXml.Tests/NeraOpenXmlStyleStateValidationTests.cs`
- `src/NeraSpreadSheet.Maui/NeraSpreadsheetView.cs`
- `.github/workflows/ci.yml`
- `docs/current-status.md`

## Bước tiếp theo duy nhất

Sau khi chốt exact-head cho malformed style-state, hoàn thành **loaded MAUI runtime/device lifecycle validation**:

1. mở native MAUI Windows `Window` chứa `NeraSpreadsheetView`;
2. xác nhận first GPU frame, resize và workbook/display-list binding;
3. phát pan, pinch, wheel và tap qua cùng production handlers;
4. ép teardown/recreate GL context rồi xác nhận frame kế tiếp không giữ tài nguyên cũ;
5. thêm Android/iOS/Mac Catalyst build matrix phù hợp khả năng hosted runner;
6. chỉ nâng trạng thái production-validated khi exact-head Core/Windows/MAUI gates đều xanh.
