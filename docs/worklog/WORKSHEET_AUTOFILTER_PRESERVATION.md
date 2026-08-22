# Worksheet AutoFilter preservation and paged sessions

Validated implementation milestone:

- Commit: `023835495a5c56aea19830aff299765808ab5598`.
- CI: `#586`, run `32543422821`.
- Conclusion: `success` on August 22, 2026.
- PR #1 remains Draft and unmerged.

## Validated scope

### Worksheet AutoFilter Core and editing

- One direct AutoFilter range per worksheet, independent from Tables.
- Shared Table filter predicates and compressed row-span projection.
- Production commands through `SpreadsheetSession.WorksheetFilters`.
- Exact Undo/Redo for range and criteria changes.
- Structural insert/delete/reorder mapping with conservative header/range guards.
- Rejection of Table and merged-cell overlap.

### Standard SpreadsheetML

- Worksheet `autoFilter` read/write.
- Value and blank filters.
- One/two custom comparison filters with AND/OR.
- Leading/trailing wildcard conversion for begins-with, ends-with, contains and does-not-contain.
- Empty equal/not-equal conversion for blank/nonblank.
- Office 2013 schema validation.
- Malformed-input rejection before workbook restoration.

### Preservation

- Dedicated worksheet AutoFilter copy-and-patch stage.
- Existing AutoFilter `extLst` and namespaced attributes retained when semantics are refreshed.
- Opaque worksheet part bytes and relationships retained.
- Two consecutive preserved saves update criteria correctly and remain schema-valid.

### Paged session foundation

- Generation-checked refresh publication.
- Cancellation of prior refreshes.
- Asynchronous page requests with search and bounded page size.
- Snapshot isolation across worksheet mutations.
- Disposed-state rejection.

## CI evidence

CI #586 passed:

1. Core build/tests and architecture verification.
2. Windows full build/tests and desktop GPU runtime smoke.
3. MAUI Android build.
4. MAUI iOS and Mac Catalyst builds.
5. MAUI Windows build and handler checks.
6. Loaded MAUI Table-filter smoke.
7. Loaded MAUI context-recreation smoke.
8. Loaded MAUI scale/orientation smoke.

## Deliberately pending

- Native WPF/WinForms/MAUI binding to the paged session.
- Direct worksheet filter-button geometry and native presenters.
- Top10, dynamic/date-group, color/icon and sort-state models.
- External XLSX producer compatibility corpus.

Source of truth: `docs/current-status.md`.
PR #1 must remain Draft while any newer exact-head CI is red or unknown.