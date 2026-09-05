# TABLE-005 — Contextual Table Design

## Claim

- Checkpoint: `TABLE-005`.
- Owner: Codex task `TABLE-005 Contextual Table Design`.
- Branch: `feature/table-005-contextual-design`.
- Base integration SHA: `57a8c0c0fe8eb452bcb054432d2d37b9e9807e73`.
- Owned files/directories: Table lifecycle/model/controller and focused tests in
  `NeraSpreadSheet.Core`, `NeraSpreadSheet.Editing`, `NeraSpreadSheet.Commands`,
  `NeraSpreadSheet.Ribbon.Core`, Table OpenXML mapping, host-neutral bindings for
  WPF/WinForms/MAUI, checkpoint contract, and this task-specific worklog.
- Shared coordination files explicitly excluded: `docs/worklog/CURRENT.md`,
  `docs/current-status.md`, and `docs/worklog/RIBBON_TABLE_FILTER_UX.md`.

## Status

- State: `VALIDATED_LOCAL`.
- Implementation SHA: pending commit.
- Exact-head GitHub Actions: pending push.

## Completed

- Added one shared `SpreadsheetSession.TableDesign` selection projection and
  registered the complete 19-command Table lifecycle/design catalog without a
  Ribbon dependency in Editing.
- Filled the production contextual `table-design` tab and added disposable
  WPF, WinForms, and MAUI bindings which project the same session state into the
  existing Ribbon runtime.
- Implemented Create, resize, header/totals row, style options, calculated and
  totals formulas, Table-local row/column insert/delete, remove duplicates, and
  convert-to-range through `SpreadsheetSession.Tables`.
- Each successful mutation is one history entry. Metadata, sparse affected
  cells, selection, stable Table/column IDs, filter/sort offsets, dependency
  graph rebuild, incremental recalculation, Undo, and Redo share that operation.
- Header visibility uses a dedicated bounded row grow/shrink so data is not
  overwritten. Empty header-only Tables can receive their first data row.
- Added atomic merge/spill/overlap/bounds/occupancy/reference validation. A1
  references which cannot retain identity under a Table-local rectangular
  compact are rejected before history; structured references retain stable IDs.
- Added bounded, cached TABLE-004 style previews (maximum 256 entries and 12 x
  12 cells per preview) and bounded remove-duplicates work (100,000 rows and
  1,000,000 key cells).
- Added `ShowFilterButtons` metadata, shared geometry suppression, and schema-
  valid XLSX read/write which preserves criteria while buttons are hidden.
- Documented the behavior and intentional limits in
  `docs/table-contextual-design-contract.md`.

## Primary files

- `src/NeraSpreadSheet.Editing/SpreadsheetTableController.Design.cs`
- `src/NeraSpreadSheet.Editing/SpreadsheetTableDesignController.cs`
- `src/NeraSpreadSheet.Editing/SpreadsheetSession.cs`
- `src/NeraSpreadSheet.Core/Tables.cs`
- `src/NeraSpreadSheet.OpenXml/OpenXmlTableCodec.cs`
- `src/NeraSpreadSheet.Ribbon.Core/RibbonProductionCommandCatalog.cs`
- `src/NeraSpreadSheet.Wpf/NeraWpfTableDesignRibbonBinding.cs`
- `src/NeraSpreadSheet.WinForms/NeraWinFormsTableDesignRibbonBinding.cs`
- `src/NeraSpreadSheet.Maui/NeraMauiTableDesignRibbonBinding.cs`
- `tests/NeraSpreadSheet.Editing.Tests/SpreadsheetTableDesignControllerTests.cs`
- `tests/NeraSpreadSheet.Commands.Tests/TableDesignCommandTests.cs`

## Verification

- Exact SDK `10.0.302`, Release build of `NeraSpreadSheet.Core.slnx`: passed,
  zero warnings and zero errors.
- Full Core solution tests: **1,388 passed, 0 failed, 0 skipped**. This includes
  Editing 260, Formulas 526, Core 134, Commands 102, OpenXML 96, Rendering 128,
  Viewport 61, and the remaining foundation/layout/interaction/export suites.
- Loaded WPF + WinForms Table Design binding smoke: **1 passed**, with real host
  controls, contextual selection transition, command execution, and history.
- MAUI Windows build with installed SDK/workload `10.0.201`: passed.
- MAUI host-neutral tests: **42 passed, 0 failed, 0 skipped**.
- Loaded MAUI Windows Ribbon smoke: **success**, 3 rendered frames, with marker
  `tableDesign=selection-context-binding` plus customization, bounded overflow,
  and all complex item kinds. One earlier pre-final run observed the existing
  split-button focus timing flake; the republished final binary passed its first
  completed run.
- `scripts/verify-architecture.ps1`: passed.
- `scripts/verify-packaging-sdk.ps1`: passed.
- `git diff --check`: passed.
- Ownership scan: no shared coordination file changed.
- Sensitive-data scan: no credential, token, machine ID, or local user path in
  the product diff.
- Benchmark was not run: TABLE-005 does not change the display-list, layout, or
  scrolling algorithm. The only render hot-path change is one boolean geometry
  guard; style previews and duplicate scans have explicit cache/work bounds.

## Intentional limits and risk

- Rename, resize, calculated/custom formula, and remove-duplicates column input
  use typed command parameters supplied by an application dialog/editor; the
  shared Ribbon runtime does not yet define a cross-platform text/range editor.
- Per-column producer-specific XLSX filter-button visibility is normalized to
  Table-wide visibility; hidden criteria remain preserved.
- Table-local compact operations reject direct A1 reference cases that cannot be
  mapped without moving the rest of the worksheet. They do not guess or silently
  apply copy-delta semantics.
- Rollback: revert the TABLE-005 implementation and handoff commits; no package,
  schema migration, or persisted model outside the XLSX-compatible Table metadata
  was introduced.

## Next step

Commit and push the validated branch, then require all three GitHub Actions
workflows to pass on the exact pushed HEAD before handoff.
