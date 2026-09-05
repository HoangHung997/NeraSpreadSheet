# Table style engine contract

Tài liệu này khóa phạm vi `TABLE-004`. Table style là metadata workbook dùng
chung; WPF, WinForms, Direct2D, Skia và OpenXML không được tạo style model hoặc
precedence riêng.

## 1. Model và identity

- `TableStyleDefinition` có `Id` ổn định, tên OpenXML duy nhất không phân biệt
  hoa thường, cờ built-in và tập phần tử style bất biến.
- Các phần tử được hỗ trợ: whole table, header row, totals row, first/last
  column, first/second row stripe, first/second column stripe và filter button.
- Stripe size là số nguyên dương có giới hạn; catalog không nhận phần tử trùng
  loại hoặc custom style trùng ID/tên.
- `TableStyleCatalog` chứa gallery built-in có ID ổn định và custom style của
  workbook. Table tiếp tục tham chiếu bằng `StyleName` để tương thích
  `tableStyleInfo`.

## 2. Theme color và tint/shade

- Màu style là RGB trực tiếp hoặc tham chiếu một slot workbook theme kèm tint
  trong khoảng `[-1, 1]`.
- Theme slot gồm light/dark 1-2, accent 1-6, hyperlink và followed hyperlink.
- Tint âm làm tối, tint dương làm sáng theo luminance HSL; alpha của màu nguồn
  được giữ nguyên.
- Resolver chuyển mọi màu thành `ColorRgba` trước khi tạo display list. Backend
  không tự resolve theme hoặc tint.

## 3. Composition và precedence

Một ô Table được compose theo thứ tự từ thấp đến cao:

1. whole table;
2. row stripe khi `ShowRowStripes` bật;
3. column stripe khi `ShowColumnStripes` bật;
4. first/last column khi cờ tương ứng bật;
5. header hoặc totals row;
6. direct/whole-axis cell formatting;
7. conditional formatting.

Mỗi layer chỉ ghi thuộc tính được khai báo. Filter button dùng phần tử riêng;
nếu thiếu thì kế thừa header style rồi dùng fallback của render theme cho các
thuộc tính chưa khai báo.

## 4. Resolved-style contract và rendering

- `ResolvedTableStyle` là contract duy nhất sau theme resolution cho cell và
  filter button.
- `WorksheetSnapshot` chụp theme và catalog để frame đang render không quan sát
  mutation về sau.
- `SpreadsheetDisplayListComposer` chỉ duyệt row/column visible + overscan và
  compose Table style trước direct/conditional style.
- WPF, WinForms, Direct2D và Skia thực thi cùng `FillRectangle`, `DrawText` và
  `DrawLine` commands; không tạo control theo ô.
- Preview gallery là ma trận nhỏ có giới hạn cố định, không cần worksheet hoặc
  materialize cell.

## 5. OpenXML

- `tableStyleInfo` round-trip tên style cùng các cờ first/last column và
  row/column stripes.
- Custom table style map tới `styles.xml/tableStyles`,
  `tableStyle/tableStyleElement` và `dxfs`; các element hỗ trợ được import vào
  Core và export schema-valid.
- Theme slot/tint đọc từ `theme1.xml` và giữ semantic reference trong custom
  style; RGB trực tiếp giữ nguyên.
- `FilterButton` là extension nội bộ phục vụ renderer, không phải
  `tableStyleElement` chuẩn của SpreadsheetML nên không được phát ra OpenXML.
- Khi `PreserveUnknownParts=true` và style chưa được Nera sở hữu/chỉnh sửa,
  custom/unknown style markup trong package gốc được giữ nguyên. Unsupported
  element không bị giả lập thành semantic khác.

## 6. Hiệu năng và an toàn

- Lookup style chỉ phụ thuộc số Table trên sheet và cell visible; không quét
  toàn logical worksheet.
- Resolved definition và tổ hợp stripe được cache trong snapshot; cache có số
  key bị chặn bởi tập element hữu hạn.
- Preview tối đa 12 hàng x 12 cột.
- Không thêm package, không tạo native control theo ô, không flatten-copy nested
  display lists.

## 7. Cổng nghiệm thu

- Core tests: validation, stable IDs, precedence/composition, theme/tint,
  preview bound và snapshot immutability.
- Rendering tests: geometry/color/font/border parity từ cùng resolved style,
  direct/conditional precedence và visible-only allocation bound.
- OpenXML tests: schema, semantic round-trip, repeated-save preservation và
  custom/unknown style preservation.
- Benchmark: Table style compose trên viewport sparse lớn với allocation được
  báo cáo.
- Loaded runtime smoke thích hợp cho desktop/render path, Release build,
  architecture, packaging, diff và secret gates.
