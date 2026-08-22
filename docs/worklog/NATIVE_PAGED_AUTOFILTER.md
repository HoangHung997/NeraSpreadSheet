# Native paged AutoFilter handoff

## Scope implemented

- Unified Table/direct-worksheet filter targets.
- Unified filter-button geometry.
- Generation-guarded Table and worksheet paged sessions.
- Random-access page cache and shared current-page presenter.
- WPF, WinForms and MAUI dispatcher bindings.
- WPF native paged popup presenter.
- WinForms native paged dropdown presenter.
- MAUI responsive paged `CollectionView` host.
- Windows MAUI keyboard/focus lifecycle foundation.
- Core, geometry, MAUI host and MAUI binding regression tests.

## Safety rules

- No per-cell control.
- Native hosts materialize one value page only.
- Stale generations cannot read or mutate a newer menu.
- Search and refresh are cancellable.
- Apply/Clear always uses production history.
- Table and direct worksheet filters share predicate and compressed-row semantics.
- PR #1 remains Draft until exact-head CI is green.

## Validation still required before promotion

- Exact-head Core, Windows, Android, iOS, Mac Catalyst and MAUI Windows matrix.
- Loaded WPF paged popup interaction smoke.
- Loaded WinForms paged dropdown interaction smoke.
- Loaded MAUI paged host Apply/Undo/Redo/reopen/focus smoke.
- Pointer passthrough over the MAUI GPU spreadsheet outside visible filter buttons.
- Search cancellation during close/reopen and workbook/worksheet replacement.
- Large distinct-value memory/performance budget.
- Screen-reader, high-contrast, localization and theme review.

## Next implementation order

1. Fix all exact-head CI compile/analyzer failures.
2. Add loaded native paged-presenter smoke coverage.
3. Harden MAUI overlay pointer routing and close-time cancellation.
4. Add XLSX top10/dynamic/date-group filters and sort state.
5. Complete Table design/resize/style manager UI.
6. Continue formula/function, printing/PDF, drawings/charts and pivot work.

Full contract: `docs/native-paged-autofilter-contract.md`.
