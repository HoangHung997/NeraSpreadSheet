# Ribbon/Table/Filter/UX execution board

## Baseline

- Program: `RTFUX-2026`.
- Integration branch: `feature/bootstrap-architecture-v0.1`.
- Baseline SHA: `34a81c5d45c2b28397c1688ae665cce0d0e8dfe7`.
- Delivery plan: `docs/ribbon-table-filter-ux-delivery-plan.md`.
- Planned two-lane window: 07/09/2026–28/10/2026.

Only the integration owner edits this board and `docs/worklog/CURRENT.md`.
Task owners write their own `docs/worklog/<CHECKPOINT>.md` handoff.

## Work queue

| Order | Checkpoint | Lane | State | Owner | Branch | Dependency | Planned window |
| ---: | --- | --- | --- | --- | --- | --- | --- |
| 1 | `RIBBON-007` | A | `CI` | Codex task `RIBBON-007 Responsive Ribbon` | `feature/ribbon-007-responsive-layout` | baseline | integrated 04/09; exact-head CI pending |
| 2 | `FILTER-005` | B | `CI` | Codex task `FILTER-005 Rich Filter Semantics` | `feature/filter-005-rich-semantics` | baseline | integrated 04/09; exact-head CI pending |
| 3 | `RIBBON-008` | A | `BACKLOG` | Unclaimed | `feature/ribbon-008-item-model` | `RIBBON-007` | 10–16/09 |
| 4 | `FILTER-006` | B | `BACKLOG` | Unclaimed | `feature/filter-006-native-ux` | `FILTER-005`; integration waits for `RIBBON-008` | 14–18/09 |
| 5 | `RIBBON-009` | A | `BACKLOG` | Unclaimed | `feature/ribbon-009-contextual-qat` | `RIBBON-008` | 17–22/09 |
| 6 | `FILTER-007` | B | `BACKLOG` | Unclaimed | `feature/filter-007-sort-accessibility` | `FILTER-006` | 21–23/09 |
| 7 | `RIBBON-010` | A | `BACKLOG` | Unclaimed | `feature/ribbon-010-customization` | `RIBBON-009` | 23–29/09 |
| 8 | `TABLE-004` | B | `BACKLOG` | Unclaimed | `feature/table-004-style-engine` | `FILTER-007` | 24–29/09 |
| 9 | `TABLE-005` | B | `BACKLOG` | Unclaimed | `feature/table-005-design-surface` | `TABLE-004`; integration waits for `RIBBON-009` | 30/09–06/10 |
| 10 | `TABLE-006` | B | `BACKLOG` | Unclaimed | `feature/table-006-compat-hardening` | `TABLE-005` | 07–09/10 |
| 11 | `UX-006` | Integration | `BACKLOG` | Integration owner | `feature/ux-006-visual-localization` | both lanes | 12–15/10 |
| 12 | `UX-007` | Integration | `BACKLOG` | Integration owner | `feature/ux-007-keyboard-a11y` | `UX-006` | 16–21/10 |
| 13 | `PERF-008` | Integration | `BACKLOG` | Integration owner | `feature/perf-008-ribbon-filter` | `UX-007` | 22–26/10 |
| 14 | `RELEASE-009` | Integration | `BACKLOG` | Integration owner | `release/0.2.0-rc1` | `PERF-008` | 27–28/10 |

## Claim protocol

Before changing code, the owner records in the task-specific worklog:

```text
Checkpoint:
Owner:
Branch:
Base integration SHA:
Owned files/directories:
Expected implementation commits:
```

The integration owner then changes exactly one row above from `READY` to
`ACTIVE`. A second worker seeing `ACTIVE`, `INTEGRATING` or `CI` must not edit
the same owned paths. It either takes another `READY` row with disjoint paths or
waits.

## Integration record

| Checkpoint | Implementation SHA | Integration SHA | Exact-head CI | Result |
| --- | --- | --- | --- | --- |
| Baseline/iconography | `2b933fd8b04042c0c52854dd73651161e9ae9322` | `34a81c5d45c2b28397c1688ae665cce0d0e8dfe7` | full #1302; iOS #123; Q003C #120 | Green |
| Program schedule | `f9340c1bf3c59e2c85336c961cf017d2c9ef8858` | `f9340c1bf3c59e2c85336c961cf017d2c9ef8858` | full #1303; iOS #124; Q003C #121 | Green |

Append one row only after exact-head CI completes. Never mark a checkpoint
`DONE` using a green run from a parent commit.

## Acceleration record

- 04/09/2026: both first checkpoints were started from exact integration SHA
  `21d496cad0d54506b0015d62e4ae80de57c34e6a`, three days before the calendar
  start. A successor starts immediately after its predecessor is integrated and
  green; calendar dates are not idle-time barriers.
