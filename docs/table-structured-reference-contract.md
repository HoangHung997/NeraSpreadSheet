# Table, Structured Reference và AutoFilter Contract

Tài liệu này khóa semantics hiện hành của NeraSpreadSheet cho Table, structured references và AutoFilter. Mọi host và adapter phải dùng cùng Core model; không tạo một Table model riêng cho WPF, WinForms, MAUI hoặc OpenXml.

## 1. Phạm vi và nguyên tắc

- Table là metadata sparse trên một `CellRange`; Table không sở hữu control/cell object cho từng địa chỉ.
- Table và column dùng stable `Guid` identity.
- Tên Table duy nhất trên toàn workbook, so sánh không phân biệt hoa thường.
- Tên và ID cột duy nhất trong một Table, so sánh tên không phân biệt hoa thường.
- Hai Table trên cùng worksheet không được chồng lấn.
- Table model không phụ thuộc OpenXml hoặc UI framework.
- Mọi transform không chứng minh được an toàn phải bị từ chối trước mutation.

## 2. Canonical ranges

Một `SpreadsheetTable.Range` là nguồn sự thật duy nhất.

- `HeaderRange`: hàng đầu tiên khi `HasHeaders=true`.
- `TotalsRange`: hàng cuối cùng khi `HasTotalsRow=true`.
- `DataRange`: phần còn lại giữa header và totals.
- Table phải có số cột metadata đúng bằng `Range.ColumnCount`.
- Table phải có đủ hàng cho cấu hình header/totals.
- Không lưu một bản sao range cạnh tranh trong presenter hoặc adapter.

## 3. Column metadata

`SpreadsheetTableColumn` giữ:

- stable ID;
- tên cột;
- calculated-column formula metadata;
- totals-row formula metadata;
- totals-row label metadata.

Formula metadata được chuẩn hóa có dấu `=` ở Core boundary. Metadata đã được XLSX round-trip nhưng chưa đồng nghĩa với việc Nera tự materialize formula vào mọi data row hoặc tự thực thi totals row.

## 4. Structural transforms

### 4.1 Insert/delete

- Insert trước Table dịch toàn Table.
- Insert hàng bên trong data range mở rộng Table theo transform chuẩn.
- Insert cột bên trong Table không được tự đoán tên/ID cột mới; thao tác mơ hồ bị từ chối.
- Delete thu hẹp Table khi vẫn bảo toàn header/data/totals semantics.
- Delete toàn bộ Table hoặc làm mất cấu hình tối thiểu bị từ chối hoặc loại bỏ theo operation contract đã kiểm thử.
- A1 references trong calculated/totals metadata được rewrite bằng Core structural rewriter.

### 4.2 Reorder

- Row/column reorder chỉ hợp lệ khi toàn `Table.Range` là một phép tịnh tiến đồng nhất.
- Range liên tục nhưng các hàng/cột bên trong có delta khác nhau không được coi là an toàn.
- Filter column identity theo `Guid`, không theo vị trí tạm thời.

### 4.3 History và rollback

- Table nằm trong `WorksheetStructuralState`.
- Add/remove/rename/filter và structural operations tham gia Undo/Redo.
- Rename Table hoặc column cùng formula rewrite phải là một transaction.
- Bất kỳ lỗi validation/rewrite nào phải phục hồi cả Table state lẫn formula state và không tăng Undo count.

## 5. Structured references được hỗ trợ

Các dạng canonical hiện hành:

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

Semantics:

- `Table[Column]` mặc định là data range của cột.
- `#All`, `#Data`, `#Headers`, `#Totals` ánh xạ trực tiếp tới canonical Table ranges.
- `#This Row` và `[@Column]` chỉ hợp lệ khi formula address nằm trong data range.
- Cross-sheet Table reference tạo quoted worksheet qualifier khi cần.
- String literals không bị nhận diện hoặc rewrite như structured reference.
- Structured reference không hợp lệ trả lỗi formula thay vì đoán một A1 range.

## 6. Formula evaluation và dependencies

- `NeraFormulaEngine` nhận `IStructuredReferenceEvaluationContext` khi workbook calculation cần Table semantics.
- Structured references được expand thành absolute A1 references trước parser/evaluator hiện hành.
- Parser, function registry và arithmetic engine không bị nhân đôi.
- A1 dependencies sau expansion được ghi vào `FormulaDependencyGraph`.
- `RecalculateAffected` vì vậy cập nhật công thức Table khi một cell trong data range thay đổi.
- Circular-reference policy hiện hành vẫn áp dụng sau expansion.

## 7. Rename rewrite

### 7.1 Table rename

```excel
=SUM(Sales[Amount])
```

sau khi đổi tên:

```excel
=SUM(Revenue[Amount])
```

### 7.2 Column rename

- Explicit `Sales[Amount]` được rewrite trên toàn workbook.
- Implicit `[@Amount]` chỉ được rewrite trong đúng owning Table range.
- String literal `"Sales[Amount]"` không đổi.
- Stable Table/column ID không đổi sau rename.

## 8. AutoFilter model

`TableAutoFilter` hiện hỗ trợ:

- explicit value set;
- blank matching;
- một custom comparison condition;
- hai custom comparison conditions kết hợp AND hoặc OR;
- nhiều filter columns kết hợp AND ở cấp hàng.

Comparison operators:

- equal/not-equal;
- greater/greater-or-equal;
- less/less-or-equal.

Chưa hỗ trợ rich text/date/top/bottom/color/icon filters hoặc direct worksheet AutoFilter ngoài Table.

## 9. Row visibility projection

- Filter evaluation dùng `WorksheetSnapshot` bất biến.
- Chỉ Table có active filter và data range mới được quét.
- Tổng số hàng đánh giá bị giới hạn rõ ràng.
- Adjacent hidden rows được nén thành `FilteredRowSpan`.
- Layout chuyển các span này thành `AxisIndexRange` trong `SparseAxisMetricIndex`.
- Không tạo một row override cho từng hàng bị lọc.
- Raw row height vẫn được giữ; xóa filter phục hồi đúng size trước đó.
- Hidden spans giảm `TotalExtent`, không sinh `AxisSlot`, và hit-test trả hàng visible kế tiếp.
- `SpreadsheetViewportEngine` refresh metrics khi worksheet version thay đổi trong lúc filter hoạt động.
- Split viewport dùng cùng engine nên không có một visibility path cạnh tranh.

## 10. Standard XLSX mapping

### 10.1 Worksheet relationship

Nera đọc/ghi:

```xml
<tableParts count="1">
  <tablePart r:id="rIdNeraTable..." />
</tableParts>
```

### 10.2 Table definition

Nera đọc/ghi:

- `table@id`;
- `name`/`displayName`;
- `ref`;
- header/totals attributes;
- `autoFilter`;
- `tableColumns/tableColumn`;
- calculated/totals formula children;
- totals label;
- `tableStyleInfo`.

Nera-generated package mã hóa identity:

- Table GUID trong relationship ID `rIdNeraTable{Guid:N}`;
- column GUID trong `tableColumn@uniqueName="nera:{Guid:N}"`.

Foreign package không có metadata Nera nhận deterministic fallback identity từ part URI, relationship ID và numeric column ID. Normal semantic save không cam kết giữ nguyên foreign relationship ID.

## 11. Malformed-input policy

Load bị từ chối khi gặp:

- nhiều `tableParts` collection;
- missing/duplicate relationship ID;
- relationship không trỏ tới `TableDefinitionPart`;
- unreferenced Table part;
- count mismatch;
- missing hoặc reversed Table range;
- width không khớp số columns;
- zero/duplicate column ID;
- duplicate formula child;
- invalid/duplicate filter column index;
- unsupported Table/filter child hoặc operator;
- package-controlled count vượt safety limit.

Lỗi được chuẩn hóa thành `InvalidDataException` trước khi workbook restoration hoàn thành.

## 12. Preservation

Khi `PreserveUnknownParts=true`:

1. Nera dựng package chuẩn từ Core model.
2. Copy-and-patch package được bảo toàn.
3. Table patcher refresh worksheet `tableParts` và owned Table definition parts.
4. Existing Table `extLst` được giữ khi generated model không thay thế extension đó.
5. Unowned worksheet/package markup và opaque graph tiếp tục được giữ bởi preservation pipeline.
6. Final package được xác minh trước destination mutation và envelope attach.

## 13. Giới hạn còn lại

- Chưa auto-fill calculated-column formulas.
- Chưa tự thực thi totals metadata hoặc filter-aware subtotal semantics.
- Chưa có native Table manager/filter dropdown trên WPF, WinForms hoặc MAUI.
- Chưa có rich filter predicates.
- Chưa có external compatibility corpus rộng.
- Chưa có streaming preservation cho package lớn hơn 512 MiB.

Các giới hạn này không được presenter hoặc adapter tự giả lập bằng một model song song.
