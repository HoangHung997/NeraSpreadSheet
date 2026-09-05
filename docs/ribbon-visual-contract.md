# RIBBON-VISUAL-011 — Visual chrome và SDK preview

## Một model, ba presenter

`RibbonResponsiveLayoutEngine` cung cấp bounds, row/span và caption đáy cho
WPF, WinForms, MAUI. Các presenter dùng chung `RibbonItemDefinition` và
`RibbonRuntimeController`; không có workbook, history hoặc model Ribbon song
song. Hợp đồng chi tiết ở [responsive layout](ribbon-responsive-layout-contract.md)
và [full item model](ribbon-item-model-contract.md).

Button/toggle nhỏ dùng icon 16 px, command chính large dùng 32 px với caption
tối đa hai dòng. Các control giữ loại native và automation identity ổn định.
Chrome WPF dùng resource dictionary có phạm vi trong Ribbon/customization;
không ghi đè resource của application. Bốn palette gồm sáng, tối, tương phản
sáng và tương phản tối. Gallery nhận thumbnail bất biến từ host, giữ selected,
hover và thao tác More; Ribbon không dùng thanh cuộn ngang để xử lý thiếu chỗ.

QAT ưu tiên icon 16 px, giữ caption trong tooltip/automation và fallback nhìn
thấy khi không resolve được icon. File dùng rail bên trái và content pane bên
phải: click rail chọn nội dung, nút action trong pane thực thi command. Key tip
trực tiếp vẫn thực thi command theo contract cũ. Không có danh sách tài liệu
gần đây giả lập hoặc đường dẫn người dùng trong preview.

## Preview dùng capability thật

Chạy sample có sẵn:

```powershell
dotnet run --project samples/NeraSpreadSheet.Wpf.Sample -- --ribbon-preview
```

Sample có Trang đầu, Chèn, Bố trí trang, Công thức, Dữ liệu, Xem lại, Xem và
Thiết kế Bảng theo selection; Tệp là Backstage. Bốn mươi chín command đã đăng ký
trong session được tái sử dụng với cùng handlers/identities. Các command host
trong sample chỉ gọi API có sẵn: cell styles, print settings/preview, formula
editing/help, filter, zoom, table rename và totals. Save ghi vào file tạm cùng
thư mục và chỉ thay bản đích sau khi serialize thành công.

Open workbook tạo cùng `RibbonPreviewWindow` đầy đủ qua constructor nhận
`SpreadsheetSession` đã import: giữ workbook, active worksheet/selection/history,
Ribbon/Backstage, formula display và worksheet selector; không mở grid-only
window hoặc thay session bằng dữ liệu bán hàng synthetic. Tiêu đề lấy tên file,
theme kế thừa cửa sổ gọi. Parameterless preview/capture vẫn dùng fixture cũ.
Worksheet navigation dùng một hàng tab ngang ở đáy cửa sổ, cùng collection
Worksheet và identity/session hiện hữu. Native ListBox có horizontal virtualized
panel, overflow cuộn ngang thay vì xuống hàng; tab đang active được đưa vào vùng
nhìn thấy. Activation từ session cập nhật tab, chuyển tab dùng nguyên cancellation/
history semantics của session. Không tạo worksheet model hoặc transaction mới.
Đây mới là checkpoint full-shell loading và tab navigation: formula display còn
read-only; editable formula bar/demo packaging thuộc R2 tiếp theo, chưa gọi
sample này là bản demo hoàn thiện. Row này chưa thêm lệnh add/rename/delete sheet.

`RibbonProductionCommandCatalog` là manifest của 49 command session có
sẵn, không thêm khả năng chưa đăng ký. Factory mặc định giữ tab/command IDs
cũ và đánh dấu năm command chính là large. Sample minh họa cách host lắp thêm
command trong cùng public definition/runtime; không phải model hay presenter
mới của SDK.

Table Style gallery dùng `TableStylePreview.Create` và workbook theme thật.
Sau tích hợp TABLE-RIBBON-012, chọn tile dispatch `Table.Style` của TABLE-005,
áp dụng style thật và Undo/Redo được; không còn chỉ xem trước như checkpoint
VISUAL-011 ban đầu. Các lệnh tham số dùng cùng callback runtime/dispatcher,
không tạo handler mutation thứ hai. Xem
[Table/Ribbon integration contract](table-ribbon-integration-contract.md).
Không mô phỏng capability Excel chưa có trong SDK.

## Chụp ảnh và regression

```powershell
./scripts/capture-ribbon-visual.ps1
```

Script build sample, chạy đúng presenter SDK với workbook sinh trong bộ nhớ,
chụp 1920/1600/1280/1024 logical px cho tám tab và Backstage trong bốn palette,
thêm customization, popup gallery More và raster export 125/150/200% của
Trang đầu/Thiết kế Bảng.
Ma trận hiện tại gồm **177 ảnh** và **128 native layout snapshots**, gồm thêm
dialog validation của Table. Khi OS giới hạn native window, capture dùng
loaded logical surface theo contract tích hợp và ghi riêng native geometry;
không nhận đó là cửa sổ vật lý vượt kích thước monitor.
Kết quả ở `artifacts/ribbon-visual-011/captures`; `manifest.json` chỉ chứa tên
file tương đối, logical/native geometry và kết quả command smoke.

Raster export DPI **không** được coi là đổi DPI của màn hình thật. Manifest
ghi native scale riêng; Core regression kiểm tra invariant layout ở scale
1/1.25/1.5/2, WinForms/WPF loaded tests kiểm tra native bounds tại DPI host,
MAUI loaded smoke kiểm tra ma trận `LayoutScale` tương ứng. Regression kiểm
tra non-overlap, caption không lấn command, overflow, lựa chọn từ popup
gallery, command selection và focus ngoài Ribbon. Customization hỗ trợ cùng
command xuất hiện ở nhiều tab; caption lookup khử trùng lặp theo identity và
đóng dialog khởi tạo dở không truy cập session chưa gán. Pixel diff byte-for-byte
không phải gate vì raster/font antialiasing thay đổi theo OS; hình ảnh luôn cần
review cùng geometry tests. Preview mở customization theo palette đang chọn;
host override width của color picker phải dành đủ chỗ cho cả nhãn và swatch.

CI Windows chạy script và upload `ribbon-visual-matrix`. Ảnh tham chiếu Excel
và audit local không thuộc artifact hoặc Git. Chỉ ảnh sinh từ sample SDK được
upload. Không thay đổi đường scroll/render worksheet; benchmark đo riêng
Ribbon layout trước/sau được ghi trong responsive contract.
