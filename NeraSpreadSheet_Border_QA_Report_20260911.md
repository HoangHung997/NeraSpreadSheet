# Báo cáo QA — NeraSpreadSheet đối chiếu viền ô với Excel

**Ngày kiểm tra:** 11/09/2026 (Asia/Saigon)  
**Commit được kiểm tra:** `54067a37f7d91ab8c0ae42671e39bfef9c9ea273` (`origin/main`)  
**Worktree QA sạch:** `D:\VSstudio\NERASPREADSHEET-QA-REPORT-20260911`  
**Workbook đối chiếu:** `Z:\2_TCTTA\2026\10_Gia_Lai\1_TĐ_SeSan4\4_HQ_TQT\4.2_Bien_ban_NT+TQT\HC_2.HC_NKTC_SeSan4.xlsx`

## 1. Kết luận nhanh

- Đồng bộ Git đã xác nhận `origin/main` và worktree QA cùng ở commit `54067a37`.
- Build Release của solution Avalonia thành công, **0 cảnh báo / 0 lỗi**.
- Các bộ kiểm thử liên quan đều xanh: Avalonia **203/203**, Rendering.Spreadsheet **128/128**, OpenXml **199/199**.
- Workbook thật mở được ở cả Excel và NeraSpreadSheet. Nera hiển thị đúng dữ liệu cơ bản, sheet, công thức và ngày tháng; đồng thời báo **2 cảnh báo định dạng tương thích**.
- XML gốc xác nhận ô `E12` có viền thật: trái/phải `thin`, trên/dưới `dotted`. Excel hiển thị các nét này rõ và đậm; Nera hiển thị nhạt hơn và không giữ đúng nét chấm trong phần hiển thị.
- Vì vậy, phản ánh “viền của app nhìn như không vẽ” là **có cơ sở**. Đây là thiếu sót của codec/rendering dùng chung trên SDK, không phải do file Excel bị mất viền.
- Không sửa source, không lưu thay đổi vào workbook và không ghi đè file người dùng trong đợt QA này.

## 2. Phạm vi và phương pháp

1. Fetch lại `origin/main` và tạo worktree QA sạch tại đúng commit trên.
2. Build Release solution Avalonia.
3. Chạy các bộ test Avalonia, Rendering.Spreadsheet và OpenXml.
4. Mở workbook gốc bằng Excel desktop ở chế độ Normal, chọn `E12`, mở `Format Cells > Border`.
5. Mở cùng workbook bằng NeraSpreadSheet Avalonia, chọn `E12`, mở chi tiết tương thích và `Định dạng ô > Đường viền`.
6. Đọc trực tiếp `xl/styles.xml` và `xl/worksheets/sheet1.xml` trong XLSX để đối chiếu với hình ảnh.

## 3. Đồng bộ, build và test

| Hạng mục | Kết quả |
|---|---:|
| `origin/main` sau fetch | `54067a37f7d91ab8c0ae42671e39bfef9c9ea273` |
| `dotnet build NeraSpreadSheet.Avalonia.slnx -c Release --nologo` | PASS — 0 Warning, 0 Error |
| `dotnet test tests/NeraSpreadSheet.Avalonia.Tests -c Release --no-build --nologo` | PASS — 203/203 |
| `dotnet test tests/NeraSpreadSheet.Rendering.Spreadsheet.Tests -c Release --nologo` | PASS — 128/128 |
| `dotnet test tests/NeraSpreadSheet.OpenXml.Tests -c Release --nologo` | PASS — 199/199 |

Các test xanh xác nhận không có regression ở các contract hiện có, nhưng chưa bao phủ đầy đủ visual fidelity của từng kiểu nét viền trên mọi backend.

## 4. Workbook được mở và đọc như thế nào

### 4.1 Excel thật

- Workbook mở thành công ở Excel desktop.
- Chuyển sang chế độ **Normal** để loại bỏ watermark Page Break Preview.
- Sheet đang đối chiếu: `DMHSNT`; sheet tabs còn thấy `NKTC`.
- Ô chọn: `E12`; formula bar hiển thị `=E11`; giá trị hiển thị là ngày `11/08/2026` theo định dạng ngày của workbook.
- Ảnh bằng chứng: [Excel mở workbook ở Normal](evidence/03-excel-loaded-normal.png).

### 4.2 NeraSpreadSheet

- Workbook mở thành công trên bản build từ commit `54067a37`.
- Sheet đang đối chiếu: `DMHSNT`; Nera hiển thị các sheet tabs `DMHSNT`, `NKTC`, `DonGia_C`, `DonGia_N`, `DonGia_B`, `ThongtinDA`, `Toado`, `DL`, `LINK_Can`, `Tra he so`.
- Ô chọn: `E12`; formula bar hiển thị `=E11`; dữ liệu/ngày tháng được nạp và hiển thị.
- Nera hiển thị banner: “Có 2 cảnh báo: một số định dạng chỉ được giữ để lưu lại, chưa hiển thị đầy đủ”.
- Ảnh bằng chứng: [Nera mở workbook](evidence/01-nera-loaded.png).
- Nội dung chi tiết cảnh báo: [Chi tiết tương thích Nera](evidence/02-nera-compatibility-details.png).

Hai cảnh báo thực tế là:

1. `styles.xml` chứa `dxf/border/color` chưa được SDK đánh giá/chỉnh sửa; metadata gốc được giữ lại nhưng không cam kết giống Excel khi render.
2. Sheet `Toado`, vùng `A40:D996`, có `conditionalFormatting/opaque-dxf`; rule được giữ nguyên nhưng chưa được render đầy đủ.

## 5. Đối chiếu XML gốc của ô E12

| Thuộc tính | Giá trị đọc từ XLSX |
|---|---|
| Cell XML | `<c r="E12" s="256"><f>E11</f><v>46245</v></c>` |
| Style ID | `256` |
| `borderId` | `22` |
| `numFmtId` | `14` (date) |
| `fontId` | `58` |
| Workbook sheets | 10 |
| Tổng formula cells | 764 |
| `styles.xml` borders | 24 |
| `styles.xml` cellXfs | 374 |

`borderId=22` trong `xl/styles.xml` là:

```xml
<border>
  <left style="thin"><color indexed="64"/></left>
  <right style="thin"><color indexed="64"/></right>
  <top style="dotted"><color indexed="64"/></top>
  <bottom style="dotted"><color indexed="64"/></bottom>
  <diagonal/>
</border>
```

`indexed="64"` là màu Automatic của Excel. Như vậy viền chấm trên/dưới và viền mảnh trái/phải là dữ liệu gốc có thật, không phải artefact của ảnh chụp.

## 6. So sánh trực quan Excel và Nera

| Nội dung | Excel | NeraSpreadSheet hiện tại |
|---|---|---|
| Viền của vùng `E12` | Đường viền đen rõ; nét chấm trên/dưới nhìn thấy; ô và lưới phân biệt tốt | Có đường viền nhưng nhạt hơn, phần lớn nhìn như nét liền/lưới; nét chấm không được thể hiện tương đương |
| Format Cells — kiểu nét | Có Hair, Dotted, DashDotDot, DashDot, Dashed, Thin, nhiều biến thể Medium, Thick, Double | Có Mảnh, Vừa, Dày, Nét đứt, Nét chấm, Nét đôi |
| Format Cells — màu | `Automatic` và bảng màu Excel | Trường nhập `#RRGGBB`, mặc định `#000000` |
| Format Cells — preview | Có sơ đồ preview theo từng cạnh và preset/edge controls | Chưa có preview từng cạnh; preset chỉ ở mức mẫu tổng quát |
| Định dạng ngày | Hiển thị `11/08/2026` theo format của cell | Hiển thị ngày và nạp được format cơ bản; cảnh báo cho phần format/dxf chưa hỗ trợ |

- [Format Cells > Border của Excel](evidence/04-excel-format-cells-border.png)
- [Format Cells > Đường viền của Nera](evidence/05-nera-format-cells-border.png)
- [Danh sách kiểu nét Nera đang cung cấp](evidence/06-nera-border-line-options.png)

## 7. Nguyên nhân kỹ thuật đã xác định

### 7.1 Thứ tự vẽ làm lưới có thể phủ lên viền ô

Trong `src/NeraSpreadSheet.Rendering.Spreadsheet/SpreadsheetDisplayListComposer.cs`, phần compose gọi `DrawUnmergedCells` khoảng dòng 180 rồi gọi `DrawGrid` khoảng dòng 188. Viền explicit của cell được phát trong `DrawCellBorders` khoảng dòng 373. Do grid được phát sau cell border, đường lưới có thể phủ/giảm tương phản của viền explicit.

### 7.2 Renderer chưa có semantics cho dash/dotted/double

`DrawBorder` khoảng dòng 628–651 cuối cùng chỉ gọi `builder.DrawLine(...)`; độ rộng có thay đổi cho Medium/Thick/DoubleLine, nhưng không có stroke pattern cho Dotted, Dashed, DashDot hoặc Hair. `DisplayList` hiện chỉ có API `DrawLine(start, end, strokeWidth, color)` tại `src/NeraSpreadSheet.Rendering.Abstractions/DisplayList.cs:34`.

### 7.3 Màu indexed/theme chưa được giải quyết đầy đủ

`OpenXmlStyleTableCodec.ReadColor` tại khoảng dòng 412–431 ưu tiên `Rgb` và dùng fallback cho màu không phải RGB. Màu Automatic của workbook đang là `indexed=64`, vì vậy màu thực tế có thể bị rơi về fallback thay vì được resolve theo Excel theme/indexed palette.

### 7.4 Phần định dạng chưa hỗ trợ được bảo toàn nhưng chưa render tương đương

`OpenXmlImportDiagnostics` tại khoảng dòng 96–97 ghi rõ `XLSX_OPAQUE_FORMATTING`: SDK giữ định dạng gốc để lưu lại nhưng không cam kết Excel visual fidelity. Đây là hành vi an toàn cho dữ liệu, nhưng giải thích trực tiếp vì sao workbook mở được mà hình thức chưa giống Excel.

### 7.5 UI Format Cells hiện mới là tập con

`src/NeraSpreadSheet.Avalonia/NeraFormatCellsDialog.cs` khoảng dòng 126–129 chỉ cung cấp 3 preset (`none/all/bottom`) và 6 kiểu nét (`Thin/Medium/Thick/Dashed/Dotted/DoubleLine`). Điều này chưa tương đương hộp thoại Border của Excel với preview theo cạnh, Hair, các biến thể dash-dot, Automatic/theme color và Double thực.

## 8. Đánh giá QA

**Mức độ:** P1 cho mục tiêu “Excel-compatible formatting/rendering”; P2 nếu phạm vi hiện tại chỉ là bảng tính cơ bản không yêu cầu fidelity cao.

**Đạt:**

- Mở workbook lớn và nhiều sheet thành công.
- Giữ được dữ liệu, công thức, giá trị ngày tháng cơ bản.
- Không crash trong lần mở này.
- Cảnh báo phần chưa hỗ trợ được hiển thị rõ và metadata gốc được giữ để lưu lại.

**Chưa đạt:**

- Viền dotted/thin của cell chưa thể hiện trực quan như Excel.
- Chưa phân biệt chắc chắn explicit border với gridline ở mọi trường hợp.
- Chưa có stroke semantics đầy đủ cho Hair, dash-dot, medium-dashed và Double thực.
- Chưa resolve đầy đủ màu Automatic/indexed/theme.
- Conditional-formatting dxf chưa được render tương đương.

## 9. Khuyến nghị triển khai tiếp theo

1. Phát lưới trước, explicit cell borders sau; khi cell có explicit edge thì suppress gridline cạnh đó hoặc dùng z-order/clip rõ ràng.
2. Mở rộng rendering abstraction để biểu diễn dash pattern, Hair và Double; cập nhật các backend WPF, WinForms, Direct2D và Skia cùng một contract.
3. Resolve màu RGB, indexed và theme/Automatic theo OpenXML color model; thêm test cho `indexed=64`.
4. Bổ sung preview/preset từng cạnh trong Format Cells, nhưng giữ model/API dùng chung, không tạo control cho từng ô.
5. Thêm visual regression fixture lấy chính `E12` của workbook này: so sánh thin/dotted/automatic color ở 100% và các zoom phổ biến.
6. Bổ sung test dxf/conditional formatting theo hướng giữ nguyên phần chưa hỗ trợ khi lưu nhưng render được các rule đã hiểu.

## 10. Giới hạn và tính toàn vẹn

- Đây là phiên QA read-only. Không sửa source, không commit, không push và không lưu workbook sau khi mở.
- Các thao tác Excel/Nera chỉ dùng để quan sát, mở hộp thoại và chụp ảnh; không thay đổi dữ liệu người dùng.
- Ảnh bằng chứng được chụp trong cùng phiên kiểm thử ngày 11/09/2026 và đặt trong thư mục `evidence/` cạnh báo cáo.
- Báo cáo không đóng gói bản sao workbook vì workbook chứa dữ liệu dự án người dùng; đường dẫn gốc được ghi để tái lập kiểm thử trong môi trường được cấp quyền.

## 11. Danh mục bằng chứng và SHA-256

| Tệp | Nội dung | SHA-256 |
|---|---|---|
| `evidence/01-nera-loaded.png` | Nera mở workbook, sheet DMHSNT, E12 và banner cảnh báo | `5B29BDF0BE18539106A1E5F340CE4BE6B7EC4FAFD5A2E1B25B2422529BF2827C` |
| `evidence/02-nera-compatibility-details.png` | Hai cảnh báo opaque formatting/dxf của Nera | `88BA3EA48D459B3DBBC8EC73C914B56EC8F30A38E81C10C7DBD917288881708F` |
| `evidence/03-excel-loaded-normal.png` | Excel mở workbook ở chế độ Normal, E12 | `1273E18687B2EA9C060F2697E783223041DEB9BA01667AE8C1279C1B5D72AE43` |
| `evidence/04-excel-format-cells-border.png` | Excel Format Cells > Border | `E882B8E6890160C51E9CD046AC1A6B8310626594394799AA48CDAB0B5FD78CFB` |
| `evidence/05-nera-format-cells-border.png` | Nera Định dạng ô > Đường viền | `79850123D14D0DD6FE488DD3F092E223C25666B472987F26B340144BE468A071` |
| `evidence/06-nera-border-line-options.png` | Các kiểu nét Nera đang cung cấp | `8FEB2EE9A1747A3BD8682020C2A99DA992B007B27806B2FE5E74DADFB6BB5DA0` |

## Phụ lục — QA dialog phụ của Ribbon

Đợt kiểm tra tiếp theo đã đối chiếu các dialog phụ của Ribbon giữa NeraSpreadSheet và Excel, gồm Format Cells, Page Setup, Zoom, Data, Review, Protect, Statistics và Ribbon customization. Xem báo cáo và ảnh bằng chứng đầy đủ tại [NeraSpreadSheet_Ribbon_Dialog_QA_Report_20260911.md](NeraSpreadSheet_Ribbon_Dialog_QA_Report_20260911.md).
