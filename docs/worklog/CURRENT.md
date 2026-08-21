# Current Work Handoff

- Ngày cập nhật: 2026-08-21
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Implementation commit đã xác minh: `819fc3c3f5f72ee89438834012d9090c6f6b7032`
- GitHub Actions: run `32446946544`, CI `#505`, kết luận `success`
- Nguồn sự thật: `docs/current-status.md`
- Contract Table: `docs/table-structured-reference-contract.md`
- Roadmap: `ROADMAP.md`

## Batch vừa hoàn thành: Calculated Columns + Filter-aware Totals

### 1. Calculated-column projection

- Thêm `SpreadsheetTableFormulaProjection` trong Core.
- Formula metadata được neo tại data row đầu rồi dịch sang từng row bằng shared A1 translator.
- Structured `[@Column]` formulas giữ một metadata expression nhưng được tính theo formula address từng row.
- Style cell được giữ; formula mới làm cached value blank trước recalculation.
- Clear metadata chuyển projected formulas thành static values hiện hành.
- Projection tối đa 1.000.000 cell/operation; oversized request rollback, không materialize logical axis và không vào history.

### 2. Structural refill và metadata recovery

- Full recalculation tự project formulas/totals cho mọi Table.
- Insert data row nhận formula ngay sau structural transaction.
- Delete/reorder tiếp tục dùng cùng path.
- Engine chuẩn hóa formula cells hiện hữu trở lại first-row anchor và chọn majority expression trước khi fill.
- Cơ chế này phục hồi A1 metadata đã được structural rewriter dịch đúng, tránh metadata cũ ghi đè cell đã move.

### 3. Production commands và atomic history

`SpreadsheetSession.Tables` có thêm:

- `SetCalculatedColumnFormula`;
- `SetTotalsRowFormula`;
- `SetTotalsRowLabel`;
- `SetTotalsRowFunction`.

Table metadata và projected cell state nằm trong cùng Undo/Redo operation. Rename Table/column rewrite cả cell formulas và calculated/totals metadata trên toàn workbook.

### 4. Filter-aware SUBTOTAL

Hỗ trợ:

- `1/101` Average;
- `2/102` Count Numbers;
- `3/103` Count Nonblank;
- `4/104` Maximum;
- `5/105` Minimum;
- `9/109` Sum.

Built-in totals function tạo structured formula dạng `=SUBTOTAL(109,Sales[Amount])`.

### 5. Dependency correctness

- `SUBTOTAL` phụ thuộc data range.
- Đồng thời phụ thuộc các filter-source column ranges quyết định row visibility.
- Sửa `Status` vì vậy trigger affected-only recalculation của subtotal trên `Amount`.
- Filter formula cells được evaluate qua cùng recursive calculation context.

### 6. Tests mới

- Calculated formula propagation và totals execution.
- Add/Undo/Redo phục hồi exact cells/Table.
- Structural insert tự fill row mới.
- Metadata commands và filter-aware totals.
- Oversized projection rollback không materialize.
- SUBTOTAL aggregate codes.
- Filter-source dependency và affected-only recalc.

## CI #505

Toàn bộ matrix xanh tại `819fc3c3...`:

- Core restore/build/tests.
- Architecture verification.
- Calculated-column, structural refill, rollback và SUBTOTAL tests.
- Toàn bộ Table/AutoFilter, validation, conditional-formatting, shared-formula, sparse-style và package preservation regressions.
- Windows full build/tests và desktop GPU runtime smoke.
- MAUI Android build.
- MAUI iOS và Mac Catalyst builds.
- MAUI Windows build/tests.
- Loaded runtime input/context recreation smoke.
- Loaded logical/raw scale/orientation smoke.

## Giới hạn có chủ ý

- Chưa hỗ trợ PRODUCT/STDEV/STDEVP/VAR/VARP trong SUBTOTAL.
- Chưa loại nested SUBTOTAL/AGGREGATE.
- Chưa phân biệt code 1–11 và 101–111 theo manual hidden rows vì manual hide metadata chưa có.
- Chưa tự suy ra metadata từ arbitrary formula-cell edit; dùng Table controller command.
- Chưa có native Table manager/filter dropdown.
- Chưa có rich filters hoặc direct worksheet AutoFilter.

## Tiến độ tổng thể

- Nền móng engine/viewport/renderer: khoảng `89%`.
- MVP bảng tính cơ bản: khoảng `79–82%`.
- Toàn bộ roadmap chuyên nghiệp: khoảng `52%`.
- Production release readiness: khoảng `27–31%`.

## Bước tiếp theo duy nhất

Triển khai **Table Manager + AutoFilter Presenter**:

1. Platform-neutral query/view-model/command contracts.
2. Distinct-value enumeration có safety bounds và search.
3. Apply/clear value/custom filters qua production history.
4. Header/filter-button hit regions trong shared host semantics.
5. WPF và WinForms presenter trước.
6. MAUI responsive popup/sheet presenter.
7. Rich text/date/top/custom-list predicates.
8. Exact-head Core/Windows/MAUI CI.

PR tiếp tục Draft; không merge khi exact-head CI đỏ hoặc chưa xác định.
