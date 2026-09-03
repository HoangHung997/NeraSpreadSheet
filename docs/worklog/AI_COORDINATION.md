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
| 1 | `RIBBON-MAUI` | Codex | Local done, CI pending | MAUI Ribbon/Bar presenters, shortcut/input mapping, customization entry point, tests, loaded Windows smoke |
| 2 | `PIVOT-OPENXML-STANDARD` | Unclaimed | Next after CI | Standard pivot creation/import/cache-record compatibility with explicit scope docs and OpenXML gates |
| 3 | `DRAWING-MEDIA-COMPAT` | Unclaimed | Pending | Broader drawing/media preservation/materialization corpus |
| 4 | `PACKAGING-SDK` | Unclaimed | Pending | Package/versioning/API compatibility validation |
| 5 | `SECURITY-RECOVERY` | Unclaimed | Pending | Trust/isolation/recovery hardening and tests |
| 6 | `LOCALIZATION-A11Y-COMPLETE` | Unclaimed | Pending | Accessibility/localization gaps beyond analytics bridge |
| 7 | `WIN11-DEMO-APP` | Unclaimed | Pending | Runnable Windows 11 demo app packaging the finished stack |
| 8 | `FINAL-ACCEPTANCE` | Unclaimed | Pending | Full validation, docs, release evidence, PR ready criteria |

## Active lane: RIBBON-MAUI

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
