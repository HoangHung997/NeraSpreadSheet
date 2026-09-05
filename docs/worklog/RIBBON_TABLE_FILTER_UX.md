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
| 1 | `RIBBON-007` | A | `DONE` | Codex task `RIBBON-007 Responsive Ribbon` | `feature/ribbon-007-responsive-layout` | baseline | integrated/green 04/09 |
| 2 | `FILTER-005` | B | `DONE` | Codex task `FILTER-005 Rich Filter Semantics` | `feature/filter-005-rich-semantics` | baseline | integrated/green 04/09 |
| 3 | `RIBBON-008` | A | `DONE` | Codex task `RIBBON-008 Full Item Model` (`client-new-thread:7c4e32a8-eb91-404b-9ea9-74acd75ef24f`) | `feature/ribbon-008-item-model` | `RIBBON-007` | integrated/green 04/09 |
| 4 | `FILTER-006` | B | `DONE` | Codex task `FILTER-006 Native Rich Filter UX` (`client-new-thread:6b8ccb3d-d6ac-4034-b044-ce5135978940`) | `feature/filter-006-native-ux` | `FILTER-005`; Ribbon integration waits for `RIBBON-008` | integrated/green 04/09 |
| 5 | `RIBBON-009` | A | `DONE` | Codex task `RIBBON-009 Contextual QAT Key Tips` (`client-new-thread:857c5cdd-87ba-4756-8942-55fd33757ccc`) | `feature/ribbon-009-contextual-qat` | `RIBBON-008` | integrated/green 05/09 |
| 6 | `FILTER-007` | B | `DONE` | Codex task `FILTER-007 Sort Reapply Accessibility` (`client-new-thread:15e93970-66ea-4bdc-b34d-5e143f3c7c1c`) | `feature/filter-007-sort-accessibility` | `FILTER-006` | integrated/green 05/09 |
| 7 | `RIBBON-010` | A | `DONE` | Codex task `RIBBON-010 Customization SDK` (`client-new-thread:5d0abfeb-94ff-4b03-ab7a-3bd7236347bb`) | `feature/ribbon-010-customization` | `RIBBON-009` | integrated/green 05/09 |
| 8 | `TABLE-004` | B | `DONE` | Codex task `TABLE-004 Table Style Engine` (`client-new-thread:9d15985f-554c-49df-be81-30fc4ebc2ef4`) | `feature/table-004-style-engine` | `FILTER-007` | integrated/green 05/09 |
| 9 | `RIBBON-VISUAL-011` | A | `DONE` | Codex task `RIBBON-VISUAL-011 — Excel-density adaptive layout` | `feature/ribbon-visual-011` | `RIBBON-010` | integrated/green implementation 05/09 |
| 10 | `TABLE-005` | Source | `DONE` | Coordinator; source imported once through A | `feature/bootstrap-architecture-v0.1` | `TABLE-004`; defined scope | combined implementation `e29acb44` green 05/09 |
| 11 | `TABLE-RIBBON-012` | A | `DONE` | Coordinator; source task `01a0704c-1f36-7232-a54f-bc67c965e89c` handed off `d283a55b` | `feature/bootstrap-architecture-v0.1` | `488c61ea` + immutable TABLE-005 source | combined implementation `e29acb44` green 05/09 |
| 12 | `TABLE-006` | B | `READY` | Coordinator; source task complete; remaining native work not dispatched | `feature/bootstrap-architecture-v0.1` | headless `7f73a97d` integrated/green at `e29acb44`; native UX/corpus still open | headless green 05/09; whole checkpoint not DONE |
| 13 | `UX-006` | Integration | `BACKLOG` | Integration owner | `feature/ux-006-visual-localization` | both lanes | 12–15/10 |
| 14 | `UX-007` | Integration | `BACKLOG` | Integration owner | `feature/ux-007-keyboard-a11y` | `UX-006` | 16–21/10 |
| 15 | `PERF-008` | Integration | `BACKLOG` | Integration owner | `feature/perf-008-ribbon-filter` | `UX-007` | 22–26/10 |
| 16 | `RELEASE-009` | Integration | `BACKLOG` | Integration owner | `release/0.2.0-rc1` | `PERF-008` | 27–28/10 |

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
| `RIBBON-007` | `0878f0f3e57783215431662894a504cbe18eefef` | `05c6974fa907f5022f28c85f13f06dbb35288556` | full #1307; iOS #128; Q003C #125 | Green |
| `FILTER-005` | `e167c1e97e1189e903fd1584937246028ddbcab1` | `05c6974fa907f5022f28c85f13f06dbb35288556` | full #1307; iOS #128; Q003C #125 | Green |
| `RIBBON-008` | `6493b34872ec4d7f8c24909ede1de5f924ae87d5` + `77bffbf82c33d0f8a21cce66876540c45324bbb5` | `d595539d616cba1bb5543ab3530035f927304069` | full #1309; iOS #130; Q003C #127 | Green |
| `FILTER-006` | `fec8f2eec1222cfa7db9f67b40be709201285b15` + `08fc0228a390e2f5626dc2bead83f9bc2d1419e3` | `d595539d616cba1bb5543ab3530035f927304069` | full #1309; iOS #130; Q003C #127 | Green |
| `RIBBON-009` | `e1e38b37416f0df1a6fea2cb59346deb22e7d3e6` + `a743826` + `5e4ffe7` + `e393840` | `f75e2f103598cbbe7b5c22f92c3ab8dd755ef8c3` | full #1312; iOS #133; Q003C #130 | Green |
| `FILTER-007` | `6510923` + `a6eddd96ab4f61b46bee243c74b9defb3c2eacf1` + `022cfd8f0a63f02377d3365e91c54e0ae52a4de2` | `f75e2f103598cbbe7b5c22f92c3ab8dd755ef8c3` | full #1312; iOS #133; Q003C #130 | Green |
| `RIBBON-010` | `eb08b0f95176b1a23e01ccf0b09a112bdc562dac` + `9aeed672cea94db3d4c2d0ecb4a4f55a85e1dbaf` | `57a8c0c0fe8eb452bcb054432d2d37b9e9807e73` | full #1318; iOS #139; Q003C #136 | Green |
| `TABLE-004` | `3a459320ef7192f5843dcd6d3bfb0a56ae7698ea` + `cffdc9d8f05c50dafc7a875910d2f0c6b4851416` + `ed01ed6b1243ea41490dc4ac3b4d38411dcc0892` | `57a8c0c0fe8eb452bcb054432d2d37b9e9807e73` | full #1318; iOS #139; Q003C #136 | Green |
| `RIBBON-VISUAL-011` | nine-commit lane ending at `7cfcfdfc6f337da1b37ca05b9254b903a33f32d2` | `7cfcfdfc6f337da1b37ca05b9254b903a33f32d2` (fast-forward) | full #1324; iOS #145; Q003C #142 | Green; documentation descendant requires exact-head gates |
| Ribbon integration documentation | `488c61ea75ea6c8f7a6ceb480035a341f24c6c19` | `488c61ea75ea6c8f7a6ceb480035a341f24c6c19` | full #1325 / 33949294692; iOS #146 / 33949294721; Q003C #143 / 33949294687 | Green at final integration HEAD |
| `TABLE-005` / `TABLE-RIBBON-012` / `TABLE-006` headless | A `d283a55b` + B `7f73a97d` + combined regression | `e29acb44bc058e91a27c9dcc35a6979909d4dd5b` | full #1333 / 33953936497; iOS #154 / 33953936520; Q003C #151 / 33953936475 | Green, all seven jobs; whole TABLE-006 remains open; final documentation descendant requires exact-head gates |

Append one row only after exact-head CI completes. Never mark a checkpoint
`DONE` using a green run from a parent commit.

## Acceleration record

- 04/09/2026: both first checkpoints were started from exact integration SHA
  `21d496cad0d54506b0015d62e4ae80de57c34e6a`, three days before the calendar
  start. A successor starts immediately after its predecessor is integrated and
  green; calendar dates are not idle-time barriers.
- 04/09/2026: both first checkpoints were integrated and exact-head green at
  `05c6974fa907f5022f28c85f13f06dbb35288556`; `RIBBON-008` and `FILTER-006`
  were immediately dispatched into two new isolated worktrees from that SHA.
- 04/09/2026: independent review rejected both initial second-wave handoffs,
  each lane fixed its blockers and added regression/runtime coverage, then the
  two disjoint commit sets were integrated without conflict for exact-head CI.
- 04/09/2026: the combined second-wave head
  `d595539d616cba1bb5543ab3530035f927304069` passed full CI #1309, iOS #130
  and Q003C/OpenXML #127. `RIBBON-009` and `FILTER-007` were immediately
  dispatched from that exact green SHA into isolated, non-overlapping worktrees.
- 05/09/2026: both third-wave lanes passed independent blocker reviews and were
  integrated without conflict. Combined head
  `f75e2f103598cbbe7b5c22f92c3ab8dd755ef8c3` passed full CI #1312, iOS #133
  and Q003C/OpenXML #130. `RIBBON-010` and `TABLE-004` were immediately
  dispatched from that exact green SHA into isolated, non-overlapping worktrees.
- 05/09/2026: `RIBBON-010` and `TABLE-004` completed on disjoint file sets and
  were cherry-picked without conflict. Combined head
  `57a8c0c0fe8eb452bcb054432d2d37b9e9807e73` passed full CI #1318, iOS #139
  and Q003C/OpenXML #136. The next dependency-valid checkpoint, `TABLE-005`,
  was immediately dispatched from that exact green branch with high reasoning.
- 05/09/2026: `RIBBON-VISUAL-011` was reviewed and fast-forwarded without
  conflict, preserving all nine commit identities. Local integration validation
  passed Core 1386/1386, MAUI 41/41, architecture/packaging and the 176-image /
  128-layout runtime capture. Windows local 74/75 retains the baseline
  foreground-activation limitation; exact-head Windows CI at `7cfcfdfc` passes
  75/75. TABLE-005 remains isolated and no successor task was dispatched.
- 05/09/2026, next wave: final Ribbon integration `488c61ea` and TABLE-005
  source `cf923db2` were both verified green in all three workflows. The user
  requested two new worktrees with `gpt-6-astra / xhigh`; TABLE-RIBBON-012 and
  TABLE-006 are now active. This bounded acceleration permits headless work
  from the green source while the first lane reconciles its UI with the new
  Ribbon. Ownership, file-transfer locking and A-then-B-delta integration are
  fixed in [`TABLE_RIBBON_WAVE_20260905.md`](TABLE_RIBBON_WAVE_20260905.md).
- 05/09/2026, integration: both lanes handed off and were cherry-picked A first,
  then only B delta after `cf923db2`, without conflict or duplicate TABLE-005
  imports. Added cross-lane Convert value/history/dependency regression and
  loaded WPF dialog assertions. Combined `e29acb44` passes all seven CI jobs;
  no successor is dispatched. Native structured-reference editor wiring and
  native producer corpus keep whole TABLE-006 open. See
  [`TABLE_RIBBON_INTEGRATION_20260905.md`](TABLE_RIBBON_INTEGRATION_20260905.md).
