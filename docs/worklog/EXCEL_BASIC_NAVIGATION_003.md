# EXCEL-BASIC-NAV-003 worklog

## Scope

- Refine adaptive navigation after direct comparison with desktop Excel.
- Keep scrollbars usable around small sheets through a bounded blank workspace.
- Separate scrollbar viewport movement from active-cell navigation.
- Keep behavior reusable in the Viewport, WPF and WinForms SDK packages.

## Implementation

- `SpreadsheetViewportEngine.GetAdaptiveNavigationExtent` now retains at least
  one viewport plus a configurable default tail of 100 rows and 20 columns.
- The overload accepting the current continuous scroll offset preserves the
  viewport being inspected without treating it as used content or recursively
  appending another tail.
- WPF and WinForms expose `AdaptiveNavigationTrailingRowCount` and
  `AdaptiveNavigationTrailingColumnCount`; both default to the shared engine
  constants.
- Direct `ScrollTo` refreshes the adaptive extent. It moves the viewport only;
  keyboard navigation still moves selection and calls `ScrollCellIntoView`.
- The external Win11 demo adopts the contract and a lighter Excel-like chrome;
  demo styling is deliberately not part of the SDK packages.

## Validation

- Viewport: 59/59 passed.
- Focused loaded WPF/WinForms adaptive navigation smoke: 2/2 passed.
- Full build, architecture verification, demo packaging and exact-head GitHub
  Actions evidence are recorded in `CURRENT.md` at handoff.

## Boundaries

- This is adaptive navigation, not row/column deletion or materialization.
- Split panes retain their existing full physical extent.
- MAUI does not yet expose this desktop host opt-in.
- Final scrollbar thumb geometry remains a host responsibility because it
  depends on the actual body viewport and surrounding application chrome.
