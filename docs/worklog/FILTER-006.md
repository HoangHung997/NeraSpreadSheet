# FILTER-006 Native Rich Filter UX Worklog

- Status: `READY_FOR_INTEGRATION`; exact-head GitHub gates pending integration-owner cherry-pick.
- Owner branch: `feature/filter-006-native-ux`.
- Exact integration base: `05c6974fa907f5022f28c85f13f06dbb35288556` (`feature/bootstrap-architecture-v0.1`).
- Baseline evidence supplied by integration owner: full CI `#1307`, iOS `#128`, Q003C/OpenXML `#125` — green.
- Scope ownership: shared AutoFilter presenter, paging and session code in `NeraSpreadSheet.Editing`; Filter-specific WPF popup/binding, WinForms dropdown/binding and MAUI overlay/bottom-sheet code; Filter-only tests and loaded runtime smokes; FILTER-006 contract and this worklog.
- Explicit exclusions: `Ribbon.Core`, `NeraRibbonControl`, `NeraMauiRibbonView`, `docs/current-status.md`, `docs/worklog/CURRENT.md`, `docs/worklog/RIBBON_TABLE_FILTER_UX.md`, and Core/OpenXML rich-filter semantics unless a blocking defect is reported to the integration owner.
- Integration boundary: no complex Ribbon item integration before the RIBBON-008 handoff; implementation must remain independently cherry-pickable.
- Original implementation commits: `fec8f2eec1222cfa7db9f67b40be709201285b15`
  and `b21bf7c1ee51e09109b7ca281e48b69f98a0d488`.
- Correctness/native hardening commit:
  `08fc0228a390e2f5626dc2bead83f9bc2d1419e3`.

## Planned acceptance

- One shared host-neutral presenter supports search, checklist, select/clear visible, bounded paging/virtualization, rich filter menu categories and a lazy year/month/day date tree.
- Apply, Cancel and Clear obey generation/cancellation and create exactly one Undo/Redo entry only for successful mutations.
- WPF, WinForms and MAUI render only the current bounded page/tree projection and do not create one native control per distinct source value.
- Regression gates cover 100,000 rows, 10,000 retained distinct values and stale asynchronous results that must not overwrite a newer session.

## Completed

- Extended the existing shared presenter/session/view stack with one rich menu
  projection for value, text, number, date, fill/font color, icon, custom,
  Top/Bottom and dynamic filter families.
- Added a shared compact native criterion parser and the same Vietnamese menu
  categories/editor field to WPF Popup, WinForms DropDown and MAUI responsive
  sheet. Rich and custom Apply use the FILTER-005 Core model through production
  Table/worksheet controllers.
- Added generation-checked, bounded lazy year/month/day pages. Projection reads
  only the existing bounded distinct catalog and never creates controls for
  unloaded nodes.
- Set the shared default page size to 100 and retained the 1,000-item hard
  request limit. Native hosts continue to materialize only one page.
- Captured native binding identity in search/mutation continuations so stale
  completion cannot update status, rebuild, close or act on a newer session.
- Preserved Cancel as a non-mutating dispose path; successful Apply/Clear
  invalidate once and use exactly one production Undo/Redo transaction.
- Prevented an incomplete 10,000-item distinct catalog from silently applying
  as a complete value subset; native Apply is disabled and direct controller
  calls are rejected atomically without history.
- Added Excel serial-date projection using the worksheet 1900/1904 date system
  and effective number format, plus native lazy date trees with multiple groups
  and year-through-second precision on WPF, WinForms and MAUI.
- Added two custom conditions with AND/OR and numeric Above/Below Average to all
  native surfaces.
- Made cancellation request-owned and drain-safe, guarded every asynchronous
  continuation by binding identity, and serialized rapid selection/Apply work.
- Suppressed Table no-op history entries. Header hit testing now reads filter
  metadata without capturing the worksheet; native header controls stay bounded
  to the visible plus overscan range and remove scrolled-out controls.
- Added the FILTER-006 contract, shared stress/criterion/date-tree tests,
  WPF/WinForms loaded surface tests, MAUI binding tests and a loaded MAUI Windows
  rich-surface probe in the existing Table-filter smoke executable.
- Did not modify Ribbon/Core/OpenXML semantics or any integration-owner status,
  board or CURRENT files.

## Validation

The original implementation used SDK 10.0.302. The hardening validation used
the installed SDK 10.0.201 MSBuild/VSTest entry points because this worktree's
requested 10.0.302 SDK is not installed locally; exact-head GitHub CI remains
authoritative.

- `NeraSpreadSheet.Core.slnx` Release restore/build: passed, 0 warnings/errors.
- Core solution tests: **1313/1313 passed**.
- Editing: **238/238 passed**, including late unique values beyond 100,000 rows,
  more than 10,000 distinct values, serial dates in both date systems, no-op
  history, cancellation/disposal races, stale generations and rapid toggle then
  Apply.
- Viewport: **60/60**; Formulas: **526/526**; OpenXML: **93/93**;
  Rendering.Spreadsheet: **122/122** (all included in the Core solution run).
- Focused loaded WPF/WinForms paged binding/presenter smoke: **6/6 passed**.
- MAUI Windows tests: **36/36 passed**.
- Loaded MAUI Windows Table-filter smoke: `status=success`, 10 GPU frames,
  `pagedRichSurfaceVerified=true`, focus/Apply/Undo/Redo all true.
- WPF, WinForms and MAUI Windows Release builds: passed with 0 warnings/errors.
- Full Windows.Rendering: **63/64 passed locally**. The only failure is the
  environment-only native window smoke
  `WpfSplitScrollBarWindowMessageSmokeTests.PublicWpfScrollBarDragMovesOnlyBottomRightPaneAndPersistsState`:
  `window.Activate()` returned false before FILTER-006 behavior was exercised.
- Architecture verification, SDK packaging verification and `git diff --check`:
  passed.
- Ownership and secret/personal-path scan: no forbidden project/file changes
  and no credential or machine-identifier findings.

## Limits and risk

- The built-in native rich editor deliberately uses one compact textual operand
  shared across hosts (examples: `Top10%`, `Today`, `#RRGGBB`,
  `3TrafficLights1:0`). Gallery/color swatches and full condition-builder chrome
  can be refined in UX-006 without changing presenter or mutation contracts.
- Native date trees and two-condition editors are functional but deliberately
  compact; final visual polish belongs to UX-006.
- Physical sort/reapply, sort indicators and final keyboard/screen-reader
  certification remain FILTER-007/UX-007 scope.
- Exact-head full CI, iOS and Q003C/OpenXML gates are pending after integration.

## Rollback

Revert hardening commit `08fc0228a390e2f5626dc2bead83f9bc2d1419e3`
and original commits `b21bf7c1ee51e09109b7ca281e48b69f98a0d488`
and `fec8f2eec1222cfa7db9f67b40be709201285b15`. No workbook or package migration
is required.

## One next step

Integration owner should verify the integration head is still based on
`05c6974fa907f5022f28c85f13f06dbb35288556`, cherry-pick the implementation
and this worklog commit, push that exact head, and require full CI, iOS and
Q003C/OpenXML success before marking FILTER-006 `DONE` or starting FILTER-007.
