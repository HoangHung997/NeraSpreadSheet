# NeraSpreadSheet current implementation status

## Overall roadmap implementation progress

The fixed weighted roadmap rubric in [`project-progress.md`](project-progress.md) currently evaluates to **83.98%**, reported as **84%**. This is an implementation-roadmap score, not a claim that NeraSpreadSheet implements 84% of every Microsoft Excel feature and not a production-readiness percentage.

## Formula subsystem — CLOSED

| Counter | Value |
|---|---:|
| Eager/versioned | 468 |
| AST/reference-aware | 40 |
| Dynamic-array unique | 38 |
| **Total** | **546 / 546 locked catalog names** |
| Formula/hardening tests | **522/522** |
| Completed formula cycles | F001-F019 |

The formula catalog is considered complete. New names are added only if a compatibility audit supplies concrete evidence worth reopening the catalog.

## Q001 — differential and fuzz hardening — DONE

Q001 locked deterministic scalar, arithmetic, dependency and malformed-input fuzz gates. Exact-head CI **#924** passed.

## Q002 — workbook/editing + OpenXML differential hardening — DONE

Q002 added deterministic `SpreadsheetSession` state-model fuzz, structural row/column model fuzz with atomic boundary rejection, and sparse/extreme OpenXML save-load-save differential corpora while retaining unknown-part/package preservation gates. Exact-head CI **#932** passed.

## Q003A — analytics foundation + shared vector rendering — DONE

Q003A added chart models/projection (Column, Bar, Line, Pie), pivot models/projection (Sum, Count, Average, Minimum, Maximum), per-worksheet analytics editing with Undo/Redo, shared DisplayList analytics rendering and common polygon rendering across Skia/Direct2D/WPF/WinForms. Exact-head CI **#958** passed.

## Q003B — floating analytics interaction + native accessibility — DONE

Validated capabilities include:

- host-neutral analytics placement state, viewport mapping and floating chart/pivot overlay composition;
- select, move, resize, Delete/Escape and normalized keyboard editing through shared Undo/Redo semantics;
- scroll/freeze/split integration and synchronized placement snapshots for UI/GPU concurrency;
- WPF native `AutomationPeer` analytics children;
- WinForms native `AccessibleObject` analytics children;
- MAUI Windows native UI Automation children over the GPU surface;
- Android/TalkBack-compatible virtual per-chart/per-pivot accessibility children with loaded emulator smoke;
- iOS/VoiceOver-compatible virtual `UIAccessibilityElement` chart/pivot children with loaded iOS Simulator smoke;
- Mac Catalyst/VoiceOver-compatible virtual `UIAccessibilityElement` chart/pivot children with loaded host smoke;
- native names/identifiers, roles, visible/clipped bounds and activation-to-selection behavior;
- preservation of the single GPU-backed spreadsheet surface without creating a native control per cell.

The previously bounded iOS runtime gap is closed. Q003B is no longer ACTIVE.

## Q003C — analytics/OpenXML managed chart persistence — DONE FOR DEFINED SCOPE

Implemented and validated:

- `SpreadsheetSession.SaveSessionAsync` automatically materializes managed charts into standard XLSX worksheet drawing/chart parts;
- native analytics metadata preserves chart/pivot identity, semantics, worksheet ownership and floating placement across session round trips;
- Save → Load → Save keeps a single managed chart/drawing relationship instead of accumulating duplicate/orphan managed parts;
- removing the final managed chart removes the now-empty Nera-managed drawing relationship/part;
- foreign/third-party drawing content survives analytics Save → Load → Save when the existing opt-in `PreserveUnknownParts = true` import/export contract is enabled;
- standard generated drawing/chart markup remains OpenXML-schema valid.

Q003C does not claim standard Excel pivot creation/import.

## Q003D — standard Excel PivotTable/PivotCache preservation — DONE FOR DEFINED SCOPE

Q003D was implemented test-first and required **no production serializer patch**: the existing package-envelope preservation path already preserves a schema-valid standard Excel PivotTable/PivotCache graph when preservation is explicitly enabled.

Regression coverage proves that:

- an existing workbook-level `pivotCaches` entry and `PivotTableCacheDefinitionPart` survive `SpreadsheetSession` Load → Save;
- the worksheet `PivotTablePart` survives and continues to point at the same cache-definition part;
- workbook → cache, worksheet → pivot and pivot → cache relationship IDs remain stable across repeated save cycles;
- standard pivot/cache part URIs, pivot name/cache ID and worksheet source `sheet`/`ref` metadata remain stable;
- the preserved standard pivot graph remains OpenXML-schema valid;
- a Nera-managed pivot may be added to the same session without destroying or taking ownership of the external standard PivotTable package graph;
- a standard external Excel PivotTable is not silently imported or reclassified as a Nera-managed pivot merely because package preservation is enabled.

This is intentionally **preservation interoperability**, not full pivot semantic interoperability. Still open: creation of standard Excel pivot parts from a Nera pivot, semantic import of an Excel pivot into the Nera model, cache-record interoperability, refresh/calculation equivalence, pivot destination-cell modeling, slicers/timelines and broader Excel UI parity.

### Combined exact-head evidence for Q003B/Q003C/Q003D

Combined implementation checkpoint: `ff7af0da897efc5007645905f529c4bdbe9eb202`.

- full CI: **#1209 / run 33248651484 — success**;
- iOS analytics accessibility gate: **#12 / run 33248651481 — success**;
- Q003C/OpenXML gate: **#9 / run 33248651547 — success**;
- Core solution: **1212/1212 passed**, **0 warnings**, **0 errors**;
- Core **110/110**;
- Editing **209/209**;
- Interaction **20/20**;
- Rendering.Spreadsheet **118/118**;
- Rendering.Skia **14/14**;
- Viewport **56/56**;
- Formulas **518/518**;
- OpenXML **65/65**;
- architecture verification: **passed**;
- Windows hosts + desktop GPU runtime smoke: **passed**;
- MAUI Android build + loaded analytics accessibility smoke: **passed**;
- MAUI iOS build + loaded analytics accessibility smoke: **passed**;
- MAUI Mac Catalyst build + loaded analytics accessibility smoke: **passed**;
- MAUI Windows build + handler-resolution + loaded Table-filter/runtime/analytics/scale smokes: **passed**.

## Ribbon and Bars SDK — DESKTOP STACK INTEGRATED

Desktop Ribbon/Bars from the Codex lane remains preserved in the combined checkpoint. Implemented and validated:

- immutable Ribbon/Bars customization for visibility, ordering and Ribbon item size;
- deterministic versioned JSON persistence with legacy migration and bounded input validation;
- command-state presentation snapshots and runtime controllers using the shared command dispatcher;
- native WPF and WinForms Ribbon, toolbar, menu and context-menu presenters;
- native WPF and WinForms customization dialogs with apply/reset/save/load flows;
- normalized shortcut resolution and WPF/WinForms keyboard bindings;
- loaded desktop smoke coverage for presentation, activation, state refresh, customization and shortcuts.

`RIBBON-MAUI` is **DONE** at exact-head checkpoint
`b806cc7ed2317b456a6171672e577ee816e4692d`. The implementation adds MAUI
Ribbon/Bar presenters, shortcut binding, Ribbon customization binding and a
loaded MAUI Windows Ribbon smoke. GitHub CI evidence: full CI **#1214 / run
33777851358 — success**, iOS analytics accessibility gate **#17 / run
33777851359 — success**, and Q003C/OpenXML gate **#14 / run 33777851399 —
success**.

## PIVOT-OPENXML-STANDARD — DONE FOR DEFINED SCOPE

Implemented and locally validated:

- Nera-managed pivots materialize as standard Excel PivotTable, PivotCache and
  PivotCacheRecords package parts during session save;
- compatible standard worksheet-range PivotTables import into the existing Nera
  pivot model during normal loads;
- repeated Save -> Load -> Save keeps a single managed standard pivot package
  graph instead of accumulating duplicate/orphan pivot parts;
- explicit `PreserveUnknownParts = true` keeps Q003D preservation-only behavior
  for external Excel pivots and does not silently reclassify them as
  Nera-managed pivots;
- generated standard pivot package graphs remain OpenXML-schema valid.

Supported scope remains intentionally bounded to one row field, one value
field, worksheet-range sources, header-row field names and
Sum/Count/Average/Minimum/Maximum aggregation. Slicers, timelines, calculated
fields/items, filters, multi-axis layouts, OLAP/external data sources,
refresh/calculation equivalence and user-editable pivot destination cells remain
future work.

## DRAWING-MEDIA-COMPAT — DONE FOR DEFINED SCOPE

Implemented and locally validated:

- worksheet drawing image content referenced from a `oneCellAnchor` survives
  repeated preserved session Load/Save cycles;
- worksheet background `picture` image relationships survive with stable
  relationship IDs, part URIs and bytes;
- legacy VML drawing parts referenced by `legacyDrawing` survive repeated
  preserved session saves;
- workbook edits made between round trips do not disturb third-party
  drawing/media package graphs;
- managed chart and standard pivot OpenXML gates remain green.

This lane is preservation-first. User-authored pictures, VML drawings,
background pictures, OLE/control objects, video/audio and rich shape semantics
remain opaque until Nera has first-class ownership models for them.

## PACKAGING-SDK — DONE FOR PACKAGE-READINESS GATE SCOPE

Implemented and locally validated:

- common NuGet project URL, README file and package tags are defined in
  `Directory.Build.props`;
- the root `README.md` is packed into every SDK package;
- all `src` SDK projects now declare explicit package IDs and descriptions;
- tests, samples and benchmarks are checked to remain non-packable;
- `scripts/verify-packaging-sdk.ps1` validates package metadata and source
  package boundaries;
- CI now runs the packaging verifier, packs `NeraSpreadSheet.Core.slnx`, and
  uploads generated `.nupkg` files as the `sdk-packages` artifact.

Local validation produced 17 SDK packages, confirmed every package contains
`README.md`, and kept Core Release tests at 1216/1216 with 0 build warnings and
0 errors.

Exact-head GitHub evidence at commit
`47591a8ac223f0ee5141e92bb31fca304fbe0a50`:

- full CI: **#1239 / run 33787957484 — success**;
- Core job: build/test, architecture verification, SDK packaging metadata
  verification, SDK package pack and artifact upload all passed;
- `sdk-packages` artifact: ID `9906198247`, digest
  `sha256:3d697797e79866e1420bd58702d574eb1469cd26856cdd57a3cfe80b8d77bd06`;
- iOS analytics accessibility gate: **#42 / run 33787957432 — success**;
- Q003C/OpenXML gate: **#39 / run 33787957448 — success**.

## SECURITY-RECOVERY — ACTIVE

First ten hardening patches are integrated and exact-head validated:

- external formula provider failures are contained at scalar, higher-order and
  dynamic-array boundaries and return `#N/A` instead of escaping through formula
  evaluation;
- `WEBSERVICE`, `CALL` and `STOCKHISTORY` provider exception paths are covered;
- document-level XLSX saves now snapshot seekable/readable destinations and
  restore the previous bytes if the final write or flush fails after package
  validation;
- workbook-level XLSX saves now use the same recovery path for generated
  packages and preserve-unknown output;
- session-level XLSX saves now use the same recovery path after applying split
  view, analytics, pivot and chart package metadata;
- document, workbook and session final package writes now use a shared internal
  recovery helper;
- the document, workbook and session save-failure recovery paths are covered by
  failing stream tests that verify existing destination bytes survive
  unchanged.
- preserve-unknown worksheet reference replacement is rejected before
  destination bytes are mutated.
- malformed percent-escapes in relationship targets are rejected before URI
  decode, while common Excel hyperlink/file/relative/fragment targets remain
  accepted.
- malformed percent-escapes in part URIs and relationship type URIs are covered
  by direct validator regressions while safe escaped part/type forms remain
  accepted.
- package-level XLSX archive scanning now validates every ZIP part name and
  `.rels` entry before OpenXML SDK load, covering malformed escaped internal
  part targets, relationship type URIs and external relationship targets even
  when the SDK would not materialize those relationships into the workbook
  object graph.
- `[Content_Types].xml` override `PartName` values are validated during archive
  scanning so malformed escaped package part names in the content-type table
  are rejected before OpenXML SDK load.
- duplicate ZIP entries are rejected during archive scanning so ambiguous
  repeated package part names cannot reach OpenXML SDK load or preservation
  merge paths.

Exact-head GitHub evidence at commit
`275c1b4e5e24b5c53d27546492c4e343509e127f`:

- full CI: **#1286 / run 33816357502 — success**;
- iOS analytics accessibility gate: **#106 / run 33816357459 — success**;
- Q003C/OpenXML gate: **#103 / run 33816357453 — success**;
- `sdk-packages` artifact: ID `9916694642`, digest
  `sha256:a2a7d18733f941a770202723f67bddb7212ee3ecff07a8e75ddacdbc6c9df306`.

## EXCEL-BASIC-COMPAT-001 — LOCAL IMPLEMENTATION COMPLETE

The user-reported workbook compatibility and reusable formula-editing gaps now
have a bounded SDK implementation:

- Excel differential fills that use only `bgColor`, including files with no
  `patternType`, import without aborting the workbook load;
- explicit unknown-part preservation tolerates unsupported conditional-format
  rules and preserves the original rule/`dxf` mapping on save;
- the supplied six-sheet workbook `Excel_Thuan Thanh 6789.xlsx` passes an
  in-memory Load -> Save -> Load smoke with its source SHA-256 unchanged and
  zero OpenXML schema validation errors;
- host-neutral formula completion, A1 point-mode insertion and static reference
  analysis APIs are available;
- shared rendering draws visible precedent outlines, and the WPF control binds
  suggestions, mouse-drag reference insertion and formula-cell highlights;
- formula-reference theme defaults retain stable record equality so viewport
  display-list cache keys remain reusable across equivalent default themes;
- OpenXML 83/83, Editing 214/214, Rendering.Spreadsheet 120/120 and Formulas
  522/522 pass locally; Core solution 1241/1241 and the focused WPF
  formula-editing smoke pass.

The demo-specific visible scrollbar and horizontal sheet-tab layout remain
outside this SDK change. Exact-head GitHub Actions evidence is pending.

## Current boundaries

PR #1 remains **Draft, open and unmerged**. Q003B, Q003C, the defined Q003D
preservation scope, `RIBBON-MAUI`, `PIVOT-OPENXML-STANDARD`,
`DRAWING-MEDIA-COMPAT` and `PACKAGING-SDK` are closed for their defined scopes.
`SECURITY-RECOVERY` remains active after the first ten validated hardening
patches. Major remaining roadmap areas include additional security/isolation
coverage, broader localization/accessibility completion and final
acceptance/release evidence.
