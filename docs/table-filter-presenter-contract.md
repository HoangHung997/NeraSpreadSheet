# Table Manager and AutoFilter Presenter contract

This document defines the validated Table-manager and Table AutoFilter presentation contract for NeraSpreadSheet. It describes Nera-owned behavior only; Microsoft Excel, LibreOffice and DevExpress are external compatibility references, not runtime dependencies.

Validated implementation head: `e3a814f5c0f6eb0fff75d30ee5ee217069139d71`.
Validated GitHub Actions run: CI `#570`, run `32474664182`, completed successfully on August 21, 2026.

## 1. Architectural boundary

- `NeraSpreadSheet.Core` owns Table identity, ranges, columns, calculated/totals metadata and `TableAutoFilter` state.
- `NeraSpreadSheet.Editing` owns platform-neutral manager snapshots, filter-menu state, navigation and production mutations.
- `NeraSpreadSheet.Rendering.Spreadsheet` owns shared filter-button geometry and display-list rendering semantics.
- WPF, WinForms and MAUI own native controls, placement, focus and platform input translation only.
- Native presenters must not mutate `SpreadsheetTable`, `SpreadsheetTableColumn`, worksheet row visibility or formula state directly.
- Every apply/clear operation must flow through `SpreadsheetSession.Tables`, so the same Table state, recalculation and Undo/Redo history are used by every host.
- No per-cell native control is permitted. Native filter buttons are created only for visible Table header columns.

## 2. Table-manager snapshot

`SpreadsheetTablePresenterController.GetManagerSnapshot()` returns a read-only projection of the active worksheet:

- worksheet name;
- Table stable `Guid`, name, canonical range, header/totals flags and style name;
- whether a Table has an active filter;
- column stable `Guid`, name and worksheet column index;
- calculated-formula, totals-formula, totals-label and filtered-state flags.

The snapshot is a query result, not a second writable model. Presenters must refresh it after workbook, worksheet or Table mutations instead of retaining mutable copies as authoritative state.

## 3. Filter-menu construction and safety bounds

A filter menu is opened by stable Table and column identity. The controller validates that both still belong to the active worksheet before enumerating values.

Default safety limits are:

- maximum scanned data rows: `100,000`;
- maximum retained distinct values: `10,000`.

Enumeration rules:

- source values come from one immutable `WorksheetSnapshot` of the canonical Table data range;
- occurrence counts are accumulated for each retained `CellValue`;
- blank is represented by `CellValue.Blank` and remains distinct from an empty text value;
- scanning never expands the logical worksheet axis or creates cell controls;
- row-scan and distinct-value truncation are reported independently;
- `DistinctValueCount` is the count of retained enumerated values when truncation occurs, not a claim that every source value was retained;
- callers may provide smaller positive limits for constrained hosts or tests.

A zero or negative safety limit is rejected before enumeration.

## 4. Search and selection semantics

Search text is trimmed and matched with ordinal, case-insensitive substring comparison against the invariant display text of each retained value.

- Search changes only the visible projection; it does not discard selections hidden by the current search.
- Values remain ordered deterministically: blank first, then display text, then value kind.
- `SelectAllVisible` selects only values visible under the current search.
- `ClearVisibleSelection` clears only values visible under the current search.
- Toggling one value updates the shared menu immediately.
- Applying an empty selection is invalid and must not create history.
- Applying all enumerated values clears the column filter only when enumeration was complete.
- If enumeration was truncated, applying all retained values still creates an explicit value filter; unenumerated values are never silently treated as selected.
- A value filter stores selected nonblank values and the blank-inclusion flag through the existing `TableFilterColumn` contract.

## 5. Custom and clear-filter commands

The platform-neutral controller supports:

- one custom comparison condition;
- two custom comparison conditions combined with AND or OR;
- clear the current column filter;
- clear all filters on the target Table.

Custom-condition validation remains the responsibility of the Core filter contracts. Rich date grouping, top/bottom, color, icon and custom-list predicates are outside this milestone.

## 6. Mutation, recalculation and history

Apply and clear commands replace the targeted column while preserving filters on other Table columns. The resulting `TableAutoFilter` is submitted through `SpreadsheetSession.Tables.SetAutoFilter` or `ClearAutoFilter`.

A successful mutation must therefore:

1. enter production Undo/Redo history exactly once;
2. rebuild compressed filtered-row spans;
3. refresh layout extent and hit testing;
4. trigger affected formula recalculation, including filter-aware `SUBTOTAL` dependencies;
5. preserve stable Table and column identities;
6. restore the same state through Undo and Redo.

A rejected or no-op command must not leave a partial filter, visibility projection or history record.

## 7. Shared header-button geometry

`SpreadsheetTableFilterButtonGeometry` derives visible button hits from `WorksheetSnapshot`, `ViewportLayout` and `SpreadsheetRenderTheme`.

Each hit contains:

- Table `Guid`;
- column `Guid`;
- worksheet column index;
- filtered-state flag;
- one viewport-relative rectangle.

Rendering, pointer hit testing and native overlay placement must consume this same identity and geometry. Hosts may translate the rectangle for row/column header chrome and zoom, but they must not recompute an unrelated button position.

Filter buttons exist only when:

- Table headers are enabled;
- the header row and column are visible in the composed viewport;
- `ShowTableFilterButtons` is enabled in the render theme.

## 8. Platform-neutral keyboard navigation

`SpreadsheetTableFilterNavigator` owns active-value identity and these commands:

- move previous/next;
- move first/last;
- page previous/next;
- toggle current value;
- select all visible;
- clear visible selection.

The active value is tracked by `CellValue`, not by an unstable visual index, so search and list rebuilding preserve focus whenever the value remains visible.

The validated native mapping is:

- `Alt+Down` from the active cell: resolve its Table/column and open that filter;
- Down from search: move to the first visible value;
- Up from search: move to the last visible value;
- Up/Down in the value list: previous/next;
- Home/End: first/last;
- Page Up/Page Down: page navigation;
- Space or Enter in the value list: toggle current value;
- Enter in search: apply when the current selection is valid;
- Escape: close without applying pending selection changes;
- Ctrl+A outside search: select all visible values;
- Shift+Ctrl+A outside search: clear visible selection.

Platform event types must not enter the navigator or presenter-controller public contracts.

## 9. Focus and lifecycle contract

On open:

- remember the initiating filter button or spreadsheet surface;
- expose the native popup/dropdown/sheet;
- move focus to search and select its text;
- keep the keyboard root attached only while the host is loaded.

On close:

- release search focus;
- close and dispose menu-navigation state;
- restore focus to the initiating visible/enabled control, otherwise to the spreadsheet surface;
- never reopen the sheet because of a delayed focus callback.

The MAUI Windows binding uses WinUI `FocusManager.TryFocusAsync` with a bounded retry policy while the native `TextBox` is loaded and visible. Retries stop after success, user navigation to the value list, sheet closure, host disposal or the fixed attempt limit. COM and invalid-lifecycle failures are treated as retryable only within that bounded window.

Automation identifiers are stable for the lifetime of each MAUI element and are assigned no more than once.

## 10. Native presenter bindings

### WPF

- Native `Popup` presentation.
- Automatic visible Table-header button host.
- Native search box, value checkboxes and command buttons.
- Keyboard navigation and focus restoration verified in a loaded WPF window.

### WinForms

- Native `ToolStripDropDown` presentation.
- Automatic visible Table-header button host.
- Native search, checked-value list and command items.
- Keyboard navigation and focus restoration verified with a created native handle and message loop.

### MAUI

- `NeraSpreadsheetTableHost` overlays visible native filter buttons over the shared GPU spreadsheet surface.
- Responsive filter sheet supports host placement as an overlay/bottom-sheet surface without per-cell controls.
- Stable Automation IDs, semantic descriptions, hints and heading metadata are exposed.
- Windows loaded smoke verifies live Skia `GRContext`, focus transitions, Apply, compressed visibility, Undo, Redo, reopen and close.
- Android, iOS and Mac Catalyst real-target compilation must remain green for shared MAUI changes.

## 11. Validation gates

This capability is implemented only while the exact head passes:

- Core restore/build/tests and architecture verification;
- platform-neutral presenter, navigator, target-resolver and geometry tests;
- full Windows solution build and tests;
- loaded WPF/WinForms presenter and keyboard/focus smokes;
- Windows desktop GPU runtime smoke;
- MAUI Android build;
- MAUI iOS and Mac Catalyst builds;
- MAUI Windows build and handler tests;
- loaded MAUI Windows Table-filter smoke;
- loaded MAUI Windows GPU/context and scale/orientation smokes.

## 12. Intentional limits and next work

The validated milestone does not yet provide:

- virtualized or paged native value lists;
- a complete Table design/resize/style manager UI;
- rich text/date/top/bottom/color/icon/custom-list filters;
- direct worksheet AutoFilter outside Tables;
- full mobile virtual-keyboard and IME lifecycle;
- complete screen-reader, high-contrast, localization and theme certification;
- external XLSX compatibility corpus coverage for every producer-specific AutoFilter variant.

These limitations must remain visible in status documents and must not be hidden by a platform-specific presenter claim.

## 13. Independence rule

Excel, LibreOffice and DevExpress may be used to compare user-visible behavior and file compatibility. Their runtime engines, command identifiers, controls and public types are not NeraSpreadSheet dependencies.