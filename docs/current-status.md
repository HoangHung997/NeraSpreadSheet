# NeraSpreadSheet current implementation status

This file is the handoff source of truth for the current development branch. A capability is listed as implemented only when executable source, automated tests and the applicable desktop/runtime gate exist.

## Product rules

NeraSpreadSheet is an independent spreadsheet SDK.

- No runtime dependency on Microsoft Excel, LibreOffice or DevExpress.
- No UNO/Excel command identifiers in Nera public contracts.
- No UI control per cell.
- Workbook, formula, editing, layout, scrolling and command projects remain independent from WPF, WinForms and MAUI.
- Viewports use continuous `double` pixel offsets and may stop between row/column boundaries.
- WPF, WinForms and GPU/fallback hosts consume shared viewport/display-list semantics rather than implementing separate workbook engines.

## Implemented

### Core workbook, formula and editing

- Sparse worksheets over an Excel-size logical address space.
- Multiple worksheets, versioned snapshots, values, formulas, style IDs, row/column dimensions and native merged ranges.
- Structural insert/delete for complete row/column axes with overflow preflight, formula/reference mapping and atomic rollback.
- Formula tokenizer, parser and AST for arithmetic, comparison, concatenation, references, ranges and basic cross-sheet references.
- `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT` and `IF`.
- Dependency graph, circular-reference detection and affected-only recalculation.
- Session-owned selection, undo/redo, calculation, clipboard, formatting, merge, sort, editor, view and structure controllers.
- Single, extended, multi-range, whole-row, whole-column and whole-sheet selection.
- Native clipboard package plus TSV interoperability and relative/absolute A1 translation during paste.
- One reusable in-cell editor per desktop host.
- Per-worksheet freeze-pane state; merge/freeze boundaries cannot split a merged range.

### Continuous viewport, freeze and cache

- Sparse row/column metric index and fractional pixel scrolling without row/column snapping.
- Pixel hit testing, content extent and merged-anchor resolution.
- Worksheet snapshot cache and bounded translated viewport tile cache.
- Freeze panes compose through frozen corner, frozen rows, frozen columns and scrolling body.
- Pane-aware freeze cache replays a shared tile origin with axis-specific translation.
- Nested display lists retain immutable child references rather than flatten-copying command arrays.
- GDI+, WPF and Direct2D executors share clip/translation semantics.
- Allocation regression tests and BenchmarkDotNet coverage exist for normal and frozen scrolling.

### Split-pane foundation

- Platform-neutral one-pane, vertical, horizontal and four-pane topology.
- Validated/clamped split coordinates, separator thickness and minimum pane extent.
- Half-open pane/separator hit regions, including separator intersection.
- Every pane owns an independent `ContinuousScrollController` with `double` X/Y offsets and bounds.
- Precision, wheel, touch and programmatic input can target one pane without moving the others.
- Hidden panes retain scroll state; an unavailable active pane falls back to `TopLeft`.
- Pane-local hit testing resolves merged anchors and returns common body-coordinate cell bounds.
- Shared split chrome renders headers, selection, separator continuation and active-pane state.

### Per-worksheet split state and structural mapping

- `SpreadsheetSplitViewState` stores topology, split X/Y, active pane and all four pane offsets per worksheet.
- Hidden-pane offsets remain stored.
- Source-tagged events prevent feedback loops between the shared view controller and desktop hosts.
- The outgoing worksheet state is captured before activation changes; the incoming worksheet state is restored afterward.
- Disabling/re-enabling a public split overlay restores the stored state.
- Row structural edits map pane Y offsets from exact pre-mutation row metrics.
- Column structural edits map pane X offsets from exact pre-mutation column metrics.
- Delete collapses offsets inside the removed interval and subtracts its exact physical extent from later offsets.
- Structural undo/redo restores exact pre/post split snapshots; failed operations leave split state unchanged and do not enter history.
- Direct split-view changes are not standalone undo-history commands yet.

### Per-pane split scrollbars

- Shared geometry creates horizontal/vertical bars for every visible pane whose content exceeds its viewport.
- Track, proportional thumb, maximum offset and hit geometry use pane-local bounds and continuous offsets.
- Hit testing distinguishes arrow/button, thumb, track-before and track-after behavior in the integrated host path; the optional overlay contract supports thumb and paged track interaction.
- Thumb drag maps pointer position to a continuous offset without row/column snapping.
- A request targets exactly one pane and one axis while preserving the other axis and every other pane.
- Shared styling covers background, track, buttons, normal thumb, active-pane thumb, border, glyphs, thickness and minimum lengths.
- Integrated split-frame scrollbars are controlled by `SpreadsheetRenderTheme.ShowSplitPaneScrollBars` and render through the same display-list semantics as the worksheet.
- Public optional WinForms and WPF overlay controllers expose enable/disable, visibility, style, layout, count, hit testing and refresh.
- Scrollbar changes persist through `SpreadsheetSplitViewState`.
- WinForms runtime smoke uses real Windows mouse messages.
- WPF runtime smoke uses native OS cursor/button input so routed hit testing, mouse capture and pointer state follow the production path.
- WPF rebuilds scrollbar layout from a freshly rendered split frame after topology, offset and host-size changes and keeps it valid across DrawingContext/D3DImage switches.

### Split-aware dirty regions

- `SpreadsheetSplitViewportDirtyRegionExtensions` projects a changed range into every visible pane.
- Projection expands across intersecting merged cells and splits at freeze-row/freeze-column boundaries.
- Each rectangle is clipped to the correct frozen or scrolling pane subregion.
- Missing frame data or unsafe projection requests conservative full invalidation.
- WinForms GDI+ and Direct2D HWND use partial invalidation; `FlipDiscard` intentionally falls back to a full frame.
- WPF D3DImage presents multiple dirty rectangles; DrawingContext intentionally falls back to full visual invalidation.
- Runtime diagnostics expose partial/full counts and the most recent region set.

### Public WinForms split host

- `EnableSplitPanes` / `DisableSplitPanes` operate on the existing public `NeraSpreadsheetControl`.
- The Nera-owned child surface shares session, theme and rendering contracts while leaving the underlying single-pane control intact.
- The public controller exposes topology, split coordinates, active pane, pane scroll, hit testing and GPU diagnostics.
- Separator drag, wheel/Shift+wheel, body/header selection and one reusable editor route through the resolved pane.
- Split-aware row-height/column-width resize handles update shared sparse dimensions live; separator hit regions take priority.
- Integrated pane scrollbars render in the shared split frame; the optional transparent overlay exposes hit regions only around its own tracks/thumbs.
- GDI+, Direct2D HWND and D3D11/DXGI `FlipDiscard` consume the same split semantics.
- STA runtime smoke covers all three backends, lifecycle, real mouse-message resizing, real mouse-message scrollbar interaction and dirty-region fallback rules.

### Public WPF split host

- Public split lifecycle mirrors WinForms through a Nera-owned `Adorner` under an `AdornerLayer`/`AdornerDecorator`.
- The public controller forwards session, backend and theme and exposes split state, hit testing and GPU diagnostics.
- Wheel, selection, keyboard/text editing, separator drag and shared header-resize geometry route through the active pane.
- One reusable `TextBox` editor is arranged/clipped inside the active pane/freeze subregion.
- Integrated pane scrollbars render in the split display list; optional scrollbars can also use a second Nera-owned adorner with pane-specific mouse capture.
- DrawingContext and Nera-owned D3D11 shared-texture/D3DImage paths consume the same split semantics.
- STA runtime smoke covers render, DirectWrite cache reuse, load/unload, host resize, header resize, native OS-input scrollbar drag, persisted pane state, D3DImage render and dirty-rectangle presentation.

### Desktop rendering backends

- WPF DrawingContext and WinForms GDI+ fallbacks.
- Direct2D/DirectWrite HWND renderer.
- D3D11/DXGI two-buffer `FlipDiscard` swap-chain renderer with optional VSync.
- Hardware adapter preference with default hardware and WARP fallback.
- Nera-owned WPF D3D11 shared texture, D3D9Ex bridge and D3DImage lifecycle without child-HWND airspace.
- Shared Direct2D executor, brush/text-format caches and bounded `IDWriteTextLayout` LRU.
- One-shot renderer/device recovery and frame-pacing diagnostics.
- Runtime tests cover repeated WPF unload/reload and explicit second-frame text-layout reuse.

### XLSX and split view metadata

- Basic values, formulas/cached values, multiple sheets, row heights, column widths and merged ranges.
- `NeraOpenXmlSpreadsheetSessionSerializer` round-trips per-worksheet split state.
- Compatible topology, coordinates, active pane and top-left-cell behavior are written to standard SpreadsheetML `SheetView/Pane` metadata.
- A Nera custom XML part preserves the four independent pane offsets that standard SpreadsheetML cannot represent exactly.
- Compatible standard pane metadata is imported when native metadata is absent.
- A default unsplit session emits neither native nor standard split metadata.
- Unknown-part preservation remains explicitly unsupported.

### Desktop samples

- `samples/NeraSpreadSheet.Wpf.Sample`
- `samples/NeraSpreadSheet.WinForms.Sample`

Both samples now expose:

- `Split V`, `Split H`, `Split 4` and `Clear Split`.
- A checkable `Pane Scrollbars` control backed by `SpreadsheetRenderTheme.ShowSplitPaneScrollBars`.
- Automatic transition to `Split 4` when pane scrollbars are enabled while no split topology is active.
- Live diagnostics showing active split pane and the number of composed pane scrollbars.
- A sparse demonstration extent at row 181/column 41 so horizontal and vertical pane scrolling is visible without materializing a dense sheet.
- XLSX Open/Save through the session serializer so per-worksheet split state survives round trip.

A dedicated STA runtime smoke opens the real WinForms and WPF sample windows, operates the real toolbar/toggle controls, verifies scrollbar visibility, confirms automatic four-pane topology and requires at least eight composed bars for the demonstration extent.

## Implemented but intentionally basic

- Number formatting uses the current .NET bridge rather than a complete Excel format-code engine.
- Sort is in-memory, rejects merged ranges and uses a materialization safety limit.
- Sparse whole-axis style storage is not implemented.
- Structural rewriting covers A1 cell/range syntax, not tables, structured references, shared formulas or dynamic arrays.
- Dirty projection targets cell/range content changes. Structural, metric, topology, theme and device-lifecycle changes remain conservative full invalidations.
- `FlipDiscard` and WPF DrawingContext remain explicit full-frame fallback paths.
- Scrollbars are overlays rather than permanently reserving worksheet viewport space.
- Sustained FPS/input-latency/power behavior still requires target-hardware benchmarks.

## Next implementation work

1. Implement native header drag reordering with shared preview/drop geometry, model-safe column/row moves and desktop runtime gates.
2. Add sparse whole-axis style storage so whole-row/whole-column formatting does not materialize every cell.
3. Add standalone undo/redo commands for direct split-view changes.
4. Add longer-running injected device-loss/front-buffer-loss stress coverage.
5. Implement a production Skia GPU surface plus MAUI native handler/touch interaction.
6. Expand XLSX styles, shared formulas, conditional formatting, validation, tables, drawings and unknown-part preservation.

## Not implemented yet

- Header drag reordering.
- Standalone undo/redo commands for direct split-view changes.
- Sparse whole-axis styles.
- Full XLSX styles, shared formulas, conditional formatting, validation, tables, drawings, charts, macros and unknown-part preservation.
- Complete Excel-compatible function surface and dynamic arrays.
- AutoFilter/filter UI and advanced sort.
- Printing, page layout, preview and PDF export.
- Charts, pivot, slicers, collaboration and macro/query engine.
- Production Skia GPU/MAUI control.

## Validation policy

- `NeraSpreadSheet.Core.slnx` must restore, build and test on the cross-platform CI job.
- `NeraSpreadSheet.slnx` must restore/build on Windows and all tests must pass.
- Architecture verification must remain green.
- Windows runtime smoke is mandatory; compile-only GPU/split implementations are not accepted.
- Public WinForms split smoke must cover GDI+, Direct2D HWND and DXGI.
- Public WPF split smoke must cover DrawingContext, D3DImage and DirectWrite reuse.
- Split-aware resize requires shared geometry tests plus public-host runtime smoke.
- Per-pane scrollbars require shared geometry/interaction tests plus public WinForms/WPF native-input runtime smoke.
- Dirty-region projection requires platform-neutral projection tests plus runtime verification of partial paths and explicit full-frame fallbacks.
- Desktop sample exposure requires both sample projects in the Windows test dependency graph and a real-window/form interaction smoke for their scrollbar controls.
- PR #1 remains Draft and must not merge while latest-head CI is red or unknown.

## Latest validated implementation milestone

CI run #299 passed at implementation commit `9b98def8144c82c7d708daae0279451cb5af9ac3`:

- Core restore/build/tests and architecture verification passed on Ubuntu.
- Full Windows restore/build/test passed with zero blocking diagnostics.
- Mandatory Windows desktop GPU/runtime smoke passed.
- Both sample projects compiled as dependencies of the Windows runtime-test project.
- The real WinForms sample toggle disabled and re-enabled pane scrollbars, then produced `Split 4` with at least eight bars.
- The real WPF sample toggle performed the equivalent routed interaction under an `AdornerDecorator` and produced the same verified topology/bar count.
- The new gate exposed and corrected stale sample API usage (`ClearSplitState` -> `ClearSplitPanes`), a missing WPF `System.IO` import and nullable test diagnostics before the milestone was accepted.
- Existing split persistence, structural mapping, XLSX, resize, scrollbar, dirty-region and GPU lifecycle gates remained green.

## Independence rule

Excel, LibreOffice and DevExpress may be used only as external behavior/coverage references. Their runtime engines, command IDs and public types are not NeraSpreadSheet dependencies.
