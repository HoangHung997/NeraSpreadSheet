# RIBBON-009 — contextual tabs, QAT, backstage và Key Tips

- Checkpoint: `RIBBON-009`.
- Owner: Codex task `RIBBON-009 — Contextual tabs, QAT, backstage và Key Tips`.
- Branch: `feature/ribbon-009-contextual-qat`.
- Base integration SHA: `d595539d616cba1bb5543ab3530035f927304069`.
- Implementation SHA: `e1e38b37416f0df1a6fea2cb59346deb22e7d3e6`.
- Review hardening SHAs: `a743826` (Core/API/runtime) và `5e4ffe7` +
  `e393840` (native hosts, loaded smokes và MAUI API compatibility).
- Owned paths: `NeraSpreadSheet.Ribbon.Core`, WPF/WinForms/MAUI Ribbon presenter,
  Ribbon-only tests/smokes, contract này và `docs/ribbon-contextual-qat-contract.md`.
- Excluded: Table/Filter files và shared board/status/worklog.

## Implementation

- Contextual tab projection đọc `RibbonSelectionContext` và lọc tab theo selection
  hoặc Table state mà không sở hữu workbook.
- Minimized/expanded state nằm trong runtime và round-trip qua JSON schema version 1.
- QAT và backstage dùng stable `CommandId`, cùng presentation cache, dispatcher,
  state refresh và error boundary với Ribbon command hiện hữu.
- Command catalog mặc định khóa và đặt đủ 30 production session commands; audit
  fail-fast cho registration hoặc placement thiếu.
- Key Tips có scope Tabs/Tab/QAT/Backstage, multi-character input, collision
  validation, badge văn bản, Escape/back và focus-origin restore.
- WPF, WinForms và MAUI dựng native File/QAT/backstage chrome, accessibility metadata
  và phản ánh contextual/minimized runtime state.

## Review hardening

- Giữ exact CLR overload `Project(RibbonDefinition, CommandContext)` và tránh tạo
  overload mơ hồ cho `EnterKeyTipMode` trên MAUI.
- `ProcessCharacter('F'/'Q')` mở đúng Backstage/QAT; WPF/WinForms bind Alt, Escape và
  ký tự ở window/form owner, kể cả khi focus ở worksheet sibling. Close File không để
  stale Backstage scope.
- Focus origin dùng stable automation/control identity qua rebuild; MAUI rebuild và
  restore focus trên UI dispatcher sau activation bất đồng bộ.
- Minimized ẩn group và overflow thật trên cả ba presenter; compact item có icon vẫn
  giữ badge Key Tip dạng chữ.
- Key-tip allocator ASCII bounded chịu catalog lớn, mapping/reverse mapping là cache
  bất biến; shortcut map gồm tab, QAT và backstage.
- Production audit chạy với `SpreadsheetSession` thật và exact registry snapshot,
  phát hiện cả command bị thiếu lẫn registration mới chưa có placement/manifest.
- Selection/customization publish nguyên tử; malformed view-state root chuẩn hóa về
  `InvalidDataException`.

## Validation

- Core solution Release build/analyzers: **0 warnings / 0 errors**.
- Core solution: **1.341/1.341 passed**; Commands/Ribbon: **95/95 passed**.
- Focused loaded WPF/WinForms Ribbon presenter: **9/9 passed**.
- MAUI presenter: **36/36 passed**.
- Loaded MAUI Windows Ribbon smoke: **success**, gồm contextual Table Design,
  QAT/backstage shortcut, `F` theo từng ký tự, external-focus restore, minimized
  group/overflow và compact icon+badge ngoài coverage item/overflow sẵn có.
- MAUI Windows, Android, iOS và Mac Catalyst Release builds: **0 warnings / 0
  errors**. Android dùng SDK API 36 user-scoped đã có từ RIBBON-008.
- Architecture verification và SDK packaging verification: **passed**.
- Full Windows.Rendering: **67/68 passed locally**. Lỗi duy nhất là smoke native
  mouse đã biết không thể đưa cửa sổ background thành foreground tại
  `window.Activate()`; test đó chạy riêng **1/1 passed**, lỗi full run dừng trước khi
  chạy hành vi SDK và không thuộc Ribbon.
- Exact-head GitHub Actions: pending sau push branch; workflow_dispatch sẽ chạy
  trên commit handoff cuối.

## Remaining limits

- Shared cross-platform document File command handlers không tồn tại ở baseline;
  backstage nhận command do host đăng ký thay vì tạo lifecycle workbook song song.
- Styling chuyên sâu cho badge theo theme/high-contrast được để cho UX-006; badge
  văn bản, keyboard state, activation, collision và focus semantics đã có.
- QAT/deep layout customization thuộc RIBBON-010.

## Rollback

Revert implementation commit RIBBON-009. Constructor `RibbonDefinition(tabs)` và
toàn bộ RIBBON-008 item/customization API vẫn tương thích.

## Integration closure

- Integrated with `FILTER-007` at
  `f75e2f103598cbbe7b5c22f92c3ab8dd755ef8c3` without conflict.
- Combined local gates: Core **1354/1354**, MAUI **40/40**, focused desktop
  Ribbon/Table-filter **13/13** and loaded MAUI Windows Ribbon/Table-filter
  smokes passed.
- Exact-head GitHub gates passed: full CI `33931524467` / #1312, iOS
  `33931524461` / #133 and Q003C/OpenXML `33931524543` / #130.
