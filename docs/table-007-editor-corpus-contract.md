# TABLE-007 — editor dùng chung và corpus producer

Base bắt buộc: `2e8482c25a44797a479b276ae26f472811a0a81e`.
Phạm vi T1–T3 theo wave 05/09/2026; chưa coi kế hoạch dưới đây là nghiệm thu.

- WPF split adorner và WinForms split surface tiếp tục dùng `Session.Editor`.
  Completion, point-mode và draft references dùng assistant/analyzer hiện có.
- MAUI giữ `NeraSpreadsheetView : SKGLView`; host bao quanh chính view giữ một
  native Editor và bounded suggestion list. Không sở hữu workbook/calculation
  hoặc editor controller thứ hai. Begin/Commit/Cancel dùng `view.Session.Editor`.
- Enter commit, Alt+Enter newline, Escape cancel; Tab nhận candidate. Stale
  text/caret/selection/ID được kiểm tra ngay tại acceptance boundary; rename dùng
  tên mới resolve từ stable IDs. Completion chỉ metadata, không history/recalc.
- Layout theo toàn bộ cell/merge rectangle; viewport/freeze/split chỉ clip.
  Commit giữ incremental session transaction; không full recalculation UI.
  Split editor giữ pane bắt đầu khi cuộn hoặc kích hoạt pane khác; khi pane đó
  không còn trong layout mới, dùng pane active còn lại. Mất capture kết thúc
  drag provisional, cancel giải phóng capture; moved caret không ghi đè span cũ.
- WPF CancelEditor luôn dọn native overlay/popup khi session đã hủy draft trước
  đó (ví dụ ActivateWorksheet). Trả false nếu không có draft để hủy, không focus
  hoặc chọn lại ô cũ trong sheet mới; cleanup UI không thêm history hay sửa ô.
- LibreOffice thật import/export synthetic Nera seed trong Ubuntu CI với profile
  riêng. Ghi version, native/sanitized SHA-256, provenance và từng payload không
  đổi; chỉ core author/timestamps và ZIP timestamps được sanitize. Không gọi
  fixture đổi app metadata là LibreOffice-produced.
- `dataDxfId` preserve-only và mixed opaque CF boundaries giữ nguyên. Chỉ sửa
  converter khi fixture thực tạo repro và regression xác nhận.

Native loaded CI, headless regressions, architecture và exact-final-head CI là
cổng bắt buộc. Local desktop lease thuộc A; B không chạy loaded native local.

`SpreadsheetViewportEngine.ComputeLayout` dùng metric/filter cache hiện có;
không tạo display list, snapshot mới cho unfiltered worksheet hoặc recalculate.
Active Table filters vẫn refresh/reuse snapshot theo EnsureMetrics. MAUI overlay
đổi canvas pixels sang host DIPs, có geometry thật từ Width/Height khi GPU frame
chưa usable; geometry không chứng minh GPU đã render.

Runtime hooks phân biệt bằng chứng: Windows smoke dùng OS keyboard với kiểm tra
foreground/focus; WPF routed key và WinForms OnKeyDown trong loaded split;
Android native EditText.DispatchKeyEvent; Apple UITextView.InsertText và marked
text. Hook Apple không chứng minh hardware PressesBegan/OS-injected keyboard;
Enter/Alt+Enter/Escape bằng keyboard phần cứng trên Apple còn OPEN. Checkpoint
cf680688 CI phát hiện empty hidden draft, Apple nullability và Windows multiline
assertion; followup phải chạy lại trước khi nghiệm thu, không suy PASS từ build.

Tham chiếu: [LibreOffice CLI parameters](https://help.libreoffice.org/latest/en-US/text/shared/guide/start_parameters.html).
