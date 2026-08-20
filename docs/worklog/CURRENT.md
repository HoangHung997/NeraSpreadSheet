# Current Work Handoff

- Ngày cập nhật: 2026-08-20
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Implementation commit đã xác minh: `75b8292f060eccaaa7caff1fbed88f650f68ea7f`
- GitHub Actions: run `32330382258`, CI `#449`, kết luận `success`
- Tài liệu nguồn sự thật: `docs/current-status.md`
- Contract scale MAUI: `docs/maui-surface-scale-contract.md`
- Contract sparse styles: `docs/whole-axis-style-contract.md`

## Mốc vừa hoàn thành

### Unknown OpenXml package-part preservation

`NeraOpenXmlWorkbookSerializer` hiện hỗ trợ `PreserveUnknownParts=true`.

Kiến trúc đã khóa:

1. Khi load, toàn bộ package XLSX gốc được chụp vào `OpenXmlPackageEnvelope` nội bộ.
2. Envelope gắn với đúng `Workbook` bằng `ConditionalWeakTable`; Core không tham chiếu `DocumentFormat.OpenXml` và không có Microsoft type trong public contract.
3. Capture bị giới hạn 512 MiB.
4. Envelope giữ worksheet object identity, relationship ID và part URI theo đúng thứ tự sheet.
5. Khi save, Nera dựng một package chuẩn mới trong memory để lấy phần markup mà Nera sở hữu.
6. Sau đó Nera clone package gốc và chỉ patch các vùng được phép, thay vì dựng lại opaque relationship graph.
7. Chỉ sau khi merge thành công mới ghi bytes hoàn chỉnh vào destination.
8. Save thành công sẽ chụp lại output làm envelope mới, nhờ đó repeated save tiếp tục từ package gần nhất.

### Vùng Nera được phép thay

- Tên worksheet trong workbook markup.
- Worksheet `cols`.
- Worksheet `sheetData`.
- Worksheet `mergeCells`.
- Style-table children:
  - `numFmts`;
  - `fonts`;
  - `fills`;
  - `borders`;
  - `cellStyleXfs`;
  - `cellXfs`;
  - `cellStyles`.
- Nera exact sparse style-state custom part.

Mọi workbook/worksheet/stylesheet markup khác được giữ nguyên. Các phần tử mới do Nera chèn được đặt theo SpreadsheetML schema order.

### Invariants đã có test

Round-trip rename/edit và repeated save giữ nguyên:

- opaque `ExtendedPart` dưới workbook;
- opaque `ExtendedPart` dưới worksheet;
- relationship ID;
- part URI;
- relationship type;
- content type;
- raw binary/XML bytes;
- external worksheet relationship và target URI;
- workbook `extLst` payload ngoài vùng Nera sở hữu;
- worksheet `extLst` payload ngoài vùng Nera sở hữu;
- stylesheet `extLst` payload ngoài vùng Nera sở hữu;
- worksheet rename;
- cell edits của lần save thứ nhất và thứ hai.

Test failure atomicity:

- load package với preservation;
- thêm worksheet làm topology không còn ánh xạ được;
- destination đã có sentinel bytes;
- save phải ném `InvalidOperationException` trước merge/write;
- destination phải giữ nguyên sentinel bytes.

New workbook cũng có thể save với preservation bật; output đầu tiên trở thành baseline envelope.

## Validation exact implementation

CI `#449` xanh toàn bộ:

- Core restore/build/tests.
- Architecture verification.
- Unknown-part preservation tests.
- XLSX style fidelity, no-flattening và malformed-input hardening.
- Full Windows build/tests.
- Windows desktop GPU runtime smoke.
- MAUI Android build.
- MAUI iOS build.
- MAUI Mac Catalyst build.
- MAUI Windows build/tests.
- Loaded repeated input/resize/context-recreation smoke.
- Loaded scale/orientation/width-class smoke.

Hai lỗi ở lượt CI đầu chỉ là analyzer và đã được sửa:

- dùng `StartsWith(char)` thay cho `StartsWith(string)`;
- trả về `Dictionary<string, int>` cụ thể cho schema-order factory.

## Giới hạn có chủ ý

- Preserve mode chỉ nhận ordinary worksheet topology.
- Chart sheet và dialog sheet bị từ chối.
- Thêm/xóa/đổi thứ tự worksheet sau load bị từ chối; rename vẫn được phép.
- Unknown formula, defined name, table, drawing và vendor-extension semantics không được tự sửa.
- Envelope hiện giữ package trong memory và giới hạn 512 MiB.
- Package copy giữ nguyên nested parts theo bytes/relationship graph, nhưng chưa có fixture riêng cho drawing/media và package-root opaque relationships.
- Save với `PreserveUnknownParts=false` là full Nera rewrite và chủ động bỏ envelope cũ.
- PR tiếp tục Draft; không merge nếu exact-head CI đỏ hoặc chưa xác định.

## File trọng tâm

- `src/NeraSpreadSheet.OpenXml/OpenXmlPackageEnvelope.cs`
- `src/NeraSpreadSheet.OpenXml/OpenXmlPackagePreserver.cs`
- `src/NeraSpreadSheet.OpenXml/NeraOpenXmlWorkbookSerializer.cs`
- `tests/NeraSpreadSheet.OpenXml.Tests/OpenXmlRoundTripTests.cs`
- `tests/NeraSpreadSheet.OpenXml.Tests/UnknownPartPreservationTests.cs`
- `docs/current-status.md`
- `docs/worklog/CURRENT.md`

## Bước tiếp theo duy nhất

Gia cố **unknown package graph preservation** trước khi chuyển sang shared formulas:

1. thêm nested opaque relationship fixture;
2. thêm standard drawing part + image/media bytes và worksheet drawing reference;
3. thêm non-Nera custom XML part cùng properties/relationships;
4. thêm package-root relationship fixture;
5. thêm duplicate/conflicting relationship ID, unsafe URI và malformed relationship tests;
6. chạy `OpenXmlValidator` sau rename/edit và repeated save;
7. xác nhận failure atomicity khi merge phát hiện conflict;
8. sau khi exact-head xanh mới chuyển sang shared-formula import/export.
