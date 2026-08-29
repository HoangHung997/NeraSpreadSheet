# NeraSpreadSheet project progress rubric

## Purpose

This file defines the stable percentage used in project status reports. It measures completion of the repository's current implementation roadmap. It is **not** a percentage of every Microsoft Excel feature and it is **not** a production-release readiness score.

The weights below stay fixed unless roadmap scope materially changes. A row's completion percentage should move only when repository evidence and exact-head CI support the change. If the weights are ever changed, the commit must document why.

## Weighted roadmap score

| Roadmap pillar | Weight | Current completion | Weighted points | Evidence / remaining gap |
|---|---:|---:|---:|---|
| Core spreadsheet engine & editing | 16% | 100% | 16.00 | Sparse workbook, editing, structural transforms, Undo/Redo and Q002 state-model fuzz validated. |
| Formula/calculation compatibility | 14% | 100% | 14.00 | Locked catalog **546/546**, formula/hardening suite **518/518**. |
| Rendering/scrolling/native hosts | 14% | 95% | 13.30 | Fractional/pixel scrolling, DisplayList, WPF/WinForms/MAUI, Direct2D/Skia and host smokes are established; broader visual/hardware corpus remains. |
| File I/O, OpenXML, print & PDF | 12% | 86% | 10.32 | Q002 preservation/round-trip corpus plus Q003C analytics session persistence and standard managed chart/drawing materialization are validated; standard Excel pivot-table package interoperability and broader drawing/media preservation remain. |
| Tables/filter/native UX | 8% | 90% | 7.20 | Tables/AutoFilter, native/paged presenters and the desktop Ribbon/Bars stack are implemented with loaded host smoke; MAUI Ribbon and broader UX edges remain. |
| Charts/pivots/drawing | 10% | 88% | 8.80 | Q003A and Q003B are closed; chart placement/interaction/accessibility is cross-platform and managed charts persist as standard XLSX drawings. Standard Excel pivot-table parts and broader drawing object types remain. |
| Hardening/differential/performance | 8% | 72% | 5.76 | Q001/Q002 fuzz/differential gates, Q003C preservation regression and multi-host runtime smokes exist; broader visual/performance/security corpora remain. |
| Packaging/API/SDK distribution | 6% | 45% | 2.70 | API/module, Function Extension SDK and modular Ribbon/Bars foundations exist; distribution/versioning compatibility remains incomplete. |
| Security/isolation/recovery | 5% | 30% | 1.50 | Baseline architecture/security discipline exists; plugin trust/isolation and recovery hardening remain substantial. |
| Localization/accessibility | 4% | 65% | 2.60 | Floating analytics now have native per-item exposure and loaded runtime validation across desktop, Android, iOS and Mac Catalyst; broader editor accessibility and localization coverage remain. |
| Final acceptance/evidence/release | 3% | 20% | 0.60 | Strong CI evidence exists, but final product acceptance/release packaging is not yet closed. |
| **Total** | **100%** |  | **82.78** | **Reported project progress: 83%** |

## Current checkpoint

- Combined implementation checkpoint: `5500d2e7cae8097891f5d152afd0a6f89acd08a6`.
- Full exact-head CI: **#1204 / run 33242739854 — success**.
- iOS analytics accessibility gate: **#7 / run 33242739858 — success**.
- Q003C analytics OpenXML gate: **#4 / run 33242739856 — success**.
- Core solution: **1210/1210 passed**, **0 warnings**, **0 errors**; OpenXML **63/63**.
- Architecture verification, Windows desktop GPU, Android loaded accessibility, iOS loaded accessibility, Mac Catalyst loaded accessibility and MAUI Windows Table-filter/runtime/analytics/scale smokes all passed.
- Q003B is **DONE**.
- Q003C is **DONE for its defined managed analytics/chart persistence scope**; this does not claim standard Excel pivot-table-part interoperability.
- Desktop Ribbon/Bars is integrated; `RIBBON-MAUI` remains a separate Codex lane.

The increase from 79.84% to 82.78% is limited to evidence-backed movement in OpenXML persistence, charts/analytics interaction, hardening and native accessibility. No roadmap pillar weight changed.

Future status reports should quote both the exact percentage and the rounded percentage when the rubric changes, for example `82.78% ≈ 83%`.
