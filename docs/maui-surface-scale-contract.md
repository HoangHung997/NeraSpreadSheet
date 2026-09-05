# MAUI surface scale and viewport-class contract

## Scope

This contract defines how the public MAUI spreadsheet host distinguishes:

1. the logical MAUI viewport used for layout;
2. the Skia renderer canvas exposed by `SKPaintGLSurfaceEventArgs.Info`;
3. the raw backing surface exposed by `SKPaintGLSurfaceEventArgs.RawInfo`.

The contract belongs to `NeraSpreadSheet.Maui`. Workbook, formulas, editing, layout,
scrolling and shared display-list projects remain platform-independent.

## Coordinate spaces

### Viewport units

`ViewportWidth` and `ViewportHeight` are the logical dimensions of the loaded
`NeraSpreadsheetView` in MAUI layout units. Selection, size-class decisions and
orientation decisions use these values.

### Canvas units

`CanvasWidth` and `CanvasHeight` are the dimensions seen by the production
spreadsheet renderer for the current frame.

When `IgnorePixelScaling` is enabled, one canvas unit should represent one
logical viewport unit. When it is disabled, canvas dimensions normally match
the physical backing surface.

### Raw pixels

`RawPixelWidth` and `RawPixelHeight` describe the native GPU backing surface.
They are never substituted for logical viewport dimensions when classifying
the user interface.

## Scale relationships

For each completed frame:

- `CanvasUnitsPerViewportUnit = CanvasSize / ViewportSize`;
- `RawPixelsPerViewportUnit = RawPixelSize / ViewportSize`;
- `RawPixelsPerCanvasUnit = RawPixelSize / CanvasSize`.

On Windows, `RawPixelsPerViewportUnit` must agree with the native
`SKSwapChainPanel.ContentsScale`, allowing only integer surface-rounding error.

### Logical-canvas mode

With `IgnorePixelScaling = true`:

- canvas units per viewport unit are approximately `1`;
- raw pixels per canvas unit are approximately `ContentsScale`;
- renderer geometry remains independent of monitor DPI.

This is the Nera default.

### Physical-canvas mode

With `IgnorePixelScaling = false`:

- canvas size equals raw backing size;
- raw pixels per canvas unit are approximately `1`;
- canvas units per viewport unit are approximately `ContentsScale`.

The mode is supported as a diagnostic and compatibility path; it must not
silently change workbook, selection, zoom or fractional-scroll state.

## Orientation

`NeraSurfaceOrientation` is derived only from logical viewport dimensions:

- `Portrait`: height is greater than width;
- `Landscape`: width is greater than height;
- `Square`: width and height differ by no more than `0.5` logical unit;
- `Unknown`: no valid completed frame is available.

## Width classes

`NeraSurfaceWidthClass` is derived from logical viewport width:

- `Compact`: width below `600`;
- `Medium`: width from `600` inclusive to below `840`;
- `Expanded`: width `840` or greater;
- `Unknown`: no valid completed frame is available.

Raw pixel dimensions and monitor DPI never change the logical width class.

## Capture timing

`NeraSurfaceMetrics.Capture(view, frame)` is valid only from the public
`PaintSurface` callback after production `NeraSpreadsheetView` rendering has
closed its GPU frame lease.

At capture time:

- a live GPU context must exist;
- no frame lease may remain active;
- `ContextGeneration` must match current GPU diagnostics;
- `FrameSequence` must match the completed frame count;
- canvas and raw dimensions must match the paint event.

## State-preservation invariants

Changing pixel-scaling mode, logical orientation or width class, and recreating
the native handler/context must preserve:

- the same `SpreadsheetSession`;
- workbook contents;
- exact active/anchor selection and ranges;
- selection version;
- zoom;
- fractional current and target scroll offsets;
- a stopped render loop when no animated input remains;
- empty pointer/pinch/tap state.

Every recreated native surface must use a new MAUI handler, platform view and
Skia `GRContext`. Context generation and create/loss/recreate counters advance
exactly once. Completed frames may not become failed, abandoned or stale.

## Required validation

The MAUI Windows scale smoke must exercise on the same public view:

1. physical-canvas portrait `Compact`;
2. logical-canvas landscape `Expanded`;
3. logical-canvas square `Medium`;
4. handler/platform-surface/`GRContext` recreation after every scenario;
5. native `ContentsScale` relationships before and after each recreation;
6. exact session, selection, zoom and fractional-scroll preservation.

The gate must not assume a fixed hosted-runner DPI. A 100%, 125%, 150% or
changed scale is accepted only when all coordinate-space relationships remain
valid.
