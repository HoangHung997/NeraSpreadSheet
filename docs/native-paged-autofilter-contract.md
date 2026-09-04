# Native paged AutoFilter contract

This document defines the shared paging and native presentation contract for Table and direct worksheet AutoFilter. NeraSpreadSheet owns the behavior; Excel, LibreOffice and other spreadsheet applications are compatibility references only.

## 1. One filter model, two owners

The shared presenter supports:

- `SpreadsheetAutoFilterOwnerKind.Table`;
- `SpreadsheetAutoFilterOwnerKind.Worksheet`.

`SpreadsheetAutoFilterTarget` is the stable host-facing identity. It contains the canonical filter range, header cell, worksheet column, column offset, owner/column display names and active-filter state. Table targets additionally carry stable Table and Table-column `Guid` values. Worksheet targets never invent Table identities.

## 2. Platform-neutral paging stack

The paging stack is layered as follows:

```text
Core filter owner and criteria
→ owner-specific paged session
→ ISpreadsheetAutoFilterPagedSession
→ SpreadsheetAutoFilterPagedView
→ SpreadsheetAutoFilterPagedPresenter
→ native dispatcher binding
→ native popup/dropdown/sheet
```

The native host must not evaluate rows, rebuild filter criteria or modify worksheet/Table state directly.

## 3. Generation and cancellation

Every refresh creates a monotonically increasing generation. Page reads and mutations are accepted only for the current generation.

A stale request must not:

- publish a page over a newer search/refresh;
- change selected values;
- apply or clear a filter;
- replace a newly opened presenter;
- restore focus after a newer presenter has opened.

Search and refresh operations use cancellation tokens. Cancellation is cooperative and bounded; it is not treated as an error alert in native UI.

## 4. Page cache

`SpreadsheetAutoFilterPagedView` stores only requested pages. Default native page size is 100 values; the platform-neutral maximum remains 1,000 values per request.

The cache supports:

- first-page initialization;
- previous/next navigation;
- random access by global item index;
- trimmed ordinal-ignore-case search;
- stable selected state across loaded pages;
- select-all-visible and clear-visible over the complete search projection;
- invalidation after Apply/Clear or a newer refresh.

A native control must never create one child control for every distinct source value.

## 5. Shared button geometry

`SpreadsheetAutoFilterButtonGeometry` combines:

- Table header buttons;
- direct worksheet AutoFilter header buttons.

Both use the same `WorksheetSnapshot`, `ViewportLayout` and `SpreadsheetRenderTheme`. Rendering, native overlay placement and pointer hit testing must use the same hit identity and rectangle.

The current Core contract forbids a direct worksheet AutoFilter from overlapping a Table, so the combined hit stream is deterministic.

## 6. WPF binding and presenter

`NeraWpfAutoFilterPagedBinding` publishes one page through an `ObservableCollection` on the WPF dispatcher.

`NeraAutoFilterPagedPopupPresenter` provides:

- visible native header buttons;
- native `Popup`;
- cancellable search;
- current-page checkboxes;
- previous/next page commands;
- select-all-visible and clear-visible;
- Apply/Clear through production history;
- keyboard and focus restoration.

## 7. WinForms binding and presenter

`NeraWinFormsAutoFilterPagedBinding` publishes one page through a `BindingList` on a native dispatcher control.

`NeraAutoFilterPagedDropDownPresenter` provides:

- visible native header buttons;
- native `ToolStripDropDown`;
- cancellable search;
- current-page `CheckedListBox` values;
- previous/next page commands;
- Apply/Clear through production history;
- keyboard and focus restoration.

## 8. MAUI binding and host

`NeraMauiAutoFilterPagedBinding` publishes one page through an `ObservableCollection` on an `IDispatcher`.

`NeraSpreadsheetAutoFilterHost` provides:

- the production `NeraSpreadsheetView`;
- visible native buttons for Table and worksheet filters;
- a responsive overlay/bottom-sheet surface;
- a virtualizing `CollectionView` containing only the current page;
- cancellable search and paging;
- Apply/Clear through production history;
- stable Automation IDs and semantic metadata;
- Windows `Alt+Down`, Escape, Page Up/Page Down and bounded search-focus acquisition.

The overlay layer must not replace the shared GPU spreadsheet surface or introduce per-cell controls.

## 9. Mutation and history

All value/custom Apply and Clear operations flow through:

- `SpreadsheetSession.Tables` for Table filters;
- `SpreadsheetSession.WorksheetFilters` for direct worksheet filters.

A successful mutation must update compressed row visibility, viewport metrics, hit testing, dependent formulas and Undo/Redo exactly once. A rejected, canceled or stale mutation must not add history.

## 10. Required gates

Before this milestone is promoted, the exact head must pass:

1. Core build/tests and architecture verification.
2. Shared target, generation, paging, random-access and mutation tests.
3. Combined Table/worksheet button geometry tests.
4. WPF and WinForms full Windows compilation/tests.
5. MAUI Windows handler/tests and loaded runtime smoke.
6. MAUI Android, iOS and Mac Catalyst builds.
7. Existing GPU context-recreation and scale/orientation smokes.

## 11. Deliberately pending

- Loaded runtime automation for every new WPF/WinForms/MAUI paged control path.
- Incremental background publication while a distinct-value catalog is still being built.
- Native menu categories/editors for the FILTER-005 date-group, Top/Bottom,
  dynamic, color/icon and sort-state model (scheduled in FILTER-006/FILTER-007).
- Complete Table design/resize/style manager UI.
- Full mobile IME/virtual-keyboard lifecycle.
- Screen-reader, high-contrast, localization and theme certification.
- External XLSX producer compatibility corpus.

These items remain explicit Codex/final-system test work and must not be represented as already validated.
