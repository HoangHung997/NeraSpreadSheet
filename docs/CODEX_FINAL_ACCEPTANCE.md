# Codex final acceptance plan

This is the repository-wide validation backlog for work that cannot be completely proven by hosted CI or source-level tests alone. Codex must run this plan after implementation batches are complete and before PR #1 is promoted from Draft.

## 1. Automated exact-head gate

Run from a clean checkout:

```powershell
./scripts/run-complete-validation.ps1 \
    -Configuration Release \
    -RequireCleanWorkingTree
```

Keep the generated JSON/TRX evidence. Any failure blocks promotion.

## 2. Function Extension SDK packaging and compatibility

Build representative extension packages covering:

- API `1.0` success;
- future/incompatible API rejection;
- exact and highest-version resolution;
- side-by-side versions and removal fallback;
- alias collisions and alias stability;
- logical versus flattened argument counting;
- scalar and range invocation metadata;
- additional dependency declaration;
- deterministic and volatile functions;
- rejection of external-state and unsupported array capabilities;
- legacy `IFormulaFunction` registration.

Before external plugin distribution is enabled, validate and document:

- assembly discovery/loading locations;
- package/manifest schema;
- dependency version conflicts;
- publisher/signature/trust policy;
- API binary compatibility;
- formula-text version pinning or migration policy;
- unload/reload behavior;
- crash/failure containment;
- process or sandbox isolation decision;
- filesystem/network permissions;
- telemetry and support-bundle redaction.

The current milestone intentionally does not claim isolated or trusted execution of arbitrary third-party code.

## 3. Conditional aggregate compatibility corpus

Compare `COUNTIF(S)`, `SUMIF(S)` and `AVERAGEIF(S)` with current Excel and LibreOffice using:

- `=`, `<>`, `<`, `<=`, `>`, `>=`;
- invariant number/date/Boolean/error/text operands;
- blank and non-blank criteria;
- `*` and `?` wildcards;
- tilde escapes;
- criteria supplied from cells/formulas;
- one and multiple criteria ranges;
- same-shape ranges at different worksheet locations;
- matched/unmatched aggregate errors;
- text/Boolean/blank aggregate cells;
- no-match and no-numeric-average cases;
- source edits and affected-only recalculation;
- large ranges near the two-million positional-pass budget;
- malformed and excessively long criteria.

Record every locale/coercion difference rather than silently expanding the contract. Run criteria-string and range-shape fuzzing under strict evaluation budgets.

## 4. Dynamic arrays compatibility, scale and fuzzing

Validate `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE` against Excel and LibreOffice using scalar, vectors, rectangles, nesting, blanks, errors, source edits, blockers, recovery, clipboard, structure, Undo/Redo and XLSX resave.

Performance samples:

- 1 cell;
- 100 cells;
- 10,000 cells;
- 100,000 cells;
- 1,000,000 cells.

Record latency, allocation, committed memory, dependency cost, UI responsiveness and recovery. Fuzz shapes, collision maps, formulas, structural changes and clipboard ranges.

The current milestone does not claim `A1#`, `@`, array constants, LET/LAMBDA, higher-order arrays or full Office extension metadata.

## 5. Independent PDF validation

For every generated sample run:

```text
qpdf --check
pdfinfo
mutool draw or equivalent rasterization
```

Cover A4/A3/Letter/Legal, portrait/landscape, fit-to-page, repeated titles, manual breaks, merged cells, headers/footers, multi-sheet numbering, 100/500/10,000 pages, cancellation and output limits. Diff print preview against rasterized PDF.

## 6. Font and international text

Validate Vietnamese, Latin accents, CJK, RTL, combining marks, emoji/fallback, bold/italic/underline, wrapping/clipping and deterministic substitution. Record embedding/subsetting/substitution and size impact.

## 7. Native print preview and printers

On supported Windows hardware:

- preview 1/100/500/10,000 pages;
- fractional scrolling and anchored zoom;
- resize/minimize/restore/monitor DPI transitions;
- 4K at 60/120 Hz;
- memory, latency and GPU recovery;
- physical/virtual printer drivers;
- hard margins, paper/orientation/copies/cancellation.

## 8. MAUI devices

On Android/iOS/macOS/Windows target devices:

- pan/pinch with overlays;
- dynamic-array owner/child selection after source edits;
- virtual keyboard and IME;
- suspend/resume and orientation changes;
- screen reader/focus order;
- high contrast/large text;
- cancellation during paging/export;
- no orphaned focus/overlay after reopen.

## 9. XLSX compatibility corpus

Round-trip files from current/older Excel, LibreOffice, Google Sheets export, OpenXml SDK and common third-party writers.

Cover:

- extension-function formulas as unknown names;
- conditional aggregate formulas and cached values;
- dynamic-array owner/cached-child conventions;
- blocked/recovered spills;
- modern Office extension metadata;
- page setup and print titles;
- Tables/AutoFilter;
- drawings/images/custom XML;
- repeated preservation saves;
- malformed relationships and URIs.

Run OpenXml schema validation after every output.

## 10. CSV/TSV corpus and fuzzing

Cover CR/LF/CRLF, quote pairs across buffers, multiline fields, huge fields/rows, alternate delimiters/encodings, formula-injection cases, malformed quotes, cancellation and output limits.

## 11. Security and reliability

Run:

- scalar/dynamic/criteria parser fuzzing;
- extension registry/manifest/version fuzzing;
- clipboard spill fuzzing;
- XLSX package/relationship/URI fuzzing;
- low-disk/access-denied replacement;
- crash/restart during staged export;
- memory pressure and repeated open/save/export/recalculate loops;
- API compatibility checks;
- NuGet/source-link/symbol verification.

## 12. Evidence pack

Store:

- exact commit SHA;
- OS/runtime/SDK/driver/device versions;
- JSON/TRX results;
- external validator logs;
- screenshots/raster diffs;
- formula SDK/criteria/dynamic-array traces;
- performance/memory traces;
- printer/device matrix;
- corpus manifest and hashes;
- accepted limitations and blockers.

## 13. Promotion rule

PR #1 remains Draft until:

1. exact-head hosted CI is green;
2. this plan has no unresolved release blocker;
3. documentation matches executable behavior;
4. packaging, trust, compatibility, performance and security gates are approved;
5. the human owner explicitly decides to promote or merge.
