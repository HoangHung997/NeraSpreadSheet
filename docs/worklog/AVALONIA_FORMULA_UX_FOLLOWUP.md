# Formula UX Avalonia — triển khai bổ sung sau audit

Base `b2bd9b3ef8ab2b1cd70134144a4052c1fc1a5a88`, PR #4,
`feature/avalonia-001-host`. Chỉ mục 1 của người dùng. Mục 2/3 giữ trong
Codex deferred queue tại PR #1 comment 5579073325. Không sửa clipboard,
shared selection/view state/serializer, root CURRENT hoặc source host cũ.

## Phạm vi đã triển khai trong source candidate

1. Kéo reference đối xứng giữa mọi split pane, kể cả bắt đầu trong editor pane.
   Chuyển capture trước khi tạo point anchor; TextBox/caret/selection không bị
   biến thành reference pointer. Khi capture lost/cancel không resurrect draft.
2. Raw move được gộp, frame 60Hz cập nhật ô cuối và edge-autoscroll qua shared
   ContinuousScrollController. Offset vẫn double; DIP/zoom đúng, frozen band
   không cuộn trục tương ứng. Stall chỉ tối đa 50ms chuyển động. Ngoài window
   giữ hovered pane cuối, release flush vị trí cuối không thêm motion; stop
   timer khi cancel/detach/dispose. Không recalculate/parse trong Render.
3. Incomplete draft outline: balanced preview tạm qua shared analyzer, không
   sửa text/evaluate hay tạo parser/workbook/history song song. Giữ nhiều
   completed references và màu thứ tự khi còn thiếu ngoặc/đối số/cuối token.
   Literal/quoted-sheet/structured token được bảo vệ; không trùng provisional.
4. NeraFormulaReferencePicker: UI chọn sheet/range read-only dùng snapshot và
   sparse layout/composer chung, KHÔNG tạo SpreadsheetSession thứ hai. Giữ
   canonical owner trên sheet nguồn; Apply chèn vào draft, Cancel không đổi
   draft. Validate exact editor identity/text/selection + workbook metadata và
   versions/names/dimensions của từng sheet; snapshot stale bị từ chối.
   Sample có nút Chọn tham chiếu và tab sheet khi đang formula sẽ mở chooser.
5. Direct A1 reference border move/resize: hit actual edge/corners qua layout,
   đổi đúng source span, giữ prefix tên sheet/dấu $/reference khác; một lần
   commit vẫn qua canonical history. Freeze-quadrant clips không tạo cạnh giả
   tại mép viewport. Capture loss khôi phục draft trước gesture; text ngoài
   thay đổi không bị old-pointer overwrite; sheet mutation hủy gesture.
6. Operand-slot guard: shared CanInsertReference chỉ chặn literals/structured
   tokens, không chặn adjacency. Avalonia thêm boundary check để không sinh
   '=D5B2' khi bấm lưới sau một operand đã hoàn chỉnh. Provisional replacement,
   dấu phân cách/ngoặc và selection guards vẫn giữ.

## Regression / evidence

Baseline 77 ca test không bị bỏ. Thêm 48 ca: 5 cross-pane routed drags,
9 incomplete-reference projection, 8 frame/edge, 8 chooser, 12 border/geometry,
6 operand-boundary/stale context. Workflow nâng minimum thành 125 test/process,
5 tiến trình đều phải pass/0skip, không retry-until-green hoặc suppress analyzer.
Giữ full Release graph/hash/architecture/isolation và hai native gates cũ.

Thêm native `--formula-ux-smoke` độc lập: 23 check IDs bắt buộc + 2 PNG:
`formula-ux-references.png`, `formula-ux-cross-sheet.png`. Chạy trên cửa sổ native,
routed pointer press/move/release, actual production timer cho edge scroll,
read-only chooser actual range drag/apply/cancel, commit/recalculate/undo.
Không có Headless assembly trong sample. Input được dispatch qua API native,
KHÔNG là physical mouse/keyboard/IME hoặc đo GPU latency. Capture là loaded
native visual tree, không dùng ảnh thiết kế. Exact SHA bắt buộc trong result.

Lịch sử red-before-fix:
- df3b6fcc: 3 tests capture-lost failed; 3bac9d0f transfer capture trước anchor.
- a9212d3a: CA1512; f6d5b107 dùng checked helper, không suppress. f6d5b107 đã
  có matrix 3 OS green tại run34187678980 (bounded first99 tests).
- b88e983d: 1/107 fail vì Workbook.Version không theo Worksheet.SetValue;
  7ab1b237 thêm worksheet clocks, giữ regression.
- 7ab1b237: 1/119 fail '=D5B2' khi opt-out; 6c5ff9da sửa operand-slot guard
  và thêm6regressions, không thay expectation để ép xanh.

Tại lúc soạn: final native gate mới CHƯA có kết quả. Không local compile/PASS
vì shell/Python ClientError. Chỉ công nhận Actions của exact final pushed SHA;
PR evidence comment ghi final IDs/counts, không dùng parent green.

## Ranh giới còn lại

Chooser là cửa sổ chọn range read-only, không giả seamless chuyển worksheet
chính khi giữ edit. Mọi commit vẫn ở source worksheet. Border move/resize hiện
cho direct A1 references; structured/named/3D/reversed ranges vẫn chỉnh qua
text/assistant, không tự đổi nghĩa hay normalize khi kéo. Best-effort preview
không hứa highlight mọi malformed syntax. Chưa full physical IME/touch/a11y/
multi-monitor-DPI/performance; không claim pixel-perfect Excel hoặc toàn dự án.
Mục2/3 và Table/Filter/native print preview vẫn giữ scope/queue riêng.

Bước tiếp theo duy nhất: xem toàn bộ final exact-head CI và native Formula UX
artifacts, sửa failure quan sát được mà không nới gates, rồi cập nhật PR #4 và
handoff PR #1. Không Ready, merge hoặc public publish. Rollback chỉ owned
Avalonia/sample/test/workflow/docs delta, không workbook schema migration.
