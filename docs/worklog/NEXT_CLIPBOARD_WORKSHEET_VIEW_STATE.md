# Hàng chờ — Clipboard UX và trạng thái riêng từng sheet

## Chỉ đạo và điều kiện bắt đầu

Nguồn yêu cầu: [PR #1, comment5579073325](https://github.com/HoangHung997/NeraSpreadSheet/pull/1#issuecomment-5579073325),
được người dùng xác nhận trực tiếp ngày08/09/2026. Coordinator đã đọc toàn bộ.
Trạng thái cả hai track: **QUEUED — chưa audit source mới, chưa dispatch, chưa sửa**.

Không ngắt công việc hiện tại, không đổi ownership hoặc phạm vi các nhánh đang
chạy. Chỉ bắt đầu sau khi công việc hiện hành đã hoàn tất và checkpoint tích hợp
đã được xác minh. Nếu công việc hiện tại bị chặn, không tự đổi sang hai track này
trái yêu cầu thứ tự; báo đúng trạng thái và xin hướng xử lý nếu cần.

Khi tiếp nhận, root đang ở `4639767ef8c5f3c44d1e074084d3b7f78dac00fc`, PR#1
Draft/open/unmerged; A làm UX-008 accessibility, C chuẩn bị local diagnostic,
whole B vẫn HOLD. Đây chỉ là mốc nhận yêu cầu, không phải baseline implementation
của hai track. Comment nguồn dùng refs cũ1217/39/Avalonia b2bd: phải đọc lại
latest refs/PR/source/contracts/tests trước mọi kết luận hoặc sửa.

Hai track là yêu cầu bổ sung được xếp hàng, không làm đổi phạm vi nghiệm thu
U1–U5/T1–T3/P1–P3/R1–R4 đang chạy hoặc xóa bất kỳ OPEN/HOLD hiện có.

## NEXT-CLIPBOARD-UX — Mục2, ưu tiên nguy cơ mất dữ liệu

1. Tái hiện Cut vùng rời nhau: package copy phạm vi nào thì không được xóa ngoài
   phạm vi đó. Finding CopyPrimarySelection/ClearSelection trong comment chưa
   được coordinator xác minh ở source mới. Test A1/C1, reject nguyên tử hoặc
   multi-range contract rõ ràng; giữ undo/redo, protection, spills và merges.
2. Thiết kế/bổ sung Paste Special trong lớp dùng chung trước khi nối UI:
   All/Values/Formulas/Formats; xác định rõ phạm vi transpose/skip blanks,
   translation, validation/merges/spills/bounds và transaction. Không menu giả.
3. Copy/cut source state tách khỏi selection/reference: operation, stable
   workbook/sheet identity, ranges/payload/OS ownership, busy/error/cancel/events.
   Viền nguồn chỉ visible+overscan, đúng frozen/split/DPI, không control mỗi ô
   hoặc full repaint theo timer.
4. Esc theo contextual priority, không xóa OS clipboard mặc định hoặc nuốt
   editor/IME/popup. Cut chỉ xóa đúng vùng sau OS write thành công. Async chậm,
   đổi sheet/selection/session, dispose không được ghi/xóa dữ liệu sai.
5. Audit adapter/CanPaste/state/nút Paste của WPF, WinForms, MAUI, Avalonia trên
   source thực tế. External clipboard replacement/unavailable, rich↔TSV fallback,
   nhiều cửa sổ và stale completion cần regression + actual host interaction.

## NEXT-WORKSHEET-VIEW-STATE — Mục3, giữ view và bảo toàn XLSX

1. Selection gồm active cell/anchor/directed/multiple ranges, continuous scroll
   offsets và zoom phải lưu theo stable worksheet identity trong session/window,
   không khóa bằng tên hoặc để các cửa sổ ghi đè nhau.
2. Tái sử dụng SpreadsheetViewController/freeze/split state và adapters hiện có,
   gồm topology/active pane/offset từng pane; xử lý rename/reorder/remove sheet,
   hidden/deleted/merged axes và viewport clamp. Không view/history model song song.
3. Tái hiện A→B→A→B với selection, fractional offsets và zoom khác nhau; standalone
   và split, nhiều cửa sổ, hoán đổi subscription order. Kiểm nguy cơ Reset/Refresh/
   ViewportChanged ghi trả offset vào sheet vừa active; restore/suppression phải
   bao phủ transaction chuyển view, không chỉ topology.
4. Tách retention trong session khỏi XLSX persistence. Ưu tiên regression
   SheetViews có sẵn selection/zoom/view IDs/unknown metadata không bị mất khi
   load/save, kể cả no split. Kiểm ReplaceStandardSheetView ở source mới; cập nhật
   targeted fields, không mặc định xóa toàn markup hoặc coi finding cũ là đã tái hiện.

## Ownership, bằng chứng và nghiệm thu khi bắt đầu

- Root đọc latest shared layer/host/serializer/tests và lập bounded path grants;
  một writer mỗi file. Không tự mở worktree/task trước điều kiện bắt đầu.
- Mục1 Formula UX Avalonia do ChatGPT giữ theo comment tại PR#4. Trước khi đụng
  FormulaEditing/point-mode hoặc shared Avalonia paths phải kiểm lại owner và
  thống nhất giao quyền; không tự nhập hoặc ghi đè nhánh đó.
- Bắt buộc feature regressions, actual host smoke, build/analyzers/architecture,
  privacy và source/combined exact-HEAD CI. Tests topology/Copy-Paste hiện có hay
  CancellationToken tests không thay các kịch bản mới và actual Esc interaction.
- Báo findings có bằng chứng trước/sau và giới hạn từng host; chưa được xác minh
  phải ghi UNKNOWN/OPEN. Giữ PR Draft, không merge/Ready/public NuGet publish.
- Một bước tiếp theo sau checkpoint hiện tại hoàn tất: kiểm latest refs và tái
  hiện nguy cơ Cut nhiều vùng xóa ngoài clipboard package trước khi chốt fix scope.
