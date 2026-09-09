# AVALONIA-001 — native Ribbon / split / editor / clipboard integration

## Source and ownership

PR #4, branch `feature/avalonia-001-host`. This implementation starts from
`a243f6f74fcb6da4ee32d6bf5f93803e6eb07224`. PR #1/root advanced independently
to `d73cd2e2bcb7e2832e9a8b3fd071373b62cb2a0f`; do not overwrite its WPF
formula-bar/iOS/coordination changes. No shared engine or existing platform-host
source was changed. Final source SHA and CI run IDs belong in the PR evidence
comment; a build on a parent SHA does not qualify this integration.

## Implemented, not a whole-product completion claim

- Native NeraRibbonControl: shared responsive layout, QAT/backstage/contextual
  tabs, minimized view, overflow, buttons/toggles/combos/color choices/split
  buttons/hierarchical menus and gallery previews. Shared command runtime,
  selectable-value validation, icons, key tips and routed-key arbitration.
- NeraBarPresenter: native toolbar/menu/context menu, state, enabled/checked
  commands, context collection, errors, queued refresh and deterministic cleanup.
- NeraRibbonCustomizationControl: native catalog/structure/QAT editor, add/remove,
  rename/reorder/visibility/size, policy checks, Apply/Cancel/Reset and shared JSON.
  No-op Apply preserves the original profile; structural edits leave an inherited
  QAT inherited. Explicit empty QAT remains explicit. This is a host-boundary
  intent-preservation rule, not a fix/closure of the shared/other-host QAT hold.
- OS clipboard: actual Avalonia clipboard text and private selection token;
  bounded TSV, native rich-package reuse only when token/text/package match.
  Cut clears only after successful OS write and unchanged operation lease.
  Deferred operations reject stale session/sheet/selection/version/detach/dispose.
- Native split host: four stable panes, per-pane scrollbars, shared topology,
  fractional offsets, active pane and split Undo/Redo. Resize retains native
  editors; hidden panes cancel owned drafts. Cross-pane formula pointing routes
  to the original draft owner without committing or moving the active pane.
- Formula editor: native completion and structured-reference suggestions via the
  existing assistant, nested argument help, UTF-16 directed selection bridge,
  grammar-aware reference insertion, replaceable provisional range and highlights.
  Selection/quoted-text guards and queued TextChanged acknowledgements protect
  native caret semantics. Render/scroll do not recalculate or parse formulas.
- Default sample is now FullShellWindow: Ribbon, menu, QAT/customization,
  one canonical formula bar, worksheets, split panes, OS clipboard, zoom/freeze,
  Open/Save XLSX. The old sample remains the independent `--smoke` regression.

## CI and regression contract

The workflow retains repository analyzers, complete Release graph/hash checks,
architecture/isolation, five fresh test processes, and the old loaded-native
smoke. New minimum is 77 tests per process, with zero skips/failures required.
Additional `--full-ui-smoke` requires 23 named feature assertions, exact source
SHA and two native captures. It exercises the actual OS clipboard and native
window/control tree, but is not physical mouse/keyboard/IME/GPU latency proof.
No full-UI check is silently substituted with a headless/basic-host pass.

Red-before-fix history includes CA2012 async test handling, CA1859 return types,
Avalonia PlaceholderText API, and cross-pane premature commit in source 5210753.
The latter occurred because Avalonia's tunneling EventRoute traverses sibling
handlers in reverse order. Fix f5e25ed4 routes formula handling explicitly inside
the single parent input handler before ActivatePane; the failing pointer test
and its draft/history/selection assertions are retained unchanged.

No local C# build claim: the execution environment was unavailable. Only actual
GitHub Actions results on the pushed final source may be reported as PASS.

## Still OPEN / blocked

The attempted Table/Filter popup write was rejected by the tool safety check.
Neither `NeraAutoFilterPopupPresenter.cs` nor `NeraSpreadsheetControl.Filter.cs`
was committed. Do not retry via another route or claim Table/Filter completion.
There is no Table Design mutation shell or paged/date/rich filter native proof
in this candidate. Table/contextual Ribbon tests validate projection only.

Also OPEN: dedicated print-preview behavior/native tests, full Table editor
corpus, high-contrast/screen-reader/IME/touch/multi-monitor validation, exhaustive
command/UI parity, isolated NuGet consumer/full dependency feed and performance.
The sample's staged save is not atomic disk recovery. Global QAT inheritance,
Excel-1900 boundary and dual-failure recovery holds are unchanged.

## Run / next handoff

```powershell
dotnet build NeraSpreadSheet.Avalonia.slnx -c Release
dotnet test tests/NeraSpreadSheet.Avalonia.Tests -c Release
dotnet run --project samples/NeraSpreadSheet.Avalonia.Sample -c Release
```

Next action: verify every job and full-UI native result on the final pushed SHA,
repair any observed failure without weakening gates, and record the evidence in
PR #4. Both PRs stay Draft, no Ready/merge/public publish. Before root integration,
reconcile only owned paths and run the existing six root gates plus Avalonia on
the combined exact head. Rollback is by reverting these Avalonia-only commits;
there is no workbook migration.
