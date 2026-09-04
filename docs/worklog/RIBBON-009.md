# RIBBON-009 — contextual tabs, QAT, backstage và Key Tips

- Checkpoint: `RIBBON-009`.
- Owner: Codex task `RIBBON-009 — Contextual tabs, QAT, backstage và Key Tips`.
- Branch: `feature/ribbon-009-contextual-qat`.
- Base integration SHA: `d595539d616cba1bb5543ab3530035f927304069`.
- Implementation SHA: `e1e38b37416f0df1a6fea2cb59346deb22e7d3e6`.
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

## Validation

- Core solution Release build/analyzers: **0 warnings / 0 errors**.
- Core solution: **1.331/1.331 passed**; Commands/Ribbon: **85/85 passed**.
- Focused loaded WPF/WinForms Ribbon: **11/11 passed**.
- MAUI presenter: **36/36 passed**.
- Loaded MAUI Windows Ribbon smoke: **success**, gồm contextual Table Design,
  QAT, backstage, minimized và Key Tips ngoài coverage item/overflow sẵn có.
- MAUI Windows, Android, iOS và Mac Catalyst Release builds: **0 warnings / 0
  errors**. Android dùng SDK API 36 user-scoped đã có từ RIBBON-008.
- Architecture verification và SDK packaging verification: **passed**.
- Full Windows.Rendering: **66/67 passed locally**. Lỗi duy nhất là smoke native
  mouse đã biết không thể đưa cửa sổ background thành foreground tại
  `window.Activate()`; nó dừng trước khi chạy hành vi SDK và không thuộc Ribbon.
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
