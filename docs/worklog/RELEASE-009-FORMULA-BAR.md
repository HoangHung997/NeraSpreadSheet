# RELEASE-009 Formula Bar — Handoff lane A

## Checkpoint6d3fa4a1 — sửa build và active-dispose regression

- Implementation `6d3fa4a12323dd06234e4a859301338b4d9a2926` đã push:
  full33992065580/iOS33992066955/Q33992068085/packages33992069020/
  demo33992070323. Q/packages SUCCESS; Windows101375951600 và demo FAIL build
  vì `Color` bị existing partial method Color(string) che tên type. Follow-up
  dùng tên WPF Color đầy đủ, không đổi logic hoặc bỏ analyzer/test.
- Bổ sung test dispose khi draft đang active. Shell hủy canonical draft thuộc
  native host trước SDK detach; không focus lại editor trong lúc dispose hoặc
  cancel một session draft mà host không có. Standalone SDK Dispose tự nó chưa
  đảm nhiệm cancellation này; không sửa SDK ngoài scope.
- Native/capture6d chưa chạy; source follow-up phải qua đủ final gates riêng.

- Branch `feature/release-009-formula-bar`, base chính xác
  `37a55b05e3af25809d412b67debb0d9e3581f929` sau root nhận SDK slice.
  PR #1 Draft/open/unmerged. HEAD chứa checkpoint này là implementation đầu,
  chưa có CI source-green, chưa release ownership.
- Branch SDK đã release giữ `eea6ba6f84cd39f58eae3002464ff60b9d27febb`;
  navigationbed và artifacts cũ nguyên trạng. Không reset/rebase/cleanup hoặc
  heavy local/native. Baseline combined CI thuộc root, không thay final gates.
- Grant chỉ shell FormulaBar, bốn formula actions/Help, bounded captures, tests,
  hai resources thêm nhãn và docs riêng/formula guide; không SDK/filter/navigation/
  shared CI/CURRENT/status/wave. Không tạo task, model override hoặc subagent.

## Implementation chờ kiểm chứng

- Actual TextBox mirrors native SDK draft, focus starts once, guard bidirectional
  updates và queued refresh đọc latest snapshot. Địa chỉ theo draft anchor;
  canonical cancel/sheet change/dispose dọn view/help/subscriptions.
- Enter/validation/AltEnter/Esc và Tab/F2 handoff; window handler trước Ribbon
  giữ CtrlZ/Y/C/X/V ở native TextBox. Không second editor/undo model hoặc mutation
  workbook mỗi ký tự. Bốn formula actions dùng same draft adapter; Help actual
  nested function/caret, không hardcoded SUM/modal message box.
- Native regression mới `Release009FormulaBarSmokeTests`:12 loaded cases trong
  hai hosts, focus/mirror/commit/history, validation, Alt/SystemKey/keytips,
  native text Undo/Redo/copy/cut/paste, actual point-mode/4buttons/help, stale
  refresh/sheet/cancel/dispose. Existing loaded-workbook formula assertion đổi
  từ TextBlock sang TextBox; không sửa assertion khác.
- Capture thêm4bar PNG tại640/1280 standalone/split và1Help PNG, đọc canonical
  metadata thật; existing capture assertions giữ nguyên. Hai resource catalogs
  thêm9keys cho bar/action labels, tooltip, help/validation fallback.
- Local architecture/diff checks PASS; chưa local build/native vì grant và
  dung lượng. Không tuyên bố runtime/capture/benchmark hoặc final CI đã qua.
- Tab ở bar chỉ FocusEditor, native Tab mới nhận suggestion. Direct completion,
  arbitrary cross-control direction/undo transfer và split paged filter OPEN.
- Rollback: revert riêng source/doc commits của slice; không migrate workbook.
- Một bước tiếp theo: commit/push checkpoint này, dispatch đủ5workflow đúngSHA,
  xử lý build/native/capture failures trước final evidence và release manifest.
