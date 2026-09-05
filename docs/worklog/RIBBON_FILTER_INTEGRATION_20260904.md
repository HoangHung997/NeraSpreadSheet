# RIBBON-007 + FILTER-005 integration hardening

## Branch and source commits

- Integration branch: `feature/bootstrap-architecture-v0.1`.
- Coordination branch: `feature/ribbon-table-filter-ux-plan`.
- Verified pre-integration head: `f03487b3048d75efa9cf3ecaa9f9a59b3f4c87b3`.
- Ribbon lane commits: `0878f0f3e57783215431662894a504cbe18eefef`,
  `44bd0dc76539bdff477b1306df59c83dd3df7831`.
- Filter lane commits: `e167c1e97e1189e903fd1584937246028ddbcab1`,
  `cbec438d07f55f6e8c162dffa7f38be1353a6505`.

## Integration review and fixes

The two lane branches were clean and disjoint, and were cherry-picked without a
merge conflict. Integration review then fixed correctness gaps before promoting
either checkpoint:

- preserve Ribbon group collapse priority through customization;
- retain selected-tab identity without rebuild event handlers overwriting it;
- coalesce resize rebuilds and never restore Ribbon focus after the user moved
  focus back to the worksheet/editor;
- keep a visible caption and tooltip when an icon cannot be resolved;
- bound and vertically scroll the MAUI overflow surface;
- use schema-valid dynamic-month tokens and exact wildcard escaping/rejection;
- prohibit unsupported left-to-right sort semantics while preserving its XML in
  preservation mode;
- validate unsupported standard filter/sort attributes and rich-filter values;
- avoid aggregate column scans for simple predicates and single-execute cached
  aggregate compilation;
- preserve existing differential-style IDs while remapping generated
  color-filter and color-sort references;
- correct early Excel 1900-system serial dates.

## Local validation

- Core solution: **1296/1296 passed**.
- Commands: **67/67 passed**; Core: **127/127 passed**; OpenXML: **93/93
  passed**; MAUI presenter: **34/34 passed**.
- Focused loaded WPF/WinForms Ribbon integration tests: **3/3 passed**.
- Loaded MAUI Windows Ribbon smoke: **success**, including missing-icon caption
  fallback and bounded scrolling overflow.
- MAUI Windows Ribbon smoke publish/build: **0 warnings, 0 errors**.
- Full Windows.Rendering has one known local foreground-window-only failure at
  `window.Activate()`; it occurs before SDK behavior and is not caused by this
  change. The affected new focused tests pass.
- Architecture verification, SDK packaging verification, `git diff --check`
  and the secret/personal-path scan passed.

## Limits

- Split/dropdown, combo, gallery and color-picker item kinds remain RIBBON-008.
- Native rich filter popup/category UX remains FILTER-006.
- Left-to-right sort execution/import remains FILTER-007; preserved workbooks
  retain its producer XML when preservation mode is enabled.
- Exact-head `05c6974fa907f5022f28c85f13f06dbb35288556` passed full CI run
  `33883244367` / #1307, iOS run `33883244356` / #128 and Q003C/OpenXML run
  `33883244366` / #125. Both checkpoints are `DONE` for their defined scope.

## Rollback

Revert this integration hardening commit, then revert the two lane commit pairs.
No data or package migration is required.
