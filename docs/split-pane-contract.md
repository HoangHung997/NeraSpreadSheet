# Split-pane contract

This document locks native NeraSpreadSheet split-pane semantics shared by layout, viewport, session state, XLSX serialization, WinForms and WPF. Split panes are independent from freeze panes: a split creates multiple independently scrolled views of one worksheet, while freeze panes pin leading rows/columns inside each view.

## 1. Topology and geometry

Supported modes:

- `None`: `TopLeft` only.
- `Vertical`: `TopLeft`, `TopRight`.
- `Horizontal`: `TopLeft`, `BottomLeft`.
- `Both`: all four stable pane IDs.

Rules:

- Split coordinates are body-local `double` pixel coordinates.
- Separator thickness and minimum pane extent must be finite and positive.
- Requested coordinates are clamped so both panes meet minimum extent.
- An axis is disabled when its viewport cannot contain two minimum panes and a separator.
- Pane/separator hit testing uses half-open bounds and distinguishes vertical separator, horizontal separator and their intersection.
- Header bands lie outside the body viewport and translate body geometry by row-header width/column-header height.

## 2. Independent continuous scrolling

Every pane owns one `ContinuousScrollController`.

- X/Y offsets and targets remain `double`.
- No row/column snapping is introduced.
- Precision/touch input applies pixel deltas; wheel/programmatic animation uses shared frame physics.
- Bounds are computed independently from content extent minus that pane's viewport extent.
- Scrolling one pane cannot mutate another.
- Hidden pane offsets remain stored and are re-clamped when topology restores them.
- If the active pane disappears, active pane falls back to `TopLeft`.

## 3. Composition and freeze panes

`SpreadsheetSplitViewportEngine` composes each visible pane through the shared `SpreadsheetViewportEngine`.

- Panes share one `SpreadsheetSession`, worksheet, selection and style catalog.
- Each pane receives its own offsets and viewport size.
- Pane display lists are clipped and translated into common body coordinates.
- Freeze corner/rows/columns/body composition remains internal to each pane.
- Separators, active-pane border and integrated pane scrollbars are appended after pane content.
- Nested display lists retain immutable child references and must not be flatten-copied.

## 4. Hit testing, bounds and shared chrome

- Body hit testing resolves topology first, then runs pane-local worksheet hit testing with that pane's offsets.
- Merged-cell hits resolve to the merged top-left anchor.
- Cell/editor bounds are calculated pane-locally and translated into common body/control coordinates.
- Separators never resolve to worksheet cells.
- Top-edge panes supply column headers; left-edge panes supply row headers.
- Vertical separators continue through column headers; horizontal separators continue through row headers.
- Split chrome validates unique pane metadata, matching IDs/bounds and matching viewport sizes.

## 5. Row/column dimension resizing

`SpreadsheetSplitHeaderResizeGeometry` is shared by WinForms and WPF.

- Left-edge panes expose row resize handles.
- Top-edge panes expose column resize handles.
- Separator continuation through a header band is not a resize handle.
- Separator and scrollbar hit regions take priority.
- Returned edge coordinates use full-control coordinates.
- Starting resize activates the source pane.
- Live drag updates shared sparse worksheet dimensions; every pane observes the same metric.
- Dimension changes conservatively recompose the host because later row/column edges may move.

## 6. Per-worksheet split state

`SpreadsheetSplitViewState` stores:

- topology;
- split X/Y;
- active pane;
- offsets for all four pane IDs.

State is independent per worksheet. Hidden-pane offsets remain stored. Source-tagged change events prevent desktop feedback loops. Disabling/re-enabling a split overlay restores the stored state rather than replacing it with defaults.

Direct split-view changes are not standalone undo-history operations yet. Structural edits and axis reorders include split snapshots in their transactions.

## 7. Structural insert/delete mapping

- Row insertion/deletion maps pane Y offsets.
- Column insertion/deletion maps pane X offsets.
- Mapping uses exact pre-mutation sparse metrics.
- Insert shifts offsets at/after the insertion boundary by inserted physical extent.
- Delete collapses offsets inside the deleted interval and subtracts deleted extent from later offsets.
- The unaffected axis, topology, split coordinates and active pane remain unchanged.
- Undo/redo restores exact pre/post split snapshots.
- Failed preflight/rollback leaves split state unchanged and creates no history entry.

## 8. Axis reorder mapping

A fixed-length row/column reorder is a permutation, not insert/delete.

For every pane, the affected offset preserves the identity of the top-left row/column and its fractional local pixel offset. Exact sparse metrics before and after the move are used. The unaffected axis, topology, split coordinates and active pane remain unchanged.

Complete semantics, formula mapping, merged/freeze rules, selection mapping and desktop drag behavior are locked in `docs/header-reordering-contract.md`.

## 9. XLSX representation

`NeraOpenXmlSpreadsheetSessionSerializer` serializes per-worksheet split state.

- Compatible topology, coordinates, active pane and top-left-cell behavior are written to standard SpreadsheetML `SheetView/Pane` metadata.
- Standard SpreadsheetML cannot represent four independent pane offsets exactly.
- A Nera custom XML part preserves the complete state.
- Valid native metadata is the high-fidelity Nera source.
- Compatible standard metadata is imported when native metadata is absent.
- A default unsplit session emits neither native nor standard pane metadata.
- Unknown-part preservation remains unsupported.

## 10. Integrated and optional pane scrollbars

Integrated scrollbars are composed into the split display list when `SpreadsheetRenderTheme.ShowSplitPaneScrollBars` is enabled.

- Each scrollable pane may receive horizontal and/or vertical bars.
- Buttons, track, proportional thumb, maximum offset and active styling are pane-local.
- Input supports line steps, page steps and continuous thumb drag.
- A request targets exactly one pane and one axis.
- Horizontal/vertical bars reserve their shared bottom-right corner.

Optional public overlay controllers also exist for WinForms and WPF and expose enable/disable, visibility, style, layout, hit testing and refresh. They must not be enabled simultaneously with an indistinguishable duplicate integrated presentation in product UI.

## 11. Split-aware dirty regions

`SpreadsheetSplitViewportDirtyRegionExtensions.ProjectDirtyRange` maps changed cell/range content into body-local rectangles.

- A current split frame is required; otherwise full invalidation is requested.
- Ranges expand transitively through intersecting merges.
- Ranges split at freeze-row/freeze-column boundaries.
- Every subrange projects through each pane's local offsets and clips to its correct freeze subregion.
- Empty offscreen results are omitted.
- Unsafe/unprojectable cases request conservative full invalidation.

Backend policy:

- WinForms GDI+ and Direct2D HWND consume partial invalidation.
- DXGI `FlipDiscard` uses a full frame.
- WPF D3DImage accepts multiple native dirty rectangles.
- WPF DrawingContext uses full visual invalidation.

## 12. Header drag reordering in split hosts

Shared source/drop/threshold geometry is implemented through `SpreadsheetSplitHeaderReorderGeometry`.

Input priority:

1. pane scrollbar;
2. split separator;
3. dimension resize;
4. header reorder;
5. ordinary header selection.

WinForms uses actual message `wParam` button state, pointer capture and display-list preview. WPF uses preview routed input, mouse capture and a lightweight `DrawingVisual` preview above DrawingContext/D3DImage content. Both commit through `SpreadsheetSession.Reorder` and preserve atomic undo/redo semantics.

The unsplit public-control drag path and drag-edge auto-scroll remain separate follow-up work; programmatic reorder is host-independent.

## 13. Public WinForms lifecycle

- `EnableSplitPanes` creates/reuses a Nera-owned child surface.
- `DisableSplitPanes` removes/disposes it and reveals the unchanged single-pane control.
- The child shares session, theme and selected rendering backend.
- `RenderNow` performs layout, creates the child handle and invokes the selected renderer explicitly.
- Wheel/Shift+wheel, selection, editor, separator drag, dimension resize, pane scrollbar and header reorder route through resolved pane geometry.
- GDI+, Direct2D HWND and D3D11/DXGI consume the same split semantics.

## 14. Public WPF lifecycle

- `EnableSplitPanes` creates/reuses a Nera-owned split `Adorner`.
- The host supplies an `AdornerLayer`, normally through `AdornerDecorator`.
- The controller attaches on load, detaches on unload and reattaches on reload.
- `DisableSplitPanes` removes/disposes the split adorner.
- DrawingContext and Nera-owned shared-texture D3DImage consume the same split semantics.
- Wheel, selection, keyboard editing, editor, separator drag, dimension resize, pane scrollbar and header reorder route through active/resolved pane geometry.

## 15. Runtime gates

Split functionality is not accepted on compile-only evidence. Required gates include:

- topology/clamping/separator hit tests;
- independent fractional pane scrolling and hidden-pane persistence;
- pane-local cell hit/bounds and merge-anchor tests;
- shared chrome metadata/translation tests;
- split-state persistence, structural mapping and XLSX round trip;
- resize geometry and real desktop input tests;
- scrollbar geometry/interaction plus WinForms/WPF native-input tests;
- dirty-region projection plus backend partial/full runtime tests;
- axis-reorder permutation/formula/transaction tests;
- shared reorder geometry/preview tests;
- WinForms real-message row and column reorder tests;
- WPF native-pointer reorder plus post-move D3DImage presentation;
- full Windows build/tests/GPU runtime gate;
- cross-platform Core build/tests and architecture verification.

## 16. Current exclusions

- Unsplit-control header drag UI.
- Auto-scroll during header reorder drag.
- Standalone undo/redo commands for direct split-view changes.
- Production MAUI/Skia split host.
- Structured/shared/dynamic-array formula rewrite semantics.
