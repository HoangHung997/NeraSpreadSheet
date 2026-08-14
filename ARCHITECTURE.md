# Kiến trúc NeraSpreadSheet

## 1. Mục tiêu

NeraSpreadSheet là một hệ sinh thái control, không phải một DLL giao diện duy nhất. Logic workbook và layout dùng chung; mỗi nền tảng có host và backend render riêng.

## 2. Các tầng

### Foundation

Chứa primitive không phụ thuộc UI: điểm, kích thước, hình chữ nhật và màu.

### Core

Chứa workbook dạng sparse, worksheet, địa chỉ ô, range, giá trị và metadata hàng/cột. Core không biết người dùng đang dùng WPF, WinForms hay MAUI.

### Formula

Chứa parser/evaluator, dependency graph và registry hàm. Bootstrap hiện chỉ khóa contract; implementation được bổ sung theo từng nhóm hàm.

### Layout

Chuyển workbook metric và viewport offset thành danh sách hàng/cột nhìn thấy. `SparseAxisMetricIndex` lưu delta kích thước bằng Fenwick tree sparse, tránh cấp phát mảng hơn một triệu phần tử cho mỗi sheet.

### Scrolling

Nhận delta từ chuột, touchpad, touch và lệnh chương trình. Offset là `double`, được cập nhật theo frame và không bị snap về hàng/cột.

### Rendering

Layout tạo display list trung lập nền tảng. Backend Direct2D hoặc Skia thực thi display list. Backend không truy cập trực tiếp workbook.

### Platform host

WPF, WinForms và MAUI chuyển input nền tảng thành command/scroll delta, quản lý editor overlay, accessibility, clipboard và lifecycle.

## 3. Luồng một frame

```text
Input events
   ↓ cộng dồn
ContinuousScrollController
   ↓ snapshot offset mới nhất
ViewportLayoutEngine
   ↓ visible rows/columns
DisplayListBuilder
   ↓ immutable display list
IRenderBackend
   ↓
Direct2D/DirectWrite hoặc Skia GPU
```

## 4. Freeze pane

Freeze pane được mô hình hóa thành tối đa bốn viewport có clip và transform riêng:

```text
┌──────────────┬───────────────────────┐
│ góc cố định  │ hàng cố định          │
├──────────────┼───────────────────────┤
│ cột cố định  │ vùng cuộn chính       │
└──────────────┴───────────────────────┘
```

Không dùng một viewport khổng lồ rồi sửa tọa độ thủ công cho từng trường hợp.

## 5. Snapshot và bất đồng bộ

Renderer chỉ đọc snapshot ổn định. Formula recalculation, import, save, AutoFit và page layout chạy ngoài đường cuộn; khi hoàn thành chúng phát invalidation cho vùng bị ảnh hưởng.

## 6. Spreadsheet không phải DataGrid

Spreadsheet dùng địa chỉ ô và công thức. DataGrid dùng record/schema. Hai control có thể dùng chung input, editor, theme, command và rendering primitives nhưng không dùng chung data model.

## 7. Ranh giới chưa triển khai ở M0

- Direct2D device, swap chain và DirectWrite text cache.
- Skia GPU surface/handler.
- Formula parser/evaluator.
- XLSX round-trip preservation layer.
- Tile cache, dirty-region compositor và editor overlay.

Các project tương ứng chỉ khóa contract và dependency; không được coi là backend hoàn chỉnh.
