# FILTER-006 Native Rich Filter UX Worklog

- Status: `INTEGRATING`; exact-head GitHub gates pending integration-owner cherry-pick.
- Owner branch: `feature/filter-006-native-ux`.
- Exact integration base: `05c6974fa907f5022f28c85f13f06dbb35288556` (`feature/bootstrap-architecture-v0.1`).
- Baseline evidence supplied by integration owner: full CI `#1307`, iOS `#128`, Q003C/OpenXML `#125` — green.
- Scope ownership: shared AutoFilter presenter, paging and session code in `NeraSpreadSheet.Editing`; Filter-specific WPF popup/binding, WinForms dropdown/binding and MAUI overlay/bottom-sheet code; Filter-only tests and loaded runtime smokes; FILTER-006 contract and this worklog.
- Explicit exclusions: `Ribbon.Core`, `NeraRibbonControl`, `NeraMauiRibbonView`, `docs/current-status.md`, `docs/worklog/CURRENT.md`, `docs/worklog/RIBBON_TABLE_FILTER_UX.md`, and Core/OpenXML rich-filter semantics unless a blocking defect is reported to the integration owner.
- Integration boundary: no complex Ribbon item integration before the RIBBON-008 handoff; implementation must remain independently cherry-pickable.
- Implementation commit: `fec8f2eec1222cfa7db9f67b40be709201285b15`.

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
- Added the FILTER-006 contract, shared stress/criterion/date-tree tests,
  WPF/WinForms loaded surface tests, MAUI binding tests and a loaded MAUI Windows
  rich-surface probe in the existing Table-filter smoke executable.
- Did not modify Ribbon/Core/OpenXML semantics or any integration-owner status,
  board or CURRENT files.

## Validation

All .NET commands used SDK 10.0.302 under the user profile.

- `NeraSpreadSheet.Core.slnx` Release restore/build: passed, 0 warnings/errors.
- Core solution tests: **1300/1300 passed** before the final native-only stale
  callback guard; the affected Editing suite was rerun after all shared changes.
- Editing: **227/227 passed**, including 100,000 rows/10,000 distinct, a
  100-item current page, both Table and worksheet rich Apply, one Undo/Redo
  entry, stale mutation/date-page rejection, parser families and lazy date tree.
- Viewport: **60/60**; Formulas: **526/526**; OpenXML: **93/93**;
  Rendering.Spreadsheet: **121/121** (all included in the Core solution run).
- Focused loaded WPF/WinForms paged binding/presenter smoke: **4/4 passed**.
- MAUI Windows tests: **35/35 passed**.
- Loaded MAUI Windows Table-filter smoke: `status=success`, 10 GPU frames,
  `pagedRichSurfaceVerified=true`, focus/Apply/Undo/Redo all true.
- WPF, WinForms, MAUI Windows, iOS and Mac Catalyst Release builds: passed with
  0 warnings/errors.
- Full Windows.Rendering: **62/63 passed locally**. The only failure is the
  pre-existing environment-only native mouse smoke: `GetCursorPos` returned
  false before SDK behavior was exercised.
- Android compile reached the platform toolchain but could not run because the
  machine has Android SDK platforms 34/35, while .NET 10 requests API 36, and
  no local Java runtime is configured. Exact-head CI must provide the Android
  build evidence.
- Architecture verification, SDK packaging verification and `git diff --check`:
  passed.
- Ownership and secret/personal-path scan: no forbidden project/file changes
  and no credential or machine-identifier findings.

## Limits and risk

- The built-in native rich editor deliberately uses one compact textual operand
  shared across hosts (examples: `Top10%`, `Today`, `#RRGGBB`,
  `3TrafficLights1:0`). Gallery/color swatches and full condition-builder chrome
  can be refined in UX-006 without changing presenter or mutation contracts.
- Date-tree data is exposed as lazy pages to all three hosts; the compact default
  editor accepts a direct date while applications may render the returned
  year/month/day pages as their preferred native tree control.
- Physical sort/reapply, sort indicators and final keyboard/screen-reader
  certification remain FILTER-007/UX-007 scope.
- Exact-head full CI, iOS and Q003C/OpenXML gates are pending after integration.

## Rollback

Revert implementation commit
`fec8f2eec1222cfa7db9f67b40be709201285b15`. No workbook or package migration
is required.

## One next step

Integration owner should verify the integration head is still based on
`05c6974fa907f5022f28c85f13f06dbb35288556`, cherry-pick the implementation
and this worklog commit, push that exact head, and require full CI, iOS and
Q003C/OpenXML success before marking FILTER-006 `DONE` or starting FILTER-007.
