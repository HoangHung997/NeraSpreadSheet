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
- Dimension and metric changes conservatively invalidate/recompose the affected host because they can move every later row/column edge.

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

## 11. Per-pane scrollbar geometry and interaction

`SpreadsheetSplitScrollBarGeometry` and `SpreadsheetSplitScrollBarInteractionController` are platform-neutral.

- A visible pane receives a horizontal bar only when content width exceeds that pane's width and a vertical bar only when content height exceeds that pane's height.
- Scrollbar coordinates are body-local and expressed as `double`.
- Track length, proportional thumb length, minimum thumb length, margin, thickness and hit slop come from `SpreadsheetSplitScrollBarStyle`.
- Horizontal and vertical bars reserve the bottom-right corner from one another when both are present.
- Thumb hit testing has priority over track hit testing.
- Track hits distinguish before-thumb and after-thumb paging.
- Thumb dragging stores its grab offset so the thumb does not jump when capture begins.
- Pointer-to-offset mapping clamps continuously to `[0, MaximumOffset]` and never snaps to row/column boundaries.
- A scrollbar request contains one pane ID, one axis and one offset. Applying it must preserve the other axis and every other pane.
- The active pane uses the active-thumb style; activating or dragging a scrollbar makes its pane active.
- Hidden/non-scrollable/too-short tracks are omitted rather than exposing inert geometry.
- Scrollbar input publishes the resulting pane offset into the per-worksheet split view state.

## 12. Public optional scrollbar lifecycle

Scrollbars are optional Nera-owned overlays and do not alter the split topology or permanently reserve worksheet viewport space.

### WinForms

- `EnableSplitPaneScrollBars` creates or reuses one public controller associated with the spreadsheet control.
- The overlay is parented to the Nera split child surface and exposes hit-test regions only around scrollbar tracks/thumbs.
- `DisableSplitPaneScrollBars` removes/disposes the overlay without disabling split panes.
- The public controller exposes visibility, style, layout, count, body-local hit testing and explicit refresh.
- Pointer capture supports thumb drag; track click and wheel input target the resolved pane.
- The overlay refreshes after split render, scroll, topology and host resize changes.

### WPF

- `EnableSplitPaneScrollBars` creates or reuses a separate Nera-owned `Adorner` above the split adorner.
- The host must provide an `AdornerLayer` through an `AdornerDecorator` or equivalent.
- `DisableSplitPaneScrollBars` removes/disposes only the scrollbar adorner.
- The public controller exposes the same visibility/style/layout/count/hit-test/refresh concepts as WinForms.
- WPF routed input and mouse capture own the drag; the adorner rebuilds from a freshly rendered split frame after topology, offset and size changes.
- Scrollbar geometry remains available after DrawingContext/D3DImage backend switches and across load/unload lifecycle.

## 13. Split-aware dirty-region projection

`SpreadsheetSplitViewportDirtyRegionExtensions.ProjectDirtyRange` maps a worksheet range to body-local rectangles.

- Projection requires a current split frame; absent frame data requests full invalidation.
- The input range expands transitively across every intersecting merged range.
- A range crossing frozen rows or frozen columns is divided at those boundaries before projection.
- Every subrange is projected separately into every visible pane using that pane's scroll state.
- Each rectangle is clipped to the correct frozen corner, frozen-row, frozen-column or scrolling-body subregion.
- Empty offscreen results are omitted.
- If any required endpoint cannot be projected safely, the whole request becomes conservative full invalidation.
- Projection represents cell/range content changes only. Structural, metric, topology, theme and device-lifecycle changes remain full invalidations.

Backend policy:

- WinForms GDI+ and Direct2D HWND consume projected rectangles through partial control invalidation.
- D3D11/DXGI `FlipDiscard` intentionally presents a full frame because partial Windows invalidation does not define partial swap-chain presentation semantics.
- WPF D3DImage forwards multiple native dirty rectangles to the shared-texture surface.
- WPF DrawingContext intentionally uses full visual invalidation.
- Hosts expose diagnostic counts and the last projected region set so runtime tests can verify partial/full behavior.

## 14. Public WinForms split lifecycle

`NeraSpreadSheet.WinForms.NeraSpreadsheetSplitExtensions` overlays a Nera-owned child surface on the existing public control.

- `EnableSplitPanes` returns a reusable controller associated with the control.
- `DisableSplitPanes` removes and disposes the overlay, revealing the unchanged single-pane control.
- The child surface shares the owner's session, theme and selected rendering backend.
- `RenderNow` explicitly performs layout, creates the child handle and calls the selected renderer.
- Wheel and Shift+wheel target the pane under the pointer.
- Separator dragging uses pointer capture.
- Row/column header selection, dimension resizing, scrollbar input and body selection activate the resolved pane.
- The reusable editor is clipped to the active pane and its freeze subregion.
- Backend switching supports GDI+, Direct2D HWND and D3D11/DXGI swap chain.

## 15. Public WPF split lifecycle

`NeraSpreadSheet.Wpf.NeraSpreadsheetSplitExtensions` overlays a Nera-owned split `Adorner`.

- The spreadsheet must be loaded under an `AdornerLayer`, normally provided by `AdornerDecorator`.
- The controller attaches on load, detaches on unload and can reattach on reload.
- `DisableSplitPanes` removes/disposes the split adorner and restores the unchanged single-pane view.
- Session/backend/theme forwarding through the controller invalidates the split overlay.
- DrawingContext and Nera-owned shared-texture D3DImage render the same split display-list semantics.
- Wheel, Shift+wheel, selection, keyboard editing, separator drag, header resizing and optional scrollbar input route through the active pane.
- The reusable WPF editor is arranged/clipped within active pane and freeze geometry.

## 16. Runtime gates

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
- Shared scrollbar geometry, style validation, thumb mapping, track paging, targeted request application and other-pane invariance.
- Public WinForms scrollbar smoke through real Windows mouse messages.
- Public WPF scrollbar smoke through native OS cursor/button input, routed hit testing and mouse capture; it must persist only the target pane and survive D3DImage rendering.
- Dirty-range merged/freeze projection, offscreen omission and conservative full fallback.
- WinForms dirty-region runtime smoke proving partial GDI+/Direct2D invalidation and full `FlipDiscard` fallback.
- WPF dirty-region runtime smoke proving D3DImage dirty rectangles and full DrawingContext fallback.
- Public WinForms STA smoke across GDI+, Direct2D HWND and DXGI, including render, hit test, disposal and real mouse-message dimension resizing.
- Public WPF STA smoke inside an `AdornerDecorator` across DrawingContext and D3DImage, including DirectWrite cache reuse, host resize application, repeated unload/reload and clean shutdown.

## 17. Current exclusions

The following are deliberately not claimed yet:

- Header drag reordering.
- Standalone undo/redo commands for direct split-view changes.
- Sparse whole-axis styles.
- Split/scrollbar controls exposed in the desktop sample toolbars.
- Production MAUI/Skia split host.

These exclusions must remain visible in status and PR documents until executable code and the relevant CI/runtime gates exist.
