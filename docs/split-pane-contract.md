# Split-pane contract

This document locks NeraSpreadSheet split-pane semantics shared by layout, viewport, session state, XLSX serialization, WinForms and WPF. Split panes create independently scrolled views of one worksheet; freeze panes pin leading rows/columns inside each view.

## 1. Topology and geometry

- `None`: `TopLeft` only.
- `Vertical`: `TopLeft`, `TopRight`.
- `Horizontal`: `TopLeft`, `BottomLeft`.
- `Both`: all four stable pane IDs.
- Split coordinates are body-local `double` pixels.
- Separator thickness and minimum pane extent must be finite and positive.
- Requested coordinates are clamped; an axis is disabled when it cannot fit two minimum panes plus separator.
- Pane/separator hit testing uses half-open bounds and distinguishes separator intersection.
- Header bands are outside the body viewport.

## 2. Independent scrolling and composition

- Every pane owns one `ContinuousScrollController`.
- Offsets remain continuous `double`; no row/column snapping.
- Precision/touch, wheel and programmatic inputs can target one pane without moving another.
- Hidden pane offsets remain stored and re-clamp when topology restores them.
- Unavailable active panes fall back to `TopLeft`.
- Each pane composes through the shared `SpreadsheetViewportEngine` with its own offsets and viewport size.
- Freeze corner/rows/columns/body composition remains internal to each pane.
- Pane lists are clipped/translated into common body coordinates; nested display lists are retained, not flatten-copied.

## 3. Hit testing, headers and resizing

- Body hit testing resolves pane topology before worksheet hit testing.
- Merged-cell hits resolve to the merged top-left anchor.
- Top-edge panes supply column headers; left-edge panes supply row headers.
- Vertical separators continue through column headers; horizontal separators continue through row headers.
- `SpreadsheetSplitHeaderResizeGeometry` is shared by desktop hosts.
- Pane scrollbars and split separators take priority over resize handles.
- Live row/column resize updates shared sparse metrics and conservatively recomposes the host.

## 4. Per-worksheet split state

`SpreadsheetSplitViewState` stores topology, split X/Y, active pane and all four pane offsets per worksheet.

- Hidden-pane offsets remain stored.
- Source-tagged events prevent desktop feedback loops.
- Disable/re-enable restores stored state.
- Structural insert/delete and axis reorder include split-state snapshots in their transactions.
- Direct split-view changes are not standalone undo-history commands yet.

## 5. Structural and reorder mapping

Insert/delete mapping uses exact pre-mutation sparse metrics and affects only the changed axis.

Axis reorder mapping preserves the identity of each pane's top-left row/column plus its fractional local pixel offset, using exact metrics before and after the permutation. The unaffected axis, topology, split coordinates and active pane remain unchanged.

Detailed reorder semantics are in `docs/header-reordering-contract.md`.

## 6. XLSX representation

- Compatible topology, coordinates, active pane and top-left-cell behavior use standard SpreadsheetML `SheetView/Pane` metadata.
- A Nera custom XML part preserves all four independent pane offsets.
- Native metadata is the high-fidelity source; compatible standard metadata is imported when native data is absent.
- Default unsplit sessions emit neither native nor standard pane metadata.
- Unknown-part preservation remains unsupported.

## 7. Pane scrollbars

- Integrated pane scrollbars are composed when `SpreadsheetRenderTheme.ShowSplitPaneScrollBars` is enabled.
- Horizontal/vertical bars are emitted only when content exceeds pane extent.
- Buttons, track, proportional thumb and active style are pane-local.
- Input supports line, page and continuous thumb drag and targets exactly one pane/axis.
- Optional public WinForms/WPF overlay controllers also expose lifecycle, style, layout, hit testing and refresh.

## 8. Dirty regions

- Changed cell/range content projects through each pane's offsets.
- Projection expands through merges and splits at freeze boundaries.
- Empty offscreen regions are omitted; unsafe projection requests full invalidation.
- WinForms GDI+/Direct2D HWND use partial invalidation.
- DXGI `FlipDiscard` and WPF DrawingContext use full-frame fallback.
- WPF D3DImage accepts multiple native dirty rectangles.

## 9. Header reorder input priority

1. pane scrollbar;
2. split separator;
3. dimension resize;
4. header reorder;
5. ordinary header selection.

WinForms uses actual message button state and capture. WPF uses preview routed handlers, optional capture and a `DrawingVisual` preview. Both call the same `SpreadsheetSession.Reorder` transaction.

WPF hosted CI validates a real loaded public split host and the production drag state machine deterministically; it does not claim reliable global OS pointer injection on a hosted desktop.

## 10. Public host lifecycle

### WinForms

- `EnableSplitPanes` creates/reuses a Nera-owned child surface.
- `DisableSplitPanes` removes it and reveals the unchanged single-pane control.
- GDI+, Direct2D HWND and DXGI consume shared split semantics.

### WPF

- `EnableSplitPanes` creates/reuses a Nera-owned split `Adorner` under an `AdornerLayer`/`AdornerDecorator`.
- The controller attaches/detaches across load/unload.
- DrawingContext and shared-texture D3DImage consume shared split semantics.

## 11. Required runtime gates

- topology, clamping and separator hit tests;
- independent pane scrolling and hidden-state persistence;
- pane-local cell hit/bounds and merge-anchor tests;
- split-state persistence, structural mapping and XLSX round trip;
- shared resize and real desktop input tests;
- scrollbar geometry/interaction and desktop runtime tests;
- dirty-region partial/full backend tests;
- axis-reorder permutation/formula/transaction tests;
- shared reorder geometry/preview tests;
- WinForms real-message row and column reorder tests;
- WPF loaded-window drag-state/preview/commit/undo and D3DImage test;
- full Windows GPU/runtime gate and cross-platform architecture verification.

## 12. Current exclusions

- unsplit-control header drag UI;
- drag-edge auto-scroll;
- standalone undo/redo commands for direct split-view changes;
- production MAUI/Skia split host;
- structured/shared/dynamic-array formula rewrite semantics.
