# Kiến trúc NeraSpreadSheet

## 1. Mục tiêu và lựa chọn UI hiện hành

NeraSpreadSheet là SDK bảng tính có engine độc lập, không phải một app hoặc một
DLL UI duy nhất. Từ quyết định sản phẩm 09/09/2026, **Avalonia là SDK UI chính
cho mọi ứng dụng mới trong hệ sinh thái**; các app chia sẻ UI thay vì viết riêng
một app WPF và một app MAUI. WPF/WinForms/MAUI được giữ để bảo trì/tích hợp ứng
dụng cũ, tạm dừng tính năng UI mới theo mặc định. Xem
[quyết định và quy tắc chuyển tiếp](docs/avalonia-first-ui-strategy.md).

Một UI framework không thay thế kiểm thử từng OS. Không tự xóa host/package/API
hoặc CI, không nhúng WPF vào Avalonia, không kéo UI dependencies vào engine.
Các platform adapters không reference lẫn nhau.

## 2. Các tầng

### Foundation

Primitive không phụ thuộc UI: điểm, kích thước, hình chữ nhật và màu.

### Core

Workbook sparse, worksheet, địa chỉ ô/range, giá trị, styles và metadata hàng/cột.
Core không biết Avalonia/WPF/WinForms/MAUI hay OpenXML. Có thể dùng không UI.

### Formulas và Editing

Parser/evaluator, dependency graph, session, selection, transactions/history và
command là logic dùng chung. Locale nhập/hiển thị phải nhất quán nhưng không
đổi biểu diễn numeric canonical trong XLSX. Không dựng model parallel trong host.

### Layout và Scrolling

Chuyển metric/offset thành hàng/cột visible + overscan. SparseAxisMetricIndex
tránh cấp phát theo toàn bộ một triệu hàng. Offset là double; input liên tục
đi qua frame scheduler, không snap theo hàng/cột hoặc full render từng raw event.

### Rendering

DisplayList trung lập UI được thực thi bởi backend. Avalonia dùng DrawingContext
adapter; Direct2D/Skia tiếp tục phục vụ các consumer hiện hữu. Không suy rằng
thay UI cho phép xóa backend hay trộn managed/native Skia khác phiên bản.
Renderer không sở hữu workbook và không tính công thức khi scroll.

### Platform host

Avalonia là host chính: input, native editor tái sử dụng, clipboard, automation,
focus và lifecycle. Các adapter WPF/WinForms/MAUI còn lại có cùng ranh giới nhưng
không còn là ba UI cần phát triển song song cho mọi tính năng mới.
Ribbon.Core/Commands và responsive geometry dùng chung; theme/template thuộc
SDK UI. Không bắt mỗi app tự override global theme để làm Ribbon hiển thị đúng.

### OpenXML

Chuyển đổi package/stream và metadata sang/từ model chung, không thành model
nội bộ khác. Đọc được, evaluate/render đúng và bảo toàn khi lưu là các capability
riêng. View state phải có worksheet/window identity, không chỉ parse XML rồi bỏ.

## 3. Luồng một frame

```text
Input → frame-coalesced scroll → viewport snapshot
     → layout visible + overscan → immutable DisplayList → renderer
```

Backend/host không gọi recalculate, AutoFit hoặc paginate vì một thay đổi scroll.
Nested display lists giữ reference semantics, không flatten-copy arrays.

## 4. Freeze và split

Freeze tối đa bốn vùng clip/transform: góc cố định, hàng cố định, cột cố định,
vùng cuộn chính. Split là các viewport độc lập trên cùng workbook/session,
không copy workbook. Precision offsets không được rút gọn thành chỉ số ô đầu.
Thay đổi selection/freeze/split/dimensions và Undo theo transaction chung.

## 5. Snapshot, cache và bất đồng bộ

Render đọc snapshot ổn định; cache phải chứa tất cả khóa ảnh hưởng output và có
invalidation. Import/save/formula/layout nặng không chạy lại do raw input.
Bất đồng bộ clipboard/view restore phải kiểm identity/version/source tag và
không cho completion cũ sửa sheet mới hoặc ghi đè metadata đang restore.

## 6. Spreadsheet không phải DataGrid

Spreadsheet dùng địa chỉ ô/công thức, DataGrid dùng record/schema. Không thay
spreadsheet bằng DataGrid để giả đủ UI và không tạo visual/editor cho mỗi ô.

## 7. Trạng thái và ownership

Đây là tài liệu kiến trúc, không phải progress tracker. Các ghi chú bootstrap
M0/M2 cũ không phản ánh completeness hiện tại; xem CURRENT và own task worklog
ở đúng SHA. Root Codex sở hữu shared status; mọi task khác giữ branch/phạm vi
được cấp. Rà soát source/CI exact head trước integration; không lấy parent green,
ảnh mẫu hoặc số command làm acceptance toàn SDK.
