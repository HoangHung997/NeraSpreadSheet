# Table Design theo ngữ cảnh — Contract TABLE-005

Tài liệu này khóa hành vi của checkpoint `TABLE-005`. Core, Editing, Ribbon và
ba host desktop phải dùng cùng một Table model và cùng command registry; host
không được tạo model, history hoặc calculation path riêng.

## 1. Nguồn sự thật và trạng thái theo selection

- `SpreadsheetSession.Tables` là mutation boundary duy nhất.
- `SpreadsheetSession.TableDesign` chiếu selection hiện hành thành một snapshot
  chỉ đọc: Table/column ID, tên, range, header/totals, style options, filter
  buttons, calculated formula, totals function và style gallery.
- Contextual tab `table-design` chỉ hiện khi active cell nằm trong Table của
  active worksheet. Chuyển sheet, đổi selection hoặc đổi Table metadata phải
  refresh cùng trạng thái này.
- WPF, WinForms và MAUI chỉ bind snapshot vào
  `RibbonSelectionContext`; không host nào tự suy luận Table semantics.

## 2. Command surface

Production Ribbon đăng ký và audit các command ổn định sau:

```text
Table.Create                 Table.Rename
Table.Resize                 Table.ConvertToRange
Table.HeaderRow              Table.TotalsRow
Table.FirstColumn            Table.LastColumn
Table.BandedRows             Table.BandedColumns
Table.FilterButtons          Table.Style
Table.CalculatedColumn       Table.TotalsFunction
Table.Row.Insert             Table.Row.Delete
Table.Column.Insert          Table.Column.Delete
Table.RemoveDuplicates
```

Các toggle lấy `IsChecked` trực tiếp từ snapshot. Gallery dùng identity của
`TableStyleCatalog`; totals ComboBox dùng `SpreadsheetTableTotalsFunction`.
`RibbonItemActivation` được chuyển qua interface command-neutral, nên Editing
không reference Ribbon.Core.

Các command cần dữ liệu nhập nhận parameter chuẩn: tên Table/cột/style là
`string`, resize là `CellRange`, calculated/custom totals là formula `string`,
và remove-duplicates là danh sách stable column `Guid`. Ứng dụng host có thể
thu thập parameter bằng dialog/editor riêng nhưng mutation vẫn phải gọi command
registry hoặc `SpreadsheetSession.Tables`.

## 3. Lifecycle và stable identity

- Create sinh Table ID và column IDs một lần; tên mặc định là `TableN` duy nhất
  toàn workbook.
- Rename dùng structured-reference rewriter hiện hữu trên toàn workbook.
- Resize giữ nguyên top-left, Table ID và ID của mọi cột còn lại. Cột mới nhận
  ID mới; thu hẹp cột bị tham chiếu bị từ chối trước history.
- Insert/delete Table row chỉ compact dữ liệu trong Table, không giả lập whole-
  worksheet structural insert/delete.
- Insert column giữ ID cũ, sinh đúng một ID mới và remap sort/filter offsets.
- Delete column giữ ID của các cột còn lại và từ chối nếu formula cell hoặc
  Table formula metadata nào tham chiếu trực tiếp cột đó.
- Convert-to-range bỏ metadata nhưng giữ cell values và styles; TABLE-006
  chuyển structured references của Table đích thành A1 trước khi xóa metadata.
  Formula text có thể đổi để giữ nghĩa; Undo phục hồi tham chiếu Table ban đầu.
- Remove-duplicates ổn định theo thứ tự nguồn và nhận column IDs, không index
  tạm thời.

## 4. Atomic safety và Undo/Redo

Mỗi thao tác thành công tạo đúng một history entry. Failed validation không tăng
Undo count. Undo/Redo phục hồi Table metadata, stable IDs, affected sparse cells
và selection trước thao tác.

Trước khi mutate, operation phải từ chối:

- Table range chồng Table khác, merged range hoặc dynamic-array spill;
- growth row/column/totals đè lên used destination cells;
- resize đổi top-left hoặc không đủ chỗ cho header/totals;
- xóa cột được structured reference tham chiếu;
- compact row/column/remove-duplicates khi A1 formula bên ngoài tham chiếu vùng
  di chuyển, hoặc khi formula/metadata A1 nằm trong dữ liệu phải di chuyển;
- xóa cột cuối cùng hoặc vượt worksheet bounds.

Không có partial mutation khi validation hoặc projection lỗi. Merge/spill không
bị tách hay đổi nghĩa tự động. Freeze/split, dimensions và scroll offsets không
đổi vì Table row/column commands không phải worksheet-axis commands.

## 5. Formula và calculation

- Calculated column và totals tiếp tục dùng
  `SpreadsheetTableFormulaProjection`; không có formula store thứ hai.
- Structured-reference formula đi cùng stable Table/column identity khi dữ liệu
  được compact. A1 formula không được dịch theo copy-delta vì thao tác chỉ dịch
  hình chữ nhật Table, không dịch cả worksheet; operation từ chối nguyên tử nếu
  không thể bảo toàn direct-reference identity.
- Sau mutation thay topology Table, dependency graph được chuẩn bị lại rồi chỉ
  `RecalculateAffected` trên union range và transitive dependents. Undo/Redo dùng
  cùng contract; không full-workbook recalculation cho TABLE-005 path.
- Convert-to-range dùng cùng formula-rewrite transaction trên workbook và
  `RecalculateAffected`; metadata-only style/banding/filter-button mutations
  không rebuild graph hoặc tính lại. Chi tiết tại
  [TABLE-006 contract](table-compatibility-hardening-contract.md).
- Rename Table/column vẫn dùng full-workbook path hiện hữu vì chính operation đó
  rewrite formula cells và Table formula metadata trên nhiều worksheet.

## 6. Bounded chrome và sparse performance

- Gallery có tối đa `256` entry; built-in TABLE-004 đứng trước custom styles.
- Mỗi preview dùng `TableStylePreview` tối đa `12 x 12`, được cache theo snapshot
  và chỉ invalidated tường minh khi catalog/theme đổi. Cell/scroll event không
  build lại preview.
- Remove-duplicates tối đa `100.000` data rows và `1.000.000` key cells.
- Calculated projection tiếp tục chịu bound `1.000.000` cells của contract hiện
  hữu.
- Structural Table operations chỉ enumerate used cells trong affected range;
  không materialize toàn hàng/cột hay logical worksheet.
- Rendering và hit-test vẫn dùng display list/geometry trên visible + overscan;
  không tạo native control cho từng ô.

## 7. Style và filter buttons

- `Table.Style` resolve qua workbook `TableStyleCatalog`; first/last column và
  banded rows/columns giữ precedence của TABLE-004.
- `ShowFilterButtons` là Table metadata độc lập với filter criteria. Tắt button
  không xóa filter/sort state; shared geometry không phát sinh button hit target.
- Filter-buttons command chỉ enable khi Table có header. Nếu header tắt, metadata
  visibility vẫn được giữ để có thể phục hồi khi header bật lại.

## 8. XLSX round-trip

- Table, column stable IDs tiếp tục map vào deterministic relationship ID và
  `uniqueName` hiện hữu.
- Table có filter buttons nhưng chưa có criteria vẫn ghi standard `autoFilter`.
- Khi buttons bị ẩn mà criteria còn tồn tại, exporter giữ `autoFilter` và ghi
  `showButton="0"` cho mọi Table column; importer bỏ các button-only
  `filterColumn` khỏi criteria model nhưng phục hồi `ShowFilterButtons=false`.
- Export phải schema-valid; repeated round-trip giữ relationship ID, column IDs,
  style/formula/filter metadata và preservation envelope hiện hữu.

## 9. Giới hạn chủ ý

- Ribbon runtime chưa định nghĩa editor text/range dùng chung; rename, resize và
  formula commands nhận parameter từ application host thay vì nhúng dialog
  platform-specific vào Core/Ribbon.
- Chỉ có visibility toàn Table cho filter buttons; producer-specific trạng thái
  ẩn/hiện riêng từng cột được quy về visible nếu còn bất kỳ button nào hiện.
- Remove-duplicates so sánh `CellValue` đã tính; không có fuzzy/text-locale
  matching.

Host hoặc adapter không được tự lấp các giới hạn này bằng code path cạnh tranh.
