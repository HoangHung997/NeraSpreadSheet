# Current Work Handoff

- Ngày cập nhật: 2026-08-20
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Implementation commit đã xác minh: `64682b9d633bfae699832dee5b73ef5646271bad`
- GitHub Actions: run `32392801690`, CI `#486`, kết luận `success`
- Tài liệu nguồn sự thật: `docs/current-status.md`
- Roadmap còn lại: `ROADMAP.md`

## Batch vừa hoàn thành: Data Validation

### 1. Core model và sparse ownership

- Thêm `DataValidationType`: Whole, Decimal, List, Date, Time, TextLength và Custom.
- Thêm các toán tử Between/NotBetween, Equal/NotEqual, Greater/Less và hai biến thể OrEqual.
- Rule có ID ổn định, nhiều range sparse, một/hai formula, `AllowBlank`, input-message, error-alert, Stop/Warning/Information và dropdown visibility.
- Một ô chỉ thuộc tối đa một validation rule; mọi overlap trong/cross-rule bị từ chối nguyên tử.
- Giới hạn rule, range, formula, title và message được áp dụng trước khi state thay đổi.
- `WorksheetSnapshot` deep-copy rule; `WorksheetStructuralState` lưu rule cho exact rollback/history.

### 2. Evaluator và candidate semantics

- Whole, decimal, date, time và text length dùng cùng operator engine.
- Literal list và same-sheet A1 range list được hỗ trợ.
- Custom formula và formula-backed limits dùng Core A1 translator cùng Nera formula engine.
- Context formula thay giá trị target bằng candidate đang commit, tránh đọc nhầm giá trị cũ.
- `AllowBlank` là policy tuyệt đối: blank chỉ hợp lệ khi được bật, không bị coerce thành số 0.
- Cross-sheet/named-range/external list/custom formulas chưa thuộc supported contract.

### 3. Production editor UX và history

- `SpreadsheetSession.Validation` cung cấp query, evaluate, input message, diagnostics và add/remove rule.
- Stop chặn invalid commit; Warning/Information chỉ cho qua khi host xác nhận rõ ràng.
- `ShowErrorMessage=false` cho phép commit im lặng nhưng diagnostics/highlight vẫn phát hiện ô sai.
- `ValidationFailed` cung cấp address, style, title và message cho host.
- Add/remove validation rule và accepted cell edit đều tham gia Undo/Redo; failed rule add không vào history.

### 4. Diagnostics và renderer

- Bounded diagnostic scan trả về đúng các ô invalid, không lưu invalid-cell index materialized.
- Shared display-list composer vẽ invalid outline cho cell/merged cell nhìn thấy.
- WPF, WinForms và MAUI dùng cùng theme/color/stroke semantics.
- `ShowValidationErrors=false` chỉ tắt highlight, không tắt rule hoặc evaluator.
- Dirty invalidation mở rộng bảo thủ tới validation target ranges.

### 5. Structural safety

- Insert/delete map range và rewrite formula bằng shared structural-reference rewriter.
- Delete toàn bộ target loại bỏ rule; undo phục hồi ID/range/formula/metadata.
- Reorder yêu cầu uniform translation; contiguous-but-internally-permuted target bị từ chối trước mutation.
- Transformed rule set được preflight overlap trước khi thay cells/dimensions/merges/styles.

### 6. XLSX standard interoperability

- Thêm `OpenXmlDataValidationCodec` cho `dataValidations`, `dataValidation`, `formula1`, `formula2`.
- Hỗ trợ type/operator, multiple `sqref`, `allowBlank`, inverse `showDropDown`, prompt/error metadata và error style.
- Output vượt `OpenXmlValidator(FileFormatVersions.Office2013)`.
- Từ chối duplicate collection, count mismatch, unsupported type/operator/child, invalid `sqref`, bad formula count và overlapping targets.

### 7. Unknown-part preservation

- Thêm `OpenXmlDataValidationPackagePatcher` để cập nhật validation markup sau copy-and-patch package gốc.
- Hai consecutive `PreserveUnknownParts=true` saves giữ opaque bytes, relationships và unowned markup trong khi validation rules vẫn được refresh.
- Final output envelope chỉ được gắn sau khi package hoàn chỉnh đã được validate và destination write thành công.

## CI #486

Toàn bộ matrix xanh tại implementation commit `64682b9d...`:

- Core restore/build/tests.
- Architecture verification.
- Validation model/evaluator/candidate/blank/editor/history/diagnostic/renderer/structural tests.
- Validation XLSX schema, malformed-input và repeated-preservation tests.
- Toàn bộ conditional-formatting, style, shared-formula và package-graph regressions.
- Windows full build/tests và desktop GPU runtime smoke.
- MAUI Android build.
- MAUI iOS và Mac Catalyst builds.
- MAUI Windows build/tests.
- Loaded production input/resize/context-recreation smoke.
- Loaded logical/raw scale and orientation smoke.

## File trọng tâm

- `src/NeraSpreadSheet.Core/DataValidation.cs`
- `src/NeraSpreadSheet.Core/Worksheet.cs`
- `src/NeraSpreadSheet.Core/WorksheetSnapshot.cs`
- `src/NeraSpreadSheet.Core/WorksheetStructuralState.cs`
- `src/NeraSpreadSheet.Formulas/DataValidationEvaluator.cs`
- `src/NeraSpreadSheet.Editing/SpreadsheetDataValidationController.cs`
- `src/NeraSpreadSheet.Editing/SpreadsheetCellEditorController.cs`
- `src/NeraSpreadSheet.Rendering.Spreadsheet/SpreadsheetDisplayListComposer.cs`
- `src/NeraSpreadSheet.Rendering.Spreadsheet/SpreadsheetRenderTheme.cs`
- `src/NeraSpreadSheet.OpenXml/OpenXmlDataValidationCodec.cs`
- `src/NeraSpreadSheet.OpenXml/OpenXmlDataValidationPackagePatcher.cs`
- `src/NeraSpreadSheet.OpenXml/NeraOpenXmlWorkbookSerializer.cs`
- Data-validation test files trong Core, Formulas, Editing, Rendering và OpenXml projects.

## Giới hạn có chủ ý

- Một ô không nhận nhiều validation rules chồng nhau.
- Chưa hỗ trợ named-range, external hoặc cross-sheet validation evaluation.
- List hiện hỗ trợ quoted comma list và same-sheet A1 range.
- Chưa có native rule-manager, prompt popup hoặc list dropdown presenter.
- Programmatic worksheet mutations bypass interactive editor gate nhưng vẫn bị diagnostics/highlight phát hiện.
- Date/time hiện dùng .NET/OA serial bridge, chưa phải complete Excel date-system compatibility.
- Dirty invalidation bảo thủ theo toàn bộ validation/conditional target ranges.

## Tiến độ tổng thể sau batch

- Nền móng engine/viewport/renderer: khoảng `87%`.
- MVP bảng tính cơ bản: khoảng `73–77%`.
- Toàn bộ roadmap chuyên nghiệp: khoảng `48%`.
- Production release readiness: khoảng `24–28%`.

Batch nâng tổng thể khoảng `2` điểm phần trăm so với mốc Conditional Formatting.

## Bước tiếp theo duy nhất

Triển khai batch **Tables + Structured References + AutoFilter**:

1. Table Core model, stable table/column IDs và unique naming.
2. Header/data/totals range semantics cùng structural insert/delete/reorder.
3. Structured-reference tokenizer/parser/evaluator/dependency integration.
4. Formula rewrite qua table/column rename và structural edits.
5. Standard XLSX table parts, relationships, table styles và repeated preservation.
6. AutoFilter model, filter columns, predicates và hidden-row projection.
7. Desktop filter dropdown/command contracts cùng shared diagnostics.
8. Malformed-input, schema, atomic rollback và compatibility gates.
9. Exact-head Core/Windows/MAUI CI trước khi cập nhật mốc hoàn thành.

PR tiếp tục Draft; không merge khi exact-head CI đỏ hoặc chưa xác định.
