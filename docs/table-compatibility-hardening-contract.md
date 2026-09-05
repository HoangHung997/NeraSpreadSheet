# TABLE-006 — compatibility và hardening headless

Phạm vi này mở rộng TABLE-005 từ `cf923db2`. Một workbook model, một mutation
boundary `SpreadsheetSession.Tables`, cùng UndoRedoManager và calculation engine
hiện hữu. Không có native UI hoặc engine thứ hai. Đây là handoff headless;
chưa đóng toàn bộ checkpoint TABLE-006 trong delivery plan.

## XLSX và nguồn corpus

| Nội dung | Nera sở hữu semantic | Preservation bật |
| --- | --- | --- |
| Table/range/header/totals, column names/IDs | Có | Cập nhật phần sở hữu |
| Calculated/custom totals formulas | Có, qua projection dùng chung | Giữ formula text và stable column identity |
| Totals function | average, countNums, count, max, min, sum; custom có formula | Cùng semantic |
| Filter và sort đã hỗ trợ | Có; sort đọc được ở Table hoặc autoFilter | Clear không phục hồi criteria cũ |
| Style/name/options, custom style hỗ trợ | Có; resolver TABLE-004 | Dùng dxf remap hiện hữu |
| Table/column extensions và attributes ngoại | Không | Giữ cùng Table/column identity |
| Unsupported criteria/custom styles | Không | Giữ trong package envelope, không giả lập kết quả |
| Query/XML-mapped Table, array Table formulas, unsupported totals functions | Không | Từ chối khi không thể bảo toàn semantic |

Corpus được tạo bằng Nera và chỉnh trực tiếp bằng OpenXML trong tests. Fixtures
in-memory, synthetic, không chứa workbook cá nhân hoặc binary lớn. Metadata
`rIdForeignTable`/namespace vendor mô phỏng producer ngoại, **không phải bằng
chứng Excel hoặc LibreOffice đã tạo file**. Native Excel/LibreOffice corpus với
provenance hợp lệ vẫn là gap; không mở Excel/desktop hoặc cài LibreOffice trong
wave này.

`totalsRowCount` quyết định geometry hiện tại. `totalsRowShown` chỉ ghi lịch sử
đã hiển thị totals, không được biến data row cuối thành totals. Mapping tham
chiếu [Table trong OpenXML](https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.spreadsheet.table)
và [structured references của Microsoft](https://support.microsoft.com/en-us/excel/using-structured-references-with-excel-tables).

## Identity và preservation

- Numeric Table ID phải dương và duy nhất toàn workbook; preflight trước import
  cells. Table/column model IDs, malformed `nera:`/relationship identity và
  workbook overlap/name constraints không được tự sửa khi import.
- Preserve-only Save ghép theo stable Table ID, giữ relationship ID và part URI
  ngoại. Column được ghép theo stable ID, giữ numeric ID/uniqueName gốc và
  extensions, kể cả sau insert/delete/rename. ID mới được cấp tránh trùng.
- Nera-generated package tiếp tục dùng deterministic relationship và `nera:`
  column markers. Strict export từ package ngoại chuẩn hóa relationship/part
  sang Nera; model IDs vẫn ổn định. Muốn giữ graph ngoại phải bật preservation
  ở cả load và save.
- Unknown filter-column markup đi cùng column identity khi chèn cột; không
  ghép theo offset cũ. Criteria được hỗ trợ mà người dùng Clear không được
  resurrection từ envelope. Table-wide button visibility vẫn là giới hạn
  TABLE-005; không tuyên bố giữ semantic per-column visibility.

## Rejection và transaction

Malformed IDs/names, count/width, reversed/out-of-bounds ranges, unresolved hoặc
duplicate relationships, merge/overlap, duplicate formulas, array formula,
formula/label totals cạnh tranh và sort bounds không hợp lệ bị từ chối với
`InvalidDataException`. Source stream bytes không bị thay đổi; loader không trả
workbook/session partial. Underlying model validation exception giữ inner cause.

Mutation tiếp tục validation trước history. `Tables.Add` cũng kiểm tra spill như
Create/resize. Mixed A1 + structured formula không được làm static analyzer bỏ
sót direct reference trước Table-local compact. Failed operation giữ cells,
Table metadata, selection/history; thành công có đúng một Undo entry.

## Structured reference và interface cho host

Mở rộng `SpreadsheetFormulaEditingAssistant` hiện hữu:

- `GetStructuredReferenceSuggestions`: Table name hoặc fragment đơn giản
  `Table[column` / `[@column`; tối đa 256 items (default 12), chỉ đọc metadata.
- `ApplyStructuredReferenceSuggestion`: resolve Table/column GUID lại khi apply;
  rename giữa lúc lấy suggestion và apply dùng tên mới. Stale text/deleted ID
  bị từ chối, không đổi workbook/history.
- `InsertReference` overload nhận workbook, formula worksheet/address, reference
  worksheet và selected range. Exact Table area/current row sinh structured
  reference; partial range dùng lại A1 insertion. `InsertedSpan` cho phép replace
  provisional drag, không chèn lặp và không làm rộng selection âm thầm.
- `FormulaReferenceAnalyzer.TryGetReferences` overload có workbook/address expand
  qua translator dùng chung để lấy precedent geometry. Overload A1-only mask
  structured tokens để vẫn phát hiện A1 trong mixed formulas, không evaluate.

Column escaping dùng apostrophe cho `[]#'@`. Rename khớp toàn column token, không
đổi cột có tên cùng prefix hoặc string literals. Column separators cần nested
brackets. Selector union/multi-area chưa hỗ trợ trả `#REF!`, không biến thành
contiguous range. Cross-sheet `#This Row` bị từ chối.

Native mouse/keyboard/completion/highlight wiring chưa triển khai trong lane B;
host wiring cần checkpoint kế tiếp. Nested incomplete completion fragments chưa
hỗ trợ; complete structured references vẫn đi qua parser/evaluator hiện hữu.

## Sparse và performance

- Style/banding/filter-button mutations dùng `AffectsCalculation=false` sẵn có,
  không project cells hoặc rebuild/recalculate graph, kể cả Undo/Redo.
- Table cell capture/occupancy dùng bounded lookup khi area nhỏ hơn số used
  cells và tối đa 100.000 probes; range lớn dùng sparse used-cell enumeration.
  Không materialize logical worksheet hoặc whole axis.
- Topology/formula changes vẫn cần chuẩn bị dependency graph và chỉ
  `RecalculateAffected`; rename vẫn dùng full-workbook formula rewrite theo
  TABLE-005. Direct-reference safety phải đọc workbook formula inventory; không
  tuyên bố mọi structural operation O(Table size) độc lập số used cells.
- Giữ bounds cũ: projection 1.000.000 cells, remove-duplicates 100.000 rows /
  1.000.000 key cells. Rendering/visible+overscan/frame scheduler không đổi.

Harness `TableCompatibilityBenchmarks` đo toggle+Undo và completion với 0 hoặc
100.000 unrelated cells. Kết quả và command tái lập nằm trong worklog. Đây là
microbenchmark local ngắn, không phải SLO hoặc benchmark native Excel.

## Cổng bàn giao

Core Release build/analyzers, full Core tests, focused Editing/Formulas/OpenXML,
schema validation, in-memory session mutation/Undo/Redo/Save-Load-Save,
architecture và packaging verifiers. Ba workflow phải xanh trên HEAD cuối kể
cả tài liệu. Remote platform smokes chỉ chứng minh platform regression hiện
hữu; không chứng minh native structured-reference UX mới hoặc producer corpus.

Rollback: revert riêng delta `cf923db2..HEAD`; không có migration/package mới.
