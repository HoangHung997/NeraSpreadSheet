# Ribbon, Table, Filter and UX delivery plan

## Outcome

Deliver a polished, application-customizable spreadsheet command surface for
WPF, WinForms and MAUI. Applications can use Nera defaults, replace artwork,
hide/show/reorder commands and eventually create their own tabs and groups
without changing workbook or calculation code.

## Work sequence

| Checkpoint | Scope | Exit criteria | Estimate |
| --- | --- | --- | --- |
| `ICON-001` | Semantic icon inventory and visual rules | File/Home/Insert/Page Layout/Formulas/Data/Review/View/Table Design/customization families mapped; license and provenance recorded | Complete |
| `ICON-002` | Cross-platform asset package | SVG masters plus 16/20/24/32/48 PNGs for light, dark and both high-contrast themes; deterministic generator and manifest tests | Complete |
| `ICON-003` | Presenter integration | WPF, WinForms and MAUI Ribbon/Bar presenters use default icons; existing application resolvers still override them; active command catalogs have icon keys | Current batch |
| `RIBBON-007` | Responsive Ribbon layout | large/small item layouts, group measurement, overflow/collapse, DPI/theme refresh and no per-cell UI regression | 2–3 working days |
| `RIBBON-008` | Full user customization | cross-group drag/drop, user-created tabs/groups, QAT, reset/import/export and conflict-safe persistence | 3–5 working days |
| `TABLE-004` | Contextual Table Design surface | table styles gallery, header/total/banded toggles, resize/remove/convert actions and state binding | 3–4 working days |
| `FILTER-005` | Complete Filter UX | text/number/date/color/custom filters, search/checklist, clear/reapply, filter-state indicators and keyboard accessibility | 4–6 working days |
| `UX-006` | Visual and accessibility polish | keyboard tips/focus, disabled/checked/hover states, dark/high-contrast inspection, Vietnamese resources and loaded runtime smokes | 2–3 working days |
| `RELEASE-007` | Demo and NuGet checkpoint | Win11 x64 demo, package verification, screenshots, exact-head CI green and rollback notes | 1–2 working days |

Estimates are implementation ranges, not elapsed calendar guarantees. A later
checkpoint starts only after the previous checkpoint's build, focused tests,
loaded runtime smoke and architecture checks are green.

## Ownership and synchronization

- Each active worker uses an isolated feature branch.
- A checkpoint has one integration owner and one status update in
  `docs/worklog/CURRENT.md`.
- No two workers edit `CURRENT.md` simultaneously; the integration owner writes
  the checkpoint result after all component commits are available.
- Assets are regenerated only from the pinned upstream commit and the checked-in
  mapping script. Generated files are never hand-edited.
- Ribbon presenters consume semantic keys only; command handlers do not depend
  on platform image types.

## Icon acceptance rules

- Shared 24-by-24 optical grid, rounded geometry and consistent stroke weight.
- Meaning remains recognizable at 16 pixels and no important shape is clipped.
- Large commands use 32 pixels; compact commands and menus use 16 pixels.
- Theme variants meet contrast needs without duplicating command identities.
- Missing artwork falls back to caption-only rather than disabling a command.
- Excel and DevExpress visuals are interaction references only; their artwork is
  not copied or redistributed.
