# Printing and delimited-text implementation handoff

## Implemented source surface

### Core

- Physical paper-size and margin value objects.
- Page orientation, scaling, fit-to-page, repeated titles and manual break settings.
- Worksheet print-settings value object with detached copy semantics.

### Pagination and preview

- Deterministic page planning from an immutable worksheet snapshot.
- Fit-to-wide/tall scaling.
- Repeated title rows and columns.
- Manual row/column breaks.
- Automatic merged-cell break avoidance and manual-break rejection.
- Printable page row/column grid and cell-coordinate lookup.
- Header/footer token formatting.
- Virtualized, multi-column print-preview page slots with fractional offsets and hit testing.

### CSV/TSV

- Configurable delimiter/quote/newline/culture/encoding.
- Quoted delimiter, newline and doubled-quote parsing.
- Parser state retained across the 8,192-character buffer boundary.
- Explicit number/Boolean/date inference.
- Explicit leading-equals formula import.
- Formula export and formula-like text protection.
- Row/column/cell-character safety limits and cancellation.

## Tests added

- Core print-setting validation and copy isolation.
- A4/landscape/multi-page/fit-to-page/repeated-title/manual-break pagination.
- First-page and later-page merged-cell boundaries.
- Printable coordinates and header/footer tokens.
- Large virtualized preview layouts and hit testing.
- CSV/TSV quote/newline/type/formula/safety behavior.
- Escaped quote pair across parser-buffer boundary.
- CR-only final terminator without an extra row.

## Deliberately pending

- Persisting print settings directly on `Worksheet` and structural history integration.
- OpenXml page-setup, print-area, title, break and header/footer round-trip.
- Shared print display-list composition.
- Native preview, PDF and printer adapters.
- Atomic replacement for arbitrary delimited-text destination streams.
- External CSV/TSV and print-layout compatibility corpus.
- Target printer, font substitution, 4K/120-Hz preview and mobile preview validation.

Run `scripts/run-complete-validation.ps1` for the broad automated matrix. Use Codex/final-system validation for target hardware, real printers, physical multi-monitor DPI, mobile IME/accessibility and compatibility/fuzzing corpus work.
