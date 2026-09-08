# Formula UX Avalonia — bổ sung sau audit

Base: `b2bd9b3ef8ab2b1cd70134144a4052c1fc1a5a88`, PR #4,
branch `feature/avalonia-001-host`. Chỉ mục 1 của người dùng. Mục 2/3 giữ
Codex deferred queue PR #1 comment 5579073325; không sửa clipboard/view-state
shared source hoặc root CURRENT đang do coordinator quản lý.

## Routing kéo reference đối xứng

Split host nhận capture từ mọi pane khi bắt đầu kéo reference trên lưới,
kể cả pane giữ editor. Mỗi bước hit-test theo pane dưới con trỏ, không dùng
tọa độ pane xuất phát sau khi vượt separator. Pointer TextBox/TextPresenter
vẫn giữ caret/selection. Canonical cancellation dừng capture và timer.

5 tests routed MouseDown/Move/Up giữ nguyên. Candidate df3b6fcc build xanh
nhưng 3 tests mới bắt lỗi capture-lost xóa point anchor ngay sau MouseDown.
Correction 3bac9d0f chuyển capture TRƯỚC BeginPointReference; không sửa
assertion để giấu lỗi. Final acceptance cần exact-head, không parent green.

## Autoscroll theo frame và giữ references của draft chưa hoàn chỉnh

- Raw moves chỉ cập nhật vị trí cuối; timer 60Hz/frame scheduler cập nhật range.
- Edge velocity bounded theo DIP và zoom; document offsets vẫn double. Stall
  chỉ áp tối đa 50ms chuyển động/frame. Dùng ContinuousScrollController hiện có.
- Ngoài cửa sổ giữ pane cuối, clamp hit-test vào body rồi cuộn; separator/header
  trong split không được diễn giải thành vùng dữ liệu giả.
- Frame tham chiếu chỉ đổi text khi ô cuối đổi. Release flush vị trí cuối mà
  không thêm chuyển động. Cancel/detach/dispose dừng timer và bỏ tham chiếu host.
- Frozen bands không tự cuộn trục tương ứng. Split chỉ cuộn pane đang hover,
  editor/history/selection/active pane vẫn thuộc owner ban đầu.
- Draft reference projection dùng tối đa hai ephemeral balanced previews qua
  shared FormulaReferenceAnalyzer. Không có evaluator/parser công thức thứ hai,
  không ghi preview vào editor, workbook hoặc history. Giữ nhiều completed
  references khi thiếu ngoặc/đối số/cuối token; quoted text không thành reference.
- Normalize current-sheet identity khi thêm provisional dependency để tránh
  vẽ hai outline cho cùng range. Preserved source order giữ màu trước đó.
- Thêm 9 projection tests và 8 edge-frame/routed-input tests; baseline vẫn giữ.

Shell và Python môi trường tác giả báo ClientError. Không local build/PASS.
Các mã mới là candidate cho tới khi CI exact source chạy qua đầy đủ gate.
Native full-shell/basic smoke cũ vẫn bắt buộc; headless không thay physical UX.

## Còn mở

UI chọn reference từ sheet khác mà không hủy canonical draft; hit-test/move/
resize viền reference có sẵn; kiểm chuyên biệt frozen-pane outline clipping và
physical IME/DPI/native capture cho toàn bộ Formula UX. Chưa whole mục 1 DONE.

Bước tiếp theo: đọc exact-source CI, sửa lỗi quan sát được không nới gate,
sau đó tiếp tục các gap Formula UX. PR Draft, không merge/Ready; rollback chỉ
các owned Avalonia files, không workbook schema migration.
