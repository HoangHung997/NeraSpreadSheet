# AI coordination backlog

Repository: `HoangHung997/NeraSpreadSheet`
Branch: `feature/bootstrap-architecture-v0.1`
PR: #1 Draft, open, unmerged
Current roadmap score: `83.98% ~= 84%`

This file is the shared coordination ledger for Codex/ChatGPT work. Agents must
claim one lane at a time, keep scope boundaries explicit, and update this file
before moving to another lane.

## Operating rules

- Keep PR #1 Draft until final acceptance/release gates are green at exact HEAD.
- Do not reopen the formula catalog without concrete compatibility evidence.
- Do not treat Q003D as standard pivot creation/import; it is preservation only.
- Do not create native controls per cell.
- Update `docs/worklog/CURRENT.md` and the relevant lane worklog before handoff.
- Each lane closes only after local tests plus exact-head CI evidence where
  available.

## Lane queue

| Order | Lane | Owner | Status | Exit evidence |
|---:|---|---|---|---|
| 1 | `RIBBON-MAUI` | Codex | Done | MAUI Ribbon/Bar presenters, shortcut/input mapping, customization entry point, tests, loaded Windows smoke; exact-head CI #1214, iOS gate #17, OpenXML gate #14 all success |
| 2 | `PIVOT-OPENXML-STANDARD` | Codex | Done | Standard pivot creation/import/cache-record compatibility with explicit scope docs and OpenXML/Core/architecture gates |
| 3 | `DRAWING-MEDIA-COMPAT` | Codex | Done | Broader drawing/media preservation/materialization corpus |
| 4 | `PACKAGING-SDK` | Codex | Done | Package metadata/readme/pack-artifact gate exact-head validated; CI #1239, iOS gate #42, OpenXML gate #39 all success |
| 5 | `SECURITY-RECOVERY` | Codex | Active | External provider, document/workbook/session save recovery, preserve-unknown topology rejection and OpenXML graph escaped URI hardening plus archive-level duplicate-entry/package relationship/content-type scanning exact-head validated; lane remains active for next bounded surface |
| 6 | `LOCALIZATION-A11Y-COMPLETE` | Unclaimed | Pending | Accessibility/localization gaps beyond analytics bridge |
| 7 | `WIN11-DEMO-APP` | Unclaimed | Pending | Runnable Windows 11 demo app packaging the finished stack |
| 8 | `FINAL-ACCEPTANCE` | Unclaimed | Pending | Full validation, docs, release evidence, PR ready criteria |

User-reported compatibility interrupts may be handled as bounded hotfix lanes
without transferring ownership of the active roadmap lane. `EXCEL-BASIC-COMPAT-001`
and `EXCEL-BASIC-NAV-002` are such lanes. The latter owns the reusable adaptive
navigation extent and desktop keyboard-follow behavior, while demo-only chrome
remains outside the repository.

`EXCEL-BASIC-NAV-003` refines that reusable contract with a bounded scrollable
tail and independent viewport/selection behavior after direct Excel comparison.
The external demo consumes the SDK behavior, but its visual styling remains
outside the repository.

## Closed lane: RIBBON-MAUI

Scope:

- Add MAUI-native Ribbon and Bar presenters backed by existing
  `RibbonRuntimeController` and `BarRuntimeController`.
- Reuse existing command runtime, presentation, customization and shortcut
  contracts.
- Keep presenters independent from workbook ownership and spreadsheet render hot
  paths.
- Cover creation, rebuild, command activation, state refresh and shortcut
  resolution with MAUI tests.
- Add loaded MAUI Windows smoke only after the testable presenter surface is in
  place.

Out of scope for this lane:

- Standard pivot creation/import.
- Broader drawing/media compatibility.
- Product packaging and final Win11 demo integration.
- Reworking WPF/WinForms Ribbon behavior unless required to preserve shared
  contracts.

## Claim log

| Date | Agent | Lane | Notes |
|---|---|---|---|
| 2026-09-03 | Codex | `RIBBON-MAUI` | Workspace cloned and active lane claimed from PR #1 handoff. |
| 2026-09-03 | Codex | `RIBBON-MAUI` | Local implementation complete: MAUI Ribbon/Bar presenters, shortcut/customization binding, Windows Ribbon smoke, architecture pass, Core 1212/1212, MAUI 34/34. |
| 2026-09-03 | Codex | `RIBBON-MAUI` | Closed after exact-head GitHub evidence on `b806cc7ed2317b456a6171672e577ee816e4692d`: CI #1214 success, iOS gate #17 success, OpenXML gate #14 success. |
| 2026-09-03 | Codex | `PIVOT-OPENXML-STANDARD` | Claimed after `RIBBON-MAUI` exact-head CI passed; scoped to basic standard pivot creation/import/cache records for the existing Nera one-row-field/one-value-field model. |
| 2026-09-03 | Codex | `PIVOT-OPENXML-STANDARD` | Local implementation complete: standard PivotTable/PivotCache/PivotCacheRecords export, compatible semantic import, preservation-only behavior retained for `PreserveUnknownParts`, OpenXML 68/68, Core 1215/1215, architecture pass, solution build 0 warnings/errors. |
| 2026-09-04 | Codex | `PIVOT-OPENXML-STANDARD` | Remote integration complete at `0ee635e54f7b13bf4fc85f306eda1c1723669692` with matching local tree `0b4f9d403bf6d80489ac1949604ba0ca15173b59`; moving to `DRAWING-MEDIA-COMPAT`. |
| 2026-09-04 | Codex | `DRAWING-MEDIA-COMPAT` | Claimed after Pivot standard package lane; scope is broader drawing/media preservation and materialization without reopening completed analytics/pivot contracts. |
| 2026-09-04 | Codex | `DRAWING-MEDIA-COMPAT` | Added first local preservation gate for worksheet drawing image anchors, sheet background pictures and legacy VML drawing parts through repeated preserved session saves. OpenXML 69/69, Core 1216/1216, solution build 0 warnings/errors and architecture pass are green locally. |
| 2026-09-04 | Codex | `DRAWING-MEDIA-COMPAT` | Remote integration complete at `53c9da051a0a616e194058690e016e0f756a7b7f` with matching local tree `f85d0e5a7f3ac7853dc33458abc114e62c15c44a`; moving to `PACKAGING-SDK`. |
| 2026-09-04 | Codex | `PACKAGING-SDK` | Claimed after Drawing/Media compatibility lane; scope is package/versioning/API compatibility validation before security/recovery and Win11 demo lanes. |
| 2026-09-04 | Codex | `PACKAGING-SDK` | Local implementation complete: common package URL/readme/tags, explicit SDK `PackageId`s, packaging verifier, CI pack step and SDK package artifact upload. Local Release Core 1216/1216, architecture and packaging verifiers passed, and 17 SDK packages include `README.md`. |
| 2026-09-04 | Codex | `PACKAGING-SDK` | Closed at `47591a8ac223f0ee5141e92bb31fca304fbe0a50`: exact-head CI #1239, iOS gate #42 and Q003C/OpenXML gate #39 all succeeded; `sdk-packages` artifact ID `9906198247` uploaded with digest `sha256:3d697797e79866e1420bd58702d574eb1469cd26856cdd57a3cfe80b8d77bd06`. |
| 2026-09-04 | Codex | `EXCEL-BASIC-NAV-002` | Bounded compatibility hotfix locally complete: adaptive used/navigation extent, WPF/WinForms keyboard-follow scrolling, Viewport 58/58, loaded desktop smoke 2/2, Core 1243/1243 and architecture pass. Exact-head CI pending. |
| 2026-09-04 | Codex | `EXCEL-BASIC-NAV-002` | Closed at `3a4b8e885b204d99354216a29dd9ead759672c8c`: full CI #1292, iOS gate #113 and Q003C/OpenXML gate #110 all succeeded. |
| 2026-09-04 | Codex | `EXCEL-BASIC-NAV-003` | Implemented locally: configurable 100-row/20-column adaptive tail, current-viewport retention without compounded growth, independent scrollbar/selection behavior, Viewport 59/59 and focused desktop smoke 2/2. Exact-head CI pending. |
| 2026-09-04 | Codex | `EXCEL-BASIC-NAV-003` | Closed at `45fae777c58a4a83bb65716c3dc3aecf71c5dd83`: full CI #1294, iOS gate #115 and Q003C/OpenXML gate #112 all succeeded. |
| 2026-09-04 | Codex | `SECURITY-RECOVERY` | Claimed after PACKAGING-SDK exact-head evidence; scope is trust/isolation/recovery hardening before broader localization/a11y and Win11 demo lanes. |
| 2026-09-04 | Codex | `SECURITY-RECOVERY` | First hardening patch prepared: external formula provider exceptions are contained and mapped to `#N/A`, with scalar and dynamic-array provider boundary tests. Local SDK 10.0.302 is unavailable in this workspace session, so validation is deferred to GitHub CI. |
| 2026-09-04 | Codex | `SECURITY-RECOVERY` | First two hardening patches exact-head validated at `dd0134a43f3888b78b64106e45210acf9231d83a`: full CI #1248, iOS gate #51 and Q003C/OpenXML gate #48 all succeeded. Document save failure recovery is covered and SDK packages artifact `9907540966` was uploaded. |
| 2026-09-04 | Codex | `SECURITY-RECOVERY` | Workbook save failure recovery exact-head validated at `e819c9b26c2136cc6fd9d08c8e4711f6129c888b`: full CI #1254, iOS gate #60 and Q003C/OpenXML gate #57 all succeeded. SDK packages artifact `9910001536` was uploaded. |
| 2026-09-04 | Codex | `SECURITY-RECOVERY` | Session save failure recovery and shared package-write recovery helper exact-head validated at `06feca4168cb18fb5182601ee7654bd6887dfde5`: full CI #1257, iOS gate #64 and Q003C/OpenXML gate #61 all succeeded. SDK packages artifact `9910892535` was uploaded. |
| 2026-09-04 | Codex | `SECURITY-RECOVERY` | Preserve-unknown worksheet reference replacement rejection exact-head validated at `472e89ce787c1071d80b119025c176b6abb78c57`: full CI #1264, iOS gate #73 and Q003C/OpenXML gate #70 all succeeded. SDK packages artifact `9912165390` was uploaded. |
| 2026-09-04 | Codex | `SECURITY-RECOVERY` | OpenXML graph malformed escaped relationship target rejection exact-head validated at `7faaf10f940b9b1c165d499b71a27f2d1b2ba51e`: full CI #1268, iOS gate #79 and Q003C/OpenXML gate #76 all succeeded. SDK packages artifact `9913122998` was uploaded. |
| 2026-09-04 | Codex | `SECURITY-RECOVERY` | OpenXML graph escaped part URI and relationship type regression coverage exact-head validated at `9d0a5c07bd2c53ab64b98bf4890a15d8d1028123`: full CI #1272, iOS gate #85 and Q003C/OpenXML gate #82 all succeeded. SDK packages artifact `9913929809` was uploaded. |
| 2026-09-04 | Codex | `SECURITY-RECOVERY` | Package-level OpenXML archive relationship scan exact-head validated at `7f87a9bc2d7e8cb2d26b5b66210f1fa35005d839`: local Core 1226/1226 and OpenXML 79/79 passed; full CI #1278, iOS gate #94 and Q003C/OpenXML gate #91 all succeeded. SDK packages artifact `9915090019` was uploaded. |
| 2026-09-04 | Codex | `SECURITY-RECOVERY` | Content-type override `PartName` validation exact-head validated at `af80a91df5b077077b0017d59ce5fd09eb5e5f52`: local Core 1227/1227 and OpenXML 80/80 passed; full CI #1282, iOS gate #100 and Q003C/OpenXML gate #97 all succeeded. SDK packages artifact `9915968341` was uploaded. |
| 2026-09-04 | Codex | `SECURITY-RECOVERY` | Duplicate ZIP package part entry rejection exact-head validated at `275c1b4e5e24b5c53d27546492c4e343509e127f`: local Core 1228/1228 and OpenXML 81/81 passed; full CI #1286, iOS gate #106 and Q003C/OpenXML gate #103 all succeeded. SDK packages artifact `9916694642` was uploaded. |
| 2026-09-04 | Codex | `EXCEL-BASIC-COMPAT-001` | Bounded user-reported hotfix implemented locally: tolerant Excel differential fills and opaque unsupported conditional-format preservation; host-neutral formula suggestions, point-mode references and static reference analysis; visible reference outlines and WPF integration. Supplied six-sheet workbook Load -> Save -> Load passes without modifying the source. Exact-head CI pending. |
| 2026-09-04 | Codex | `EXCEL-BASIC-COMPAT-001` | Closed for defined scope at `29d07537243d937e3ab83b7c03e71f2041c8d4f5`: full CI #1290, iOS gate #111 and Q003C/OpenXML gate #108 all succeeded. Demo scrollbar and horizontal sheet-tab layout remain outside this SDK hotfix. |
| 2026-09-04 | Codex | `EXCEL-BASIC-COMPAT-002` | User-workbook recalculation regression fixed locally: 981 errors reduced to the original 28 with zero new errors; sparse whole-column VLOOKUP, path-aware lookup errors, WPF Ctrl+wheel zoom and cell-style-aware multiline editor added. Core 1246/1246, formula 524/524 and external workbook smoke pass; exact-head CI pending. |
| 2026-09-04 | Codex | `EXCEL-BASIC-COMPAT-002` | Closed for defined scope at `d86376403e15011304974c5476fe11683347fd19`: full CI #1296, iOS gate #117 and Q003C/OpenXML gate #114 all succeeded. |
| 2026-09-04 | Codex | `EXCEL-BASIC-VISIBILITY-004` | Local implementation complete after observing Excel row 107 -> 149 and column A -> C navigation across hidden axes; sparse SDK hide/unhide, undo/redo, OpenXML, WPF/WinForms normal and split navigation, samples and external demo are green locally. Exact-head CI pending. |
| 2026-09-04 | Codex | `EXCEL-BASIC-VISIBILITY-004` | Closed for defined scope at `944fadd9864bfeca41abf9ff8e155305fc8cd06c`: full CI #1298, iOS gate #119 and Q003C/OpenXML gate #116 succeeded; SDK package artifact 9928856103 was produced. |

## Closed lane: PIVOT-OPENXML-STANDARD

Scope:

- Materialize Nera-managed pivots as standard XLSX PivotTable, PivotCache and
  PivotCacheRecords package parts.
- Import compatible standard worksheet-range PivotTables into the existing Nera
  pivot model when unknown-part preservation is not enabled.
- Preserve Q003D behavior: external standard PivotTables are not silently
  claimed as Nera-managed pivots during explicit unknown-part preservation.
- Keep the first implementation aligned with the existing Nera pivot model:
  one row field, one value field, Sum/Count/Average/Minimum/Maximum aggregation
  and worksheet-range sources.

Out of scope for this lane:

- Slicers, timelines, calculated fields/items, filters, multi-axis pivot
  layouts and OLAP/external data sources.
- Full Excel refresh/calculation parity.
- User-mode pivot destination editing; export uses a deterministic default
  placement beside the source range until destination-cell modeling is added.

## Closed lane: DRAWING-MEDIA-COMPAT

Scope:

- Inventory the existing OpenXML drawing/media coverage after managed charts and
  pivot package gates.
- Add focused preservation/materialization tests for worksheet drawing content
  not yet covered by managed charts.
- Preserve explicit unknown-part behavior for third-party drawing/media package
  graphs.
- Keep chart and pivot OpenXML gates green while expanding drawing/media
  compatibility.

Batch status:

- `DRAWING-MEDIA-001`: Done.
- `DRAWING-MEDIA-002`: Done; preservation-only is the right scope until Nera has
  a first-class model for user-authored pictures, VML drawings and background
  pictures.
- `DRAWING-MEDIA-003`: Done locally; exact-head GitHub evidence is collected on
  the follow-up direct checkpoint commit.

Out of scope for this lane:

- Product packaging and Win11 demo integration.
- Reopening formula, Q003B accessibility, Q003C managed chart or
  `PIVOT-OPENXML-STANDARD` semantics unless a regression is discovered.

## Closed lane: PACKAGING-SDK

Scope:

- Inventory NuGet/package metadata, public API surface and sample integration
  expectations after the OpenXML/Ribbon/MAUI lanes.
- Add focused validation for versioning and package readiness without changing
  runtime behavior unnecessarily.
- Keep PR #1 Draft until final acceptance/release gates are complete.

Out of scope for this lane:

- Trust/isolation/recovery hardening.
- Final Windows 11 demo app packaging.
- Publishing packages or marking the PR ready.

Batch status:

- `PACKAGING-SDK-001`: Done.
- `PACKAGING-SDK-002`: Done.
- `PACKAGING-SDK-003`: Done; exact-head CI #1239, iOS gate #42 and Q003C/OpenXML
  gate #39 all succeeded.

## Active lane: SECURITY-RECOVERY

Scope:

- Inventory plugin/external input boundaries, persistence recovery paths and
  renderer/session failure modes.
- Add focused trust/isolation/recovery tests before final demo packaging.
- Keep PR #1 Draft until final acceptance/release gates are complete.

Out of scope for this lane:

- Final Windows 11 demo app packaging.
- Publishing packages or marking the PR ready.
- Broad localization/a11y completion beyond security-relevant recovery paths.

Batch status:

- `SECURITY-RECOVERY-001`: Done for first pass; inventory current trust,
  recovery and failure-handling surfaces.
- `SECURITY-RECOVERY-002`: Done for the first ten patches; external provider
  exception containment plus document, workbook and session save failure
  recovery, preserve-unknown worksheet topology rejection and OpenXML graph
  malformed escaped URI rejection plus archive-level duplicate-entry, package
  relationship and content-type override scanning are integrated.
- `SECURITY-RECOVERY-003`: In progress; exact-head evidence collected for the
  first ten patches, lane remains active for additional bounded coverage.

## Active lane: EXCEL-FORMAT-EDIT-HELP-005

Owner: Codex on `feature/excel-format-editing-help`.

Scope:

- shared Excel-compatible formatting and expanded Format Cells round trip;
- WPF full-cell editor geometry and Enter/Alt+Enter behavior;
- indexed dependency preparation plus affected-only edit recalculation;
- signature, description and active-argument help for the implemented formula
  surface;
- integration and packaging of the external Windows 11 x64 demo.

Coordination boundary:

- Do not modify the files in this lane from another branch until its exact-head
  CI result is recorded here.
- The external demo directory is outside this repository and is not a merge
  source; only reusable SDK behavior belongs in Git.

Status: implementation and local gates passed; commit/push/exact-head CI and
the final demo publish archive are pending.
