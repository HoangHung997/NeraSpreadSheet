# Table, Structured Reference, Calculated Column và AutoFilter Contract

Tài liệu này khóa semantics hiện hành của NeraSpreadSheet. Mọi host và adapter phải dùng cùng Core model; không tạo Table, calculated-column, totals hoặc filter model cạnh tranh trong WPF, WinForms, MAUI hay OpenXml.

## 1. Canonical Table model

- Table là metadata sparse trên một `CellRange`, không sở hữu một control/cell object cho từng địa chỉ.
- Table và column dùng stable `Guid` identity.
- Tên Table duy nhất toàn workbook; tên và ID cột duy nhất trong Table.
- Hai Table cùng worksheet không được chồng lấn.
- `HeaderRange`, `DataRange` và `TotalsRange` suy ra từ một `SpreadsheetTable.Range` duy nhất.
- Table state nằm trong snapshot, structural state và Undo/Redo.

## 2. Column metadata

`SpreadsheetTableColumn` giữ:

- stable ID và tên;
- `CalculatedColumnFormula`;
- `TotalsRowFormula`;
- `TotalsRowLabel`.

Formula được chuẩn hóa có dấu `=` tại Core boundary. Totals formula và label là hai cách trình bày cạnh tranh trong một cột; khi đặt label mới, totals formula hiện hành được bỏ, và ngược lại.

## 3. Calculated-column projection

`SpreadsheetTableFormulaProjection` là implementation duy nhất cho calculated columns.

- Formula metadata được neo tại cell của dòng data đầu tiên trong cột.
- Mỗi data row nhận formula được dịch bằng `A1FormulaReferenceTranslator` từ anchor tới destination.
- Structured reference như `[@Amount]` giữ nguyên text và được resolver ánh xạ theo formula address khi tính toán.
- Style hiện tại của cell được giữ.
- Cached value được giữ khi formula text không đổi; formula mới làm cached value trở thành blank trước recalculation.
- Xóa calculated metadata chuyển formula cells thành static values hiện hành.
- Không lưu một mảng formula riêng cạnh tranh với worksheet cells.

### Safety bound

Một operation chỉ được project tối đa `1,000,000` formula/label cells. Vượt giới hạn phải lỗi và rollback trước khi materialize logical worksheet.

## 4. Structural transforms và metadata recovery

- Insert trước Table dịch toàn Table.
- Insert data rows mở rộng Table; full recalculation project formula vào row mới.
- Delete thu hẹp Table khi còn bảo toàn header/data/totals semantics.
- Row/column reorder chỉ hợp lệ khi toàn Table là một uniform translation.
- Cell formulas được structural rewriter dịch trước recalculation.
- Trước khi project, engine dịch mọi formula cell hiện hữu trở lại anchor và chọn biểu thức xuất hiện nhiều nhất. Metadata vì vậy nhận lại A1 formula đã được structural rewriter sửa đúng, thay vì ghi đè cell bằng metadata c�	.
- Totals metadata được refresh từ totals formula cell đã di chuyển.
- Nếu không còn data formula cell để suy ra, metadata hiện hữu được giữ nguyên.

## 5. Table metadata commands và history

`SpreadsheetSession.Tables` cung cấp:

```text
Add / Remove
RenameTable / RenameColumn
SetCalculatedColumnFormula
SetTotalsRowFormula
SetTotalsRowLabel
SetTotalsRowFunction
SetAutoFilter / ClearAutoFilter
```

- Table metadata và projected cells thay đổi trong cùng operation.
- Undo/Redo phục hồi exact Table IDs, metadata, formulas, values và styles trong affected range.
- Rename Table/column rewrite cả workbook cell formulas và formula metadata của mọi Table.
- Failed duplicate rename hoặc oversized projection không được tăng Undo count.

## 6. Structured references

Các dạng hiện hỗ trợ:

```excel
Sales[Amount]
Sales[#Data]
Sales[#All]
Sales[#Headers]
Sales[#Totals]
Sales[#This Row]
Sales[[#Headers],[Amount]]
[@Amount]
```

- `Table[Column]` mặc định là data range.
- `#This Row` và `[@Column]` chỉ hợp lệ trong data row của owning Table.
- Cross-sheet Table reference nhận quoted worksheet qualifier.
- String literal không bị rewrite.
- Structured references expand thành absolute A1 trước parser/evaluator.
- A1 dependencies sau expansion đi vào `FormulaDependencyGraph`.

## 7. Totals functions và SUBTOTAL

`SpreadsheetTableTotalsFunction` hiện hỗ trợ:

| Function | SUBTOTAL code |
|---|---:|
| Average | 101 |
| Count Numbers | 102 |
| Count Nonblank | 103 |
| Maximum | 104 |
| Minimum | 105 |
| Sum | 109 |
| Custom | formula do caller cung cấp |

Các code tương ứng `1,2,3,4,5,9` cũng được evaluator chấp nhận.

- Built-in function tạo formula chuẩn `=SUBTOTAL(code,Table[Column])`.
- Hàng bị AutoFilter loại bỏ luôn bị bỏ khỏi aggregate.
- SUM không có số trả 0; AVERAGE không có số trả `#DIV/0!`; MIN/MAX không có số trả 0.
- COUNT Numbers đếm numeric cells; COUNT Nonblank đếm mọi cell không blank.
- Visible error cells được truyền qua cho SUM/AVERAGE/MIN/MAX; COUNT variants áp dụng semantics đếm của chúng.

### Filter dependencies

`SUBTOTAL` ghi hai nhóm dependency:

1. range dữ liệu được aggregate;
2. các filter-source column ranges quyết định visibility của cùng row span.

Vì vậy thay đổi `Status` có thể tính lại `SUBTOTAL(Sales[Amount])` dù `Status` nằm ngoài Amount range.

## 8. AutoFilter và row projection

- Hỗ trợ value sets, blank matching và một/hai comparison conditions kết hợp AND/OR.
- Nhiều filter columns kết hợp AND theo hàng.
- Row visibility được nén thành spans, không ghi `row height = 0` cho từng row.
- Hidden spans giảm content extent, không tạo viewport slot và bị hit-test bỏ qua.
- Raw row size được giữ để clear filter phục hồi đúng.

## 9. XLSX mapping và preservation

Nera đọc/ghi standard worksheet `tableParts/tablePart` và `TableDefinitionPart` gồm:

- Table name/display name/range;
- header/totals attributes;
- columns;
- calculated/totals formulas và totals labels;
- Table style;
- AutoFilter predicates hiện hỗ trợ.

Nera-generated package dùng relationship/column metadata để giữ stable identities. Foreign package nhận deterministic fallback identities. `PreserveUnknownParts=true` refresh owned Table markup nhưng giữ unowned worksheet/package markup và Table `extLst` qua repeated saves.

## 10. Malformed-input policy

Load bị từ chối với missing/duplicate relationships, unreferenced Table parts, count mismatch, invalid/reversed ranges, width mismatch, bad/duplicate column IDs, duplicate formula children, invalid filter indexes hoặc unsupported markup. Lỗi phải xảy ra trước workbook restoration hoàn thành.

## 11. Giới hạn còn lại

- Chưa có PRODUCT/STDEV/STDEVP/VAR/VARP trong `SUBTOTAL`.
- Chưa loại nested `SUBTOTAL`/`AGGREGATE` khỏi aggregate range.
- Chưa phân biệt manual-hidden rows giữa code `1–11` và `101–111` vì manual hide metadata chưa có.
- Chưa tự suy ra metadata từ arbitrary user edit vào một formula cell; dùng Table controller command.
- Chưa có native Table manager/filter dropdown.
- Chưa có rich date/text/top/bottom/color/icon filters hoặc direct worksheet AutoFilter.
- Chưa có external compatibility corpus rộng.

Presenter và adapter không được tự giả lập các phần còn thiếu bằng model riêng.
