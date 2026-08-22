# PDF export contract

This document defines NeraSpreadSheet-owned PDF behavior. The implementation uses the same page layout and platform-neutral display lists as print preview; PDF is not allowed to introduce a second spreadsheet renderer.

## 1. Layering

```text
Worksheet / Workbook
→ immutable WorksheetSnapshot
→ SpreadsheetPageLayoutPlanner
→ SpreadsheetPrintDisplayListComposer
→ SkiaDisplayListPdfExporter
→ staged Stream or atomic file replacement
```

- `NeraSpreadSheet.Rendering.Spreadsheet` owns spreadsheet pagination and page display lists.
- `NeraSpreadSheet.Rendering.Skia` owns low-level display-list-to-PDF serialization only.
- `NeraSpreadSheet.Export.Pdf` owns worksheet/workbook orchestration and file replacement.
- Core, Editing, OpenXml, WPF, WinForms and MAUI do not depend on the PDF project.

## 2. Units and page size

Nera display lists use 96-DPI-independent units. PDF page geometry uses 72 points per inch. The Skia boundary applies one page-level scale of `72 / 96` so all spreadsheet layout remains in Nera units.

Each page validates finite positive dimensions and a configurable maximum dimension before PDF generation begins.

## 3. Low-level PDF serialization

`SkiaDisplayListPdfExporter` accepts a sequence of `SkiaPdfPage` values. It:

- creates one PDF page per display list;
- reuses `SkiaDisplayListRenderer`;
- supports multi-page documents;
- validates page count, page dimensions, raster DPI and output-byte limits;
- rejects empty documents;
- observes cancellation during page enumeration/rendering;
- stages the complete PDF before mutating the caller destination.

Default safety limits are:

- 100,000 pages;
- 512 MiB staged output;
- 200,000 DIPs per page dimension;
- raster fallback DPI between 36 and 2,400.

## 4. Destination commit semantics

For a seekable destination stream:

- generation, validation, page-limit, byte-limit and pre-commit cancellation failures leave the previous bytes unchanged;
- once staged commit begins, caller cancellation does not intentionally interrupt the copy and create a partial destination;
- physical stream/device failures during commit remain the responsibility of the host storage layer.

`SpreadsheetPdfFileExporter` writes a unique temporary file in the destination directory, flushes it, then replaces the requested path. Generation/cancellation failures preserve the existing file and attempt to delete the temporary file.

## 5. Worksheet export

`SpreadsheetPdfExporter`:

- reads worksheet print settings by default;
- permits explicit print-area/page-setup overrides without mutating worksheet state;
- falls back to the sparse used-cell rectangle when no print area exists;
- rejects a blank worksheet without an explicit print area;
- snapshots before pagination/composition;
- returns the exact page-layout plan and output length when available.

## 6. Workbook export

`SpreadsheetPdfDocumentExporter`:

- exports all nonempty worksheets by default;
- supports explicit worksheet selection and ordering;
- supports per-worksheet print options;
- rejects duplicate and out-of-range selections;
- stages all worksheet sections into one PDF;
- applies one global page number/total to header/footer tokens;
- still uses each worksheet's local first-page rule for first-page headers and footers;
- returns section metadata and global first-page numbers.

## 7. Header/footer behavior

The print composer supports:

- odd header/footer;
- even header/footer when `DifferentOddEven=true`;
- first-page header/footer when `DifferentFirstPage=true`;
- global page-number offsets and total-page overrides for multi-worksheet documents.

Template selection uses the local worksheet page number for first-page semantics and the global document page number for odd/even parity and `&P`/`&N` tokens.

## 8. Print preview parity

`SpreadsheetPrintPreviewSession` consumes the same `SpreadsheetPageLayoutPlan` and `SpreadsheetPrintDisplayListComposer` as PDF. It provides:

- fractional X/Y offsets;
- anchored zoom;
- one or more page columns;
- visible/overscan page composition only;
- bounded page display-list cache;
- hit testing with page-local coordinates.

A visual difference between preview and PDF is a defect unless caused by a documented platform font substitution.

## 9. Required automated gates

Before the PDF milestone is promoted, exact-head CI must prove:

- PDF signature and document termination;
- spreadsheet display-list integration;
- multiple page enumeration;
- page/byte/dimension limits;
- empty/invalid document rejection;
- stream failure atomicity;
- file replacement failure atomicity;
- worksheet settings/used-range/override behavior;
- multi-worksheet order and global page limits;
- Core/Windows/MAUI regression matrix remains green.

## 10. Required Codex/final-system gates

The repository-wide final validation must additionally run:

- `qpdf --check` and/or another independent PDF validator;
- visual raster diffs for representative workbooks;
- font substitution and Unicode/CJK/RTL samples;
- 100/500/10,000-page memory and throughput tests;
- file replacement under low disk space, access denial and interrupted storage;
- native printer hard margins and driver capability negotiation;
- physical 4K/120-Hz preview scrolling and zoom;
- accessibility/high-contrast/localization review of native preview UI.

## 11. Deliberately pending

- PDF metadata, outlines/bookmarks, links and tagged-PDF accessibility.
- Font embedding/subsetting policy certification.
- Drawing/chart pagination and vector fidelity.
- Password/encryption/signature support.
- Native WPF/WinForms printer adapters.
- Complete native print-preview presenters.
- PDF/A and archival conformance.

These items must remain visible until source, automated tests and the applicable runtime/external validation exist.
