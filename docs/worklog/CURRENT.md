# Current Work Handoff

- Ngày cập nhật: 2026-08-20
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Implementation commit đã xác minh: `59293ff52c95b1f61d92560a49f90f931df5bb47`
- GitHub Actions: run `32337216394`, CI `#459`, kết luận `success`
- Tài liệu nguồn sự thật: `docs/current-status.md`
- Roadmap còn lại: `ROADMAP.md`
- Contract MAUI scale: `docs/maui-surface-scale-contract.md`
- Contract sparse styles: `docs/whole-axis-style-contract.md`

## Mốc vừa hoàn thành

### Nested unknown-package graph preservation

Đã thêm `UnknownPackageGraphPreservationTests.cs` với package XLSX chứa đồng thời:

- package-root opaque `ExtendedPart`;
- nested opaque child dưới package-root part;
- package-root external relationship;
- external relationship dưới nested child;
- standard worksheet `DrawingsPart`;
- worksheet `<drawing r:id>` reference;
- PNG `ImagePart` có bytes thật;
- opaque nested part dưới drawing;
- non-Nera `CustomXmlPart`;
- `CustomXmlPropertiesPart`;
- opaque nested part dưới custom XML.

Gate thực hiện:

1. tạo package baseline;
2. load với `PreserveUnknownParts=true`;
3. rename worksheet và sửa cell;
4. save lần một;
5. xác nhận schema, graph, ID, URI, content type và bytes;
6. sửa cell lần hai;
7. save lần hai;
8. xác nhận lại toàn bộ invariants và cell edits.

Các output đều vượt `OpenXmlValidator(FileFormatVersions.Office2013)`.

### Package graph validator

Đã thêm `OpenXmlPackageGraphValidator` và nối vào public preservation flow.

Validator duyệt:

- package root;
- mọi nested `OpenXmlPartContainer`;
- internal part relationships;
- external relationships;
- hyperlink relationships;
- data-part reference relationships.

Các invariant bị khóa:

- tối đa `100,000` package parts;
- tối đa `100,000` relationships trên một container;
- package-wide part URI phải duy nhất;
- relationship ID phải duy nhất trên một `.rels` container;
- relationship ID phải là XML NCName, tối đa `1,024` ký tự;
- relationship type phải là absolute URI, tối đa `64 KiB`;
- relationship target tối đa `64 KiB` và không chứa control character kể cả dạng percent-encoded;
- part URI phải relative OPC path bắt đầu bằng `/`;
- từ chối `.`/`..`, `%2E%2E`, encoded slash/backslash, empty segment, backslash, query, fragment và control character.

### Atomic preflight ordering

Luồng preservation hiện là:

```text
Load:
read bounded package bytes
→ validate complete relationship graph
→ LoadCore workbook/styles/cells
→ capture validated envelope
→ return workbook

Save:
build supported Nera package in memory
→ copy-and-patch captured package
→ capture + validate final output envelope
→ truncate/write destination
→ attach new envelope
```

Vì vậy graph xấu bị từ chối trước workbook restoration; merge/output xấu bị từ chối trước destination mutation.

## Exact CI #459

Toàn bộ matrix xanh tại implementation commit `59293ff...`:

- Core restore/build/tests.
- Architecture verification.
- Exact sparse style and malformed-input tests.
- Existing unknown-part repeated-save and topology atomicity tests.
- New nested drawing/image/custom-XML/package-root graph gate.
- New URI, relationship ID/type/target negative tests.
- Windows full build/tests và desktop GPU runtime smoke.
- MAUI Android build.
- MAUI iOS và Mac Catalyst builds.
- MAUI Windows build/tests.
- Loaded production input/resize/context recreation smoke.
- Loaded logical/raw scale and orientation smoke.

## File trọng tâm của lát cắt

- `src/NeraSpreadSheet.OpenXml/OpenXmlPackageGraphValidator.cs`
- `src/NeraSpreadSheet.OpenXml/OpenXmlPackageEnvelope.cs`
- `src/NeraSpreadSheet.OpenXml/OpenXmlPackagePreserver.cs`
- `src/NeraSpreadSheet.OpenXml/NeraOpenXmlWorkbookSerializer.cs`
- `src/NeraSpreadSheet.OpenXml/Properties/AssemblyInfo.cs`
- `tests/NeraSpreadSheet.OpenXml.Tests/UnknownPackageGraphPreservationTests.cs`
- `tests/NeraSpreadSheet.OpenXml.Tests/OpenXmlPackageGraphValidatorTests.cs`
- `tests/NeraSpreadSheet.OpenXml.Tests/UnknownPartPreservationTests.cs`

## Quyết định kỹ thuật đã khóa

- Opaque package graph được giữ bằng copy-and-patch, không convert vào Core model.
- Microsoft/OpenXml types chỉ tồn tại trong adapter OpenXml.
- Nera chỉ thay những XML regions mà mình thực sự sở hữu.
- Unknown semantic content được giữ nguyên, không đoán và viết lại.
- Relationship validation phải bao phủ package root, nested parts và mọi reference relationship category.
- Graph validation phải xảy ra trước restoration và trước destination mutation.
- Worksheet topology-changing preservation tiếp tục bị từ chối cho đến khi có mapping contract an toàn.

## Giới hạn còn lại

- Preserve mode vẫn yêu cầu cùng worksheet objects và cùng thứ tự; rename được phép nhưng add/remove/reorder bị từ chối.
- Chart sheet và dialog sheet chưa được hỗ trợ.
- Package envelope nằm trong memory và giới hạn 512 MiB.
- Drawing/image/custom XML hiện được bảo toàn opaque; chưa phải first-class editable Nera model.
- Shared formulas, conditional formatting, validation, tables và complete function surface chưa có.
- PR tiếp tục Draft; không merge khi exact-head CI đỏ hoặc chưa xác định.

## Bước tiếp theo duy nhất

Triển khai **shared-formula import/export và reference translation**:

1. đọc `CellFormula` loại shared, `SharedIndex`, anchor formula và shared range;
2. xác thực duplicate/missing shared index, range sai và follower nằm ngoài range;
3. dịch A1 references từ anchor tới từng follower, bảo toàn `$`, quoted sheet names và string literals;
4. không materialize ô ngoài shared range hoặc vượt sparse safety limit;
5. export nhóm formula tương đương thành shared formula khi an toàn;
6. fallback sang normal formulas khi nhóm không liên tục hoặc translation không biểu diễn được;
7. round-trip với cached values bật/tắt;
8. structural insert/delete/reorder phải giữ đúng logical formula identity;
9. thêm malformed-input, repeated-save và compatibility tests;
10. chỉ cập nhật mốc hoàn thành sau exact-head Core/Windows/MAUI CI xanh.
