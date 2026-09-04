# RIBBON-008 — full Ribbon item model

- Base SHA: `05c6974fa907f5022f28c85f13f06dbb35288556`.
- Branch: `feature/ribbon-008-item-model`.
- Implementation SHA: `6493b34872ec4d7f8c24909ede1de5f924ae87d5`.
- Independent-review fix SHA: `77bffbf82c33d0f8a21cce66876540c45324bbb5`.
- Owner scope: `NeraSpreadSheet.Ribbon.Core`; the concrete WPF, WinForms and
  MAUI Ribbon presenters; Ribbon-only contract/tests/runtime smokes; this
  checkpoint worklog.
- Excluded shared ownership: `docs/current-status.md`,
  `docs/worklog/CURRENT.md`, `docs/worklog/RIBBON_TABLE_FILTER_UX.md`, Core,
  Editing and OpenXml Table/Filter code, and Filter popup presenter files.

## Implementation

- `RibbonItemKind` covers button, toggle, split button, dropdown, menu, combo
  box, gallery, color picker and separator while preserving the legacy
  constructor and automatic checked-command toggle behavior.
- `CommandState`, `CommandItem` and `CommandPresentation` now carry defensively
  materialized selected value and nested items source alongside enabled and
  checked state. Duplicate sibling values are rejected.
- Selectable item activation carries `RibbonItemActivation` through the shared
  dispatcher, retains the original host parameter, refreshes after success and
  propagates cancellation/error without swallowing it.
- Item measurement callback runs in logical units inside the shared responsive
  engine and rejects non-finite or negative results.
- WPF, WinForms and MAUI map the same snapshot to native button/toggle,
  split/dropdown/menu, combo/picker, gallery and separator chrome. Tooltip,
  shortcut, automation-name override, caption/icon fallback, nested overflow
  choices and stable focus identity use the same model on all three hosts.
- Ribbon customization preserves kind, automation name and measurement callback
  when applying visibility/order/size overrides, so RIBBON-007 behavior remains
  intact.
- The independent review restored the exact legacy positional-record API shape
  for `CommandState`, `CommandPresentation` and `RibbonItemDefinition`, including
  deconstruction, `with` and the original constructor signatures. New selectable
  state remains available through separate, non-ambiguous overloads.
- Selectable activation now revalidates the current snapshot, command state,
  leaf identity and enabled ancestor path before dispatch. A successful dispatch
  refreshes state with the original host context; stale, parent, disabled,
  cancelled and failed activations cannot publish a false-success snapshot.
- Responsive measurement is cached once per item/size during one layout pass.
  Separator visual width now matches the shared measured width on every host.
- Loaded WPF, WinForms and MAUI presenters now exercise usable overflow content,
  explicit button/toggle semantics, scrollable galleries with icons, and stable
  split-button primary/menu focus and automation identities.

## Validation

- Core solution Release build/analyzers: **0 warnings, 0 errors**.
- Core solution tests: **1,307/1,307 passed**.
- Commands/Ribbon contract tests: **78/78 passed**.
- Focused loaded WPF/WinForms Ribbon tests: **10/10 passed**.
- MAUI presenter tests: **34/34 passed**.
- Loaded MAUI Windows Ribbon smoke: **success**, including every item kind,
  selected-value activation, opening and activating bounded overflow content,
  gallery icons/scrolling, toggle accessibility, split focus restoration and
  native platform views.
- WPF, WinForms, MAUI Windows, Android, iOS and Mac Catalyst Release builds:
  **0 warnings, 0 errors**. Android API 36 was installed into a user-scoped
  temporary SDK outside the repository.
- Architecture verification and SDK packaging verification: **passed**.
- `git diff --check` and diff scan for secrets, machine identifiers, personal
  paths and tokens: **passed**.
- Full Windows.Rendering: **62/63 passed locally**. The single failure is the
  known environment-only native mouse smoke, which could not read the desktop
  cursor position and stopped before exercising SDK behavior. The Ribbon-only
  loaded set is fully green.
- The repository pins SDK `10.0.302`, unavailable on this machine; local gates
  used installed SDK `10.0.201` from a working directory outside the worktree
  without changing `global.json`.
- Exact-head GitHub Actions: pending integration. The feature-branch push does
  not match the checked-in `ci` push trigger (`main`/`develop` only), and the
  public Actions API listed no run for this branch. The integration owner must
  cherry-pick the implementation, push the resulting exact integration HEAD
  and require full CI, iOS and Q003C/OpenXML success there.

## Remaining limits

- Contextual tabs, minimized/backstage state, QAT and key tips remain
  RIBBON-009; deep customization remains RIBBON-010.
- Combo/color-picker native rows use text fallback when a host picker cannot
  render an item image; command and menu chrome resolve icons normally.
- WPF exposes disabled combo rows natively. WinForms and MAUI picker APIs do not
  provide a stable per-row disabled visual; the shared runtime still rejects a
  stale or disabled selected value atomically, and the presenter restores the
  last valid selection. This host limitation is not claimed as full visual
  picker parity.

## Rollback

Revert the review fix `77bffbf82c33d0f8a21cce66876540c45324bbb5`
and implementation commit `6493b34872ec4d7f8c24909ede1de5f924ae87d5`.
The RIBBON-007 definition and responsive snapshot remain the compatibility
baseline, and the legacy positional records remain source and binary compatible.
