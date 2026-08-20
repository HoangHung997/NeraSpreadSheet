# Current Work Handoff

- Ngày cập nhật: 2026-08-20
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Implementation commit đã xác minh: `58ed4a1c440b22bc75f8b3add40a3ba988a50517`
- GitHub Actions: run `32372708251`, CI `#476`, kết luận `success`
- Tài liệu nguồn sự thật: `docs/current-status.md`
- Roadmap còn lại: `ROADMAP.md`

## Batch vừa hoàn thành

### 1. Conditional Formatting Core model

- Thêm `ConditionalFormattingRuleType`: `CellIs`, `Expression`.
- Thêm các toán tử equal/not-equal, greater/greater-or-equal, less/less-or-equal, between/not-between.
- Rule có ID ổn định, nhiều range, priority, `StopIfTrue`, một hoặc hai formula và differential-style ID.
- `DifferentialStyleCatalog` lưu `CellStylePatch` và deduplicate trong worksheet.
- `WorksheetSnapshot` deep-copy rule và differential style.

### 2. Evaluation và renderer

- `ConditionalFormattingEvaluator` chạy theo priority.
- `StopIfTrue` ngăn rule ưu tiên thấp hơn.
- Property xung đột giữ rule ưu tiên cao; property không xung đột vẫn compose.
- Expression dùng Core A1 translator để dịch từ anchor tới từng cell.
- Shared `SpreadsheetDisplayListComposer` áp conditional style vào fill, text, border và number format; WPF/WinForms/MAUI nhận cùng kết quả.

### 3. Dirty-region invalidation

- Thay đổi rule invalidates union target range.
- Mọi cell mutation mở rộng tín hiệu `CellsChanged` tới toàn bộ conditional target ranges của worksheet.
- Đây là chính sách bảo thủ để đảm bảo đúng khi một source cell ảnh hưởng vùng khác; không enumerate/materialize cell trong range.

### 4. Structural mapping và history

- A1 translator và structural-reference rewriter được đưa về Core; Editing giữ facade tương thích.
- Rule range/formula nằm trong `WorksheetStructuralState`.
- Insert/delete rewrite relative, mixed, absolute và range references.
- Undo/redo qua `SpreadsheetStructureController` phục hồi chính xác rule ID, priority, range và formula.
- Reorder chỉ được phép nếu mỗi target range là một uniform translation. Contiguous-but-internally-permuted range bị từ chối trước mutation.

### 5. XLSX standard interoperability

- Thêm `OpenXmlConditionalFormattingCodec`.
- Import/export chuẩn `dxfs`, `dxf`, `conditionalFormatting`, `cfRule`, `formula`.
- Hỗ trợ `cellIs`, `expression`, operator, priority, `stopIfTrue`, `dxfId`, nhiều `sqref`.
- Differential style hỗ trợ font, fill, border, alignment và number format trong phạm vi Core hiện tại.
- Workbook-wide `dxf` được deduplicate ổn định khi save.
- Output vượt `OpenXmlValidator(FileFormatVersions.Office2013)`.

### 6. Malformed input và preservation

- Từ chối duplicate/missing/zero priority, invalid `sqref`, out-of-range `dxfId`, sai số formula, unsupported rule/style markup và count vượt giới hạn.
- `OpenXmlPackagePreserver` sở hữu `conditionalFormatting` và `dxfs`, đồng thời vẫn giữ opaque parts, relationships và unowned markup.
- Hai consecutive `PreserveUnknownParts=true` saves giữ exact opaque bytes và conditional rules.

## CI #476

Toàn bộ matrix xanh tại implementation commit `58ed4a1c...`:

- Core restore/build/tests.
- Architecture verification.
- Conditional model/evaluator/renderer/invalidation/structural/history tests.
- Conditional XLSX schema, round-trip, malformed-input và preservation tests.
- Toàn bộ OpenXml/style/shared-formula/package-graph regressions.
- Windows full build/tests và desktop GPU runtime smoke.
- MAUI Android build.
- MAUI iOS và Mac Catalyst builds.
- MAUI Windows build/tests.
- Loaded production input/resize/context-recreation smoke.
- Loaded logical/raw scale and orientation smoke.

CI #474 từng có một Windows native fast-fail không kèm managed exception; cùng implementation path đã xanh ở CI #472 và exact implementation CI #476, nên không được ghi nhận là regression tái hiện được.

## File trọng tâm

- `src/NeraSpreadSheet.Core/ConditionalFormatting.cs`
- `src/NeraSpreadSheet.Core/A1FormulaReferenceTranslator.cs`
- `src/NeraSpreadSheet.Core/FormulaStructuralReferenceRewriter.cs`
- `src/NeraSpreadSheet.Core/Worksheet.cs`
- `src/NeraSpreadSheet.Core/WorksheetSnapshot.cs`
- `src/NeraSpreadSheet.Formulas/ConditionalFormattingEvaluator.cs`
- `src/NeraSpreadSheet.Rendering.Spreadsheet/SpreadsheetDisplayListComposer.cs`
- `src/NeraSpreadSheet.OpenXml/OpenXmlConditionalFormattingCodec.cs`
- `src/NeraSpreadSheet.OpenXml/NeraOpenXmlWorkbookSerializer.cs`
- `src/NeraSpreadSheet.OpenXml/OpenXmlPackagePreserver.cs`
- conditional-formatting test files trong Core, Editing, Formulas, Rendering và OpenXml projects.

## Giới hạn có chủ ý

- Chưa có color scales, data bars, icon sets và các rule duplicate/top/average/time-period chuyên biệt.
- Imported differential colors chỉ hỗ trợ explicit RGB; theme/indexed colors chưa có semantic conversion.
- Conditional expression evaluator hiện dùng snapshot của worksheet hiện tại; cross-sheet conditional formula chưa phải supported contract.
- Dirty invalidation hiện bảo thủ theo toàn bộ target ranges trên worksheet.
- Chưa có rule-manager UI và external compatibility corpus.

## Tiến độ tổng thể sau batch

- Nền móng engine/viewport/renderer: khoảng `86%`.
- MVP bảng tính cơ bản: khoảng `70–74%`.
- Toàn bộ roadmap chuyên nghiệp: khoảng `46%`.
- Production release readiness: khoảng `22–26%`.

Batch nâng tổng thể khoảng `1` điểm phần trăm so với mốc shared-formula end-to-end.

## Bước tiếp theo duy nhất

Triển khai batch **Data Validation**:

1. Core validation model và sparse range ownership.
2. whole number, decimal, date, time, text length và list rules.
3. custom formula validation dùng Core A1 translator/evaluator.
4. input message, error alert và blank/error policy.
5. production editor commit gate cùng undo/redo.
6. invalid-cell diagnostics/highlight và dirty invalidation.
7. XLSX `dataValidations` import/export, schema và malformed-input gates.
8. structural insert/delete/reorder mapping và failure atomicity.
9. `PreserveUnknownParts=true` repeated-save coexistence.
10. exact-head Core/Windows/MAUI CI trước khi cập nhật mốc hoàn thành.

PR tiếp tục Draft; không merge khi exact-head CI đỏ hoặc chưa xác định.
