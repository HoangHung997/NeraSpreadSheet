# Worksheet view state — V1/V2 dùng chung

Batch `BATCH-20260909-006`, queue `CHATGPT-CLIPBOARD-VIEW-20260908`.
Source/test đã được chuẩn bị để kiểm thử chung sau C1/C2/V1/V2; chưa được nhận
là build, CI, native host hoặc integration PASS.

## V1 — Ownership và khôi phục

SpreadsheetViewController giữ state bằng **Worksheet instance trong từng
SpreadsheetSession**, không dùng tên sheet hoặc state global. Snapshot chứa
active cell, anchor, ranges có thứ tự, zoom, freeze và split state. Ranges được
copy thành collection chỉ đọc; version selection không phải identity của ô.

Scroll vẫn nằm trong SpreadsheetSplitViewState hiện có: TopLeftScroll cũng là
standalone scroll khi mode None. Không có FirstVisibleRow/Column, không snap
fractional offsets hoặc tạo scrolling engine mới. Native metadata giữ các offset
ẩn của những pane chưa hiện. Zoom model nhận 10%–400% để giữ miền chuẩn XLSX;
những host hiện giới hạn 25% phải được nối/chốt policy riêng trong H1.

ActivateWorksheet chụp sheet cũ, lấy state sheet mới, hủy editor cũ và restore
selection trước khi phát các thông báo view/worksheet. IsRestoringWorksheetView
bao phủ cả chuỗi thông báo; setters view bỏ qua feedback write trong pha này.
Không recalculate, ghi cell hay thêm Undo chỉ vì đổi sheet/scroll/zoom.

SetWorksheetState/SetWorksheetViewport có source tag; dữ liệu không hữu hạn,
zoom ngoài miền hoặc freeze không hợp lệ bị từ chối. ClampWorksheetViewport
nhận bounds thực từ host, chỉ clamp sheet chỉ định và giữ phần thập phân.

Rename giữ identity. Removed worksheets được prune khỏi cache; sheet mới cùng
tên không thừa hưởng state cũ. Active/anchor mới nằm trong hidden interval được
chuyển đến ô hiện được lân cận; merge được resolve về anchor. Selection nhiều
vùng còn lại được giữ, không materialize axes. Hai sessions cùng workbook không
ghi đè selection/zoom/scroll của nhau.

Split/freeze và mapping của structural/reorder transactions hiện có tiếp tục
là nguồn dữ liệu có thẩm quyền. Batch không sửa transaction controllers hoặc
suy ra phép reorder chỉ từ một CellsChanged event. Exact identity remapping
cho cached selection ở một session khác khi external structural edits xảy ra
vẫn cần event/transaction grant và nghiệm thu bổ sung; không claim đã xử lý
mọi biến đổi xuyên cửa sổ từ cơ chế khôi phục cơ bản này.

## V2 — Standard XML và độ chính xác native

Workbook serializer đã giữ envelope XLSX khi PreserveUnknownParts=true. Session
serializer tái sử dụng envelope đó, không tạo workbook/XML preservation store
thứ hai. SheetViews không còn bị xóa cả collection khi lưu.

Chọn WorkbookViewId một lần theo view cuối của sheet đầu tiên có view, rồi
đọc/patch đúng ID đó trên tất cả sheet. Giữ sibling views và attributes/extension
children không do state mới quản lý. Pane/selection được chèn qua AddChild theo schema order thay vì prepend
trước sheetPr/dimension. View ID không được tự reset về 0 khi view đã tồn tại;
reference đến workbook view không tồn tại bị từ chối. WorkbookView tương ứng
được cập nhật ActiveTab; các workbook views khác được giữ.

Standard metadata lưu selection/sqref/activeCellId, zoom percent, topLeftCell,
pane split và freeze tương thích. Sibling pane selections được giữ; chỉ chỉnh
selection của pane hoạt động. Sqref hỗ trợ một/nhiều A1 ranges và whole-axis
A:C / 1:3 mà không materialize cells. Metadata đầu vào không hợp lệ không được
đổi nghĩa lặng lẽ để tạo file có vẻ hợp lệ.

Version 1 native worksheetViews giữ nguyên pane fields cũ, thêm fields tùy chọn:
zoom chính xác, frozenRows/frozenColumns, selection active/anchor/ranges và
activeWorksheet. Native dùng tên trong file để bind vào worksheet sau import;
identity runtime vẫn là object, không phải tên. Old native pane-only files lấy
selection/zoom/freeze từ standard metadata thay vì reset về default.

Standard freeze dùng count hàng/cột và top-left cell của phần scrollable;
conversion cộng/trừ sparse frozen extent. Split+freeze đồng thời và fractional
pixel offsets/zoom được giữ chính xác trong native supplement. Metadata native
không thay thế chuẩn SpreadsheetML hoặc sibling views đã được giữ.

Giới hạn defensive: 1024 selection ranges, 65536 ký tự sqref, 16 Mi ký tự native
XML; DTD bị cấm. Duplicate native worksheet entries, version chưa hỗ trợ hoặc
reference sai bị từ chối. Không thay serializer recovery/analytics/pivot/chart
pipeline. Topology restrictions của existing envelope vẫn có hiệu lực.

Workbook mới ở view mặc định không phát SheetViews/native view part thừa; giữ
nguyên regression DefaultSessionDoesNotEmitNativeOrStandardSplitMetadata.
PreserveUnknownParts=false vẫn là lựa chọn bỏ envelope/unknown parts của caller;
không nhận preservation đầy đủ cho sibling metadata đã chủ động bỏ ở lớp đó.

## Gates và giới hạn còn pending

Thêm tests cho A→B→A→B, directed/multiple selection, zoom/offset khác nhau,
independent sessions, restore feedback, rename/remove, hidden/merge/clamp và
split/freeze. Thêm OpenXML tests cho load/save/load exact native, unsplit multi
view/unknown metadata, update/clear pane có giới hạn, standard freeze, old native
compatibility và OpenXmlValidator/schema order. Test code không là test PASS.

H1 chưa nối các host vào API mới; source WPF/WinForms/MAUI/Avalonia, input/raw
render/timer, native transport và workflows không đổi. Không dùng shared state
hoặc round-trip test thay nghiệm thu native, performance hoặc hardware.

Chỉ chạy CI sau HEAD kết hợp cuối theo chỉ đạo người dùng ngày 09/09/2026;
không giảm assertion hoặc nhận CI của commit cha. Gate cần .NET build/analyzers,
Editing/OpenXML/Core suite, architecture và exact-head workflows. Worker không
có runtime .NET khả dụng tại lúc viết; kết quả execution vẫn pending.

Rollback: revert batch commit, không migration workbook. Reader cũ hiểu pane
fields version 1 nhưng không sử dụng các optional fields mới. Revert cũng bỏ
bảo vệ SheetViews mới, không được gọi là trạng thái đã nghiệm thu.

## Rà soát batch007 — cập nhật viewport và binding cửa sổ

SetWorksheetViewport chỉ cập nhật zoom/TopLeftScroll, không gọi đường restore
selection hoặc normalize hidden/merged cell. Cuộn không được tự dời active cell,
phát lại freeze không đổi hoặc tạo history. Zoom-only phát WorksheetViewChanged
và tăng View.Version; đổi offset phát PaneScroll, giữ source tag và feedback guard.
Đặt đúng trạng thái hiện hành không phát event. Full SetWorksheetState vẫn
normalize, nhưng so sánh trạng thái đã normalize trước khi ghi cache.

View snapshot từ chối active cell nằm ngoài tất cả selected ranges trước khi
nhận vào cache; anchor vẫn được bảo toàn riêng. Nếu callback trong activation
ném lỗi, giải phóng feedback guard và giữ nguyên lỗi; không phát completion giả
trong finally. Đây không phải rollback mọi side effect do callback tùy ý gây ra.

Session serializer chọn WorkbookViewId ban đầu một lần, từ view cuối của sheet
đầu tiên có view, rồi đọc/patch đúng ID đó trên tất cả worksheet. Thứ tự XML
SheetView khác nhau trên các sheet không được làm trộn các cửa sổ. Sheet thiếu
view cùng ID nhận state mặc định; khi cần ghi state thì thêm đúng ID, không sửa
view thuộc cửa sổ khác. Kiểm ID có workbook view tương ứng. Mappings đếm
WorksheetPart thực, giữ chỉ số Sheet gốc để map ActiveTab; không dùng chỉ số grid
worksheet để vô tình chọn nhầm non-grid sheet. Điều này không mở rộng workbook
serializer thành hỗ trợ đầy đủ chart-sheet/topology preservation.

Thêm 8 tests SpreadsheetWorksheetViewStateRegressionTests và 8 tests
WorksheetViewStateWindowBindingTests: viewport không đổi selection/freeze,
source tag/guard/no-op/error paths; thứ tự view đảo giữa hai sheet, view thiếu,
ID không tồn tại, duplicate SheetViews và hai vòng lưu/nạp. Các assertion schema
và toàn bộ tests chưa được chạy. Không giảm assertion/test cũ.

Cross-session inactive structural identity remapping và H1 vẫn cần typed
transaction/host grant. Không giả lập structural signal bằng CellsChanged hoặc
lấy những sửa lỗi binding này để đóng các phần đó. Chưa có build, benchmark,
native smoke hoặc exact-head CI cho batch007.
