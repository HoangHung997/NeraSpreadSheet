# RELEASE-009 — Paged AutoFilter trong WPF split host

## Một presenter, đúng native surface

WPF sample tạo một `NeraAutoFilterPagedPopupPresenter` từ đầu. Header pointer,
Alt+Down và Sample.Filter dùng cùng instance cho Table và worksheet AutoFilter.
Presenter/binding/view/session phân trang hiện có tiếp tục sở hữu dữ liệu,
search, selection và Apply/Clear/Sort; không thêm workbook/filter/editor model.

Host adapter lấy split controller bằng registry hiện có, nối preview input với
actual split adorner. Chỉ thêm internal surface/frame/pane-activation hooks.
Frame do renderer vừa trình bày cung cấp layout từng pane, cùng owner/header
identity và pane ID. Geometry hiện có được dịch qua pane origin/chrome offset,
clip trong body không gồm scrollbars, rồi dùng chung cho draw/hit/popup anchor.
Không dùng offset của control nền hoặc chọn header đầu tiên ở pane khác.

Raw hit testing đọc snapshot geometry đã trình bày, không Compose/RenderNow,
WorksheetSnapshot, scan source values hoặc recalculation. Header overlay chỉ vẽ
các nút visible, không tạo control cho từng ô/giá trị nguồn. Refresh được gộp ở
dispatcher Render; giữ frame scheduler và nested display-list semantics hiện có.

## Input và lifecycle

- Khi native draft active, filter open bị từ chối: State/text/range/focus/
  selection/history giữ nguyên; không implicit commit/cancel.
- Hit filter tiêu thụ preview event trước ordinary cell selection; native
  separator, scrollbar và header resize vẫn giữ precedence của split surface.
- Keyboard/command chọn header ở active pane; pointer chọn pane thực sự chứa
  nút. Mở/đóng popup không thêm history hoặc dịch selection/scroll để giả anchor.
- Popup giữ open-context identity gồm generation, session, worksheet, surface,
  pane và header. Sheet/surface change, unload và dispose đóng/cancel context.
  Session replacement được kiểm trước callback/mutation và khi host refresh.
- Callback search/page/close/focus cũ không được dọn binding hay giành focus của
  popup mới. Close bình thường khôi phục focus hợp lệ; fallback là actual native
  surface. Context cũ sau host change không tự khôi phục focus.
- Scroll/resize giữ cùng header visible thì relocate popup; header rời viewport
  hoặc bị ẩn thì đóng. Offset vẫn double/fractional, không snap về biên ô.

## Dữ liệu và history

Default page vẫn 100 values, hard maximum 1.000/request, retained catalog cap và
truncated-subset rejection giữ theo native-paged/FILTER-006 contracts. Chỉ open,
search hoặc explicit mutation gọi existing shared data operations. Không đưa
catalog/counting work vào paint, pointer hoặc scroll refresh.

Apply/Clear/Sort tiếp tục dùng Session.Tables/WorksheetFilter/Sort đúng một lần;
compressed visibility, dependent formulas và Undo/Redo do canonical controllers
đảm nhiệm. Split host tái dùng subscription/invalidation hiện có sau mutation.

## Gates và giới hạn

Loaded full-shell regression chạy hai owners trong standalone/bốn split panes:
first native header click, Alt+Down SystemKey, Ribbon command, 250 values/page100,
Apply/Clear/Undo/Redo, fractional offsets, hidden axes, clipping, zoom/resize,
negative chrome hits, draft refusal và stale close/search/sheet/session/dispose.
Tests phải xác minh dispatcher idle và raw hit không compose frame.

Capture gồm 14 full-shell scenarios với actual Popup images: 1280 cho từng owner
và standalone/bốn panes, 640 split và resize/hidden-axis hoặc sheet recreation.
Manifest ghi pane/header/owner IDs/offsets/clip/anchor/page/history; phải xem ảnh
thật. Existing tests/captures/caps không bị bỏ hoặc giảm assertions.

Source build/analyzers, native regressions, architecture/resource parity và năm
exact-source workflows là gates. Root chạy sáu combined gates riêng. Không coi
source tests/ảnh là P3 performance, physical hardware/DPI/screen-reader hoặc
whole MAUI acceptance. Không package mới, migration hoặc thay public model API.
