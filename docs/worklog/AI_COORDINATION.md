# AI coordination backlog

Repository: `HoangHung997/NeraSpreadSheet`
Branch: `feature/bootstrap-architecture-v0.1`
PR: #1 Draft, open, unmerged
Current roadmap score: `83.08% ~= 83%`

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
| 2 | `PIVOT-OPENXML-STANDARD` | Codex | Local done, CI pending | Standard pivot creation/import/cache-record compatibility with explicit scope docs and OpenXML gates |
| 3 | `DRAWING-MEDIA-COMPAT` | Unclaimed | Pending | Broader drawing/media preservation/materialization corpus |
| 4 | `PACKAGING-SDK` | Unclaimed | Pending | Package/versioning/API compatibility validation |
| 5 | `SECURITY-RECOVERY` | Unclaimed | Pending | Trust/isolation/recovery hardening and tests |
| 6 | `LOCALIZATION-A11Y-COMPLETE` | Unclaimed | Pending | Accessibility/localization gaps beyond analytics bridge |
| 7 | `WIN11-DEMO-APP` | Unclaimed | Pending | Runnable Windows 11 demo app packaging the finished stack |
| 8 | `FINAL-ACCEPTANCE` | Unclaimed | Pending | Full validation, docs, release evidence, PR ready criteria |

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

## Active lane: PIVOT-OPENXML-STANDARD

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
