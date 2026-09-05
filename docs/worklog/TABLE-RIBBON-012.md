# TABLE-RIBBON-012 — Table Design integration and UX

- State: ACTIVE.
- Branch: `feature/table-ribbon-012-integration`.
- Base: `488c61ea75ea6c8f7a6ceb480035a341f24c6c19`.
- Imported immutable TABLE-005 baseline, in order:
  `c8793a4f` → `1c6faead`, `0c2c64f6` → `b206dcdf`,
  `cf923db2` → `8957f42a`.
- Import conflicts only: production Ribbon placement helper, WinForms Ribbon
  fields, MAUI smoke result markers. Retained both VISUAL-011 dense chrome and
  TABLE-005 identities/bindings. Core/Editing/OpenXml/Commands/rendering baseline
  is unchanged from the source commits.
- Owned delta: Ribbon.Core, Ribbon/TableDesignBinding host files, focused
  presentation tests, WPF preview shell and existing capture script, this log
  and `docs/table-ribbon-integration-contract.md`.
- Shared coordination documents, project files and workflows are excluded.
- No additional task, worktree or agent is created. TABLE-006 code is not imported.

## Implemented

- Optional `ActivationContextProvider` collects typed parameters before the same
  dispatcher used by buttons, choices, shortcuts, key tips and QAT. Null cancels;
  disabled commands do not prompt; pending activation rechecks visibility and
  original cancellation before dispatch. Runtime resumes on the caller context.
- Production definition accepts optional style thumbnails and retains all 49
  session command IDs (19 Table commands), dense layout and customization.
- WPF preview consumes the production Table tab and handlers. Removed the sample
  style-preview, rename and totals mutation handlers. Thumbnails reuse TABLE-005
  bounded preview snapshots and invalidate by snapshot identity.
- Dialogs provide Create name/current range, Rename, Resize, calculated/custom
  totals formula, insert-column name, stable column IDs for Remove duplicates,
  Convert to range, and primary style/totals activation from QAT/key tips. Basic
  field errors stay in the dialog; engine errors are translated at the UI boundary.
- A worksheet chooser exercises contextual transitions through the real session.
  Synthetic sample calculated columns use structured references.
- WPF/WinForms/MAUI bindings use the latest shared snapshot on the UI dispatcher
  and discard queued callbacks after disposal. Preview removes subscriptions on
  close and restores focus after parameter collection.

## Local validation

- SDK 10.0.302: full solution and Release WPF sample builds **0 warnings /
  0 errors**. MAUI uses installed 10.0.201 without changing global.json.
- Core solution **1407/1407**, including Commands **121/121**; architecture and
  SDK packaging verifiers passed.
- MAUI **43/43**; framework-dependent loaded Windows Ribbon smoke **success**,
  three frames, Table binding, complex items, customization, bounded overflow,
  native layout/focus and existing 100/125/150/200 scale matrix.
- WPF/WinForms presenter suite **11/11**, including the new queued-dispose test.
- Full Windows before the final additional dispose test: **74/76**.
  `DesktopRibbonItemModelSmokeTests.VerifyWpfRibbon`, line 97, failed at
  `Assert.IsTrue(splitMenu.Focus())` before command activation; focused rerun
  reproduced this (**16/17**). `WpfSplitScrollBarWindowMessageSmokeTests`, line
  39, failed at `GetCursorPos` before any SDK behavior. The base handoff records
  a mouse activation limitation, but not this exact pair on this local desktop;
  an environment cause remains an inference. No assertions were weakened.
- Initial full capture **177 images / 128 native snapshots**. Final focused
  Table capture **33 images / 16 native snapshots**: four palettes and widths
  1024/1280/1600/1920, 125/150/200 raster exports, independent logical-layout
  invariants at scale 1/1.25/1.5/2 (also 820 width). Images inspected locally.
- Capture verifies native gallery and totals ComboBox mutation/Undo, cancel with
  no history, Vietnamese validation, Create/Rename/Resize/calculated/custom
  totals/Remove duplicates/Convert to range with one history entry and Undo,
  plus worksheet switching. Artifacts/logs are not committed.
- No layout/scroll/render algorithm changed; no new benchmark threshold is
  claimed. Gallery caching remains bounded by the TABLE-005 snapshot catalog.
- `git diff --check` passed. No Core/Editing/OpenXml/Commands/rendering delta
  after imported baseline `8957f42a`; no package/project/workflow/shared
  coordination/private workbook/machine path is committed.

## Remaining gate and limits

Exact-head full CI, iOS and Q003C runs are pending. Local focus/cursor failures
must be assessed against unchanged full Windows CI. This is not yet a handoff.

Dialogs are WPF sample integration; SDK users provide a callback on any host.
Read-only policy continues through command handlers; no workbook protection
model is added. Create keeps TABLE-005 selection/header defaults. Physical
multi-monitor DPI and screen-reader acceptance remain outside raster/native-scale
checks. TABLE-006 changes are not imported. External packaged demo and PR #1
remain unchanged.

## Integration and rollback

Coordinator integrates `488c61ea..HEAD` once, including the three baseline imports.
For TABLE-006, take only the delta after source `cf923db2`. Revert this lane's
delta to return to `488c61ea`; no dependency or persisted schema migration.
