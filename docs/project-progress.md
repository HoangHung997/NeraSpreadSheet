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
| File I/O, OpenXML, print & PDF | 12% | 87% | 10.44 | Q002 round-trip/package preservation, Q003C managed chart materialization and Q003D preservation of an existing standard PivotTable/PivotCache relationship graph are validated. Native standard-pivot creation/import and broader drawing/media compatibility remain. |
| Tables/filter/native UX | 8% | 90% | 7.20 | Tables/AutoFilter, native/paged presenters and the desktop Ribbon/Bars stack are implemented with loaded host smoke; MAUI Ribbon and broader UX edges remain. |
| Charts/pivots/drawing | 10% | 89% | 8.90 | Q003A/Q003B interaction/accessibility are closed, managed charts persist as standard XLSX drawings, and existing standard Excel pivot package graphs survive preserved session round trips. Standard pivot creation/import and broader drawing object types remain. |
| Hardening/differential/performance | 8% | 73% | 5.84 | Q001/Q002 fuzz/differential gates, Q003C drawing preservation and Q003D standard-pivot graph regression coverage exist; broader visual/performance/security corpora remain. |
| Packaging/API/SDK distribution | 6% | 45% | 2.70 | API/module, Function Extension SDK and modular Ribbon/Bars foundations exist; distribution/versioning compatibility remains incomplete. |
| Security/isolation/recovery | 5% | 30% | 1.50 | Baseline architecture/security discipline exists; plugin trust/isolation and recovery hardening remain substantial. |
| Localization/accessibility | 4% | 65% | 2.60 | Floating analytics have native per-item exposure and loaded runtime validation across desktop, Android, iOS and Mac Catalyst; broader editor accessibility and localization coverage remain. |
| Final acceptance/evidence/release | 3% | 20% | 0.60 | Strong CI evidence exists, but final product acceptance/release packaging is not yet closed. |
| **Total** | **100%** |  | **83.08** | **Reported project progress: 83%** |

## Current checkpoint

- Combined implementation checkpoint: `ff7af0da897efc5007645905f529c4bdbe9eb202`.
- Full exact-head CI: **#1209 / run 33248651484 — success**.
- iOS analytics accessibility gate: **#12 / run 33248651481 — success**.
- Q003C/OpenXML gate: **#9 / run 33248651547 — success**.
- Core solution: **1212/1212 passed**, **0 warnings**, **0 errors**; OpenXML **65/65**.
- Architecture verification, Windows desktop GPU, Android loaded accessibility, iOS loaded accessibility, Mac Catalyst loaded accessibility and MAUI Windows Table-filter/runtime/analytics/scale smokes all passed.
- Q003B is **DONE**.
- Q003C is **DONE for its defined managed analytics/chart persistence scope**.
- Q003D is **DONE for standard Excel PivotTable/PivotCache package preservation only**: an existing schema-valid standard pivot graph survives repeated `SpreadsheetSession` Load/Save cycles with `PreserveUnknownParts = true`, including relationship IDs, part URIs and worksheet source metadata, while external standard pivots are not silently reclassified as Nera-managed pivots.
- Q003D does **not** claim standard-pivot creation, semantic import into the Nera pivot model, refresh/calculation equivalence, cache-record interoperability, slicers/timelines or Excel UI parity.
- Desktop Ribbon/Bars is integrated; `RIBBON-MAUI` remains a separate Codex lane.

The increase from 82.78% to 83.08% is deliberately small because Q003D adds evidence-backed package interoperability/preservation rather than full native pivot creation/import. No roadmap pillar weight changed.

Future status reports should quote both the exact percentage and rounded percentage when the rubric changes, for example `83.08% ≈ 83%`.
