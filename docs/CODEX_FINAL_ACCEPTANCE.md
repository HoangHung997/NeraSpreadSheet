# Codex final acceptance plan

This is the final repository-wide validation backlog for work that cannot be completely proven by hosted CI or by source-level tests alone. Codex must run this plan after implementation batches are complete and before PR #1 is promoted from Draft.

## 1. Automated repository gate

Run from a clean exact-head checkout:

```powershell
./scripts/run-complete-validation.ps1 \
    -Configuration Release \
    -RequireCleanWorkingTree
```

Keep the generated JSON report with the release evidence. Any failure blocks promotion.

## 2. Dynamic arrays compatibility, scale and fuzzing

Validate `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT` and `UNIQUE` against current Excel and LibreOffice using:

- scalar, row-vector, column-vector and rectangular results;
- nested dynamic-array functions;
- blank, number, text, Boolean, DateTime and error values;
- row and column filtering;
- ascending/descending row and column sort;
- duplicate and exactly-once uniqueness;
- source edits that shrink and grow output;
- dependent formulas consuming owner and child values;
- blocked output from values, formulas, spills, merged cells, Tables and worksheet bounds;
- blocker removal and spill recovery;
- copy/cut/paste of complete and partial spill ranges;
- row/column insert/delete/reorder with Undo/Redo;
- XLSX save, external open, external resave, Nera reopen and recalculation.

Performance and resource samples:

- 1 cell;
- 100 cells;
- 10,000 cells;
- 100,000 cells;
- 1,000,000 cells.

Record calculation latency, allocation, committed memory, worksheet cell count, dependency-graph cost, UI responsiveness and cancellation/recovery behavior. Run mutation fuzzing over shapes, formulas, collision maps, structural changes, clipboard ranges and repeated recalculation. Respect the one-million-cell per-array limit and fail closed on excessive shapes.

The first-generation milestone does not claim compatibility for `A1#`, `@`, array constants, LET/LAMBDA, higher-order array functions or full Microsoft Office dynamic-array extension metadata. Treat those as expected limitations, not silent passes.

## 3. Independent PDF validation

For every generated sample:

```text
qpdf --check
pdfinfo
mutool draw or equivalent rasterization
```

Required samples:

- A4/A3/Letter/Legal;
- portrait and landscape;
- fit-to-one-page and natural pagination;
- repeated row/column titles;
- manual breaks;
- merged cells near every page boundary;
- odd/even/first headers and footers;
- multi-worksheet global page numbering;
- 100, 500 and 10,000 pages;
- cancellation and output-limit failures;
- existing destination replacement.

Perform pixel/geometry diffs between print preview and rasterized PDF.

## 4. Font and international text

Validate:

- Vietnamese;
- Latin accents;
- CJK;
- RTL scripts;
- emoji and unsupported glyph fallback;
- bold/italic/underline;
- narrow/wide columns;
- wrapped text and clipped text;
- deterministic substitution when the requested font is absent.

Record embedded/subset/substituted font behavior and file-size impact.

## 5. Native print preview and printer devices

On supported Windows hardware:

- open native preview with 1, 100, 500 and 10,000 pages;
- continuously scroll at fractional offsets;
- zoom around cursor/selection anchors;
- resize, minimize/restore and change monitor DPI;
- test 4K at 60 Hz and 120 Hz;
- monitor memory, UI latency and GPU/device recovery;
- print through multiple physical and virtual printer drivers;
- validate hard margins, custom paper, orientation, copies and cancellation.

## 6. MAUI devices

On Android/iOS/macOS/Windows target hardware:

- touch pan and pinch while native filter/preview overlays exist;
- inspect spill owner/child selection and error behavior after source edits;
- virtual keyboard and IME lifecycle;
- suspend/resume;
- orientation and window-size changes;
- screen reader and focus order;
- high contrast and large text;
- cancellation during paging/export;
- no orphaned native focus or overlay after close/reopen.

## 7. XLSX compatibility corpus

Round-trip files from:

- current Microsoft Excel;
- older Excel versions where available;
- LibreOffice;
- Google Sheets export;
- OpenXml SDK-generated files;
- common third-party XLSX writers.

Cover:

- dynamic-array owner formulas and cached child conventions;
- blocked and recovered spills;
- package extension metadata used by modern Office;
- page setup/margins/print area/print titles;
- row/column manual breaks;
- odd/even/first headers and footers;
- Tables and worksheet AutoFilter;
- unknown parts, drawings, images and custom XML;
- repeated preservation saves;
- malformed relationships and package URIs.

Run OpenXml schema validation after every output. Confirm that dynamic-array child cleanup does not remove unrelated styles, metadata or extension payloads.

## 8. CSV/TSV corpus and fuzzing

Include:

- CR/LF/CRLF;
- quote pairs across buffer boundaries;
- quoted multi-line cells;
- very large fields and rows;
- alternate delimiters and encodings;
- formula-like text injection cases;
- malformed/unclosed quotes;
- cancellation and staged-output limits.

Run mutation fuzzing with strict row/column/cell/output budgets.

## 9. General security and reliability

Run:

- scalar and dynamic formula parser/evaluator fuzzing;
- clipboard fuzzing, including partial/complete spill selections and paste collisions;
- XLSX package/relationship/URI fuzzing;
- low-disk and access-denied file replacement;
- crash/restart during staged export;
- memory pressure and repeated open/save/export/recalculate loops;
- API compatibility checks;
- NuGet/source-link/symbol package verification.

## 10. Evidence pack

Store:

- exact commit SHA;
- OS/runtime/SDK/driver/device versions;
- automated JSON/TRX results;
- external validator logs;
- screenshots and raster diffs;
- dynamic-array result/collision/performance traces;
- performance/memory traces;
- printer/device matrix;
- compatibility corpus manifest and hashes;
- every accepted limitation and release blocker.

## 11. Promotion rule

PR #1 remains Draft until:

1. exact-head hosted CI is green;
2. this final acceptance plan has no unresolved blocker;
3. documentation and feature matrix match executable behavior;
4. release packaging/security/performance gates are approved;
5. the human owner explicitly decides to promote or merge.
