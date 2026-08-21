# Current Work Handoff

- Ngày cập nhật: 2026-08-21
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Implementation commit đã xác minh: `f1c899554343aa49dee072be10145554eb86e371`
- GitHub Actions: run `32440549596`, CI `#503`, kết luận `success`
- Tài liệu nguồn sự thật: `docs/current-status.md`
- Contract Table: `docs/table-structured-reference-contract.md`
- Roadmap còn lại: `ROADMAP.md`

## Batch vừa hoàn thành: Table/Structured References/AutoFilter foundation

### 1. Hợp nhất một Table model duy nhất

- Xóa model Table song song từng làm branch không compile.
- Giữ `SpreadsheetTable`, `SpreadsheetTableColumn`, `TableAutoFilter` và `WorksheetTableCollection` trong Core làm nguồn sự thật duy nhất.
- Table và column dùng stable `Guid` identity.
- Tên Table duy nhất toàn workbook; tên/ID cột duy nhất trong Table; Table cùng sheet không chồng lấn.
- Header/data/totals ranges được suy ra từ một canonical Table range, không materialize cell.

### 2. Structural mapping và production history

- Table nằm trong `WorksheetSnapshot` và `WorksheetStructuralState`.
- Insert/delete map Table range cùng formula metadata; unsafe header/totals deletion hoặc implicit unnamed-column insertion bị từ chối trước mutation.
- Reorder yêu cầu toàn Table là một uniform translation.
- `SpreadsheetSession.Tables` cung cấp add/remove/rename Table, rename column và set/clear AutoFilter.
- Mọi operation có Undo/Redo; failed duplicate rename rollback Table/formula state và không vào history.

### 3. Structured references và dependency graph

- Hỗ trợ `Table[Column]`, `#All`, `#Data`, `#Headers`, `#Totals`, `#This Row` và `[@Column]`.
- Structured references được expand thành A1 trước parser/evaluator hiện tại.
- String literal không bị rewrite; cross-sheet Table reference nhận quoted sheet qualifier đúng.
- `[@Column]` chỉ hợp lệ trong data row của Table.
- A1 range sau expansion đi vào dependency graph và affected-only recalculation.
- Rename Table/column rewrite formula toàn workbook trong cùng history transaction.

### 4. AutoFilter và compressed row projection

- Value sets, blank matching và một/hai comparison conditions theo AND/OR.
- Nhiều filter columns kết hợp AND theo hàng.
- Snapshot evaluator tạo `FilteredRowSpan` có safety limit.
- Adjacent filtered rows được nén thành `AxisIndexRange` trong `SparseAxisMetricIndex`.
- Hàng lọc không chiếm content extent, không tạo layout slot và bị hit-test bỏ qua; raw row size vẫn được giữ để phục hồi.
- Filter/source-cell changes tự refresh metrics; Undo/Redo filter cập nhật visibility.

### 5. Standard XLSX Table parts

- Thêm `OpenXmlTableCodec` cho worksheet `tableParts/tablePart` và `TableDefinitionPart`.
- Round-trip name/displayName, range, header/totals state, columns, style, calculated/totals formula metadata và totals labels.
- Nera package dùng relationship ID `rIdNeraTable{Guid:N}` và `tableColumn@uniqueName="nera:{Guid:N}"` để giữ identity.
- Foreign package dùng deterministic fallback identity khi không có Nera metadata.
- AutoFilter value/blank/custom comparison filters round-trip.
- Output vượt `OpenXmlValidator(FileFormatVersions.Office2013)`.

### 6. Malformed input và preservation

- Từ chối duplicate/missing Table relationships, unreferenced parts, count mismatch, bad/reversed ranges, duplicate/zero column IDs, invalid filter indexes và unsupported markup.
- Thêm `OpenXmlTablePackagePatcher` cho copy-and-patch save.
- Consecutive `PreserveUnknownParts=true` saves refresh Table parts nhưng giữ Table `extLst` payload và unowned worksheet/package content.
- Blank-only filter, custom two-condition filter và totals metadata có regression gates riêng.

## CI #503

Toàn bộ matrix xanh tại implementation commit `f1c89955...`:

- Core restore/build/tests.
- Architecture verification.
- Table model/naming/structural/history tests.
- Structured-reference translation, evaluation, dependency và affected-recalc tests.
- AutoFilter predicates, compressed span, layout/viewport/content extent/hit-test tests.
- Standard XLSX Table schema, malformed-input, filter/totals và repeated-preservation tests.
- Data Validation, Conditional Formatting, sparse style, shared-formula và package-graph regressions.
- Windows full build/tests và desktop GPU runtime smoke.
- MAUI Android build.
- MAUI iOS và Mac Catalyst builds.
- MAUI Windows build/tests.
- Loaded production input/resize/context-recreation smoke.
- Loaded logical/raw scale and orientation smoke.

## File trọng tâm

- `src/NeraSpreadSheet.Core/Tables.cs`
- `src/NeraSpreadSheet.Core/StructuredReferences.cs`
- `src/NeraSpreadSheet.Core/FilteredRowProjection.cs`
- `src/NeraSpreadSheet.Core/Worksheet.cs`
- `src/NeraSpreadSheet.Core/Workbook.cs`
- `src/NeraSpreadSheet.Formulas/NeraFormulaEngine.cs`
- `src/NeraSpreadSheet.Formulas/WorkbookCalculationEngine.cs`
- `src/NeraSpreadSheet.Formulas/StructuredReferenceFormulaEngine.cs`
- `src/NeraSpreadSheet.Editing/SpreadsheetTableController.cs`
- `src/NeraSpreadSheet.Layout/SparseAxisMetricIndex.cs`
- `src/NeraSpreadSheet.Viewport/SpreadsheetViewportEngine.cs`
- `src/NeraSpreadSheet.OpenXml/OpenXmlTableCodec.cs`
- `src/NeraSpreadSheet.OpenXml/OpenXmlTablePackagePatcher.cs`
- `tests/*Table*`, structured-reference, formula, layout và viewport test files.

## Giới hạn có chủ ý

- Calculated-column/totals formula metadata đã round-trip nhưng chưa tự fill/execute.
- Chưa có native Table manager hoặc filter dropdown presenter.
- Chưa có direct worksheet AutoFilter ngoài Table.
- Rich date/text/top/bottom/color/icon filters chưa có.
- Foreign relationship ID được deterministic-map; normal semantic save không hứa giữ nguyên ID ngoại lai.
- Chưa có corpus tương thích từ Excel/LibreOffice/các generator thực tế.

## Tiến độ tổng thể sau batch

- Nền móng engine/viewport/renderer: khoảng `88%`.
- MVP bảng tính cơ bản: khoảng `76–80%`.
- Toàn bộ roadmap chuyên nghiệp: khoảng `50–51%`.
- Production release readiness: khoảng `26–30%`.

Batch nâng tổng thể khoảng `2–3` điểm phần trăm so với mốc Data Validation.

## Bước tiếp theo duy nhất

Triển khai batch **Calculated Columns + Totals + Native Table/Filter UX**:

1. Auto-fill calculated-column formulas qua data range với stable logical identity.
2. Execute totals-row metadata và subtotal semantics có filter awareness.
3. Thêm command/presenter contract cho Table manager và filter dropdown.
4. Nối WPF/WinForms presenter trước, sau đó MAUI responsive UX.
5. Mở rộng text/date/top/custom-list predicates.
6. Thêm external XLSX Table/AutoFilter compatibility corpus.
7. Chạy exact-head Core/Windows/MAUI CI trước khi cập nhật mốc tiếp theo.

PR tiếp tục Draft; không merge khi exact-head CI đỏ hoặc chưa xác định.
