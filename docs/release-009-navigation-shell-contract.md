# RELEASE-009 — Điều hướng worksheet trong Ribbon sample

## Phạm vi

RibbonPreviewWindow dùng cùng SpreadsheetSession đã tải từ XLSX. Hai thanh cuộn
WPF nằm quanh worksheet độc lập; hàng tab sheet giữ ListBox ngang hiện có.
Thanh cuộn là presentation của NeraSpreadsheetControl, không sở hữu engine,
workbook, selection hoặc lịch sử mới. Không thay SDK, package hay serializer.

## Worksheet độc lập

- ContentWidth/ContentHeight, ScrollSnapshot và SpreadsheetChromeGeometry là
  nguồn extent, offset và kích thước body; đơn vị logical không nhân zoom lần hai.
- Offset giữ double. Native thumb, nút/page và automation ValueChanged gom giá
  trị mới nhất cho mỗi trục qua CompositionTarget.Rendering; tối đa một ScrollTo
  cho một burst trong frame. Wheel/precision vẫn qua scheduler của control.
- ScrollChanged, resize, zoom, selection, view, cells và dimensions lên lịch
  refresh coalesced sau SDK/render. Không polling layout hoặc quét sparse cells
  trong shell. Feedback guard ngăn cập nhật thanh cuộn phát input ngược lại.
- Giữ adaptive navigation extent hiện có: trailing workspace, active cell và
  current viewport floor; kéo đến cuối không tự nối thêm tail. Thumb không đổi
  selection, cell, calculation hoặc workbook history.
- Khi đổi sheet/selection, unload hoặc dispose, hủy vị trí thumb đang chờ và tháo frame
  handler. Subscription cells/dimensions chỉ theo worksheet đang hoạt động.
- Automation IDs là preview-worksheet-scroll-horizontal và
  preview-worksheet-scroll-vertical; nhãn Việt/Anh theo localization riêng từng
  runtime, cập nhật khi đổi ngôn ngữ mà không ảnh hưởng cửa sổ khác.
- Header/theme commands làm mới body metrics và renderer đang hoạt động qua
  shell snapshot; no-op refresh không gọi ScrollTo hoặc đổi workbook/view state.

## Split đã lưu

Shell gắn EnableSplitPanes với chính xác mode đã lưu, sau khi control Loaded.
SDK đồng bộ SpreadsheetSplitViewState trước SetMode; thao tác cùng trạng thái
không publish view change hoặc thêm undo/redo. Không ép Both, đặt lại split X/Y,
replay offsets hoặc ghi far cell để tạo extent.

Chỉ một input topology hoạt động: split adorner cùng integrated pane scrollbars
khi có split, hai native standalone bars khi None. Standalone control không nhận
hit-test/focus khi adorner hoạt động. Chuyển về None tháo adorner, trả input về
control và nạp TopLeft offset đã lưu qua ScrollTo, không ghi lại model. Controller
split hiện có theo dõi sheet/topology/state; hidden pane offsets giữ nguyên.
Split dùng full physical extent theo split contract, chưa hỗ trợ adaptive extent.

## Kiểm chứng và giới hạn

Windows native tests phải kiểm native thumb/track, frame coalescing/fractional
offset, extent-only changes, freeze/hidden axes, resize/zoom, worksheet switch và
dispose. Loaded XLSX smoke kiểm same session, split X/Y, active/hidden panes,
history/selection/cells, scrollbar chỉ đổi đúng pane, split/non-split tab roundtrip,
clear/restore và save/reload. Capture matrix thêm 8 ảnh actual loaded shell: split
và standalone ở 4 Ribbon themes. Đây là logical offscreen capture, không phải
physical DPI/touch/screen-reader proof hoặc latency benchmark.

Thanh công thức hiện vẫn read-only. Formula draft/commit/cancel và filter command
routing trong split chưa được nghiệm thu bằng slice này; phụ thuộc public editor
bridge và lifecycle release của B, cùng command audit của root. Không dựng editor
hoặc filter host thay thế trong sample. Native per-pane accessibility vẫn theo
giới hạn split contract. Shared status/CI do coordinator sở hữu.

Rollback: revert các commit RELEASE-009 navigation trên nhánh tích hợp, phục hồi
body AdornerDecorator trước đó. Không cần chuyển đổi dữ liệu workbook hoặc history.
