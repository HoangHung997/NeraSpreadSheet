# TABLE-RIBBON-012 — Nối Table Design vào Ribbon

`SpreadsheetSession.Tables` tiếp tục là mutation boundary. Lane này nhập nguyên
baseline TABLE-005 rồi chỉ phát triển presentation, parameter collection và smoke.

## Runtime và SDK

Ribbon runtime cung cấp callback tùy chọn để host thu thập `CommandContext`
trước dispatch. Callback nhận stable command ID, selected value và context gốc;
kết quả null là hủy, không dispatch/history. Không gọi callback lúc project state.
Kiểm tra visibility/enabled trước và sau callback; cancellation token tiếp tục
được tôn trọng. Nút, key tips, shortcut, QAT và selectable item dùng cùng đường
runtime/dispatcher. Không thay registry hoặc tạo Table handler thứ hai.

Definition production giữ 19 Table command IDs. Host gắn thumbnail thông qua
gallery-preview callback hiện hữu; lựa chọn `Table.Style` đổi Table thật, checked
state và Undo/Redo lấy từ TABLE-005. Customization giữ identity và item semantics.
Binding ba host cập nhật context theo selection/sheet/metadata và bỏ callback đã
xếp hàng khi binding bị dispose.

## WPF preview

Preview dùng tab `table-design` production cùng layout ba hàng, bốn palette,
QAT và customization VISUAL-011. Dialog nhỏ cung cấp tên Create/Rename, range
Resize, calculated/custom totals formula, stable column IDs cho Remove duplicates,
và xác nhận Convert to range. Validation engine vẫn là nguồn quyết định; UI đưa
lỗi tiếng Việt tại boundary. Hủy không sửa workbook hoặc lịch sử.

Create dùng primary selection của TABLE-005 và giữ mặc định có hàng tiêu đề;
không thêm tham số hay API engine. SDK không bắt ứng dụng nhúng dùng sample dialog.

Primary activation của Style/Totals từ QAT hoặc key tips mở hộp chọn tham số
trong sample; chọn từ gallery/ComboBox truyền selected value trực tiếp. Parameter
chuẩn được giữ cả khi host dùng `RibbonItemActivation` cho primary action.
Read-only tiếp tục là policy trong `ICommandHandler.CanExecute`/command state,
không phải một workbook protection model mới. Đổi sheet dùng
`SpreadsheetSession.ActivateWorksheet`; đóng preview tháo các subscription.

Thumbnail sample dùng cache hữu hạn của `TableDesign.Snapshot.Styles`; đổi
snapshot catalog xóa cache chrome. Không gọi style engine lại khi dựng lại mỗi
tile hoặc khi cuộn worksheet. Dialog không có dependency/resource toàn ứng dụng.

Chạy `scripts/capture-ribbon-visual.ps1 -TableDesignOnly` để tái hiện focused
matrix nhỏ; bỏ switch để chạy toàn bộ tám tab, File và customization. Cả hai
đều kiểm tra dialog/mutation trước ảnh, gồm native gallery/totals ComboBox,
cancel/validation, typed parameters, stable duplicate keys và Undo.

## Kiểm chứng

Gates gồm catalog exact audit, cancellation/dispatch/context/dispose regressions,
loaded host smokes và capture với dữ liệu synthetic tạo bởi Nera. Width chính:
1024/1280/1600/1920 logical px; layout scale 1/1.25/1.5/2. Raster export được ghi
riêng với native DPI, không nhận là physical monitor DPI test. Actual style/undo
và totals/undo phải được xác minh trước screenshot. Kết quả và giới hạn cuối ở
`worklog/TABLE-RIBBON-012.md`.

Windows có thể chặn kích thước native window theo monitor (local 1553,6 DIP).
Capture vẫn arrange cây WPF đã load tại đúng 1600/1920 logical px và chỉ bỏ
layout clip trên root sample trong chế độ capture. VisualBrush dùng viewbox
tọa độ tuyệt đối để không kéo giãn nguồn bị clip. Manifest ghi riêng window,
root, Ribbon width và native DPI; assertions khóa root/Ribbon/layout width
bằng width yêu cầu. Đây là chụp logical surface đã load, không nhận là resize
cửa sổ vật lý vượt giới hạn monitor.

## Blocker core đang bàn giao cho TABLE-006

Coordinator xác nhận baseline TABLE-005 có lỗi Convert to range đối với
structured references: một số công thức thành `#REF!` sau khi bỏ Table metadata.
Lane này không sửa hoặc nhập fix core đang chạy. Smoke Convert hiện chỉ chứng
minh dispatch/metadata/history/Undo; không phải bằng chứng giá trị công thức sau
convert đúng. Nghiệm thu kết hợp phải nhận fix TABLE-006 rồi chạy lại regression
và native command path tại HEAD tích hợp.
