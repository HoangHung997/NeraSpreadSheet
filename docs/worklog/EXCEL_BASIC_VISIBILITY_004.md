# EXCEL-BASIC-VISIBILITY-004 worklog

## Scope

- Observe Excel desktop hide/unhide keyboard behavior.
- Add reusable sparse row/column visibility state and undoable SDK commands.
- Make WPF and WinForms keyboard navigation skip hidden ranges.
- Preserve hidden flags and retained custom sizes through XLSX round trips.
- Expose the commands in the desktop samples and external Win11 demo Ribbon.

## Excel observation

- Existing hidden rows 108-148: Down from `A107` selected `A149`; Up returned
  to `A107`.
- A temporary hidden column B: Right from `A107` selected `C107`; the temporary
  hide was undone immediately and the workbook was not saved.

## Implementation

- `WorksheetDimensions` owns normalized hidden row/column intervals and keeps
  raw custom sizes separate from effective zero size.
- Structural insert/delete and `WorksheetAxisMove` map hidden intervals without
  materializing the worksheet axis.
- `SpreadsheetAxisVisibilityController` participates in session undo/redo.
- `SpreadsheetVisibleCellNavigation` jumps across hidden intervals, and all
  WPF/WinForms normal and split keyboard paths use it.
- OpenXML imports and exports standard hidden rows/columns while retaining
  custom sizes.

## Validation

- Core solution: **1254/1254 passed**.
- Focused Core visibility tests: **2/2 passed**.
- Focused Editing visibility/navigation tests: **4/4 passed**.
- Focused Viewport hidden-axis tests: **3/3 passed**.
- Focused OpenXML hidden-axis round trip: **1/1 passed**; the full OpenXML
  suite is included in the green Core solution run.
- Focused loaded WPF/WinForms hidden-axis keyboard smokes: **2/2 passed**.
- Full solution build/analyzers: **0 warnings, 0 errors**.
- Architecture verification and SDK packaging metadata verification: **passed**.
- External Win11 demo build, internal smoke and supplied-workbook smoke:
  **passed**.
- Full Windows.Rendering: **54/56 passed locally**. The two failures are the
  same pre-existing environment-sensitive WPF checks: a 0.4-DIP automation
  scaling difference and inability to activate an off-screen test window.
  Neither touches axis visibility; exact-head GitHub CI remains the release
  gate.

Exact-head GitHub Actions evidence must be recorded before this lane is closed.
