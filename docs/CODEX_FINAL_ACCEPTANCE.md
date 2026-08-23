# Codex final acceptance plan

This is the repository-wide validation backlog for behavior that hosted CI cannot completely prove. Run it from a clean exact-head checkout before PR #1 is promoted from Draft.

## 1. Automated repository gate

```powershell
./scripts/run-complete-validation.ps1 \
    -Configuration Release \
    -RequireCleanWorkingTree
```

Store the generated JSON/TRX evidence. Any failure blocks promotion.

## 2. Function Extension SDK compatibility and security

Validate third-party extension prototypes against SDK API `1.0`:

- exact/highest version selection and unregister fallback;
- aliases and conflict rejection;
- host API rejection;
- scalar/range/array capability rejection;
- logical versus flattened argument count;
- additional dependency declarations;
- deterministic, volatile and external-state metadata;
- exception containment;
- concurrent lookup/registration stress;
- maximum versions per identity;
- legacy adapter compatibility.

Before external plugin release, define and validate manifest format, package discovery, publisher signatures, trust policy, loading/unloading, isolation, CPU/memory/time quotas, audit logs and API compatibility tooling.

## 3. Engineering compatibility corpus

Compare Nera results with current Excel and LibreOffice for:

- `DELTA`, `GESTEP` defaults and coercion;
- all bit functions at `0`, `2^48-1`, negative, fractional and overflow boundaries;
- positive and negative shifts, including magnitude `53` and rejection above it;
- every decimal/base and cross-base conversion;
- sign-bit boundaries for binary 10-bit, octal 30-bit and hexadecimal 40-bit formats;
- optional `places` from 1 through 10;
- literal versus referenced numeric text, blank, Boolean, DateTime and error arguments;
- invalid digits, excessive width and target-range failures.

Run property-based round-trip checks where the signed source value is representable in both bases. Fuzz radix strings, shift amounts and conversion boundaries; record every intentional compatibility difference.

## 4. Database compatibility and scale

Test all twelve database functions with:

- field by text and numeric index;
- one/multiple/missing records for `DGET`;
- AND within a criteria row and OR across criteria rows;
- duplicate criteria headers;
- blank criteria cells and fully blank rows;
- comparison operators, wildcards and tilde escaping;
- numbers, DateTime, text, Boolean, blank and error selected fields;
- matched versus nonmatching error rows;
- malformed, blank and duplicate database headers;
- sample/population variance/deviation precision;
- field-selector and criteria dependencies after edits;
- maximum database/criteria/comparison budgets.

Performance samples should include 1,000, 100,000 and 1,000,000 records where hardware permits. Record latency, allocation, memory and affected-recalculation cost. Fuzz criteria-table shapes and values under strict budgets.

Expected limitation: criteria cells are values, not executed formula-expression criteria; database execution is a bounded scan, not an index.

## 5. Conditional aggregate, statistics and finance corpus

Validate:

- wildcard/tilde and comparison criteria across all conditional aggregate families;
- literal/reference coercion differences;
- percentile/quartile interpolation boundaries;
- variance/deviation numerical stability;
- mode/rank ties;
- cash-flow sign and beginning/end timing;
- zero-rate and near-zero-rate paths;
- IRR multiple-root nearest-guess selection and deterministic repeat;
- NPV/IRR ordered range traversal and resource budgets.

Run differential and mutation fuzzing for formula parser, criteria parser and each numerical family.

## 6. Dynamic arrays compatibility, scale and fuzzing

Validate `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT` and `UNIQUE` using scalar, row-vector, column-vector and rectangular inputs; nested functions; all `CellValue` kinds; source resize; blockers; recovery; dependents; clipboard; structure; Undo/Redo; and XLSX round-trip.

Performance samples: 1, 100, 10,000, 100,000 and 1,000,000 cells. Record calculation latency, allocations, committed memory, sparse cell count, graph cost and UI responsiveness.

Expected limitations: no `A1#`, `@`, array constants, LET/LAMBDA/higher-order arrays or complete Office extension metadata yet.

## 7. XLSX compatibility corpus

Round-trip files from current/older Excel, LibreOffice, Google Sheets export, OpenXml SDK and common third-party writers. Cover:

- scalar, engineering, database, statistical, financial and dynamic formulas;
- cached formula values and external recalculation behavior;
- dynamic owner/child conventions and extension metadata;
- page setup, Tables, filters, rules, drawings/images and custom XML;
- repeated preservation saves;
- malformed relationships, URIs and package graphs.

Run OpenXml schema validation after every output and preserve corpus manifests/hashes.

## 8. PDF, print preview and physical printers

For generated PDF samples run:

```text
qpdf --check
pdfinfo
mutool draw or equivalent rasterization
```

Cover paper/orientation, fit-to-page, repeated titles, manual breaks, merged cells near boundaries, headers/footers, multi-sheet numbering, 100/500/10,000 pages, cancellation and output limits. Perform preview-versus-raster geometry diffs.

On physical/virtual Windows printers validate drivers, hard margins, custom paper, orientation, copies, collation and cancellation. Test native preview at 4K/60/120 Hz with DPI changes and device recovery.

## 9. Fonts and international text

Validate Vietnamese, Latin accents, CJK, RTL, combining marks, emoji, missing-glyph fallback, bold/italic/underline, wrapping and clipping. Record embedding/subsetting/substitution and file-size impact.

## 10. MAUI devices and accessibility

On Android/iOS/macOS/Windows hardware validate touch/pinch, filter/preview overlays, spill owner/child UX, virtual keyboard/IME, suspend/resume, orientation/window changes, screen readers, focus order, high contrast, large text, localization and cancellation. Confirm no orphaned native focus or overlay after reopen.

## 11. CSV/TSV and clipboard fuzzing

Cover CR/LF/CRLF, cross-buffer quote pairs, quoted multiline fields, huge fields/rows, alternate delimiters/encodings, formula injection, malformed quotes, cancellation and staged limits. Fuzz clipboard packages, formula translation, partial/complete spills and paste collisions.

## 12. Security, reliability and packaging

Run low-disk/access-denied replacement, crash/restart during staged export, memory pressure, repeated open/save/export/recalculate loops, formula/XLSX/CSV/clipboard fuzzing, API compatibility checks, NuGet/source-link/symbol verification, crash recovery/safe mode and support-bundle validation.

## 13. Evidence pack

Store exact commit SHA; OS/runtime/SDK/driver/device versions; JSON/TRX; validator logs; screenshots/raster diffs; formula/criteria/dynamic traces; performance/memory traces; printer/device matrix; corpus manifest/hashes; and every accepted limitation/blocker.

## 14. Promotion rule

PR #1 remains Draft until exact-head hosted CI is green, this plan has no unresolved blocker, documentation matches executable behavior, packaging/security/performance gates are approved and the human owner explicitly promotes or merges it.
