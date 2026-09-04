# FILTER-005 worklog

## Branch and baseline

- Branch: `feature/filter-005-rich-semantics`
- Integration baseline: `21d496cad0d54506b0015d62e4ae80de57c34e6a`
- Implementation commit: `e167c1e97e1189e903fd1584937246028ddbcab1`
- Pull request: not created; branch handoff only
- Exact-head CI: pending integration owner/GitHub Actions

## Completed

- Extended the existing shared Table/direct-worksheet filter model with date
  groups, Top/Bottom item and percentage filters, dynamic date and average
  filters, resolved fill/font colors, icon filters and ordered sort state.
- Kept Table and Table-column stable identities; structural column insert and
  delete remap or remove sort keys without materializing worksheet axes.
- Captured workbook date system and immutable style catalog in
  `WorksheetSnapshot`; compiled aggregate/color/icon predicates are cached per
  snapshot and source column, and visibility remains compressed spans.
- Added rich-filter and sort-state controller mutations through production
  Undo/Redo. Filter Apply/Undo/Redo now use affected-range dependency
  invalidation instead of full-workbook recalculation.
- Added shared SpreadsheetML codecs for `dateGroupItem`, `top10`,
  `dynamicFilter`, `colorFilter`, `iconFilter` and `sortState` for both Table
  definitions and worksheet AutoFilter.
- Integrated filter colors with the existing differential-style export plan.
- Preserved namespaced producer attributes, extension lists, unsupported
  producer-owned filter columns and unsupported sort markup when
  `PreserveUnknownParts=true`; strict non-preserving loads still reject
  unsupported criteria.
- Added public XML documentation and the contract in
  `docs/filter-005-rich-autofilter-contract.md`.
- Did not modify native filter UI, Ribbon files, `docs/current-status.md`,
  `docs/worklog/CURRENT.md` or `docs/worklog/RIBBON_TABLE_FILTER_UX.md`.

## Main files

- `src/NeraSpreadSheet.Core/SpreadsheetRichFilter.cs`
- `src/NeraSpreadSheet.Core/SpreadsheetFilterEvaluator.cs`
- `src/NeraSpreadSheet.Core/Tables.cs`
- `src/NeraSpreadSheet.Core/WorksheetAutoFilter.cs`
- `src/NeraSpreadSheet.Core/WorksheetSnapshot.cs`
- `src/NeraSpreadSheet.Editing/SpreadsheetTableController.cs`
- `src/NeraSpreadSheet.Editing/SpreadsheetWorksheetAutoFilterController.cs`
- `src/NeraSpreadSheet.OpenXml/OpenXmlAutoFilterCriteriaCodec.cs`
- `src/NeraSpreadSheet.OpenXml/OpenXmlTableCodec.cs`
- `src/NeraSpreadSheet.OpenXml/OpenXmlWorksheetAutoFilterCodec.cs`
- `tests/NeraSpreadSheet.Core.Tests/RichTableFilterTests.cs`
- `tests/NeraSpreadSheet.OpenXml.Tests/RichAutoFilterRoundTripTests.cs`

## Validation

All commands used repo-local .NET SDK 10.0.302 installed outside the worktree.

- `dotnet restore NeraSpreadSheet.Core.slnx`: passed.
- `dotnet build NeraSpreadSheet.Core.slnx -c Release --no-restore`: passed,
  0 warnings, 0 errors.
- `dotnet test NeraSpreadSheet.Core.slnx -c Release --no-build`: 1,283/1,283
  passed.
- Core: 125/125 passed.
- Editing: 222/222 passed.
- Formulas: 526/526 passed.
- OpenXML: 89/89 passed, including round-trip, Office 2013 schema validation,
  malformed-input and preservation coverage.
- Rendering.Spreadsheet: 121/121 passed; Viewport: 60/60 passed.
- `scripts/verify-architecture.ps1`: passed.
- `scripts/verify-packaging-sdk.ps1`: passed.
- `git diff --check`: passed.
- Secret/personal-path scan of the implementation diff: no findings.

No native presenter file changed, so no new loaded WPF/WinForms/MAUI UI smoke
was required. Existing rendering and viewport suites provide the runtime
projection regression gate for this host-neutral checkpoint.

## Limits and risks

- FILTER-005 persists sort state but does not physically reorder rows; native
  sort commands, reapply and indicators remain FILTER-007.
- Native rich-filter menu categories/editors remain FILTER-006/FILTER-007.
- Core color filtering evaluates captured base/row/column effective style; it
  does not evaluate arbitrary conditional-formatting color outcomes.
- Until Core owns conditional-formatting icon-set rules, icon filtering uses a
  documented deterministic equal-bucket numeric fallback inferred from the
  3/4/5-icon set. Foreign threshold rules remain preservation-only.
- Exact-head GitHub Actions are pending after integration; do not mark the
  checkpoint DONE until full CI, iOS and Q003C/OpenXML gates are green on the
  integrated HEAD.

## Rollback

Revert implementation commit
`e167c1e97e1189e903fd1584937246028ddbcab1` and the following worklog commit.
No package or data migration is required.

## One next step

Integration owner should fetch this branch, cherry-pick the implementation and
worklog commits onto the unchanged integration head, then push that exact HEAD
and wait for full CI, iOS and Q003C/OpenXML gates before promoting FILTER-005.
