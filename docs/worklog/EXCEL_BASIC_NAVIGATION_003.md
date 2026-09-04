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
- Core solution: 1244/1244 passed; build/analyzers 0 warnings, 0 errors.
- Architecture verification: passed.
- External demo build, internal smoke, self-contained publish and packaged EXE
  smoke: passed.
- Implementation checkpoint `45fae777c58a4a83bb65716c3dc3aecf71c5dd83`:
  full CI #1294, iOS gate #115 and Q003C/OpenXML gate #112 all passed.

## Boundaries

- This is adaptive navigation, not row/column deletion or materialization.
- Split panes retain their existing full physical extent.
- MAUI does not yet expose this desktop host opt-in.
- Final scrollbar thumb geometry remains a host responsibility because it
  depends on the actual body viewport and surrounding application chrome.

## Status

Implementation complete and exact-head validated at
`45fae777c58a4a83bb65716c3dc3aecf71c5dd83`.
