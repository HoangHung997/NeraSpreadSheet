# RELEASE-009 — Thanh công thức của WPF sample

## Một bản nháp canonical

- Thanh công thức là một WPF TextBox làm view/input adapter cho editor SDK hiện
  có. Dùng CurrentEditorDraft/EditorDraftChanged, UpdateEditorDraft, FocusEditor,
  BeginEdit, CommitEditor và CancelEditor; không thêm controller/session/history
  hoặc cập nhật workbook theo từng ký tự.
- Focus vào bar bắt đầu native edit một lần nếu chưa có draft. Khi đang sửa,
  text/address luôn theo draft anchor, kể cả selection point-mode ở ô khác.
  Khi idle, hiển thị raw formula/value của ô active, cập nhật khi dữ liệu đổi.
- Mirror chỉ thay text/range khi khác; identical refresh giữ native selection
  direction của bar. API SDK vẫn chỉ mô tả range/WPF public caret, không tuyên bố
  chuyển arbitrary moving edge hoặc toàn undo stack giữa hai TextBox.
- Callback shell đọc snapshot mới nhất khi chạy. Cancel, chuyển worksheet và
  dispose dọn subscription/help; callback cũ không hồi sinh draft hoặc ghi đè
  dữ liệu sheet mới. Chuyển focus đơn thuần không commit.

## Phím và thao tác

- Enter commit đúng một lần qua canonical SDK, thành công mới đi xuống ô visible
  tiếp theo và dùng ScrollCellIntoView của host active. Validation false giữ
  draft/range/focus ở bar và thông báo lỗi hiện có; không đổi history/selection.
- Alt+Enter thay selection bằng một newline, không commit/navigation. Xử lý
  SystemKey của WPF và thoát keytips của Alt+Enter; bare Alt vẫn dùng keytips.
- Esc hủy draft. Nếu đang ở keytip scope, Esc trước hết thoát scope theo Ribbon
  hiện có; Esc ở chế độ nhập bình thường hủy chỉnh sửa.
- Tab/F2 trên bar chuyển về actual native editor để dùng popup/candidate hiện có;
  Tab tại native editor nhận completion theo SDK. Bar chưa tự nhận completion.
  Không reflection hoặc synthetic key forwarding trong production.
- Window preview handler được đăng ký trước BindShortcuts. Ctrl+Z/Y/C/X/V khi
  bar focus gọi native TextBox commands, không rơi xuống workbook command kể cả
  text Undo đang rỗng. Ra ngoài bar, workbook shortcuts vẫn hoạt động.
- Nút xác nhận/hủy/sửa trong ô/trợ giúp có automation IDs ổn định và localized
  names/tooltips. TextBox hỗ trợ nhiều dòng với chiều cao giới hạn, cuộn nội bộ.

## Bốn nút hàm và Help

- FormulaSum/Average/If/Lookup giữ template semantics hiện có: thay draft bằng
  prefix hàm, không tự suy AutoSum range. Đã có draft thì UpdateEditorDraft giữ
  canonical State và anchor; chỉ BeginEdit khi chưa có draft, rồi FocusEditor.
- Help là popup đọc actual draft/caret/help metadata, gồm nested active argument.
  Khi chưa ở lời gọi hàm, hiển thị hướng dẫn; không hardcode SUM hoặc khởi động
  edit chỉ để mở Help. Popup không lấy focus hay commit bản nháp.

## Kiểm chứng và giới hạn

Loaded full-shell tests phải đi qua standalone/split, native bar focus/selection,
draft/text Undo/clipboard, Enter/AltEnter/Esc, validation, point-mode anchor,
bốn nút hàm, nested Help và stale callbacks/sheet/dispose. Test modifier dùng
native keyboard state của UI thread và hoàn trả sau mỗi sự kiện; không claim
global OS keyboard injection/hardware acceptance. Capture thêm 640/1280 ở hai
host cùng Help popup; không thay assertions capture cũ. Build/analyzers,
architecture, resource parity và năm exact-final workflows là gate riêng.

Paged filter split, direct completion trong bar, cross-control moving-edge/undo
transfer và whole MAUI/hardware/performance acceptance không thuộc slice này.
