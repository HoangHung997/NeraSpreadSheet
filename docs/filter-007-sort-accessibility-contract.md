# FILTER-007 sort, reapply, header-state and accessibility contract

## Scope

FILTER-007 completes physical top-to-bottom AutoFilter sorting for Table and
direct worksheet owners by extending the existing Core sort metadata,
`SpreadsheetSortController`, paged presenter and native filter hosts. It does
not add a competing workbook/filter model and does not modify Ribbon code.

## Sort execution

- Sort keys are evaluated in their declared order and the original row index is
  the final stable tie breaker.
- Ascending, descending and comma-delimited custom orders are supported for
  value sorts. Text honors the sort state's case-sensitivity flag.
- Cell-color, font-color and icon sort metadata remains round-trip metadata
  until a future conditional-formatting visual-result model can evaluate those
  attributes without inventing host-specific semantics. Execution rejects such
  keys atomically.
- The header row and Table totals row are never moved. Entire data rows inside
  the owner range move together in one bounded Undo/Redo operation.
- Formulas stored in moved data rows travel with their row and translate
  relative references from the source address to the destination address.
  Formulas outside the sorted data range retain address-based value-sort
  semantics; this operation is not an axis move and does not rewrite external
  references as though rows had been inserted, deleted or reordered.
- A sort plus its sort-state update is one transaction. Clear sort removes only
  sort metadata; it does not attempt to reconstruct an earlier physical order.
- Sorting is bounded by `SpreadsheetSortController.DefaultMaximumMaterializedCells`.
  Rejected, unsupported and over-budget requests do not mutate cells, metadata
  or history.
- Any selection or AutoFilter data range that intersects a dynamic-array spill,
  including only its root or only a child, is rejected before materialization.
  Spill ownership, cells, sort metadata and Undo/Redo history remain unchanged.
- Left-to-right sort remains preservation-only. New construction and execution
  reject it explicitly and atomically because a correct implementation requires
  column identity/formula semantics that are not yet present; it is never
  reinterpreted as top-to-bottom sorting.

## Reapply and identity

Reapply resolves the current owner at execution time. Table owners use stable
Table `Guid` identity and current column offsets after structural edits. Direct
worksheet AutoFilter uses the current mapped range and sort offsets. Reapply
executes the current sort state, then republishes current filter visibility in
the same history transaction. A missing owner, deleted sort key or unsupported
sort is rejected without partial mutation.

## Header state and accessibility

Every shared filter target and header hit exposes one of four states:
`None`, `Filtered`, `Sorted`, or `FilteredAndSorted`. Sorted state belongs only
to columns present in the ordered sort keys. Native buttons use a distinct
glyph plus a non-color accessible description. WPF uses separate unsorted
chevron, filtered funnel, ascending arrow and descending arrow shapes; a
filtered-and-sorted header also carries a visible badge, so state is never
encoded by color alone.

The shared presenter publishes the current result count and an announcement
containing owner, column, header state and result count. WPF, WinForms and MAUI
bind that text to their native accessibility properties. Native surfaces keep
the existing bounded page of controls and support Alt+Down, arrows, Home/End,
PageUp/PageDown, Space, Enter, Escape and guarded focus restoration.
Navigation keys are claimed only while search-to-list transfer or a filter
value/date navigation surface owns focus. Text/custom editors, pickers and
command buttons retain their native Home/End/Enter/Page key behavior.

The pre-FILTER-007 public constructors and `Deconstruct` overloads of the paged
snapshot and all three header-hit records remain available for source and
binary compatibility. Result counts and accessibility announcements refresh
after Apply, Clear, sort, reapply and clear-sort mutations.

The MAUI binding cancels its lifetime token, drains the serialized in-flight
operation, and only then disposes its semaphore and presenter. Synchronous
dispose starts that safe drain without blocking the UI thread; asynchronous
dispose waits for completion.

## Validation and limits

Regression coverage must include multi-key/custom-order sorting, stable ties,
Undo/Redo, clear/reapply, structural column remapping, bounded rejection,
four-state header projection, current result counts and native accessibility
metadata. Architecture and packaging verification remain mandatory.
