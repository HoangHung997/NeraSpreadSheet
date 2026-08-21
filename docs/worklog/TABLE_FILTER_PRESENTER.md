# Table Manager + AutoFilter Presenter validation handoff

This handoff records the validated native-presenter milestone. The implementation is complete for the scope below because the same exact implementation head passed Core, Windows desktop and all MAUI gates.

- Implementation commit: `e3a814f5c0f6eb0fff75d30ee5ee217069139d71`.
- GitHub Actions: CI `#570`, run `32474664182`.
- Conclusion: `success` on August 21, 2026.
- PR #1 remains Draft and has not been merged into `develop`.

## Validated source surface

- Platform-neutral Table manager and filter-menu snapshots.
- Bounded distinct-value enumeration with occurrence counts, search and independent truncation diagnostics.
- Stable selection across search; select-all-visible and clear-visible semantics.
- Value-filter, one/two-condition custom-filter, clear-column and clear-all commands through `SpreadsheetSession.Tables` production history.
- Shared `SpreadsheetTableFilterButtonGeometry` derived from `WorksheetSnapshot` and `ViewportLayout`.
- Active-cell → stable Table/column target resolution.
- Platform-neutral value-list keyboard navigator.
- Native WPF `Popup` presenter and automatic Table-header button host.
- Native WinForms `ToolStripDropDown` presenter and automatic Table-header button host.
- Responsive MAUI Table host with native visible filter buttons and overlay/bottom-sheet presenter.
- Stable MAUI Automation IDs and semantic descriptions/hints/headings.

## Validated input and focus lifecycle

- `Alt+Down` opens the active Table column filter.
- Escape closes the presenter.
- Arrow, Home, End, Page Up and Page Down navigation.
- Space/Enter toggles the active value.
- Enter from search applies a valid selection.
- Visible select-all and clear-visible keyboard commands.
- Search receives focus on open.
- Focus is released from search on close and restored to a valid initiating button or spreadsheet surface.
- MAUI Windows uses bounded asynchronous WinUI focus acquisition rather than an unbounded retry or fixed-delay assumption.

## Exact-head validation

CI #570 passed:

1. Core restore, build, tests and architecture verification.
2. Presenter, navigator, target-resolver, bounded-enumeration, history and shared-geometry regressions.
3. Full Windows build/tests.
4. Loaded WPF/WinForms presenter and keyboard/focus smokes.
5. Windows desktop GPU runtime smoke.
6. MAUI Android build.
7. MAUI iOS and Mac Catalyst builds.
8. MAUI Windows build and handler tests.
9. Loaded MAUI Windows Table-filter smoke covering GPU frames, open, focus, Apply, compressed visibility, Undo, Redo, reopen and close-time focus release.
10. Loaded MAUI Windows context-recreation and scale/orientation smokes.

## Runtime defects found by the gate

- Native WinUI search focus requested before the `TextBox` was ready.
- MAUI `AutomationId` was reassigned during filter refresh.
- Search retained native focus after closure.
- Fixed smoke delays produced runner-dependent focus flakes.

All four defects were corrected before the milestone was promoted.

## Deliberately pending

- Virtualized/paged distinct-value lists and asynchronous enumeration/cancellation.
- Complete Table design/resize/style manager UI.
- Rich date, text, top/bottom, color, icon and custom-list filters.
- Direct worksheet AutoFilter outside Tables.
- Full mobile virtual-keyboard/IME lifecycle.
- Complete screen-reader, high-contrast, localization and theme certification.
- External producer AutoFilter/XLSX compatibility corpus.

Full contract: `docs/table-filter-presenter-contract.md`.
Source of truth: `docs/current-status.md`.

PR #1 must remain Draft while any newer exact-head CI is red or unknown.