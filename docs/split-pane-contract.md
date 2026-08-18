# Split-pane contract

This document locks the native NeraSpreadSheet split-pane semantics shared by layout, viewport, session state, XLSX serialization, WinForms and WPF hosts. Split panes are independent from freeze panes: a split creates multiple independently scrolled views of one worksheet, while freeze panes pin leading rows/columns inside each view.

## 1. Topology

Supported public modes:

- `None`: one `TopLeft` pane.
- `Vertical`: `TopLeft` and `TopRight`.
- `Horizontal`: `TopLeft` and `BottomLeft`.
- `Both`: `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight`.

Pane IDs are stable. A pane temporarily absent from a topology retains its stored scroll state so restoring that topology can restore its offset.

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

`SpreadsheetSplitViewportEngine` composes every visible pane through the shared `SpreadsheetViewportEngine`.

- All panes share one `SpreadsheetSession`, worksheet, selection and style catalog.
- Each pane receives its own scroll offsets and viewport size.
- Pane display lists are clipped to pane bounds and translated into common body coordinates.
- Separators and active-pane border are appended after pane content.
- Freeze panes remain internal to each pane and use the same four-region freeze compositor/cache semantics.
- Nested display lists retain immutable child-list references; split composition must not flatten-copy pane command arrays.

## 5. Hit testing and bounds

- Body hit testing first resolves split topology, then runs pane-local worksheet hit testing with that pane's scroll.
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

## 7. Split-aware row/column resizing

`SpreadsheetSplitHeaderResizeGeometry` is shared by WinForms and WPF.

- Row resize handles are exposed only by panes touching the left body edge.
- Column resize handles are exposed only by panes touching the top body edge.
- A split-separator continuation through a header band is not a row/column resize handle.
- Separator hit testing has priority over dimension resize hit testing.
- The returned edge coordinate is expressed in full-control coordinates so drag math is identical across hosts.
- Starting a resize activates the pane that supplied the handle.
- Live drag writes to shared sparse worksheet dimensions; all panes observe the same updated row height/column width.
- WinForms uses pointer capture and real Windows mouse messages on the child surface.
- WPF uses preview mouse routing and adorner mouse capture.
- Current dimension changes conservatively invalidate/recompose the split overlay; partial dirty projection is a separate optimization.

## 8. Per-worksheet split state

`SpreadsheetSplitViewState` is owned by `SpreadsheetViewController`.

It stores:

- topology;
- split X/Y coordinates;
- active pane;
- `TopLeft`, `TopRight`, `BottomLeft` and `BottomRight` scroll offsets.

Rules:

- State is independent per worksheet.
- Hidden-pane offsets remain stored.
- An active pane that is not visible in the current topology normalizes to `TopLeft`.
- Hosts persist the outgoing worksheet before `ActiveWorksheet` changes and restore the incoming state after activation.
- Source-tagged events prevent WinForms/WPF from recursively applying their own publication.
- Disabling a public split overlay persists the state; re-enabling without an explicit replacement mode restores it.
- Direct split changes are view state and are not standalone undo-history operations yet.

## 9. Structural row/column mapping

Persisted pane offsets participate in structural edit transactions.

- Row insertion/deletion maps Y offsets only.
- Column insertion/deletion maps X offsets only.
- Mapping uses exact pre-mutation sparse dimension metrics, not default-size approximations.
- Insert shifts offsets at or beyond the insertion boundary by the inserted physical extent.
- Delete collapses offsets inside the deleted interval to its leading edge and subtracts removed physical extent from later offsets.
- Split topology, split coordinates, active pane and the unaffected axis remain unchanged.
- Undo/redo restores exact pre/post split-state snapshots.
- Failed preflight and rollback leave split state unchanged and do not enter undo history.

## 10. XLSX representation

`NeraOpenXmlSpreadsheetSessionSerializer` serializes split view state.

- Compatible topology, split coordinates, active pane and top-left-cell behavior are written to standard SpreadsheetML `SheetView/Pane` metadata.
- Standard SpreadsheetML cannot represent four independent pane scroll offsets exactly.
- A Nera custom XML part stores the complete per-worksheet `SpreadsheetSplitViewState`.
- When the custom part is present and valid, it is the high-fidelity Nera source.
- When native metadata is absent, compatible standard split-pane metadata is imported into a Nera state.
- An unsplit default session emits neither a native split custom part nor a standard split pane.
- Unknown-part preservation is still unsupported and must not be implied by split-state support.

## 11. Public WinForms lifecycle

`NeraSpreadSheet.WinForms.NeraSpreadsheetSplitExtensions` overlays a Nera-owned child surface on the existing public control.

- `EnableSplitPanes` returns a reusable controller associated with the control.
- `DisableSplitPanes` removes and disposes the overlay, revealing the unchanged single-pane control.
- The child surface shares the owner's session, theme and selected rendering backend.
- `RenderNow` explicitly performs layout, creates the child handle and calls the selected renderer.
- Wheel and Shift+wheel target the pane under the pointer.
- Separator dragging uses pointer capture.
- Row/column header selection, dimension resizing and body selection activate the resolved pane.
- The reusable editor is clipped to the active pane and its freeze subregion.
- Backend switching supports GDI+, Direct2D HWND and D3D11/DXGI swap chain.

## 12. Public WPF lifecycle

`NeraSpreadSheet.Wpf.NeraSpreadsheetSplitExtensions` overlays a Nera-owned `Adorner`.

- The spreadsheet must be loaded under an `AdornerLayer`, normally provided by `AdornerDecorator`.
- The controller attaches on load, detaches on unload and can reattach on reload.
- `DisableSplitPanes` removes/disposes the adorner and restores the unchanged single-pane view.
- Session/backend/theme forwarding through the controller invalidates the split overlay.
- DrawingContext and Nera-owned shared-texture D3DImage render the same split display list.
- Wheel, Shift+wheel, selection, keyboard editing, separator drag and header resizing route through the active pane.
- The reusable WPF editor is arranged/clipped within active pane and freeze geometry.

## 13. Runtime gates

Split panes are not considered implemented solely because layout tests compile.

Required tests include:

- Layout topology, clamping and separator/pane hit regions.
- Independent fractional pane scroll and targeted deltas.
- Hidden-pane persistence and active-pane fallback.
- Pane-local worksheet hit testing, merged anchors and common-coordinate cell bounds.
- Shared split chrome labels, translations, separator continuation and invalid metadata rejection.
- Per-worksheet split state, host source-loop prevention and enable/disable restoration.
- Structural insert/delete mapping, exact undo/redo and failed-operation invariance.
- Standard/native XLSX split-state round trip and default-session metadata absence.
- Shared split-header resize geometry, separator precedence and invalid metadata rejection.
- Public WinForms STA smoke across GDI+, Direct2D HWND and DXGI, including render, hit test, disposal and real mouse-message dimension resizing.
- Public WPF STA smoke inside an `AdornerDecorator` across DrawingContext and D3DImage, including DirectWrite cache reuse, host resize application and clean shutdown.

## 14. Current exclusions

The following are deliberately not claimed yet:

- Per-pane scrollbars.
- Header drag reordering.
- Split-aware dirty-region projection/partial invalidation.
- Standalone undo/redo commands for direct split-view changes.
- MAUI/Skia split host.

These exclusions must remain visible in status and PR documents until executable code and the relevant CI/runtime gates exist.
