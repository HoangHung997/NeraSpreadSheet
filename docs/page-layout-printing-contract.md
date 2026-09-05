# Page layout and printing foundation contract

This document defines NeraSpreadSheet-owned page-layout behavior. The current milestone provides document settings, deterministic pagination, printable coordinates and virtualized preview geometry. It does not yet claim a production PDF/printer backend.

## 1. Architecture boundary

- `NeraSpreadSheet.Core` owns document print settings.
- `NeraSpreadSheet.Rendering.Spreadsheet` owns pagination, printable coordinates, preview geometry and header/footer token expansion.
- WPF, WinForms, MAUI, PDF and printer backends must consume the same page plan.
- OpenXml adapters may map Core settings but must not leak `DocumentFormat.OpenXml` types into Core.
- No page-preview control may create one native control per worksheet cell.

## 2. Core print settings

`WorksheetPrintSettings` contains:

- optional print area;
- paper size;
- portrait or landscape orientation;
- left/right/top/bottom/header/footer margins;
- scale percent;
- fit-to-pages-wide and fit-to-pages-tall;
- repeated title rows and columns;
- manual row and column page breaks;
- horizontal/vertical centering;
- gridline and heading flags;
- odd-page header and footer templates.

Built-in paper sizes include A4, A3, Letter and Legal. Custom paper sizes are expressed in inches and must be finite and positive.

`Copy()` detaches mutable manual-break collections so snapshots/plans do not observe later caller mutations.

## 3. Pagination

`SpreadsheetPageLayoutPlanner` consumes one immutable `WorksheetSnapshot`, a print area and one `SpreadsheetPageSetup`.

The planner:

- converts physical inches to 96-DPI-independent units;
- subtracts page/header/footer margins;
- applies orientation;
- calculates a conservative effective scale;
- applies fit-to-wide/tall without enlarging beyond the requested scale;
- reserves repeated title rows/columns on every applicable page;
- honors manual breaks;
- avoids automatic breaks through merged cells;
- rejects manual breaks that split a merged cell;
- preserves fractional dimensions and offsets;
- limits one plan to 100,000 pages.

The output contains page number, row/column page indexes, data range, repeated ranges, scale, paper bounds, printable bounds, unscaled content size and centering offset.

## 4. Printable page grid

`SpreadsheetPrintPageGridBuilder` creates row and column slots only for the repeated titles and data range present on one page.

- Row slots use row metrics only.
- Column slots use column metrics only.
- Repeated-state detection is axis-specific.
- A cell can be mapped to page coordinates without materializing other cells.
- One page is limited to 2,000,000 combined row/column slots.

## 5. Header/footer tokens

`SpreadsheetHeaderFooterFormatter` supports:

- `&P`: page number;
- `&N`: total pages;
- `&A`: worksheet name;
- `&F`: workbook/file name;
- `&D`: date;
- `&T`: time;
- `&&`: literal ampersand.

Unknown tokens are preserved. One timestamp is captured per formatting call so date and time cannot disagree across midnight.

## 6. Virtualized print preview

`SpreadsheetPrintPreviewLayoutEngine` arranges pages into one or more columns using continuous pixel offsets.

- Only visible/overscan page slots are materialized.
- Documents with tens of thousands of pages do not create tens of thousands of native elements.
- Zoom is bounded from 5% to 800%.
- Page gaps and scroll offsets remain fractional.
- The effective column count never exceeds the page count.
- Hit testing returns page identity and page-local coordinates.

## 7. Required tests

Before promotion, exact-head CI must prove:

- built-in/custom paper validation;
- one-page and multi-page pagination;
- fit-to-wide/tall scale;
- repeated titles;
- manual breaks;
- merged-cell behavior on first and later pages;
- centered offsets;
- page grid coordinates;
- header/footer tokens;
- large-plan preview virtualization and hit testing;
- Core/Windows/MAUI regression matrix remains green.

## 8. Deliberately pending

- Worksheet-owned persistent print settings and structural Undo/Redo integration.
- OpenXml print-area, page-margins, page-setup, breaks, print-title and header/footer round-trip.
- Shared print display-list composer.
- Native print-preview presenter.
- PDF export and OS printer adapters.
- Font embedding/substitution policy.
- Drawing/chart pagination.
- Target-printer hard-margin and device capability negotiation.
- Large real-workbook visual-diff corpus.

These items remain explicit implementation/Codex validation work and must not be represented as complete.
