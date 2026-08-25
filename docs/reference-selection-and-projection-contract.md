# Reference Selection and Dynamic Projection contract

Tài liệu này định nghĩa behavior đã validate cho F008. Excel và LibreOffice là compatibility references; NeraSpreadSheet không phụ thuộc runtime của các sản phẩm đó.

## 1. Architecture boundary

- `FormulaParser` sở hữu missing optional arguments và parenthesized reference-union syntax.
- `NeraFormulaEngine` sở hữu `AREAS`, lazy `CHOOSE`, reference identity và scalar/range dependency capture.
- `NeraDynamicArrayFormulaEngine` sở hữu `CHOOSE` spill bridge, `CHOOSECOLS`, `CHOOSEROWS`, output shape và spill dependencies.
- `ReferenceSelectionFormulaFunctions` chứa eager/versioned `ADDRESS`.
- `FormulaArrayValue` và worksheet spill ownership tiếp tục áp dụng giới hạn/atomicity hiện có.
- `StandardFormulaFunctions.CreateAll()` vẫn là eager built-in aggregation path duy nhất.
- WPF, WinForms, MAUI và OpenXml adapters không triển khai reference-selection hoặc projection semantics.

## 2. Public functions

```text
ADDRESS(row_num,column_num,[abs_num],[a1],[sheet_text])
AREAS(reference)
CHOOSE(index_num,value1,[value2],...)
CHOOSECOLS(array,col_num1,[col_num2],...)
CHOOSEROWS(array,row_num1,[row_num2],...)
```

F008 thêm đúng năm public names. `ADDRESS` là eager/versioned; `AREAS` và `CHOOSE` là AST/reference-aware; `CHOOSECOLS` và `CHOOSEROWS` là dynamic-array names.

## 3. Parser/reference syntax

- Missing function arguments tạo `MissingArgumentNode`, cho phép các công thức như `ADDRESS(2,3,,FALSE,"Sheet 2")`.
- Parentheses thông thường vẫn group một expression.
- Parenthesized comma-separated references tạo `ReferenceUnionNode`, ví dụ `(A1:A2,B1:B2,C1)`.
- Reference union chỉ hợp lệ trong reference-aware context đã hỗ trợ; dùng như scalar/array value trả `#VALUE!`.
- Intersection operator, array constants, `A1#` và `@` chưa nằm trong F008.

## 4. ADDRESS contract

- `row_num`, `column_num`, `abs_num` được truncate toward zero.
- Row phải trong `1..SpreadsheetLimits.MaxRows`; column trong `1..SpreadsheetLimits.MaxColumns`.
- `abs_num`: `1` absolute row+column, `2` absolute row, `3` absolute column, `4` both relative.
- `a1` omitted/blank/TRUE tạo A1; FALSE tạo R1C1 text.
- `sheet_text` optional; tên cần quote được bọc single quotes và embedded quote được doubled.
- Function trả text, không tạo cell dependency.
- Descriptor: namespace `NERA.BUILTIN`, version `1.0.0`, API `1.0`, scalar-only, deterministic/pure, logical arguments.

## 5. AREAS contract

- Cell và contiguous range có một area.
- Parenthesized reference union trả tổng số nested areas.
- `AREAS` đếm geometry/reference identity, không đọc values và không capture dependency cho static references.
- `AREAS(CHOOSE(selector,...))` đánh giá selector, sau đó đếm reference được chọn; nhánh không chọn không tạo dependency.
- Non-reference argument hoặc malformed union trả `#VALUE!`.

## 6. CHOOSE contract

- Từ 1 đến 254 value arguments.
- `index_num` được truncate toward zero và phải nằm trong `1..N`.
- Chỉ nhánh được chọn được đánh giá.
- Error/dependency trong nhánh không chọn không tham gia kết quả.
- Selected scalar/cell/range giữ behavior tương ứng.
- Selected range có thể được truyền vào eager range-aware function và giữ exact source range dependency.
- Top-level `CHOOSE` qua dynamic-array engine có thể spill selected range/supported nested dynamic array.
- Selector-array/vectorized CHOOSE chưa được tuyên bố trong F008.

## 7. CHOOSECOLS và CHOOSEROWS contract

- Source có thể là range, cell, scalar hoặc supported nested dynamic array.
- Index arguments có thể là scalar, range hoặc supported nested dynamic array; values được đọc row-major.
- Numeric index được truncate toward zero.
- Positive index đếm từ đầu; negative index đếm từ cuối.
- Zero hoặc absolute index vượt dimension trả `#VALUE!`.
- Requested order và duplicate index được giữ.
- Source/index dependencies được distinct nhưng không bị mất source identity.
- Output shape:
  - `CHOOSECOLS`: source rows × selected-column count;
  - `CHOOSEROWS`: selected-row count × source columns.
- Output vượt `FormulaArrayValue.MaximumCellCount` (1.000.000 cells) trả `#NUM!`.

## 8. Error and dependency policy

- Failed coercion, invalid index/reference context hoặc unsupported union-as-value trả `#VALUE!`.
- Overflow/resource exhaustion trả `#NUM!`.
- Cell/range selector and selected branch dependencies use existing engine capture.
- Dynamic projection output uses existing spill collision, ownership, structural editing and recalculation contracts.
- No hidden volatile or external-state dependency is introduced.

## 9. Automated validation

Promotion requires:

1. ADDRESS A1/R1C1, abs modes, missing args, quoted sheets and bounds;
2. AREAS single/range/union and CHOOSE-selected reference count;
3. CHOOSE truncation, laziness, selected scalar/range dependency and spill bridge;
4. CHOOSECOLS/CHOOSEROWS positive/negative/duplicate/order and scalar/range/dynamic index arguments;
5. zero/out-of-range/range misuse/resource errors;
6. ADDRESS descriptor and shared registry count;
7. 239 eager/versioned, 20 AST/reference-aware, 7 dynamic, 266 total;
8. 234/234 formula tests and complete exact-head hosted CI matrix.

## 10. Deliberately pending

- CHOOSE selector arrays;
- reference intersection and full reference-return algebra;
- array constants/vectorized operators;
- `A1#` and `@`;
- broader Microsoft/LibreOffice/ODS differential corpus;
- remaining projection/stack/wrap/take/drop functions;
- third-party array-returning extension integration.
