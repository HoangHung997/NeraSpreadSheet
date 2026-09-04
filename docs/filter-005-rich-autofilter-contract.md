# FILTER-005 rich AutoFilter and sort-state contract

## Scope and ownership

`NeraSpreadSheet.Core` owns one filter predicate model used by Table and direct
worksheet AutoFilter. `NeraSpreadSheet.Editing` owns transactional mutations and
paged-session projection. `NeraSpreadSheet.OpenXml` converts that model to and
from SpreadsheetML. Native filter UI and Ribbon files are outside FILTER-005.

## Predicate semantics

The shared model supports:

- value and custom text/number/date comparisons;
- date groups at year, month, day, hour, minute, or second precision;
- Top/Bottom item counts and percentages, including ties at the boundary;
- dynamic relative dates and Above/Below Average;
- resolved fill/font colors;
- 3/4/5-member icon-set filters;
- ordered sort-state metadata for value, color, icon, direction, case and an
  optional custom list.

Date-group items are ORed with selected values and blank. Separate filter
columns are ANDed. Blank, error and nonnumeric values do not participate in
Top/Bottom, average or icon ranking. Numeric date serials use the workbook
1900/1904 date system captured in the immutable worksheet snapshot. A dynamic
filter may carry a `ReferenceDate` for deterministic evaluation; otherwise it
uses the current local date.

Projection caches one compiled predicate per owner column and snapshot.
Aggregate criteria scan their bounded data column once, then row visibility is
emitted through the existing compressed `FilteredRowSpan` path. Filter
mutations, Undo and Redo invalidate only the affected Table/direct-filter range
through the prepared dependency graph.

Color filtering compares the effective base/row/column cell style captured by
Core. Icon filtering uses deterministic equal numeric buckets inferred from the
3/4/5-icon set because the current Core conditional-formatting model does not
yet own icon-set rules. Importing and evaluating arbitrary producer icon-set
threshold rules remains a documented compatibility limit rather than a hidden
second model.

## OpenXML mapping

Both Table and worksheet codecs read and write:

- `filters/filter` and `filters/dateGroupItem`;
- `customFilters/customFilter`;
- `top10`;
- `dynamicFilter`;
- `colorFilter` with workbook `dxfs` materialization;
- `iconFilter`;
- `sortState/sortCondition`.

Generated packages must remain schema-valid. When
`PreserveUnknownParts=true`, namespace-qualified attributes, extension lists,
unsupported producer-owned filter columns and unsupported sort markup are
retained across repeated saves. Without preservation, unsupported standard
criteria are rejected before workbook restoration completes.

## Transaction and identity rules

Changing one rich criterion or sort state creates exactly one production
Undo/Redo entry. Table and Table-column `Guid` identities are unchanged.
Structural column insertion/deletion remaps sort offsets and drops a sort key
only when its source column is deleted. Sparse workbook storage is not expanded
to materialize the worksheet axis.

FILTER-005 owns sort metadata and round-trip. Physical row sorting, reapply,
native indicators and complete keyboard/focus behavior remain FILTER-007 work.
