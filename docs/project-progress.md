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
| File I/O, OpenXML, print & PDF | 12% | 82% | 9.84 | Preservation, Q002 round-trip corpus, printing and PDF are validated; analytics/drawing/pivot package persistence remains. |
| Tables/filter/native UX | 8% | 90% | 7.20 | Tables/AutoFilter, native/paged presenters and the desktop Ribbon/Bars stack are implemented with loaded host smoke; MAUI Ribbon and broader UX edges remain. |
| Charts/pivots/drawing | 10% | 75% | 7.50 | Q003A foundation and most Q003B floating interaction are implemented; native accessibility and package persistence remain. |
| Hardening/differential/performance | 8% | 70% | 5.60 | Q001/Q002 fuzz/differential gates and multi-host runtime smokes exist; broader visual/performance/security corpora remain. |
| Packaging/API/SDK distribution | 6% | 45% | 2.70 | API/module, Function Extension SDK and modular Ribbon/Bars foundations exist; distribution/versioning compatibility remains incomplete. |
| Security/isolation/recovery | 5% | 30% | 1.50 | Baseline architecture/security discipline exists; plugin trust/isolation and recovery hardening remain substantial. |
| Localization/accessibility | 4% | 40% | 1.60 | Shared semantic/accessibility models exist; native analytics bridges and broad localization/a11y completion remain. |
| Final acceptance/evidence/release | 3% | 20% | 0.60 | Strong CI evidence exists, but final product acceptance/release packaging is not yet closed. |
| **Total** | **100%** |  | **79.84** | **Reported project progress: 80%** |

## Current checkpoint

- Combined implementation checkpoint: `716db8c4765e5259a71fd304e624942fa6ae73d8`.
- Exact-head CI run: **33230558230 — success**.
- Core solution: **1206/1206 passed**, **0 warnings**, **0 errors**.
- Architecture verification and all Windows/Android/iOS/Mac Catalyst/MAUI Windows loaded smoke gates passed.
- Desktop Ribbon/Bars is integrated; `RIBBON-MAUI` remains.
- Q003B remains active only for loaded iOS/VoiceOver runtime validation.

Future status reports should quote both the exact percentage and the rounded percentage when the rubric changes, for example `81.26% ≈ 81%`.
