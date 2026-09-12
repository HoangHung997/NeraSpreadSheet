# Báo cáo QA — Dialog phụ của Ribbon: NeraSpreadSheet và Microsoft Excel

**Ngày kiểm tra:** 11/09/2026 (Asia/Saigon)  
**Mục tiêu:** kiểm tra từng hộp thoại phụ được mở từ Ribbon, đối chiếu giao diện/hành vi với Excel, và ghi rõ khoảng cách trước khi lập kế hoạch đạt Excel-compatible.  
**Nguồn được kiểm tra:** `origin/main` tại `54067a37f7d91ab8c0ae42671e39bfef9c9ea273`.  
**Worktree QA:** `D:\VSstudio\NERASPREADSHEET-QA-REPORT-20260911`.  
**Ứng dụng Nera:** bản Release build `NeraSpreadSheet.Avalonia.Sample` từ đúng commit trên.  
**Ứng dụng đối chứng:** Microsoft Excel desktop trên Windows 11, cửa sổ `Book1 - Excel`.

## 1. Kết luận điều hành

NeraSpreadSheet hiện có một nền tảng Ribbon và các dialog phụ dùng được cho bảng tính cơ bản, nhưng **chưa thể coi là giống Excel 100%**. Các hộp thoại đã có hoạt động ổn định và được nối đúng vào command catalog; tuy nhiên chúng là một tập con nhỏ hơn Excel cả về số lượng, trường cấu hình, bố cục, lẫn hành vi.

Các khác biệt lớn nhất:

- `Format Cells` của Nera có Number/Font/Alignment/Border/Fill, nhưng Excel có thêm Protection và nhiều lựa chọn chi tiết hơn. Excel có thư viện kiểu viền, preview theo từng cạnh, màu theme/indexed và các tùy chọn font/number phong phú hơn.
- `Page Setup` của Nera có Page/Margins/Sheet; Excel có thêm Header/Footer, Print/Print Preview/Options, print area, repeating titles, page order và nhiều trường in hơn.
- Nera `PrintOptions` hiện là command mở lại `Page Setup` ở tab Sheet, không phải một dialog độc lập như tên command gợi ý.
- Nera có Formula Help, Error Check, Workbook Statistics, Print Preview và Ribbon Customization; Excel có các chức năng tương ứng nhưng trải nghiệm/chiều sâu khác hẳn. `Print Preview` của Nera là control preview nội bộ; Excel chuyển sang trải nghiệm Backstage/print service.
- Excel có thêm dialog/flow của Data Validation, Advanced Filter, Consolidate, Protect Sheet/Workbook, Sort, Chart insertion, Spelling/Thesaurus và Custom Views. Nera chưa có các dialog tương đương trong sample/Ribbon hiện tại.

**Đánh giá hiện tại:**

| Phạm vi | Đánh giá |
|---|---|
| Mở dialog, command routing, cancel/close cơ bản | Đạt trong các dialog Nera đã có |
| Tập dialog phụ so với Excel chuẩn | Chưa đạt; thiếu nhiều nhóm Data/Review/View |
| Tương đồng bố cục/visual | Chưa đạt; Nera là layout Avalonia riêng, chưa phải bản sao Excel |
| Tương đồng trường dữ liệu và hành vi | Chưa đạt; nhiều trường Excel chưa có hoặc mới là subset |
| Mục tiêu “giống Excel 100%” | **Chưa đạt** |

Không có mã nguồn hay workbook người dùng nào được sửa trong đợt QA này. Ảnh trong báo cáo là bằng chứng quan sát UI; ảnh workbook thật và viền ô được giữ trong thư mục `evidence/` của gói QA trước.

## 2. Đồng bộ source, build và regression tests

`origin/main` đã được xác nhận ở đúng SHA nêu trên trước khi chạy. Lệnh fetch toàn bộ remote có một cảnh báo ref cũ (`feature/bootstrap-architecture-v0.1`) không còn tồn tại ở phía máy chủ; việc fetch `origin/main` vẫn thành công và không làm thay đổi commit được kiểm tra. Các nhánh tính năng như `origin/feature/ribbon-dialogs-003` được ghi nhận nhưng **không tự đưa vào main** để tránh đánh giá nhầm source chưa hợp nhất.

| Cổng | Kết quả |
|---|---:|
| `dotnet build NeraSpreadSheet.Avalonia.slnx -c Release --nologo` | PASS — 0 warning, 0 error |
| `dotnet test tests/NeraSpreadSheet.Avalonia.Tests -c Release --no-build --nologo` | PASS — 203/203 |
| `dotnet test tests/NeraSpreadSheet.Rendering.Spreadsheet.Tests -c Release --no-build --nologo` | PASS — 128/128 |
| `dotnet test tests/NeraSpreadSheet.OpenXml.Tests -c Release --no-build --nologo` | PASS — 199/199 |
| Runtime Nera Ribbon/dialog smoke | PASS — các dialog đã có mở được và chụp được |
| Runtime Excel dialog enumeration | PASS — UIA tree và ảnh đã ghi lại |

Các test xanh trên xác nhận không có regression compile/unit ở commit main, nhưng chưa phải visual conformance với Excel. Việc đạt 100% cần thêm contract/visual regression cho từng dialog và từng backend mà SDK hỗ trợ.

## 3. Phạm vi và phương pháp

1. Dùng worktree sạch từ `origin/main`, build Release rồi chạy ba bộ test liên quan.
2. Khởi chạy bản Nera Avalonia Release, ghi ảnh từng tab Ribbon và từng dialog có thể mở trực tiếp từ Ribbon/File backstage.
3. Khởi chạy Excel desktop, ghi ảnh các tab Ribbon chuẩn (Home, Insert, Page Layout, Formulas, Data, Review, View) và các dialog/flow có thể mở từ group launcher hoặc nút dialog.
4. Refresh accessibility state sau mỗi thao tác; không dùng lại element index/tọa độ sau khi UI thay đổi.
5. Blank `Book1` của Excel được dùng để quan sát dialog chung. Khi Excel yêu cầu dữ liệu (ví dụ Sort/Chart với vùng rỗng), ghi lại cảnh báo thay vì tự ý sửa workbook. Đối chiếu workbook thật và viền ô vẫn tham chiếu bộ bằng chứng `evidence/` đã kiểm tra trước.
6. Không coi các tab add-in Excel (Kutools, Foxit, ABBYY, MAPCITE, MyTools) là yêu cầu SDK lõi; chúng được loại khỏi phạm vi parity.

## 4. Bằng chứng giao diện Ribbon

### 4.1 NeraSpreadSheet Avalonia

| Ảnh | Tab/ghi chú |
|---|---|
| ![Nera Home](evidence-ribbon-dialogs/01-nera-ribbon-home.png) | Home: Clipboard, Font, Alignment, Number, Styles, Cells; có launcher Font/Alignment/Number |
| ![Nera Insert](evidence-ribbon-dialogs/02-nera-ribbon-insert.png) | Insert: chart, row/column; chart disabled khi sheet mặc định không có vùng dữ liệu |
| ![Nera Page Layout](evidence-ribbon-dialogs/03-nera-ribbon-page-layout.png) | Page Layout: orientation, paper, margins, print gridlines/headings, preview |
| ![Nera Formulas](evidence-ribbon-dialogs/04-nera-ribbon-formulas.png) | Formulas: Formula Help, SUM/AVERAGE/IF/XLOOKUP, recalc, error check |
| ![Nera Data](evidence-ribbon-dialogs/05-nera-ribbon-data.png) | Data: sort và summary/filter controls ở mức hiện có |
| ![Nera Review](evidence-ribbon-dialogs/06-nera-ribbon-review.png) | Review: error check, workbook statistics, formula help |
| ![Nera View](evidence-ribbon-dialogs/07-nera-ribbon-view.png) | View: gridlines, headers/menu, zoom, freeze/split, theme |

### 4.2 Excel chuẩn

| Ảnh | Tab/ghi chú |
|---|---|
| ![Excel Home](evidence-ribbon-dialogs/01-excel-ribbon-home.png) | Home: Clipboard, Font, Alignment, Number, Styles, Cells, Editing; ba group launcher Format Cells |
| ![Excel Insert](evidence-ribbon-dialogs/02-excel-ribbon-insert.png) | Insert: Tables, Illustrations, Controls, Chart galleries, Sparklines, Filters, Links, Comments, Text, Symbols |
| ![Excel Page Layout](evidence-ribbon-dialogs/03-excel-ribbon-page-layout.png) | Page Setup, Scale to Fit, Sheet Options, Arrange; hai launcher Page Setup và một launcher Sheet Options |
| ![Excel Formulas](evidence-ribbon-dialogs/07-excel-ribbon-formulas.png) | Function Library, Defined Names, Formula Auditing, Calculation |
| ![Excel Data](evidence-ribbon-dialogs/04-excel-ribbon-data.png) | Get & Transform, Queries/Connections, Sort & Filter, Data Tools, Forecast, Outline |
| ![Excel Review](evidence-ribbon-dialogs/05-excel-ribbon-review.png) | Proofing, Accessibility, Language, Changes, Comments, Notes, Protect |
| ![Excel View](evidence-ribbon-dialogs/06-excel-ribbon-view.png) | Workbook Views, Show, Zoom, Window, Macros |

Excel còn có Draw, Automate, Developer và nhiều add-in tabs; chúng không phải surface lõi mà Nera đang cam kết mô phỏng nên được ghi là ngoài phạm vi parity hiện tại.

## 5. Danh mục dialog phụ của Nera

### 5.1 Format Cells

Ba launcher Home cùng mở một `NeraFormatCellsDialog` và chọn tab ban đầu tương ứng. Dialog có năm tab: Number, Font, Alignment, Border, Fill.

| Ảnh | Nội dung quan sát |
|---|---|
| ![Nera Font](evidence-ribbon-dialogs/13-nera-dialog-font.png) | Font family, size, Bold/Italic/Strike, underline, script, màu `#RRGGBB`, ghi chú về TrueType |
| ![Nera Alignment](evidence-ribbon-dialogs/14-nera-dialog-alignment.png) | Horizontal/Vertical, wrap, shrink, indent, rotation; merge là command Ribbon riêng |
| ![Nera Number](evidence-ribbon-dialogs/15-nera-dialog-number.png) | Category, decimals/thousands/currency/negative, format code, preview; một số trường disabled theo category |
| ![Nera Number categories](evidence-ribbon-dialogs/16-nera-number-categories.png) | Chung, Số, Tiền tệ, Kế toán, Ngày tháng, Thời gian, Phần trăm, Phân số, Khoa học, Văn bản, Tùy chỉnh |

Nera có Border/Fill trong dialog nhưng không có launcher riêng trên Ribbon. Bằng chứng border đã có trong `evidence/05-nera-format-cells-border.png` và `evidence/06-nera-border-line-options.png`.

### 5.2 Page Setup, Print Options, Zoom

| Ảnh | Nội dung quan sát |
|---|---|
| ![Nera Page Setup Page](evidence-ribbon-dialogs/09-nera-dialog-page-setup-page.png) | Trang: orientation, paper, scale %, fit width/height |
| ![Nera Page Setup Margins](evidence-ribbon-dialogs/10-nera-dialog-page-setup-margins.png) | Lề trên/dưới/trái/phải/header/footer, center ngang/dọc |
| ![Nera Page Setup Sheet](evidence-ribbon-dialogs/11-nera-dialog-page-setup-sheet.png) | Gridlines/headings, header/footer text; cảnh báo chưa có editor in đầy đủ |
| ![Nera Print Options](evidence-ribbon-dialogs/12-nera-dialog-print-options.png) | Command `Ui.Dialog.PrintOptions` mở cùng Page Setup ở tab Sheet |
| ![Nera Zoom](evidence-ribbon-dialogs/08-nera-dialog-zoom.png) | Nhập zoom phần trăm, OK/Hủy |

### 5.3 Dialog thông tin và mở rộng Ribbon

| Ảnh | Nội dung quan sát |
|---|---|
| ![Nera Ribbon customization](evidence-ribbon-dialogs/17-nera-dialog-ribbon-customization.png) | Tùy biến tab/group/command/QAT, rename, show/hide, large/small, move, JSON import/export |
| ![Nera formula help](evidence-ribbon-dialogs/20-nera-dialog-formula-help.png) | Signature, mô tả và đối số của SUM; đóng bằng nút cửa sổ |
| ![Nera error check](evidence-ribbon-dialogs/21-nera-dialog-error-check.png) | Danh sách lỗi hoặc “Không có ô lỗi trong trang tính.” |
| ![Nera statistics](evidence-ribbon-dialogs/22-nera-dialog-statistics.png) | Số sheet, ô lưu trữ, bảng; dialog thông tin đơn giản |
| ![Nera backstage statistics](evidence-ribbon-dialogs/18-nera-backstage-statistics.png) | Trang File/Thống kê workbook trong backstage |
| ![Nera print preview](evidence-ribbon-dialogs/19-nera-backstage-print-preview.png) | Trang File/Xem trước in; preview nội bộ và nút zoom/số cột |

## 6. Danh mục dialog phụ của Excel đã kiểm tra

### 6.1 Format Cells — sáu tab

Excel mở cùng một `Format Cells` modal từ ba launcher ở Home, nhưng có sáu tab đầy đủ:

| Ảnh | Tab và thành phần |
|---|---|
| ![Excel Font](evidence-ribbon-dialogs/08-excel-dialog-format-cells-font.png) | Font, Font style, Size, Underline, Color, Effects, Normal font, Preview |
| ![Excel Alignment](evidence-ribbon-dialogs/09-excel-dialog-format-cells-alignment.png) | Horizontal/Vertical, indent, text control, right-to-left, orientation/rotation |
| ![Excel Number](evidence-ribbon-dialogs/10-excel-dialog-format-cells-number.png) | Category list và vùng preview/mô tả định dạng |
| ![Excel Border](evidence-ribbon-dialogs/11-excel-dialog-format-cells-border.png) | Nhiều kiểu nét (Hair, Dotted, DashDot, Medium, Thick, Double), màu Automatic, preset/preview từng cạnh, diagonal |
| ![Excel Fill](evidence-ribbon-dialogs/12-excel-dialog-format-cells-fill.png) | Background/pattern colors, Pattern Style, Fill Effects, More Colors, Sample |
| ![Excel Protection](evidence-ribbon-dialogs/13-excel-dialog-format-cells-protection.png) | Locked/Hidden và mô tả liên hệ với Protect Sheet |

### 6.2 Page Setup

| Ảnh | Tab và thành phần |
|---|---|
| ![Excel Page Setup Margins](evidence-ribbon-dialogs/05-excel-dialog-page-setup-margins.png) | Margins, diagram, center on page, Print/Print Preview/Options |
| ![Excel Header Footer](evidence-ribbon-dialogs/06-excel-dialog-page-setup-header-footer.png) | Header/Footer presets, Custom Header/Custom Footer, different first/odd-even pages, scale/align |
| ![Excel Sheet](evidence-ribbon-dialogs/07-excel-dialog-page-setup-sheet.png) | Print area/titles, gridlines, black-and-white, draft, row/column headings, comments/errors, page order |
| ![Excel Page Setup Page](evidence-ribbon-dialogs/04-excel-dialog-page-setup-page.png) | Tab Page có orientation, paper size, scaling, print quality, first page number |

Page Layout có hai nút Page Setup dùng chung modal này và một nút Sheet Options mở trực tiếp tab Sheet. Excel còn expose Print, Print Preview, Options ở đáy modal; Nera chưa có đủ các flow tương ứng.

### 6.3 View, Insert, Data, Review

| Ảnh | Dialog/flow |
|---|---|
| ![Excel Zoom](evidence-ribbon-dialogs/14-excel-dialog-zoom.png) | Zoom modal: 200%, 100%, 75%, 50%, 25%, Fit selection, Custom |
| ![Excel Chart warning](evidence-ribbon-dialogs/15-excel-dialog-chart-no-data.png) | Chart launcher với vùng rỗng trả cảnh báo chọn vùng dữ liệu; đây là hành vi có kiểm soát, không crash |
| ![Excel Data Validation](evidence-ribbon-dialogs/16-excel-dialog-data-validation.png) | Settings/Input Message/Error Alert, Allow/Data/Ignore blank, Clear All |
| ![Excel Consolidate](evidence-ribbon-dialogs/17-excel-dialog-consolidate.png) | Function, Reference, All references, Add/Delete, labels, link to source data |
| ![Excel Advanced Filter](evidence-ribbon-dialogs/18-excel-dialog-advanced-filter.png) | Filter in place/copy, list/criteria/copy-to range, unique records |
| ![Excel Workbook Statistics](evidence-ribbon-dialogs/19-excel-dialog-statistics.png) | Current Sheet và Workbook: cells with data, tables, formulas |
| ![Excel Protect Sheet](evidence-ribbon-dialogs/20-excel-dialog-protect-sheet.png) | Password, locked/unlocked selection và danh sách quyền format/insert/delete/sort/filter/pivot |
| ![Excel Protect Workbook](evidence-ribbon-dialogs/21-excel-dialog-protect-workbook.png) | Password, Structure, Windows |

Data `Sort...` trên blank Book1 chủ động trả cảnh báo “This can't be applied to the selected range” vì chưa có vùng dữ liệu; không tạo dữ liệu giả chỉ để ép dialog. Review `Spelling`, `Thesaurus`, `Custom Views`, `Forecast Sheet` và các flow yêu cầu workbook có dữ liệu hoặc dịch vụ ngoài được liệt kê trong backlog, chưa được coi là parity đã nghiệm thu.

## 7. Bảng so sánh trực tiếp và chênh lệch

| Nhóm | Excel | NeraSpreadSheet | Kết luận |
|---|---|---|---|
| Format Cells | 6 tab, nhiều style/palette, edge preview, Protection | 5 tab, trường rút gọn, chưa có Protection; Border/Fill chưa sâu như Excel | Thiếu chức năng và visual fidelity |
| Page Setup | 4 tab, Header/Footer, Print/Preview/Options, print area/titles/page order | 3 tab; PrintOptions là alias tab Sheet; preview nội bộ | Chưa tương đương |
| Zoom | Preset radio, Fit selection, Custom %, modal nhỏ | Một ô nhập % và OK/Hủy | Hành vi khác |
| Data validation/filter | Validation, Advanced Filter, Sort, Consolidate, Forecast | Sort/Filter mức cơ bản, không có dialog tương đương | Khoảng cách lớn |
| Protect | Protect Sheet/Workbook với quyền chi tiết | Chưa có dialog tương đương trên Ribbon sample | Thiếu hoàn toàn |
| Formula/help | Function Library, auditing, calculation options và nhiều wizard | Formula Help + bốn lệnh chèn hàm + recalc/error check | Chỉ đáp ứng subset |
| Statistics | Current Sheet/Workbook thống kê chi tiết | Thống kê workbook đơn giản; có thêm info window | Có chức năng nhưng khác schema/UI |
| Print Preview | Backstage/Excel print service | Control preview riêng, zoom và 1/2 cột | Không thể gọi là bản sao 100% |
| Ribbon customization | Excel có Customize Ribbon/options hệ thống | Nera có session JSON và editor tab/group/command/QAT | Nera mạnh về extensibility nhưng khác UI/contract |
| Responsive Ribbon | Excel thu gọn group/galleries theo kích thước | Nera có nhóm/large-small và layout riêng | Cần visual regression theo kích thước cửa sổ |

## 8. Mapping source đã xác minh

- `samples/NeraSpreadSheet.Avalonia.Sample/FullShellWindow.Dialogs.cs:9-15` đăng ký sáu command dialog: Number, Font, Alignment, PageSetup, PrintOptions, Zoom.
- `samples/NeraSpreadSheet.Avalonia.Sample/FullShellWindow.Dialogs.cs:18-52` tạo `NeraFormatCellsDialog`, `NeraPageSetupDialog`, `NeraZoomDialog` và chỉ commit state sau khi dialog trả về.
- `src/NeraSpreadSheet.Avalonia/NeraSpreadsheetRibbonPreset.cs:101-111` gắn launcher cho các group `number`, `font`, `alignment`, `page-setup`, `print-options`, `zoom`.
- `samples/NeraSpreadSheet.Avalonia.Sample/FullShellWindow.Ribbon.cs:112-121` đăng ký PrintPreview, FormulaHelp, Errors, Statistics; các mục Errors/Statistics hiện là information window.
- `src/NeraSpreadSheet.Avalonia/NeraFormatCellsDialog.cs:12-48` xác nhận enum tab hiện có `Number, Font, Alignment, Border, Fill`; không có Protection.
- `src/NeraSpreadSheet.Avalonia/NeraRibbonCustomizationControl.cs:113-143` cung cấp rename/visible/large/move/add/remove và JSON import/export cho Ribbon/QAT.

## 9. Root cause và mức độ ưu tiên

Đây không phải một lỗi “không gọi được dialog” đơn lẻ. Nguyên nhân là phạm vi UI/contract hiện tại được thiết kế như một Ribbon subset:

1. Command catalog chỉ đăng ký sáu dialog launcher chung; nhiều Excel launcher chưa có command ID hoặc model/API dùng chung.
2. Format/Page Setup model chỉ biểu diễn subset các trường Excel; nếu chỉ làm UI sẽ tạo trạng thái không lưu được hoặc không tương thích OpenXML.
3. Các backend/rendering mới ưu tiên engine độc lập UI; vì vậy visual của Avalonia không tự nhiên trở thành pixel-identical với Excel desktop.
4. Một số Nera information window dùng `TextBox` read-only, phù hợp báo cáo nhưng khác modal/pane native của Excel.
5. Dialog của Data/Review/Protection kéo theo workbook semantics (validation rules, filter criteria, sheet/workbook protection) và cần contract + OpenXML round-trip, không nên làm stub để đạt ảnh giống.

**P0 — nền tảng parity:** mở rộng shared model/codec cho format, protection, validation, print settings; preserve unknown OpenXML; test round-trip.  
**P1 — dialog fidelity:** hoàn thiện Format Cells (Protection, border preview/style catalog, theme/indexed colors), Page Setup (Header/Footer, print area/titles, page order), Zoom preset/Fit selection.  
**P1 — Data/Review:** Data Validation, Sort/Advanced Filter, Consolidate, Protect Sheet/Workbook và Statistics schema.  
**P2 — visual/UX:** đo kích thước, typography, focus/default button, keyboard traversal, responsive collapse, high-DPI và visual regression so sánh ảnh.  
**P2 — parity mở rộng:** Chart wizard, Forecast, Spelling/Thesaurus, Custom Views và các tab Excel ngoài phạm vi core.

## 10. Tiêu chí nghiệm thu cho yêu cầu “giống 100%”

Chỉ đánh dấu hoàn thành khi mỗi dialog có đủ:

- command ID và launcher đúng group/tab, cả full window và cửa sổ thu nhỏ;
- model dùng chung + OpenXML round-trip, không mất trường khi lưu/nạp;
- OK/Cancel/Apply, default focus, keyboard navigation và validation giống Excel;
- visual regression ở các kích thước/high-DPI chính; ảnh trước/sau và sai số đã xem xét;
- runtime smoke trên bản demo Win11 và test API cho WPF/WinForms/MAUI/Avalonia theo phạm vi contract;
- không tạo control cho từng ô và không đưa UI dependency vào Core/Formula/OpenXml model trái kiến trúc.

## 11. Giới hạn của đợt kiểm tra

- Excel đối chứng dùng `Book1` rỗng cho các dialog chung; các warning do thiếu dữ liệu được ghi lại, không tự tạo nội dung vào workbook.
- Không đưa add-in tabs (Kutools, Foxit, ABBYY, MAPCITE, MyTools) vào tiêu chí SDK lõi.
- Chưa kiểm tra các flow phụ thuộc dịch vụ/cloud hoặc workbook có dữ liệu riêng như Forecast/Power Query/Comments threaded.
- Đây là báo cáo QA và bằng chứng, **không phải thay đổi source**. Không lưu workbook người dùng.

## 12. Danh mục bằng chứng

Tất cả ảnh mới nằm trong [`evidence-ribbon-dialogs/`](evidence-ribbon-dialogs/). Bằng chứng workbook thật/viền ô từ đợt QA trước nằm trong [`evidence/`](evidence/). SHA-256 của các ảnh được ghi ở phụ lục sau khi gói hoàn tất để có thể kiểm tra tính toàn vẹn.

### SHA-256 ảnh bằng chứng

| Tệp | SHA-256 |
|---|---|
| `01-excel-ribbon-home.png` | `5CF44D95470C6497C88B069B1178759C5EFB3E5F727F253691E7386BEA68B822` |
| `01-nera-ribbon-home.png` | `0EF28922AB3DBFED97B99833CCAA2112D7A88C17BB446E6664A4DA44C376B422` |
| `02-excel-ribbon-insert.png` | `2208AE233A9BACCFF5EFD34660D71F23915336397F27A89E7C53CA23A13C0894` |
| `02-nera-ribbon-insert.png` | `02DDB6FF76DB8BCC914958EEA616DCBC0E46BF171AED1CBECAF01437B1AD99E7` |
| `03-excel-ribbon-page-layout.png` | `3A9565EB21647EF900FCBB12640477878CE6F7B487DD00E31BF4AB701F51C284` |
| `03-nera-ribbon-page-layout.png` | `63EEE539A299BBAA3A0B93FAB031F8B16B3F431695CF773D31175F6566A849BD` |
| `04-excel-dialog-page-setup-page.png` | `F812D6577996012A4C67CC4F5B37E700E0DF0A1283E36BE09700EAD963108D49` |
| `04-excel-ribbon-data.png` | `2C9C1C11246298647BE4CC7F545B2BCEB971AF734B80F62381C167CD0D06FB72` |
| `04-nera-ribbon-formulas.png` | `70A7DF1FB27FEAD592F39FFA7800448DF17B54A39817A91B2B24EEDA4002EA13` |
| `05-excel-dialog-page-setup-margins.png` | `CB4F226547AADD8C0AF261E0221769E31BDCE7471BE3DD58DC68382348109580` |
| `05-excel-ribbon-review.png` | `3D6CF34342FFD0BFB25DD98427CC1641B1D33BF52F88558B26B2215E7C89AFA8` |
| `05-nera-ribbon-data.png` | `B4FD1A2AF1BBE3439C3FCBC98DF924C387B9465F0E765B7DF1C469DFBC9C45B2` |
| `06-excel-dialog-page-setup-header-footer.png` | `01BA55021DE5A137501302B10CC400BD4B14E6747DF163D4EF29CAFB7E0A0ADF` |
| `06-excel-ribbon-view.png` | `E9BC56B0AD32CD8AFCA27BC4516C56E8E5AD3E7115CC6F212C0FE72E9151CD7B` |
| `06-nera-ribbon-review.png` | `AC90F53DD58445624B1DF019117B22650D96E26FDB44DE83CBFE1D8630D6B412` |
| `07-excel-dialog-page-setup-sheet.png` | `2B917AF5E83422AE33E21B8BF1D0B9EE1237E83D422A6090A3DCA32C04D147F4` |
| `07-excel-ribbon-formulas.png` | `EAEC860600B88463FC4B5140985D0055148E855C2A08A5E7CB9F2B83C8133184` |
| `07-nera-ribbon-view.png` | `2FF68FA3B449C1C96A29EA18FADC9445A70CCAFE98AF459DFB0A46B64A4CFF9C` |
| `08-excel-dialog-format-cells-font.png` | `A1175FC3A46ABDE3653BDB61833CE572F51FFA1C49BF047A11AF7D732D83A45B` |
| `08-nera-dialog-zoom.png` | `5455E58259FD6323CB0438C1935552980755B514BBFA2073823B5082DCEE630D` |
| `09-excel-dialog-format-cells-alignment.png` | `CC725652D61587CE1171097B69D33EC34A5ABD1003DE4F98098A3666B66B1F3E` |
| `09-nera-dialog-page-setup-page.png` | `A9018D13AE16623E4587C9C1D5FA6A6533C5AE7C912AD77A78743694FCAC5ABA` |
| `10-excel-dialog-format-cells-number.png` | `07D70DDFCF41910EE7FAD7727014DEC3A2946C48D139181FFDEC846C047514AC` |
| `10-nera-dialog-page-setup-margins.png` | `D6F78B4F2A611F6BE3BCDE47B7EB033178F93ECE5EC0B4D3002028829334E77D` |
| `11-excel-dialog-format-cells-border.png` | `F59820B51319BCE9CF26132B3A9CC13A35EFAE700A691173ADEB76D84E5026D1` |
| `11-nera-dialog-page-setup-sheet.png` | `4C71C3EF0B2EA37254D3B85AD306EC59B3EF5E5EEACCC4B2EECD97DCA91CC033` |
| `12-excel-dialog-format-cells-fill.png` | `80141205F424FE6BB9A314EEDD4C84691524386660738D9955106F3F4536F1A0` |
| `12-nera-dialog-print-options.png` | `C831DA3AEE275F5BCA7EB0B12088C25E283BC3385B5FD91FE0D53798C4764E55` |
| `13-excel-dialog-format-cells-protection.png` | `7FCFD340782C83F640CA546104A5406907D5A1BEC23435BDBA66C6E19BA25E0D` |
| `13-nera-dialog-font.png` | `71ABA7D498288755EB1E0BC28042AD46B4036A1964E603ABFB04E521D94F03F0` |
| `14-excel-dialog-zoom.png` | `60ABD0D5D608BD0319950D9CF5B6B8AE843E7914D35C3DAD0C35A3A07B22BDA5` |
| `14-nera-dialog-alignment.png` | `6CF3E9D3941611232CC8015D8BF48343A0DCA28599F0666F0D54EC487A03B4F1` |
| `15-excel-dialog-chart-no-data.png` | `495FD804DE341D66CA25E435FB92FE26AE133A90411A6C149C6E56395831C582` |
| `15-nera-dialog-number.png` | `49404ABE7791D922AF7D98BFD1AB328E62FC45BB4C8C79BBD119835D9E517B51` |
| `16-excel-dialog-data-validation.png` | `A6490BF5B23A0B6F0FE56F03DE5146396399FF2315D2AD42D826AB3A9A6AB1D7` |
| `16-nera-number-categories.png` | `EC004A17D92063251651DF2C0C49C487BC2654967970DC2E4CC4DE82A82C91DC` |
| `17-excel-dialog-consolidate.png` | `20E3F4D67257320F1F2BEAE800C0F1CEEF106CD162C9865275CF613DBDC0902A` |
| `17-nera-dialog-ribbon-customization.png` | `626074D5F1CCBA6E854240D73FEDAB9BD5B5583876E472D45B97C972A8BDFD7D` |
| `18-excel-dialog-advanced-filter.png` | `10AD368E757F2825BAF02548F5990C3CCD6EC7DEEF3E7C3F11D874AD9BA09CC7` |
| `18-nera-backstage-statistics.png` | `C4F3765090084961DD3D31DDB00A5B01FD704D6F6C717821323BF1B46B97E839` |
| `19-excel-dialog-statistics.png` | `E465581E1837B07EF33FF086A2393557860AF2F3193B34C3B493D23FA3A62FD6` |
| `19-nera-backstage-print-preview.png` | `07693000EF9779CAFB19C699774661BFBFB2865BCDBB9246CA4EE0A4FF30F282` |
| `20-excel-dialog-protect-sheet.png` | `A1DA3C84ADEC3E793D14FD2BD40AFB666ABE44F238D12CA43EAE043BDA764C43` |
| `20-nera-dialog-formula-help.png` | `28B0A14756664D12993470B5290C3A49FEB2EC3B1A9CD2F92A95FF734B10221E` |
| `21-excel-dialog-protect-workbook.png` | `4F6A205773766F383233E59BDB5BAAE4F2AD80A01C853CC179016D2F77C2DB5C` |
| `21-nera-dialog-error-check.png` | `C0FB2F88C61E4EDF9D91C60A85C4BFC77D56D9A1335E82EFE7E2711D7B6515A0` |
| `22-nera-dialog-statistics.png` | `E73123475E2D4E47661B2D3B0F11A90003BADFC026679E90C6A53A0C261835B5` |

Không đưa bản sao workbook người dùng vào repository; thư mục `evidence/` chỉ chứa ảnh QA và metadata cần thiết để tái lập kết luận.
