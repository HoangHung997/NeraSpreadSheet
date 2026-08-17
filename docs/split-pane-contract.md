# Split-pane contract

This document locks the current platform-neutral split-pane semantics. It distinguishes the implemented layout/viewport/rendering foundation from desktop host behavior that is still pending.

## 1. Scope

The implemented split-pane foundation is independent from WPF, WinForms, MAUI and any specific GPU backend. It consists of:

- `SpreadsheetSplitLayoutEngine` in `NeraSpreadSheet.Layout`.
- `SpreadsheetSplitScrollController` and `SpreadsheetSplitViewportEngine` in `NeraSpreadSheet.Viewport`.
- `SpreadsheetSplitChromeDisplayListComposer` in `NeraSpreadSheet.Rendering.Spreadsheet`.
- Layout, viewport and rendering regression tests in the corresponding test projects.

A split pane is not the same as a frozen pane:

- A split pane creates multiple independently scrollable views of the same worksheet.
- A frozen pane keeps leading rows/columns stationary inside a viewport.
- The current composition stack allows a pane to retain the worksheet freeze configuration inside that pane, but desktop interaction for combining both features is not yet exposed through public host controls.

## 2. Pane topology

The canonical pane identifiers are:

- `TopLeft`
- `TopRight`
- `BottomLeft`
- `BottomRight`

Supported layout topologies are:

1. No split: one `TopLeft` pane.
2. Vertical split: `TopLeft` and `TopRight`.
3. Horizontal split: `TopLeft` and `BottomLeft`.
4. Vertical plus horizontal split: all four panes.

The requested split coordinate is the leading edge of the separator in body-viewport coordinates. The separator consumes `SeparatorThickness` pixels. Each resulting pane must retain at least `MinimumPaneExtent` pixels. If the viewport is too small to satisfy both sides plus the separator, that split axis is disabled instead of creating a zero/negative pane.

Resolved split coordinates are clamped to the valid range. Callers must not assume the requested coordinate is always the resolved coordinate.

## 3. Geometry and hit testing

Pane bounds and separator bounds use the same body-viewport coordinate system.

Hit testing uses half-open rectangles:

- Left/top edges are inclusive.
- Right/bottom edges are exclusive.

This prevents one pixel from belonging to two panes. Separator regions are tested before pane regions. When both separators exist, their overlap is reported as `SeparatorIntersection`, not as either pane.

A point outside the body viewport returns `None`.

A pane hit returns:

- the resolved `SpreadsheetPaneId`;
- a local point translated from body coordinates into that pane's coordinate space.

## 4. Independent continuous scrolling

Each pane owns a separate `ContinuousScrollController`.

Consequences:

- Horizontal and vertical offsets remain `double`; no row/column snapping is introduced.
- Wheel, precision, touch and programmatic deltas can target one pane without mutating another pane.
- Each pane is clamped using its own width/height against the common worksheet content extent.
- A smaller pane may therefore have a larger maximum scroll offset than a larger pane.
- Animated targets and current offsets remain separate per pane.

The split controller creates pane controllers lazily. A pane that becomes hidden because the topology changes keeps its scroll state. Recreating that pane restores the retained offsets, subject to normal content-bound clamping.

`ResetPane` clears one pane. `Reset` clears all pane controllers and restores `TopLeft` as the active pane.

## 5. Active pane

`TopLeft` is the initial active pane.

Activating the already-active pane is a no-op. When a topology change removes the active pane, the controller falls back to `TopLeft` because every valid topology contains it.

Active-pane state currently belongs to the split viewport controller. It is not yet persisted in workbook/view state and is not part of undo/redo.

## 6. Viewport composition

`SpreadsheetSplitViewportEngine` composes one `SpreadsheetViewportFrame` per visible pane using:

- the same `SpreadsheetSession`;
- the same active worksheet and selection;
- the pane's independent scroll offsets;
- the pane's resolved width and height;
- the common overscan and render theme.

The root split display list:

1. fills the complete body background;
2. clips to the body bounds;
3. clips and translates each child viewport display list into its pane bounds;
4. draws vertical/horizontal split separators;
5. draws an active-pane border when a split topology is present.

Child display lists remain nested references. The split compositor must not flatten-copy all child commands.

If the underlying viewport engine clamps a requested offset, the split scroll controller is synchronized to the resolved offset so later hit tests and bounds calculations use the same state that was rendered.

## 7. Pane-local hit testing and bounds

Body hit testing first resolves the pane, then calls the shared viewport engine with that pane's local point and scroll offsets.

The returned cell address is worksheet-global. Merged-cell resolution follows the existing shared viewport rule and returns the merged anchor/top-left cell.

Cell bounds are calculated in pane-local coordinates by the shared viewport engine, then translated into common split-body coordinates. Callers that render desktop overlays must additionally apply header offsets and clip to the active pane/freeze subpane.

Separator hits do not return a cell.

## 8. Shared header/chrome composition

Single-pane and split-pane chrome share the same internal header renderer for:

- corner background and selection state;
- column labels;
- row labels;
- header borders;
- active/whole-axis highlighting;
- freeze-header separators;
- theme validation.

In split mode:

- panes touching the top edge contribute column headers;
- panes touching the left edge contribute row headers;
- panes that touch neither edge do not draw duplicate outer headers;
- a vertical split separator continues through the column-header band;
- a horizontal split separator continues through the row-header band.

The split chrome compositor requires exactly one viewport layout for every visible pane. It rejects:

- missing pane metadata;
- duplicate pane IDs;
- pane IDs not present in the split topology;
- bounds that differ from the split layout;
- viewport sizes that differ from pane bounds.

When headers are disabled, the compositor returns the original body display list by identity.

## 9. Regression gates

The current automated coverage locks at least these behaviors:

### Layout tests
- one-, two- and four-pane topology;
- minimum pane extent and clamping;
- pane/separator/intersection hit regions;
- local point translation.

### Viewport and scroll tests
- independent fractional X/Y offsets;
- precision delta affects only the targeted pane;
- hit testing uses the hit pane's scroll state;
- cell bounds translate into common split coordinates;
- active pane falls back when hidden;
- hidden pane offsets survive topology removal/restoration.

### Rendering tests
- headers disabled returns the original body list;
- top and left adjacent panes supply the expected labels;
- split separators continue through header bands;
- body content receives the shared header translation;
- missing pane chrome metadata is rejected.

All of these tests must remain green in `NeraSpreadSheet.Core.slnx`. Full Windows build/tests and GPU runtime smoke must also remain green because split display lists are consumed by the same executors.

## 10. Explicitly not implemented yet

The following must not be claimed as complete:

- public WPF/WinForms control properties or commands for creating/removing splits;
- draggable split separators and pointer/mouse capture;
- pane activation routed from public desktop controls;
- pane-specific wheel, touch and precision input in public controls;
- per-pane scrollbars;
- in-cell editor placement/clipping in split mode;
- row/column header selection and resize routing across split header bands;
- split-aware dirty-region invalidation in desktop controls;
- split state persistence in worksheet/workbook/view snapshots;
- undo/redo semantics for split creation, separator movement or pane scrolling;
- structural row/column mapping rules for split positions;
- desktop runtime smoke tests that host a real split control;
- MAUI/touch split-pane UX.

## 11. Next integration order

Desktop integration should proceed in this order:

1. Integrate the engine into WinForms while retaining the current single-pane path.
2. Route paint, body hit testing, active pane and wheel/precision scrolling.
3. Route editor bounds, row/column headers, resize handles and dirty invalidation.
4. Add separator drag/capture plus per-pane scrollbars.
5. Add real WinForms runtime smoke tests across GDI+, HWND Direct2D and DXGI.
6. Repeat the same contracts for WPF DrawingContext and shared-texture D3DImage.
7. Only then decide persistence, undo and structural mapping semantics.

A host integration is not complete merely because the platform-neutral split display list can be rendered. It must pass real control-level input, resize, edit and shutdown tests on the target desktop framework.
