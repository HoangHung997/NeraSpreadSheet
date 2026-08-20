# Current Work Handoff

- Ngày cập nhật: 2026-08-20
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Implementation commit đã xác minh: `d6808102298920ae868b86713341f2ccc1970594`
- GitHub Actions: run `32347684027`, CI `#469`, kết luận `success`
- Tài liệu nguồn sự thật: `docs/current-status.md`
- Roadmap còn lại: `ROADMAP.md`
- Contract MAUI scale: `docs/maui-surface-scale-contract.md`
- Contract sparse styles: `docs/whole-axis-style-contract.md`

## Batch vừa hoàn thành

Phiên này gom tuần tự năm lát cắt phụ thuộc nhau:

1. shared-formula export grouping;
2. round-trip, stable index, fallback và cached-value gates;
3. insert/delete regroup cùng reorder-safe fallback;
4. repeated preservation save giữ opaque part và shared groups;
5. đồng bộ roadmap, feature matrix, current status và handoff.

### Shared-formula export plan

Đã thêm `OpenXmlSharedFormulaExportPlan` và nối trực tiếp vào `NeraOpenXmlWorkbookSerializer.BuildWorksheet`.

Plan chỉ đọc các formula cell đã tồn tại trong sparse worksheet. Nó không tạo hoặc quét toàn logical range.

Một group chỉ được tạo khi:

- có ít nhất hai formula cell;
- các cell tạo thành rectangle liên tục không có gap;
- mọi formula đều thuộc tập token hiện được hỗ trợ;
- dịch anchor → follower bằng `FormulaReferenceTranslator` cho kết quả trùng tuyệt đối;
- dịch follower → anchor cũng phục hồi đúng formula ban đầu.

Bidirectional proof ngăn việc compact một nhóm chỉ tình cờ giống nhau theo một chiều.

### SpreadsheetML output

Shared index được cấp ổn định theo thứ tự row-major của worksheet.

Anchor ghi:

```xml
<f t="shared" si="0" ref="B2:C3">...</f>
```

Follower ghi:

```xml
<f t="shared" si="0" />
```

Các group được giới hạn:

- tối đa `100,000` group trên worksheet;
- tối đa `1,000,000` existing cell trong một group.

Cached result tiếp tục tuân theo `WriteCachedFormulaValues`.

### Conservative fallback

Nera giữ normal formula nếu gặp:

- gap hoặc discontiguous cells;
- `#REF!`/error marker;
- structured reference `[...]`;
- array marker `{...}`;
- formula không vượt bidirectional proof;
- group mơ hồ sau structural reorder.

Fallback không làm mất formula. Output vẫn schema-valid và re-import giữ đúng logical formula identity.

### Structural behavior

- Insert/delete được chạy qua production `SpreadsheetStructureController`; nếu các formula sau rewrite vẫn tạo rectangle tương đương thì exporter regroup.
- Row/column reorder được chạy qua production `SpreadsheetAxisReorderController`.
- Reorder có thể làm set formula không còn translation-equivalent; exporter khi đó không ép shared group mà ghi normal formulas.
- Re-import sau fallback giữ nguyên địa chỉ và formula text hiện hành.

### Preservation repeated save

Gate tạo workbook có shared rectangle và một opaque workbook `ExtendedPart`, sau đó:

1. load với `PreserveUnknownParts=true`;
2. sửa workbook;
3. save lần một;
4. xác nhận opaque bytes và shared group;
5. sửa workbook lần hai;
6. save lần hai;
7. xác nhận lại opaque bytes, shared group và OpenXml schema.

Điều này chứng minh copy-and-patch preservation không làm mất shared compaction mới, đồng thời shared export không làm mất package graph chưa được Nera hiểu.

## Automated tests của batch

`SharedFormulaExportTests.cs` khóa:

- rectangle 2×2 với mixed/absolute references, quoted sheet name và string literal;
- import → export → re-import cùng cached values;
- nhiều group và stable worksheet-order index;
- gap và structured-reference fallback;
- `WriteCachedFormulaValues=false`;
- insert/delete regroup;
- reorder fallback giữ formula identity;
- hai preservation saves giữ opaque part và shared groups;
- `OpenXmlValidator(FileFormatVersions.Office2013)`.

## Exact implementation CI #469

Toàn bộ matrix xanh tại `d6808102...`:

- Core restore/build/tests.
- Architecture verification.
- Shared-formula import và export tests.
- Exact sparse style, no-flattening và malformed-input gates.
- Nested unknown-package graph preservation.
- Windows full build/tests và desktop GPU runtime smoke.
- MAUI Android build.
- MAUI iOS và Mac Catalyst builds.
- MAUI Windows build/tests.
- Loaded production input/resize/context recreation smoke.
- Loaded logical/raw scale and orientation smoke.

## File trọng tâm của batch

- `src/NeraSpreadSheet.OpenXml/OpenXmlSharedFormulaExportPlan.cs`
- `src/NeraSpreadSheet.OpenXml/NeraOpenXmlWorkbookSerializer.cs`
- `tests/NeraSpreadSheet.OpenXml.Tests/SharedFormulaExportTests.cs`
- `tests/NeraSpreadSheet.OpenXml.Tests/SharedFormulaGlobalUsings.cs`
- `src/NeraSpreadSheet.OpenXml/OpenXmlSharedFormulaImportResolver.cs`
- `src/NeraSpreadSheet.Editing/FormulaReferenceTranslator.cs`
- `docs/current-status.md`
- `docs/feature-matrix.md`
- `ROADMAP.md`

## Quyết định kỹ thuật đã khóa

- Core model tiếp tục lưu independent formulas, không lưu OpenXml `SharedIndex`.
- Import mở rộng shared groups; export tự chứng minh và regroup ở document boundary.
- Shared index là output detail, được tái cấp ổn định mỗi lần save.
- Chỉ nhóm rectangle liên tục của existing formula cells.
- Forward-only equivalence không đủ; phải có reverse proof.
- Không ép regroup sau reorder nếu formula semantics không còn translation-equivalent.
- Safety và exact formula identity quan trọng hơn mức compact tối đa.
- Preservation và shared export phải cùng vượt repeated-save gate.

## Tiến độ tổng thể sau batch

- Nền móng engine/viewport/renderer: khoảng `85%`.
- MVP bảng tính cơ bản: khoảng `68–72%`.
- Toàn bộ roadmap chuyên nghiệp: khoảng `45%`.
- Production release readiness: khoảng `21–25%`.

So với mốc import-only khoảng `44%`, batch end-to-end này tăng khoảng `1` điểm phần trăm. Phần tăng đến từ export compaction, fallback safety, structural round-trip và preservation compatibility; external compatibility corpus và các tính năng spreadsheet lớn vẫn còn.

## Giới hạn còn lại

- Chưa có corpus XLSX shared formula từ nhiều phiên bản Excel, LibreOffice và third-party generators.
- Shared index nguồn không được giữ; output index được tái cấp.
- Dynamic arrays và spill formulas chưa có.
- Structured references được fallback normal, chưa được semantic model hỗ trợ.
- Conditional formatting, data validation và tables chưa có first-class Nera model.
- PR tiếp tục Draft; không merge khi exact-head CI đỏ hoặc chưa xác định.

## Batch tiếp theo

Thứ tự tiếp theo đã khóa:

1. conditional formatting Core model và differential style catalog;
2. formula/range rule evaluation và dirty-region invalidation;
3. renderer overlay cho cell-is, expression, color scale/data bar ở mức hỗ trợ đầu tiên;
4. XLSX conditional formatting import/export cùng unknown-part coexistence;
5. malformed rule, priority/stop-if-true và structural mapping tests;
6. sau đó data validation và tables/structured references.

Chỉ cập nhật mốc hoàn thành sau exact-head Core/Windows/MAUI CI xanh.
