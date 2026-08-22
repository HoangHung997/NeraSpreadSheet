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

## 2. Independent PDF validation

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

## 3. Font and international text

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

## 4. Native print preview and printer devices

On supported Windows hardware:

- open native preview with 1, 100, 500 and 10,000 pages;
- continuously scroll at fractional offsets;
- zoom around cursor/selection anchors;
- resize, minimize/restore and change monitor DPI;
- test 4K at 60 Hz and 120 Hz;
- monitor memory, UI latency and GPU/device recovery;
- print through multiple physical and virtual printer drivers;
- validate hard margins, custom paper, orientation, copies and cancellation.

## 5. MAUI devices

On Android/iOS/macOS/Windows target hardware:

- touch pan and pinch while native filter/preview overlays exist;
- virtual keyboard and IME lifecycle;
- suspend/resume;
- orientation and window-size changes;
- screen reader and focus order;
- high contrast and large text;
- cancellation during paging/export;
- no orphaned native focus or overlay after close/reopen.

## 6. XLSX compatibility corpus

Round-trip files from:

- current Microsoft Excel;
- older Excel versions where available;
- LibreOffice;
- Google Sheets export;
- OpenXml SDK-generated files;
- common third-party XLSX writers.

Cover:

- page setup/margins/print area/print titles;
- row/column manual breaks;
- odd/even/first headers and footers;
- Tables and worksheet AutoFilter;
- unknown parts, drawings, images and custom XML;
- repeated preservation saves;
- malformed relationships and package URIs.

Run OpenXml schema validation after every output.

## 7. CSV/TSV corpus and fuzzing

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

## 8. General security and reliability

Run:

- formula parser/evaluator fuzzing;
- clipboard fuzzing;
- XLSX package/relationship/URI fuzzing;
- low-disk and access-denied file replacement;
- crash/restart during staged export;
- memory pressure and repeated open/save/export loops;
- API compatibility checks;
- NuGet/source-link/symbol package verification.

## 9. Evidence pack

Store:

- exact commit SHA;
- OS/runtime/SDK/driver/device versions;
- automated JSON/TRX results;
- external validator logs;
- screenshots and raster diffs;
- performance/memory traces;
- printer/device matrix;
- compatibility corpus manifest and hashes;
- every accepted limitation and release blocker.

## 10. Promotion rule

PR #1 remains Draft until:

1. exact-head hosted CI is green;
2. this final acceptance plan has no unresolved blocker;
3. documentation and feature matrix match executable behavior;
4. release packaging/security/performance gates are approved;
5. the human owner explicitly decides to promote or merge.
