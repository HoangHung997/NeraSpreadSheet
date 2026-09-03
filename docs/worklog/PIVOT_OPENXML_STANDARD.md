# PIVOT-OPENXML-STANDARD worklog

## Goal

Close the first standard Excel PivotTable interoperability lane for the existing
Nera pivot model.

## Starting checkpoint

- Branch: `feature/bootstrap-architecture-v0.1`.
- PR #1 remains Draft, open and unmerged.
- `RIBBON-MAUI` is closed at exact-head checkpoint
  `b806cc7ed2317b456a6171672e577ee816e4692d`.
- Q003D is preservation-only and must remain true when
  `PreserveUnknownParts = true`.

## Batch plan

| Batch | Status | Scope |
|---|---|---|
| `PIVOT-OPENXML-001` | Done | Materialize Nera-managed pivots as schema-valid standard PivotTable/PivotCache/PivotCacheRecords parts. |
| `PIVOT-OPENXML-002` | Done | Import compatible standard worksheet-range pivots into the Nera pivot model without weakening explicit preservation behavior. |
| `PIVOT-OPENXML-003` | Done locally, CI pending | Repeated Save/Load/Save idempotence and docs/status evidence. |

## Scope

- Supported now: one row field, one value field, worksheet-range source,
  header-row field names and Sum/Count/Average/Minimum/Maximum aggregation.
- Export includes standard cache fields, shared items, cache records,
  workbook-level pivot cache registration and worksheet PivotTable parts.
- Import targets compatible standard PivotTables when unknown-part preservation
  is not enabled.

## Out of scope

- Slicers, timelines, calculated fields/items, filters, multi-axis layouts,
  OLAP/external sources and broad Excel UI parity.
- Refresh/calculation equivalence beyond preserving source/cache records.
- User-editable pivot destination cells. Export currently uses a deterministic
  default location beside the source range.

## Local checkpoint

Implemented:

- Added `NeraOpenXmlPivotTableCodec` and wired it into
  `NeraOpenXmlSpreadsheetSessionSerializer`.
- Nera-managed pivots now export as standard PivotTable, PivotCacheDefinition
  and PivotCacheRecords parts.
- Compatible standard worksheet-range PivotTables import into the Nera pivot
  model during normal loads.
- Explicit `PreserveUnknownParts = true` behavior remains preservation-only and
  does not silently claim external PivotTables as managed pivots.
- Repeated Save/Load/Save keeps a single managed standard pivot package graph.

Validation:

- `dotnet test tests/NeraSpreadSheet.OpenXml.Tests/NeraSpreadSheet.OpenXml.Tests.csproj --no-restore`
  passed locally with **68/68** tests.
- `dotnet build NeraSpreadSheet.slnx` passed locally with **0 warnings** and
  **0 errors**.
- `dotnet test NeraSpreadSheet.Core.slnx --no-restore` passed locally with
  **1215/1215** tests.
- `scripts/verify-architecture.ps1` passed locally.

Current status:

`PIVOT-OPENXML-STANDARD` is local-green and needs exact-head GitHub CI before
being declared closed.
