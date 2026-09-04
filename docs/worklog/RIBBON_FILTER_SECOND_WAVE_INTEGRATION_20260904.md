# RIBBON-008 + FILTER-006 second-wave integration

## Branch and source commits

- Integration branch: `feature/bootstrap-architecture-v0.1`.
- Coordination branch: `feature/ribbon-table-filter-ux-plan`.
- Exact green predecessor: `0f253a7524be24b7c4eefe007b57b0f8d18cb284`.
- RIBBON-008 remote handoff: `99cb3ccc1da2099ccf16b757c0a440d8477b4fa1`;
  implementation `6493b34872ec4d7f8c24909ede1de5f924ae87d5`, review hardening
  `77bffbf82c33d0f8a21cce66876540c45324bbb5`.
- FILTER-006 remote handoff: `690faad3a5878f829835c9925b774b253d4f5dab`;
  implementation `fec8f2eec1222cfa7db9f67b40be709201285b15`, review hardening
  `08fc0228a390e2f5626dc2bead83f9bc2d1419e3`.

The two lanes own disjoint paths and cherry-picked without conflict.

## Mandatory review hardening

The first handoffs were not promoted unchanged. Independent review found and
closed blockers before integration.

RIBBON-008 now preserves the exact public record/constructor surface used by
existing SDK consumers, validates current selectable/enabled leaf values before
dispatch, refreshes state with the original host context, keeps MAUI overflow
content usable, aligns explicit button/toggle and separator semantics, caches
measurement per layout pass, scrolls galleries, exposes stable split-button
subpart identity, and tests real overflow activation/focus/accessibility.

FILTER-006 now rejects lossy checklist Apply on truncated catalogs, recognizes
Excel serial dates through the workbook date system/effective format, renders a
lazy native date tree and two-condition editor, propagates cancellation into
bounded scans, drains async lifetimes safely, rejects stale native continuations,
serializes rapid selection before Apply, avoids no-op Table history entries,
uses metadata-only header geometry, and bounds/reuses native header controls.

## Combined local validation

- Core solution Release: **1324/1324 passed**, build/analyzers clean.
- Commands: **78/78**; Editing: **238/238**; Rendering.Spreadsheet:
  **122/122**; OpenXML: **93/93**.
- MAUI Windows presenter tests: **36/36 passed**.
- Focused loaded WPF/WinForms Ribbon and rich Filter tests: **7/7 passed**.
- Loaded MAUI Windows Ribbon smoke: **success**, including bounded overflow and
  all complex item kinds/selection.
- Loaded MAUI Windows Table-filter smoke: **success**, including focus,
  Apply/Undo/Redo and paged rich surface.
- Architecture verification, SDK packaging verification, `git diff --check`
  and the secret/personal-path scan passed. Exact-head GitHub full CI, iOS and
  Q003C/OpenXML gates remain required before promotion to `DONE`.

## Remaining limits

- WinForms and MAUI native picker rows do not expose a stable per-row disabled
  visual/icon API. Shared runtime rejects disabled or stale values atomically and
  restores the valid selection; full native picker-row parity remains a bounded
  UX follow-up.
- Contextual tabs/QAT/key tips remain RIBBON-009. Sort/reapply/indicators and
  complete Filter keyboard/accessibility remain FILTER-007.

## Rollback

Revert this integration documentation commit, then revert each lane's ordered
commit set. No package or data migration is required.
