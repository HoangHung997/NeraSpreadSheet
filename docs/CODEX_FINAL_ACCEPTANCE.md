# Codex final acceptance plan

This is the repository-wide validation backlog for work that cannot be completely proven by hosted CI or source-level tests alone. Codex must run this plan after implementation batches are complete and before PR #1 is promoted from Draft.

## 1. Automated exact-head gate

Run from a clean checkout:

```powershell
./scripts/run-complete-validation.ps1 \
    -Configuration Release \
    -RequireCleanWorkingTree
```

Keep JSON/TRX evidence. Any failure blocks promotion.

## 2. Function Extension SDK packaging and compatibility

Build representative packages covering API compatibility, exact/highest version resolution, side-by-side versions, aliases/conflicts, logical/flattened arguments, scalar/range invocation, additional dependencies, volatility/state rejection and legacy registration.

Before external distribution, validate package manifests, assembly discovery, dependency conflicts, publisher signatures, API binary compatibility, version pinning/migration, unload/reload, crash containment, isolation, filesystem/network permissions and support-bundle redaction.

The current milestone does not claim isolated or trusted execution of arbitrary third-party code.

## 3. Conditional aggregate compatibility corpus

Compare `COUNTIF(S)`, `SUMIF(S)` and `AVERAGEIF(S)` with current Excel and LibreOffice across operators, numbers/dates/Boolean/errors/text, blanks, wildcards/escapes, cell-supplied criteria, multiple ranges, errors, no-match cases, affected recalculation, large ranges and malformed criteria.

Record locale/coercion differences. Fuzz criteria strings and shapes under strict budgets.

## 4. Statistical compatibility, scale and fuzzing

Compare the following with current Excel and LibreOffice:

- `MEDIAN`;
- `MODE.SNGL`;
- `PERCENTILE.INC`;
- `QUARTILE.INC`;
- `VAR.P`, `VAR.S`;
- `STDEV.P`, `STDEV.S`;
- `RANK.EQ`;
- `LARGE`, `SMALL`.

Cover:

- odd/even and singleton sets;
- duplicate/tied values;
- scalar Boolean and numeric-text arguments;
- text/Boolean/blank cells inside ranges;
- DateTime/OLE serial values;
- error values;
- percentile endpoints and interpolation;
- quartile indexes `0..4` and invalid indexes;
- sample/population insufficient data;
- ascending/descending rank and values absent from the reference set;
- large/small boundary indexes;
- source edits and affected-only recalculation;
- large ranges near the two-million-value budget.

Run statistical input fuzzing over finite/extreme floating values, duplicates, order, mixed cell kinds, errors and range shapes. Record differences in coercion, tie grouping, floating-point precision and error codes. The current milestone does not claim exclusive percentile/quartile, MODE.MULT, RANK.AVG, covariance/correlation/regression or distribution compatibility.

## 5. Dynamic arrays compatibility, scale and fuzzing

Validate `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE` against Excel and LibreOffice using scalar/vector/rectangular results, nesting, blanks, errors, source edits, blockers, recovery, clipboard, structure, Undo/Redo and XLSX resave.

Performance samples: 1, 100, 10,000, 100,000 and 1,000,000 cells. Record latency, allocation, memory, dependency cost and UI responsiveness. Fuzz shapes, collision maps, formulas and structural/clipboard operations.

## 6. Independent PDF validation

Run `qpdf --check`, `pdfinfo` and rasterization for page-size/orientation, fit-to-page, repeated titles, breaks, merged cells, headers/footers, multi-sheet output, 100/500/10,000 pages, cancellation and output limits. Diff preview against rasterized PDF.

## 7. Font and international text

Validate Vietnamese, Latin accents, CJK, RTL, combining marks, emoji/fallback, styles, wrapping/clipping and deterministic substitution. Record embedding/subsetting and size impact.

## 8. Native preview and printers

On supported Windows hardware validate large previews, fractional scrolling, anchored zoom, resize/minimize/restore, physical DPI transitions, 4K 60/120 Hz, GPU recovery, printer drivers, hard margins, custom paper and cancellation.

## 9. MAUI devices

On Android/iOS/macOS/Windows devices validate touch with overlays, spill owner/child selection, virtual keyboard/IME, suspend/resume, orientation, screen reader/focus, high contrast/large text and cancellation.

## 10. XLSX compatibility corpus

Round-trip files from current/older Excel, LibreOffice, Google Sheets export, OpenXml SDK and third-party writers. Cover extension formulas, conditional/statistical formulas and cached values, dynamic arrays, page setup, Tables/filters, unknown parts, drawings/images/custom XML and malformed relationships/URIs. Run schema validation after every output.

## 11. CSV/TSV corpus and fuzzing

Cover CR/LF/CRLF, cross-buffer quotes, multiline/huge fields, alternate delimiters/encodings, formula-injection cases, malformed quotes, cancellation and output limits.

## 12. Security and reliability

Run scalar/dynamic/criteria/statistical parser/evaluator fuzzing, extension registry/version fuzzing, clipboard spill fuzzing, XLSX package/URI fuzzing, low-disk/access-denied replacement, crash/restart during staged export, memory pressure, repeated open/save/export/recalculate loops, API compatibility and NuGet/source-link verification.

## 13. Evidence pack

Store exact SHA, OS/runtime/SDK/driver/device versions, JSON/TRX, external validator logs, screenshots/raster diffs, function/statistics/dynamic-array traces, performance/memory traces, printer/device matrix, corpus hashes and accepted limitations/blockers.

## 14. Promotion rule

PR #1 remains Draft until:

1. exact-head hosted CI is green;
2. this plan has no unresolved release blocker;
3. documentation matches executable behavior;
4. packaging, trust, compatibility, performance and security gates are approved;
5. the human owner explicitly decides to promote or merge.
