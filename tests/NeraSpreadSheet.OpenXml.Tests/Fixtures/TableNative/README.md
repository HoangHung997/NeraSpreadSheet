# Corpus native Table nhỏ

Hai workbook chỉ chứa số và nhãn mẫu mới. Không dùng workbook cá nhân, không
nhập fixture bên thứ ba. Quyền sử dụng theo repository hiện tại; dữ liệu này
không tự cấp một giấy phép phân phối mới cho SDK. Hash, producer, version,
expected cells/formulas và graph gốc nằm trong `provenance.json`.

## Excel

Tạo workbook trắng mới bằng Ctrl+N trong Microsoft Excel Windows
16.0.20326.20132. Nhập A1 `Amount`, B1 `Double`, A2:A4 lần lượt 10/20/30.
Ctrl+T tạo Table A1:B4 có headers (tên mặc định `Table1`). Nhập B2
`=[@Amount]*2`; Excel tự tạo calculated column 20/40/60 và lưu dạng
`Table1[[#This Row],[Amount]]*2`. Bật Total Row: A5 `Total`, B5 120 và
`SUBTOTAL(109,Table1[Double])`. Nhập D1 `=SUM(Table1[Amount])`, kiểm tra 60.
F12 lưu một tên XLSX mới trong output của lane; đóng riêng workbook mẫu đã lưu.

Chạy `sanitize-excel.ps1 -Source <native-original> -Destination <new-copy>`:
xóa core document metadata, absolute save path và revision pointer; chuẩn hóa
ZIP timestamps. Chỉ payload `docProps/core.xml` và `xl/workbook.xml` thay đổi.
Table/worksheet/styles/formula caches và relationship payloads giữ nguyên byte.
Native original hash vẫn ghi trong manifest, original không commit vì có
metadata tác giả và đường dẫn máy. Table GUIDs là document identities ngẫu nhiên,
không phải Machine ID. Không đổi security/privacy settings của Excel.

## Nera

Tạo `SpreadsheetSession(new Workbook())`, điền cùng headers và ba giá trị.
Gọi `Tables.Create(A1:B4, "Table1")`, `SetCalculatedColumnFormula` cho cột
Double với `=Table1[[#This Row],[Amount]]*2`, `SetTotalsRow(true)`,
`SetTotalsRowLabel` cho Amount = `Total`, `SetTotalsRowFunction` cho Double
= `Sum`, `SetStyle("TableStyleMedium2")`, rồi `SetFormula(D1,
"=SUM(Table1[Amount])")`. Lưu bằng
`NeraOpenXmlSpreadsheetSessionSerializer.SaveSessionAsync` với export options
mặc định. Không chỉnh ZIP/XML của file Nera. GUID/ZIP timestamps có thể khác
khi tái tạo; semantic tests và graph identity qua mỗi round-trip là tiêu chí.

## Semantic và preserve-only

Cells, formulas, Table geometry/name/column identities, totals và built-in style
là semantic. Excel revision namespace attributes, data differential style
reference và native part URI/relationship ID được kiểm tra/bảo toàn khi bật
preservation; không gọi đó là đầy đủ Excel visual parity. Strict export có thể
chuẩn hóa graph sang Nera. Tests kiểm tra schema Microsoft365, cached values,
recalculate, ba vòng Save–Load, Convert/Undo/Redo và source bytes không đổi.

## Khoảng trống LibreOffice

Không thấy LibreOffice trong native inventory hoặc hai thư mục cài đặt chuẩn.
Không cài/tải/chạy producer mới. Đã kiểm tra `tablerefsnamed.xlsx`,
`tableref-column-linebreak.xlsx`, `TableStyleTest.xlsx`,
`tdf162963_TableWithTotalsEnabled.xlsx` từ
[LibreOffice core](https://github.com/LibreOffice/core/tree/master/sc/qa/unit/data/xlsx).
App metadata đều ghi Excel hoặc Excel Online, nên không dùng chúng để chứng
minh LibreOffice producer. Project công bố
[MPL licensing](https://www.libreoffice.org/licenses/); không nhập fixture
upstream khi chưa đủ version/producer/provenance và không suy ra quyền từ tên
repository. TABLE-006 còn mở cho producer LibreOffice.
