# DRAWING-MEDIA-COMPAT worklog

## Goal

Broaden OpenXML drawing/media compatibility coverage beyond managed chart
materialization while preserving the existing package-envelope contract.

## Starting checkpoint

- Branch: `feature/bootstrap-architecture-v0.1`.
- PR #1 remains Draft, open and unmerged.
- `RIBBON-MAUI` is closed at exact-head checkpoint
  `b806cc7ed2317b456a6171672e577ee816e4692d`.
- `PIVOT-OPENXML-STANDARD` is closed for the first standard PivotTable
  interoperability scope at remote integration commit
  `0ee635e54f7b13bf4fc85f306eda1c1723669692`.
- Existing Q003C and Q003D gates must remain green.

## Batch plan

| Batch | Status | Scope |
|---|---|---|
| `DRAWING-MEDIA-001` | Done locally | Preserve worksheet drawing image anchors, worksheet background pictures and legacy VML drawing parts through repeated preserved session saves. |
| `DRAWING-MEDIA-002` | Done | Keep this roadmap phase preservation-only because Nera has no first-class model for user-authored pictures, VML drawings or background pictures yet. |
| `DRAWING-MEDIA-003` | Done locally | Update status docs and collect local plus GitHub exact-head evidence. |

## Scope

- Covered now: `DrawingsPart` image content referenced from a `oneCellAnchor`,
  worksheet-level background `picture` image relationships and
  `legacyDrawing` VML parts.
- The lane is preservation-first: these third-party package graphs stay opaque
  unless Nera has a first-class model that can own them safely.
- Repeated session Load/Save cycles should keep relationship IDs, part URIs,
  bytes and worksheet relationship markup stable when
  `PreserveUnknownParts = true`.

## Out of scope

- User-facing drawing/image editing tools.
- Full shape, SmartArt, WordArt, comment, OLE/control, video/audio and external
  link semantic import.
- Changing managed chart or standard pivot ownership semantics unless a
  regression is discovered.

## Local checkpoint

Implemented:

- Added `DrawingMediaCompatibilityTests`.
- The new test builds a standard XLSX fixture with a worksheet drawing image,
  a sheet background image and a legacy VML drawing part.
- Repeated preserved `LoadSessionAsync` / `SaveSessionAsync` cycles keep the
  drawing/media graph stable while workbook cells are edited.

Validation:

- `dotnet test tests/NeraSpreadSheet.OpenXml.Tests/NeraSpreadSheet.OpenXml.Tests.csproj --no-restore --filter DrawingMediaCompatibilityTests`
  passed locally with **1/1** tests.
- `dotnet test tests/NeraSpreadSheet.OpenXml.Tests/NeraSpreadSheet.OpenXml.Tests.csproj --no-restore`
  passed locally with **69/69** tests.
- `dotnet build NeraSpreadSheet.slnx --no-restore` passed locally with
  **0 warnings** and **0 errors**.
- `dotnet test NeraSpreadSheet.Core.slnx --no-restore` passed locally with
  **1216/1216** tests.
- `scripts/verify-architecture.ps1` passed locally.
- Remote branch integration reached commit
  `53c9da051a0a616e194058690e016e0f756a7b7f` with the same tree as the local
  validated implementation:
  `f85d0e5a7f3ac7853dc33458abc114e62c15c44a`.

Current status:

`DRAWING-MEDIA-COMPAT` is closed for the defined first drawing/media
preservation scope. Follow-on drawing/media work remains listed in the
out-of-scope section above.
