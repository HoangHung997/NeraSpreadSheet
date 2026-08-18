# Split-pane contract

This document locks the native NeraSpreadSheet split-pane semantics shared by layout, viewport, session state, XLSX serialization, desktop hosts and samples. Split panes are independent from freeze panes: a split creates multiple independently scrolled views of one worksheet, while freeze panes pin leading rows/columns inside each view.

## 1. Topology

Supported public modes:

- `None`: one `TopLeft` pane.
- `Vertical`: `TopLeft` and `TopRight`.
- `Horizontal`: `TopLeft` and `BottomLeft`.
- `Both`: `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight`.

Pane IDs are stable. A temporarily absent pane retains its stored scroll state so restoring its topology can restore its offset.

## 2. Geometry

`SpreadsheetSplitLayoutEngine` owns platform-neutral geometry.

- Split coordinates are body-local pixel coordinates stored as `double`.
- Separator thickness and minimum pane extent must be finite and positive.
- A requested split is clamped so both panes meet the minimum extent.
- If the viewport cannot fit two minimum panes plus the separator, that axis is disabled.
- Pane and separator hit testing uses half-open bounds to avoid duplicate edge ownership.
- Hit regions distinguish pane, vertical separator, horizontal separator and separator intersection.
- Header bands are outside the body viewport; desktop chrome translates body geometry by row-header width and column-header height.

## 3. Independent continuous scrolling

Each pane owns an independent `ContinuousScrollController`.

- X/Y offsets and targets are `double`.
- Split mode never introduces row/column snapping.
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
- Freeze panes remain internal to each pane and use the shared frozen-corner/row/column/body semantics.
- Nested display lists retain immutable child-list references; split composition must not flatten-copy pane command arrays.

## 5. Hit testing and cell bounds

- Body hit testing resolves split topology first, then runs pane-local worksheet hit testing with that pane's scroll.
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
- Returned edge coordinates are in full-control coordinates so drag math is identical across hosts.
- Starting a resize activates the pane that supplied the handle.
- Live drag writes to shared sparse worksheet dimensions; all panes observe the new size immediately.
- WinForms uses pointer capture and real Windows mouse messages.
- WPF uses preview mouse routing and adorner mouse capture.
- Dimension/metric changes conservatively invalidate the host because they can move every later row/column edge.

## 8. Per-worksheet split state

`SpreadsheetSplitViewState` is owned by `SpreadsheetViewController` and stores:

- topology;
- split X/Y coordinates;
- active pane;
- `TopLeft`, `TopRight`, `BottomLeft` and `BottomRight` scroll offsets.

Rules:

- State is independent per worksheet.
- Hidden-pane offsets remain stored.
- An active pane that is not visible normalizes to `TopLeft`.
- Hosts persist the outgoing worksheet before `ActiveWorksheet` changes and restore the incoming state afterward.
- Source-tagged events prevent WinForms/WPF from recursively applying their own publication.
- Disabling a public split overlay persists the state; re-enabling without an explicit replacement mode restores it.
- Direct split changes are view state and are not standalone undo-history operations yet.

## 9. Structural row/column mapping

Persisted pane offsets participate in structural edit transactions.

- Row insertion/deletion maps Y offsets only.
- Column insertion/deletion maps X offsets only.
- Mapping uses exact pre-mutation sparse metrics, not default-size approximations.
- Insert shifts offsets at or beyond the insertion boundary by the inserted physical extent.
- Delete collapses offsets inside the removed interval to its leading edge and subtracts removed extent from later offsets.
- Split topology, split coordinates, active pane and the unaffected axis remain unchanged.
- Undo/redo restores exact pre/post split-state snapshots.
- Failed preflight and rollback leave split state unchanged and do not enter undo history.

## 10. XLSX representation

`NeraOpenXmlSpreadsheetSessionSerializer` serializes split view state.

- Compatible topology, split coordinates, active pane and top-left-cell behavior are written to standard SpreadsheetML `SheetView/Pane` metadata.
- Standard SpreadsheetML cannot represent four independent pane offsets exactly.
- A Nera custom XML part stores the complete per-worksheet `SpreadsheetSplitViewState`.
- When valid native metadata exists, it is the high-fidelity Nera source.
- When native metadata is absent, compatible standard pane metadata is imported.
- An unsplit default session emits neither a native split custom part nor a standard split pane.
- Unknown-part preservation remains unsupported and must not be implied by split-state support.

## 11. Integrated pane scrollbar geometry

The split viewport can compose pane scrollbars directly into its shared display list. Visibility is controlled by `SpreadsheetRenderTheme.ShowSplitPaneScrollBars`.

- A visible pane receives a horizontal bar only when content width exceeds pane width and a vertical bar only when content height exceeds pane height.
- Coordinates are body-local `double` values.
- Theme values control background, track, button area, thumb, active thumb, border, glyph, thickness, button extent, minimum thumb extent, line step and page factor.
- Horizontal and vertical bars reserve their shared bottom-right corner.
- Hit testing distinguishes decrease button, increase button, thumb, track-before-thumb and track-after-thumb.
- Button input applies the configured line step.
- Track input applies the configured page factor.
- Thumb drag stores its grab offset, clamps continuously to `[0, MaximumOffset]` and never snaps to row/column boundaries.
- A request targets one pane and one axis while preserving the other axis and every other pane.
- Activating or dragging a scrollbar makes its pane active.
- Scroll input publishes the resulting pane offset into the per-worksheet split state.
- Integrated bars share the worksheet display-list semantics and therefore render through GDI+, DrawingContext, Direct2D HWND, DXGI and D3DImage paths according to each host's backend policy.

## 12. Public optional scrollbar overlays

In addition to integrated bars, public overlay controllers are available for hosts that want separately styled/lifecycled scrollbar chrome. Applications must avoid enabling duplicate visual systems unintentionally.

### WinForms

- `EnableSplitPaneScrollBars` creates or reuses one controller associated with the spreadsheet control.
- The overlay is parented to the Nera split child surface and exposes hit-test regions only around tracks/thumbs.
- `DisableSplitPaneScrollBars` removes/disposes only the overlay.
- The controller exposes visibility, style, layout, count, body-local hit testing and refresh.
- Pointer capture supports thumb drag; track click and wheel target the resolved pane.

### WPF

- `EnableSplitPaneScrollBars` creates or reuses a separate Nera-owned adorner above the split adorner.
- The host must provide an `AdornerLayer`, normally through `AdornerDecorator`.
- `DisableSplitPaneScrollBars` removes/disposes only the scrollbar adorner.
- The controller exposes the same visibility/style/layout/count/hit-test/refresh concepts.
- Routed input and mouse capture own drag behavior.
- Layout rebuilds from a freshly rendered split frame after topology, offset and host-size changes and survives DrawingContext/D3DImage switches and load/unload.

## 13. Split-aware dirty-region projection

`SpreadsheetSplitViewportDirtyRegionExtensions.ProjectDirtyRange` maps a worksheet range to body-local rectangles.

- Projection requires a current split frame; absent frame data requests full invalidation.
- The input range expands transitively across intersecting merged ranges.
- A range crossing frozen rows or columns is divided at those boundaries before projection.
- Every subrange is projected into every visible pane using that pane's scroll state.
- Each rectangle is clipped to the correct frozen corner, frozen-row, frozen-column or scrolling-body subregion.
- Empty offscreen results are omitted.
- If any required endpoint cannot be projected safely, the request becomes conservative full invalidation.
- Projection covers cell/range content changes only; structural, metric, topology, theme and device-lifecycle changes remain full invalidations.

Backend policy:

- WinForms GDI+ and Direct2D HWND consume projected rectangles through partial invalidation.
- D3D11/DXGI `FlipDiscard` intentionally presents a full frame.
- WPF D3DImage forwards multiple native dirty rectangles to the shared-texture surface.
- WPF DrawingContext intentionally uses full visual invalidation.
- Hosts expose diagnostic counts and the last region set for runtime verification.

## 14. Public WinForms split lifecycle

- `EnableSplitPanes` returns a reusable controller associated with the public control.
- `DisableSplitPanes` removes/disposes the child split surface and reveals the unchanged single-pane control.
- The surface shares owner session, theme and rendering backend.
- `RenderNow` performs layout, creates the handle and calls the selected renderer explicitly.
- Wheel/Shift+wheel, separator drag, row/column header selection, resize, scrollbar input and body selection route through the resolved pane.
- One reusable editor is clipped to the active pane and its freeze subregion.
- Backend switching supports GDI+, Direct2D HWND and D3D11/DXGI swap chain.

## 15. Public WPF split lifecycle

- `EnableSplitPanes` overlays a Nera-owned split adorner under an `AdornerLayer`/`AdornerDecorator`.
- The controller attaches on load, detaches on unload and can reattach on reload.
- `DisableSplitPanes` removes/disposes the split adorner and restores the unchanged single-pane view.
- Session/backend/theme forwarding invalidates the split host.
- DrawingContext and Nera-owned shared-texture D3DImage render the same split semantics.
- Wheel, selection, keyboard editing, separator drag, header resizing and scrollbar input route through the active pane.
- One reusable WPF editor is arranged/clipped within active pane and freeze geometry.

## 16. Desktop sample exposure

Both desktop samples must expose the implemented split/scrollbar behavior rather than keeping it hidden behind test-only APIs.

- Toolbars expose `Split V`, `Split H`, `Split 4` and `Clear Split`.
- A checkable `Pane Scrollbars` control changes `SpreadsheetRenderTheme.ShowSplitPaneScrollBars`.
- Samples use the integrated display-list bars; they do not automatically add a second optional overlay controller.
- Enabling pane scrollbars while no split is active switches to `Both` so the pane-local behavior is immediately observable.
- Disabling bars changes chrome visibility only and does not destroy stored pane offsets/topology.
- Sample data includes a sparse extent at row 181/column 41 so horizontal and vertical bars are scrollable without dense materialization.
- Diagnostics display the current split/active pane and composed scrollbar count.
- XLSX Open/Save continues to use the session serializer; scrollbar theme visibility is host presentation state, while pane topology/offsets remain workbook-session view state.
- A sample-level STA smoke must open the actual WinForms form and WPF window, operate their real controls and verify visibility, automatic `Both` topology, four panes and at least eight composed bars.

## 17. Runtime gates

Split panes and pane scrollbars are not considered implemented solely because geometry tests compile.

Required tests include:

- Layout topology, clamping and separator/pane hit regions.
- Independent fractional pane scroll and targeted deltas.
- Hidden-pane persistence and active-pane fallback.
- Pane-local worksheet hit testing, merged anchors and common-coordinate cell bounds.
- Shared split chrome labels, translations, separator continuation and invalid metadata rejection.
- Per-worksheet state, host feedback-loop prevention and enable/disable restoration.
- Structural mapping, exact undo/redo and failed-operation invariance.
- Standard/native XLSX split-state round trip and default-session metadata absence.
- Shared split-header resize geometry, separator precedence and invalid metadata rejection.
- Integrated/public scrollbar geometry, style validation, thumb mapping, buttons/track paging, targeted application and other-pane invariance.
- Public WinForms scrollbar smoke through real Windows mouse messages.
- Public WPF scrollbar smoke through native OS pointer input, routed hit testing and mouse capture; it must persist only the target pane and survive D3DImage rendering.
- Dirty-range merged/freeze projection, offscreen omission and conservative full fallback.
- WinForms dirty-region smoke proving partial GDI+/Direct2D invalidation and full `FlipDiscard` fallback.
- WPF dirty-region smoke proving D3DImage dirty rectangles and full DrawingContext fallback.
- Public WinForms STA smoke across all three rendering backends, including hit testing, disposal and mouse-message dimension resizing.
- Public WPF STA smoke inside an `AdornerDecorator`, including DrawingContext, D3DImage, DirectWrite reuse, host resize, repeated unload/reload and clean shutdown.
- Actual desktop sample projects included in the Windows test dependency graph plus real sample-control interaction smoke.

## 18. Current exclusions

The following are deliberately not claimed yet:

- Header drag reordering.
- Standalone undo/redo commands for direct split-view changes.
- Sparse whole-axis styles.
- Production MAUI/Skia split host.

These exclusions must remain visible in status and PR documents until executable code and the relevant CI/runtime gates exist.
