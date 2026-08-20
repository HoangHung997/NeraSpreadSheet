# Current Work Handoff

- Ngày cập nhật: 2026-08-20
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Implementation commit đã xác minh: `6fb4684f0dd5361b584f7d98fde55cf449e0642c`
- GitHub Actions: run `32341414045`, CI `#463`, kết luận `success`
- Tài liệu nguồn sự thật: `docs/current-status.md`
- Roadmap còn lại: `ROADMAP.md`
- Contract MAUI scale: `docs/maui-surface-scale-contract.md`
- Contract sparse styles: `docs/whole-axis-style-contract.md`

## Task vừa hoàn thành

### Shared-formula import và A1 reference translation

Đã thêm `OpenXmlSharedFormulaImportResolver` và nối trực tiếp vào `NeraOpenXmlWorkbookSerializer.ImportCells`.

Resolver chạy hai lượt trên `sheetData`:

1. thu thập mọi shared-formula anchor có formula text, shared index và range;
2. xác minh follower rồi dịch formula từ anchor tới đúng cell hiện hữu.

Lượt thứ nhất hoàn tất trước khi follower được xử lý, vì vậy follower có thể đứng trước anchor trong thứ tự XML mà vẫn nhập đúng.

### Quy tắc dịch công thức

Shared import dùng lại `NeraSpreadSheet.Editing.FormulaReferenceTranslator`, cùng engine hiện dùng cho copy/paste và structural editing.

Gate khóa các trường hợp:

- relative `A1` dịch cả hàng và cột;
- mixed `$A1` chỉ dịch hàng;
- mixed `A$1` chỉ dịch cột;
- absolute `$A$1` giữ nguyên;
- quoted sheet name như `'Other Sheet'!A1` vẫn dịch đúng cell reference;
- chuỗi `"A1"` không bị hiểu nhầm là reference;
- follower ở cả hướng ngang, dọc và chéo.

### Sparse và cached-value policy

- Không lặp qua toàn shared range.
- Không tạo cell cho vị trí không có trong `sheetData`.
- Chỉ dịch những shared-formula cell thực sự tồn tại trong package.
- `LoadCachedFormulaValues=true` giữ cached result.
- `LoadCachedFormulaValues=false` bỏ cached result nhưng vẫn giữ formula đã mở rộng.

### Malformed shared-formula rejection

Các trường hợp sau ném `InvalidDataException` trước khi `Worksheet.SetCells` được gọi:

- missing anchor;
- duplicate anchor index;
- missing shared index;
- missing hoặc reversed anchor range;
- anchor nằm ngoài range;
- follower nằm ngoài range;
- follower khai báo range riêng;
- shared-formula cell không có A1 cell reference hợp lệ;
- vượt giới hạn `100,000` shared groups trên worksheet.

### Automated tests

Đã thêm `SharedFormulaImportTests.cs` với chín test chính:

- mở rộng relative/mixed/absolute references;
- giữ cached values;
- bỏ cached values mà không bỏ formulas;
- follower-before-anchor;
- missing anchor;
- duplicate anchor;
- follower outside range;
- missing shared index;
- reversed range;
- follower-owned range.

Fixture shared formula hợp lệ cũng vượt `OpenXmlValidator(FileFormatVersions.Office2013)`.

## Exact CI #463

Toàn bộ matrix xanh tại implementation commit `6fb4684f...`:

- Core restore/build/tests.
- Architecture verification.
- Toàn bộ OpenXml tests mới và cũ.
- Exact sparse style, no-flattening và malformed-input gates.
- Nested unknown-package graph preservation.
- Windows full build/tests và desktop GPU runtime smoke.
- MAUI Android build.
- MAUI iOS và Mac Catalyst builds.
- MAUI Windows build/tests.
- Loaded production input/resize/context recreation smoke.
- Loaded logical/raw scale and orientation smoke.

## File trọng tâm của task

- `src/NeraSpreadSheet.OpenXml/OpenXmlSharedFormulaImportResolver.cs`
- `src/NeraSpreadSheet.OpenXml/NeraOpenXmlWorkbookSerializer.cs`
- `tests/NeraSpreadSheet.OpenXml.Tests/SharedFormulaImportTests.cs`
- `src/NeraSpreadSheet.Editing/FormulaReferenceTranslator.cs`
- `docs/current-status.md`
- `ROADMAP.md`

## Quyết định kỹ thuật đã khóa

- Shared formula được mở rộng thành independent Nera formula khi import.
- Không lưu trạng thái shared group vào Core model.
- Import và copy/paste phải dùng cùng một A1 translation engine.
- Không materialize declared shared range.
- Package lỗi phải bị từ chối trước khi áp dụng cell changes.
- Export chưa tự động tạo shared groups cho đến khi chứng minh được rectangular grouping và normal-formula fallback an toàn.

## Tiến độ tổng thể sau task

- Nền móng engine/viewport/renderer: khoảng `85%`.
- MVP bảng tính cơ bản: khoảng `66–70%`.
- Toàn bộ roadmap chuyên nghiệp: khoảng `44%`.
- Production release readiness: khoảng `20–25%`.

So với mốc trước khoảng `43–44%`, task này tăng tổng thể khoảng `0,5–1` điểm phần trăm. Mức tăng bị giới hạn vì shared-formula export, complete round-trip corpus và structural identity tests vẫn chưa hoàn thành.

## Giới hạn còn lại

- Save hiện vẫn ghi normal formula cho từng cell; chưa compact thành `<f t="shared">`.
- Chưa có shared-index allocation và rectangular group proof khi export.
- Chưa có repeated-save compatibility corpus từ Excel/LibreOffice/third-party generators.
- Dynamic arrays, structured references, conditional formatting, validation và tables chưa có.
- PR tiếp tục Draft; không merge khi exact-head CI đỏ hoặc chưa xác định.

## Bước tiếp theo duy nhất

Triển khai **shared-formula export grouping và complete round-trip**:

1. chuẩn hóa formula theo candidate anchor bằng reverse translation;
2. chỉ nhóm cells tạo thành rectangle liên tục và có style/value semantics tương thích;
3. cấp shared index ổn định theo worksheet order;
4. anchor ghi formula text + `ref` + `si`;
5. follower chỉ ghi `t="shared"` + `si`;
6. fallback normal formula nếu có gap, `#REF!`, unsupported token hoặc ambiguity;
7. giữ cached values theo export option;
8. round-trip import → edit → export → re-import;
9. repeated preservation save không làm mất unknown parts;
10. insert/delete/reorder phải giữ logical formula identity trước khi regroup;
11. thêm malformed-output, schema validation và compatibility tests;
12. chỉ cập nhật mốc hoàn thành sau exact-head Core/Windows/MAUI CI xanh.
