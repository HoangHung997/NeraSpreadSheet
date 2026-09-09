# EXCEL-LOCALE-VIEW — yêu cầu tính đúng dùng chung

Từ chỉ đạo người dùng 09/09/2026. Tài liệu requirements/handoff, **chưa là bằng
chứng implementation hoặc test PASS**. Không sửa H2 hay workbook gốc.

## Hiện trạng ownership đã đọc

Root `2e80250b1b1c51ad56a415d1bbeb70d7b2e85f25` vẫn là baseline. PR #5 tại
`197d666022a8c05991f70202c7e9defc0c36e513` đã có C1/C2/V1/V2 source, chưa có
compiler/native/CI acceptance theo receipt của lane. Session/clipboard/view
serializer đang thuộc lane đó. Không triển khai bản thứ hai trên root cũ và
không ghi PR #5/plan/progress từ task Ribbon. Coordinator quyết định integration
và cấp H1 host paths sau khi đối chiếu các lane.

## Các nguyên tắc dữ liệu

1. XLSX numeric `<v>` dùng biểu diễn canonical/invariant, không có group separator.
   Không đổi parser XLSX sang CurrentCulture và không thay dấu chấm/phẩy toàn file.
2. Giá trị số, mã format và văn bản hiển thị là ba thứ khác nhau. Text trông giống
   số vẫn là text trừ khi người dùng/nguồn import cho phép chuyển kiểu.
3. UI nhập, formula bar, numeric formatting và external TSV phải dùng policy rõ
   ràng của app/session. System separators và app override không tự nằm trong
   workbook. Không đổi CultureInfo global để fix một control.
4. Initial editor text phải dùng cùng policy với commit. Mở F2 rồi Enter không
   thay đổi nội dung phải giữ value/type/formula/style, kể cả text "00123" hoặc
   chuỗi giống ngày. Đổi culture trong một draft không được reinterpret silently.
5. Không fallback CurrentCulture→Invariant với AllowThousands rồi lấy kết quả đầu
   cho dữ liệu không có provenance; "1,234" mơ hồ. Structured clipboard giữ type;
   external text dùng format/policy công bố, không đoán bằng số chữ số.
6. Number-format locale/currency directives, literals/escaping và date system phải
   được xét riêng. Retaining XML không đồng nghĩa rendering semantics đúng.

## Initial worksheet/window state

Phải đọc và ánh xạ `bookViews/workbookView@activeTab`, chosen `workbookViewId`,
`sheetView@zoomScale/topLeftCell`, selection activeCell/sqref, pane activePane,
frozen/frozenSplit/split. Không tự chọn last SheetView hoặc luôn active A1.
Tôn trọng hidden/veryHidden sheet, sheet IDs/relationships và nhiều window views;
malformed/duplicate references phải có policy/diagnostic, không xóa sibling views.

Khôi phục view cần transaction/feedback guard: việc control thay đổi Bounds hoặc
phát ScrollChanged lúc restore không được ghi default/zero vào cached state.
Selection/scroll/zoom theo từng sheet và từng session/window. Pixel offsets của
Nera vẫn double; standard topLeftCell là interoperability approximation, không
thay precision state. Lưu rồi mở lại giữ unknown SheetViews thuộc window khác;
không remove toàn bộ collection chỉ vì current view không có split.

## Test matrix bắt buộc

Culture: en-US, vi-VN, de-DE và cloned NumberFormatInfo với dấu tùy chỉnh. Kiểm
0, âm, exponent, số nhỏ/lớn, 1.234 và1,234, group không hợp lệ, percent/currency,
text numeric, dates/1900/1904, display→F2→no-op commit và full save/reload.
XLSX .xlsx/.dlda qua cùng Stream phải cùng typed values. Không claim Excel oracle
nếu chưa thực sự đối chiếu Excel. Đổi locale chỉ đổi display, không sửa numeric.

View: ba sheets có selection/zoom/offset khác nhau; activeTab không phải0;
A→B→A→B, freeze/split, nhiều window IDs thứ tự khác nhau, thiếu chosen view,
hidden sheet, sibling unknown nodes/extensions, save/reload nhiều lần. H1 kiểm
loaded Avalonia restore không bị callbacks ghi đè. Không suy source tests đã
viết là chạy, không lấy xanh của baseline thay combined exact HEAD.

## Bước tiếp theo và giới hạn

Coordinator review PR #5 mới nhất và cấp rõ owner cho culture/input + host H1;
liệt kê API final trước khi Ribbon task nối UI. Bản Ribbon hiện không sửa shared
Editor/Clipboard/Session/View/Core/OpenXML. Case DecimalSeparator của Excel là
application setting: https://learn.microsoft.com/en-us/office/vba/api/excel.application.usesystemseparators .
Reference SheetView: https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.spreadsheet.sheetview .
