# RIBBON-008 — full Ribbon item model

- Base SHA: `05c6974fa907f5022f28c85f13f06dbb35288556`.
- Branch: `feature/ribbon-008-item-model`.
- Owner scope: `NeraSpreadSheet.Ribbon.Core`; the concrete WPF, WinForms and
  MAUI Ribbon presenters; Ribbon-only contract/tests/runtime smokes; this
  checkpoint worklog.
- Excluded shared ownership: `docs/current-status.md`,
  `docs/worklog/CURRENT.md`, `docs/worklog/RIBBON_TABLE_FILTER_UX.md`, Core,
  Editing and OpenXml Table/Filter code, and Filter popup presenter files.

## Planned implementation

- Shared immutable item definitions and presentation state for button, toggle,
  split button, dropdown/menu, combo box, gallery, color picker and separator.
- Item-specific host-neutral measurement, activation and error semantics.
- Equivalent shortcut, tooltip, automation name, icon fallback, overflow and
  focus behavior across WPF, WinForms and MAUI.

## Validation

Pending implementation.

## Rollback

Revert the RIBBON-008 implementation commit; the RIBBON-007 definition and
responsive snapshot remain the compatibility baseline.
