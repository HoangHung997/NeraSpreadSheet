# FILTER-007 worklog

## Claim

- Checkpoint: `FILTER-007`
- Owner: Codex task `FILTER-007 Sort Accessibility`
- Branch: `feature/filter-007-sort-accessibility`
- Base integration SHA: `d595539d616cba1bb5543ab3530035f927304069`
- Base gates: full CI #1309, iOS #130, Q003C/OpenXML #127 — green
- Owned paths: Core/Editing filter-sort execution, filter header projection,
  WPF/WinForms/MAUI filter hosts, filter-only tests/smokes, this worklog and the
  FILTER-007 contract.
- Excluded paths: all Ribbon.Core/Ribbon presenter files,
  `docs/current-status.md`, `docs/worklog/CURRENT.md`, and
  `docs/worklog/RIBBON_TABLE_FILTER_UX.md`.
- Expected implementation commits: one self-contained implementation/test/docs
  commit, followed only by a validation-note commit if exact-head evidence
  requires it.

## Status

Implementation complete.

- Implementation commit: `6510923` (`feat(filter): complete sort and accessibility UX`).
- Branch head after this note: use the handoff SHA reported with the branch.
- No shared status/worklog or Ribbon ownership file was modified.

## Completed

- Added bounded, stable, multi-key top-to-bottom sorting for Table and direct
  worksheet AutoFilter owners, including ascending/descending, custom lists,
  case sensitivity, formula translation within moved rows, and atomic
  sort-state/cell Undo/Redo.
- Added clear-sort and reapply paths that resolve current Table and column
  identity after structural changes.
- Added four-state header projection: none, filtered, sorted, and both, with
  direction-aware glyphs and accessible descriptions.
- Added result-count announcements, bounded keyboard navigation, native sort,
  reapply and clear-sort actions across WPF, WinForms and MAUI.
- Serialized MAUI binding operations on its dispatcher. The loaded smoke found
  and verified the fix for a background-thread Skia/EGL invalidation race.
- Extended unit, loaded-native and MAUI Windows smoke coverage, including a
  100,000-row bounded stress case.

## Validation

- Core solution Release build: passed, 0 warnings, 0 errors.
- Full Core test set: 1,333/1,333 passed.
- FILTER-007 Editing focus (`SortControllerTests` and
  `SpreadsheetAutoFilterPagedPresenterTests`): 16/16 passed at final source.
- WPF/WinForms loaded paged AutoFilter focus: 6/6 passed at final source;
  Windows test project Release build passed with 0 warnings/errors.
- Full Windows rendering run: 65/66 passed. The sole failure is the existing
  environment-only `WpfSplitScrollBarWindowMessageSmokeTests` activation
  failure at `Window.Activate()`, outside FILTER-007; all new loaded tests pass.
- MAUI Windows Release build: passed, 0 warnings, 0 errors.
- MAUI test suite: 36/36 passed at final source.
- Loaded MAUI Windows TableFilter smoke: passed with 12 GPU frames,
  `filterApplied=true`, `undoRedoVerified=true`,
  `pagedRichSurfaceVerified=true`, and `pagedSortVerified=true`.
- `scripts/verify-architecture.ps1`: passed.
- `scripts/verify-packaging-sdk.ps1`: passed.
- `git diff --check`, ownership scan and changed-content credential/path scan:
  passed.

## Limits and risk

- Cell-color, font-color and icon physical sort execution remains unsupported
  and is rejected atomically; metadata preservation is unchanged.
- Left-to-right sort remains preservation-only and is rejected explicitly.
- Clear sort clears metadata only; it does not reconstruct a prior physical row
  order. Undo restores both metadata and rows for the originating sort.
- Android compilation could not run on this machine because Android SDK API 36
  is absent (`XA5207`, missing `platforms/android-36/android.jar`). No Android-
  specific FILTER-007 code was added; the shared MAUI source compiled and ran
  under Windows.
- Exact-head GitHub Actions evidence must be taken from the pushed branch or
  integration PR; local validation is complete.

## Rollback and handoff

- Rollback: revert the worklog commit, then revert implementation commit
  `6510923`.
- Cherry-pick: cherry-pick `6510923` followed by the worklog commit reported in
  the final handoff.
- Next step: integrate both commits and require the exact-head CI matrix to be
  green before merge.

## Independent review remediation

The post-implementation review found and closed the remaining integration
blockers before cherry-pick:

- selection and AutoFilter sorts now reject dynamic-array spill roots and
  children atomically, with spill/Undo regression coverage;
- WPF and MAUI paged keyboard routing no longer captures editing, picker or
  command-button keys outside the value navigation surface;
- the pre-FILTER-007 constructors and `Deconstruct` shapes of the public paged
  snapshot and three header-hit records were restored and compile/reflection
  tested;
- the production WPF, WinForms and MAUI `NeraSpreadsheetTableHost` surfaces now
  expose ascending, descending, reapply and clear-sort actions;
- result counts and announcements refresh after filter and sort mutations;
- WPF draws direction-specific arrows, a filtered funnel and a combined-state
  badge rather than relying on active color;
- MAUI binding disposal now cancels and drains an in-flight dispatcher operation
  before disposing its semaphore, with a deterministic race test;
- the loaded MAUI Windows smoke now renders both the production Table host and
  paged rich host as visible surfaces; the hidden 2x2, 1%-opacity host was
  removed, and the production host executes the runtime sort check.

Focused remediation evidence: Editing 19/19, rendering geometry 5/5, and
WPF/WinForms loaded production and paged focus 3/3 passed. Full relevant runs
passed Editing 249/249, rendering spreadsheet 124/124 and MAUI 39/39. Full
Windows rendering remained 65/66 with only the documented environment-only
`Window.Activate()` split-scrollbar failure; all FILTER-007 loaded tests passed.
Architecture and packaging verification passed. Loaded MAUI Windows TableFilter
smoke passed with 13 GPU frames and every reported
filter/focus/history/rich/sort flag true.
