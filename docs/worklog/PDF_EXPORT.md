# PDF export and print preview handoff

## Implemented source

### Low-level PDF

- `SkiaDisplayListPdfExporter` writes platform-neutral display lists through the production Skia renderer.
- 96-DIP geometry is converted to 72-point PDF geometry at the Skia boundary.
- Multi-page, page-count, page-dimension, raster-DPI and staged-byte limits.
- Complete in-memory staging before destination mutation.
- Existing seekable destination preserved on generation, validation, page-limit, byte-limit and pre-commit cancellation failures.

### Worksheet and workbook orchestration

- `NeraSpreadSheet.Export.Pdf` project.
- Stored worksheet print settings, explicit overrides and sparse used-range fallback.
- Blank worksheet rejection before destination mutation.
- Selected/all-nonempty worksheet export into one PDF.
- Explicit worksheet order, per-section settings and global page numbering.
- Atomic same-directory temporary-file replacement for file destinations.

### Print preview parity

- `SpreadsheetPrintPreviewSession` uses the same page plan and print display-list composer as PDF.
- Continuous fractional offsets, anchored zoom and multi-column page layout.
- Visible/overscan composition only.
- Bounded page display-list cache.
- Page hit testing and local coordinates.

### Extended print settings

- Odd/even/first header and footer templates.
- Local first-page selection and global odd/even/page-number semantics.
- Manual row and column page breaks.
- OpenXml `rowBreaks`, `colBreaks`, `differentOddEven`, `differentFirst`, even and first header/footer round-trip.
- Manual break count/id validation and schema-order preservation.

## Automated tests added

- Low-level PDF signature, termination, page enumeration, cancellation and failure atomicity.
- Worksheet PDF settings/override/used-range behavior.
- Atomic file replacement.
- Multi-worksheet PDF ordering and global page limits.
- First/even/odd header selection and global page numbers.
- Manual break/header-footer OpenXml round-trip and malformed-input rejection.
- Virtualized preview offsets, anchored zoom, cache bounds and hit testing.
- Later-page merged range pagination and repeated-title boundary safety.

## Intentional limits

- The current PDF gate checks generated structure/signature internally; an independent external validator remains a final-system gate.
- Page display lists may depend on platform font substitution.
- PDF metadata, links, outlines, tagged accessibility, encryption and signatures are not implemented.
- Drawings/charts are not yet first-class print items.
- Native print preview and printer adapters are not yet complete.
- Physical disk/driver interruption after commit begins is outside stream-level atomicity.

## Codex final-validation work

Run `scripts/run-complete-validation.ps1`, then follow `docs/CODEX_FINAL_ACCEPTANCE.md` for:

1. independent PDF validation;
2. visual diffs;
3. large-document performance and memory;
4. font/Unicode/CJK/RTL samples;
5. storage failure injection;
6. physical printer/device tests;
7. screen reader/high contrast/localization;
8. compatibility/fuzzing corpus.

PR #1 remains Draft until the newest exact-head CI is green and the documentation handoff has its own green exact-head run.
