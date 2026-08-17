# Split-pane contract

This document locks the native NeraSpreadSheet split-pane semantics shared by layout, viewport, WinForms and WPF hosts. Split panes are independent from freeze panes: a split creates multiple independently scrolled views of the same worksheet, while freeze panes pin leading rows/columns inside each view.

## 1. Topology

Supported public modes:

- `None`: one `TopLeft` pane.
- `Vertical`: `TopLeft` and `TopRight`.
- `Horizontal`: `TopLeft` and `BottomLeft`.
- `Both`: `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight`.

Pane IDs are stable. A pane temporarily absent from a topology retains its controller state so that restoring the topology can restore its scroll offset.

## 2. Geometry

`SpreadsheetSplitLayoutEngine` owns platform-neutral geometry.

- Split coordinates are body-local pixel coordinates stored as `double`.
- Separator thickness must be finite and positive.
- Minimum pane extent must be finite and positive.
- A requested split is clamped so both panes meet the minimum extent.
- If the viewport cannot fit two minimum panes plus the separator, that axis is disabled.
- Pane and separator hit testing uses half-open bounds to avoid duplicate edge ownership.
- Hit regions distinguish pane, vertical separator, horizontal separator and separator intersection.

Header bands are not part of the body viewport. Desktop chrome translates body geometry by row-header width and column-header height.

## 3. Independent continuous scrolling

Each pane owns an independent `ContinuousScrollController`.

- X/Y offsets and targets are `double`.
- No row/column snapping is introduced by split mode.
- Precision/touch input applies direct pixel deltas.
- Wheel/programmatic animated input uses the shared frame-response model.
- Bounds are computed independently from content extent minus that pane's viewport extent.
- Scrolling one pane must not mutate another pane.
- Hidden pane state persists and is re-clamped when the pane returns.
- If the active pane disappears, active pane falls back to `TopLeft`.

## 4. Composition

`SpreadsheetSplitViewportEngine` composes each visible pane through the shared `SpreadsheetViewportEngine`.

- All panes share the same `SpreadsheetSession`, worksheet, selection and style catalog.
- Each pane receives its own scroll offsets and viewport size.
- Pane display lists are clipped to pane bounds and translated into common body coordinates.
- Separators and active-pane border are appended after pane content.
- Freeze panes remain internal to each pane and use the same four-region freeze compositor/cache semantics.
- Nested display lists retain immutable child-list references; split composition must not flatten-copy pane command arrays.

## 5. Hit testing and bounds

- Body hit test first resolves split topology, then runs pane-local worksheet hit testing with that pane's scroll.
- Merged-cell hit testing resolves to the merged top-left anchor.
- Cell/editor bounds are computed pane-locally and translated into common body/control coordinates.
- Separator coordinates never resolve to worksheet cells.
- Top-edge panes supply column-header hit testing.
- Left-edge panes supply row-header hit testing.

## 6. Shared chrome

`SpreadsheetSplitChromeDisplayListComposer` owns desktop split headers.

- Single-pane and split-pane paths share one internal header renderer.
- Top-edge panes render column labels.
- Left-edge panes render row labels.
- Vertical separator continues through the column-header band.
- Horizontal separator continues through the row-header band.
- Split chrome validates one metadata entry per pane with matching ID, bounds and viewport size.
- Disabling headers returns the original body display list without a wrapper allocation.

## 7. Public WinForms lifecycle

`NeraSpreadSheet.WinForms.NeraSpreadsheetSplitExtensions` overlays a Nera-owned child surface on the existing public control.

- `EnableSplitPanes` returns a reusable controller associated with the control.
- `DisableSplitPanes` removes and disposes the overlay, revealing the unchanged single-pane control.
- The child surface shares the owner's session, theme and selected rendering backend.
- `RenderNow` explicitly performs layout, creates the child handle and calls the selected renderer.
- Wheel and Shift+wheel target the pane under the pointer.
- Separator dragging uses pointer capture.
- Row/column header selection and body selection activate the resolved pane.
- The reusable editor is clipped to the active pane and its freeze subregion.
- Backend switching supports GDI+, Direct2D HWND and D3D11/DXGI swap chain.

## 8. Public WPF lifecycle

`NeraSpreadSheet.Wpf.NeraSpreadsheetSplitExtensions` overlays a Nera-owned `Adorner`.

- The spreadsheet must be loaded under an `AdornerLayer`, normally provided by `AdornerDecorator`.
- The controller attaches on load, detaches on unload and can reattach on reload.
- `DisableSplitPanes` removes/disposes the adorner and restores the unchanged single-pane view.
- Session/backend/theme forwarding through the controller invalidates the split overlay.
- DrawingContext and Nera-owned shared-texture D3DImage render the same split display list.
- Wheel, Shift+wheel, selection, keyboard editing and separator drag route through the active pane.
- The reusable WPF editor is arranged/clipped within active pane and freeze geometry.

## 9. Runtime gates

Split panes are not considered implemented solely because layout tests compile.

Required tests:

- Layout topology, clamping and separator/pane hit regions.
- Independent fractional pane scroll and targeted deltas.
- Hidden-pane persistence and active-pane fallback.
- Pane-local worksheet hit testing, merged anchors and common-coordinate cell bounds.
- Shared split chrome labels, translations, separator continuation and invalid metadata rejection.
- Public WinForms STA smoke across GDI+, Direct2D HWND and DXGI, including render, resize, hit test and disposal.
- Public WPF STA smoke inside an `AdornerDecorator` across DrawingContext and D3DImage, including DirectWrite cache reuse, resize, hit test and clean shutdown.

## 10. Current exclusions

The following are deliberately not claimed yet:

- Per-pane scrollbars.
- Split-aware row/column resize handles and header reordering.
- Split-aware dirty-region optimization; current public split hosts conservatively invalidate the overlay.
- Split topology/coordinates/active-pane/scroll persistence in worksheet view state.
- Undo/redo or structural insert/delete mapping for persisted split state.
- XLSX serialization of split state.
- MAUI/Skia split host.

These exclusions must remain visible in status/PR documents until executable code and the relevant CI/runtime gates exist.
