# Delimited text import/export contract

This document defines NeraSpreadSheet-owned CSV/TSV behavior. The implementation is independent from Excel, LibreOffice and platform UI controls.

## 1. Supported input

`DelimitedTextWorkbookSerializer.LoadAsync` supports:

- configurable delimiter and quote characters;
- UTF-8 by default and caller-provided encodings;
- BOM detection;
- CRLF, LF and CR row separators;
- quoted delimiters;
- quoted line breaks;
- doubled quote escaping;
- quoted fields spanning parser-buffer boundaries;
- optional trimming of unquoted fields;
- optional finite-number, Boolean and DateTime inference;
- optional leading-equals formula import;
- cancellation.

Leading equals is text by default. Formula import requires explicit opt-in.

## 2. Safety limits

Import limits are explicit and validated before materialization:

- maximum rows, capped at the spreadsheet row limit;
- maximum columns, capped at the spreadsheet column limit;
- maximum characters in one field.

Malformed quoted fields and unsupported trailing content after a closing quote are rejected with `InvalidDataException`. A failed or canceled import does not return a partial workbook.

## 3. Supported output

`SaveAsync` supports:

- configurable delimiter, quote and newline;
- UTF-8 BOM opt-in and caller-provided encodings;
- invariant or caller-provided culture;
- caller-selected range or sparse used range;
- values or formula text;
- standard delimiter/newline/quote escaping;
- configurable DateTime format;
- cancellation.

## 4. Formula-injection policy

When exporting text values, protection is enabled by default. Text whose first non-whitespace character is `=`, `+`, `-` or `@` receives a leading apostrophe.

This policy applies only to text values. When `WriteFormulas=true`, formula cells are intentionally exported as formula text and are not prefixed.

Callers may disable protection only when the destination and consumers are trusted.

## 5. Sparse behavior

Import creates only non-empty cells. Export iterates the explicit range or the rectangular used range; it does not expand the logical worksheet axis.

CSV and TSV are single-worksheet formats. Loading creates one workbook and one worksheet using the configured worksheet name.

## 6. Buffer-boundary guarantees

The parser carries quote, escaped-quote and CRLF state between buffers. In particular:

- a doubled quote pair may cross the 8,192-character parser-buffer boundary;
- a CR at the end of a buffer may pair with LF at the start of the next buffer;
- a final CR terminator does not create an additional empty row;
- a closing quote at end-of-file is accepted;
- an unclosed quoted field is rejected.

## 7. Required tests

Before promotion, exact-head CI must prove:

- quoted delimiter/newline/quote behavior;
- buffer-boundary escaped quotes;
- CR/LF/CRLF behavior;
- number/Boolean/date inference;
- explicit formula import/export policy;
- formula-like text protection;
- CSV and TSV round-trip;
- row/column/cell limits;
- malformed input and cancellation;
- existing Core/Windows/MAUI regressions remain green.

## 8. Deliberately pending

- Atomic file replacement for arbitrary destination streams.
- Streaming export with a configurable total-byte budget.
- Encoding auto-detection beyond BOM/caller selection.
- Locale-specific delimiter auto-detection.
- Multi-sheet archive conventions.
- External producer compatibility corpus and fuzzing.

These are retained for later implementation and final Codex/system validation.
