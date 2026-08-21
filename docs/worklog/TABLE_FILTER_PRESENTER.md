# Table Manager + AutoFilter Presenter validation handoff

This handoff records the scope that must pass exact-head CI before the batch is promoted from Draft work to a validated milestone.

## Implemented source surface

- Platform-neutral Table manager and filter-menu snapshots.
- Bounded distinct-value enumeration with occurrence counts, search and truncation diagnostics.
- Select-all-visible, clear-visible, value-filter, custom-filter and clear-filter commands through `SpreadsheetSession.Tables` production history.
- Shared `SpreadsheetTableFilterButtonGeometry` derived from `WorksheetSnapshot` and `ViewportLayout`.
- Native WPF `Popup` presenter and automatic Table-header button host.
- Native WinForms `ToolStripDropDown` presenter and automatic Table-header button host.
- Responsive MAUI `ContentView` filter sheet for popup, side-sheet or bottom-sheet placement.

## Required validation

The batch is not complete until the same exact head passes:

1. Core restore, build, tests and architecture verification.
2. WPF and WinForms full build/tests plus desktop GPU runtime smoke.
3. MAUI Android, iOS, Mac Catalyst and Windows builds.
4. Loaded MAUI Windows input/context-recreation and scale/orientation smokes.
5. Presenter search, selected-state, bounded enumeration, history and shared hit-geometry regressions.

## Deliberately pending

- Keyboard navigation and focus restoration across native presenters.
- IME and screen-reader/accessibility hardening.
- Paging/virtualization for very large distinct-value lists.
- Rich date, text, top/bottom, color and icon filter UI.
- Direct worksheet AutoFilter outside Tables.

PR #1 remains Draft and must not merge while exact-head CI is red or unknown.
