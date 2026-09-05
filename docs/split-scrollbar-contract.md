# Split-pane scrollbar contract

This document locks the behavior and architecture of independent split-pane scrollbars in NeraSpreadSheet.

## Scope

Split scrollbars are an overlay presentation of existing per-pane continuous scroll state. They do not own workbook state, formula state, selection state or a second scrolling engine.

The implementation is divided into three layers:

1. `NeraSpreadSheet.Rendering.Spreadsheet`
   - platform-neutral geometry;
   - track/thumb layout;
   - hit testing;
   - display-list composition;
   - visual style data.
2. `NeraSpreadSheet.Viewport`
   - pointer interaction controller;
   - conversion from a split viewport frame to scrollbar state;
   - conversion from a scrollbar request back to one pane's X/Y offset.
3. WPF and WinForms hosts
   - thin overlays;
   - native pointer capture;
   - native painting;
   - lifecycle and accessibility integration.

## Independence rules

- Every visible split pane can expose one horizontal and one vertical scrollbar.
- A four-pane topology therefore has at most eight scrollbars.
- There is never one UI element per cell.
- All offsets remain `double`; thumb movement must not snap to rows or columns.
- Moving one pane's horizontal scrollbar cannot modify:
  - its vertical offset;
  - another pane's horizontal offset;
  - another pane's vertical offset.
- Hidden pane offsets remain owned by the split viewport/view state and are restored when the topology returns.
- Scrollbars are overlays. Their presence must not change worksheet layout, split separator positions or cell coordinates.

## Geometry

For each pane and axis:

```text
maximum offset = max(0, content extent - pane viewport extent)
thumb ratio    = pane viewport extent / content extent
thumb travel   = track length - thumb length
thumb position = track start + offset / maximum offset * thumb travel
```

Rules:

- An axis whose maximum offset is zero does not create a scrollbar.
- Thumb length is proportional but cannot be shorter than `MinimumThumbLength`.
- Track length must satisfy `MinimumTrackLength`.
- When both axes are visible, each track reserves the other's corner so the two tracks do not overlap.
- Geometry validates finite/non-negative offsets, extents, pane bounds and style values.
- Duplicate pane IDs are rejected.

## Interaction

### Thumb drag

- Pointer down on a thumb captures the pointer.
- The original grab offset inside the thumb is retained.
- Pointer movement maps continuously to a `double` offset.
- Movement is clamped to `[0, MaximumOffset]`.
- Releasing/canceling pointer capture ends the drag.

### Track click

- Clicking before the thumb pages backward.
- Clicking after the thumb pages forward.
- Page distance is `ViewportExtent * PageFactor`.
- The result is clamped to the valid range.

### Wheel

- Wheel input over a scrollbar targets that scrollbar's pane.
- Shift+wheel targets the horizontal axis.
- Ordinary wheel targets the vertical axis.
- Wheel input continues to use the existing `ContinuousScrollController` animation; scrollbar code does not implement separate physics.

## Desktop host behavior

### WinForms

- One transparent overlay control is attached to the split surface.
- Its region is the union of visible tracks plus hit slop.
- Points outside the region pass through to the split surface, preserving cell selection, separator drag and header resize.
- The overlay remains valid over GDI+, Direct2D HWND and D3D11/DXGI rendering paths.

### WPF

- One adorner is placed above the split adorner.
- `HitTestCore` returns a hit only for visible track/thumb geometry.
- Points outside scrollbar geometry continue to the underlying split adorner.
- The adorner is independent of DrawingContext versus Direct2D/D3DImage body rendering.

## Public activation

Both desktop hosts expose:

```csharp
control.EnableSplitPaneScrollBars();
control.TryGetSplitPaneScrollBarController(out var controller);
control.DisableSplitPaneScrollBars();
```

Calling `EnableSplitPaneScrollBars` also ensures that split panes are enabled. The caller can supply a `SpreadsheetSplitScrollBarStyle` to configure thickness, margin, minimum thumb/track length, hit slop, page factor and colors.

## Validation gates

The feature is not considered complete unless:

- geometry tests cover one/two/four-pane topologies;
- a four-pane topology produces eight independent bars when both axes can scroll;
- a non-scrollable axis produces no bar;
- fractional thumb mapping is tested;
- track paging is tested;
- pointer interaction targets only one pane and one axis;
- viewport-frame adaptation preserves independent pane offsets;
- WinForms runtime smoke renders and updates scrollbars over GDI+, Direct2D HWND and DXGI;
- WPF runtime smoke renders and updates scrollbars over DrawingContext and Direct2D/D3DImage;
- latest-head Core and Windows CI are green.

## Deliberately deferred

- OS-native accessibility peers/patterns for each scrollbar;
- auto-hide/fade animation;
- touch-width expansion and mobile styling;
- keyboard focus traversal between individual scrollbars;
- MAUI/Skia host implementation;
- per-pane external scrollbar placement outside the body viewport.
