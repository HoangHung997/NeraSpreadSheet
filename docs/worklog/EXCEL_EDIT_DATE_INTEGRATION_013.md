# EXCEL-EDIT-DATE-INTEGRATION-013

Checkpoint for the user-directed Excel editing/date semantics integration on 2026-09-12.

## Scope

- In-cell editing uses a single opaque native editor surface so display-list text does not remain visibly duplicated underneath the editor.
- Formula bar and in-cell editor hide native horizontal/vertical scrollbars; caret/keyboard navigation remains available for long text.
- Excel date/time values use numeric serial semantics for arithmetic, comparison, formulas and XLSX persistence. Number formats are presentation only.
- Both Excel 1900 and 1904 date systems are supported, including the 1900 compatibility discontinuity.
- DATE/TODAY/NOW/EDATE/EOMONTH/TIME and related formula coercion operate on numeric serial values.
- XLSX date cells and legacy CLR DateTime values normalize to numeric serial values on persistence/import while number formats preserve date presentation.
- Ribbon DropDown choices project the current shared selected value into checked native menu state, so Border selection stays synchronized with the workbook selection.

## Acceptance path

`INTEGRATION-013` must pass build/analyzers, Core, Formula, OpenXML, Avalonia and native executable smoke on the exact branch head. The final integrated source is then promoted to `main`, where the full `Check out — integrated SDK and test applications` workflow must pass before branch cleanup and final delivery are considered complete.
