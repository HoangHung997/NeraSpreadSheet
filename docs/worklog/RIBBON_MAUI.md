# RIBBON-MAUI worklog

## Goal

Close the remaining Ribbon/Bars MAUI lane by adding MAUI-native command chrome
that consumes the existing Ribbon/Bars runtime contracts.

## Starting checkpoint

- Branch: `feature/bootstrap-architecture-v0.1`.
- PR #1 remains Draft, open and unmerged.
- Desktop Ribbon/Bars stack is integrated and green.
- Q003B/Q003C/Q003D are closed for their defined scopes.
- Remaining gap from `docs/current-status.md`: MAUI presentation,
  customization/input mapping and loaded runtime smoke.

## Batch plan

| Batch | Status | Scope |
|---|---|---|
| `RIBBON-MAUI-001` | Done | Add MAUI Ribbon and Bar presenters with command activation and snapshot rebuild tests. |
| `RIBBON-MAUI-002` | Done | Add MAUI shortcut/input binding and customization entry point. |
| `RIBBON-MAUI-003` | Done | Add loaded MAUI Windows smoke for presenter/runtime integration. |
| `RIBBON-MAUI-004` | Done | Update contracts/status/progress and run final validation. |

## RIBBON-MAUI-001 acceptance

- `NeraMauiRibbonView` can render tabs, groups and command buttons from a
  `RibbonRuntimeController`.
- `NeraMauiBarPresenter` can render toolbar, menu and context-menu style command
  trees from a `BarRuntimeController`.
- Command activation flows through the runtime, including host-provided command
  context.
- Runtime snapshot changes rebuild the MAUI visual tree on the dispatcher.
- Tests cover initial structure, enabled/checked metadata, activation and
  snapshot refresh.

## RIBBON-MAUI-001 checkpoint

Implemented:

- `NeraMauiRibbonView` renders MAUI command chrome from
  `RibbonRuntimeController`.
- `NeraMauiBarPresenter` renders MAUI toolbar/menu/context command chrome from
  `BarRuntimeController`.
- Command metadata is projected through `NeraMauiCommandChromeDescriptor` and
  attached to MAUI `Button` controls for automation IDs, shortcuts, checked
  state and descriptions.
- Command activation uses the existing runtime/dispatcher and host-provided
  `CommandContextFactory`.

Validation:

- `dotnet test tests/NeraSpreadSheet.Maui.Tests/NeraSpreadSheet.Maui.Tests.csproj --no-restore`
  passed locally on 2026-09-03 with **32/32** tests.
- The first direct control-instantiation tests exposed that this local headless
  Windows environment cannot initialize WinUI COM classes outside a loaded MAUI
  app. Tests were therefore kept headless for this batch; loaded visual-tree
  validation remains in `RIBBON-MAUI-003`.

## RIBBON-MAUI-002 checkpoint

Implemented:

- `NeraMauiShortcutBinding` maps host-provided shortcut events to visible
  Ribbon/Bar commands through the shared shortcut map and runtime activation.
- `NeraMauiRibbonView.BindShortcuts` and `NeraMauiBarPresenter.BindShortcuts`
  expose the binding entry point for MAUI hosts.
- `NeraMauiRibbonCustomizationBinding` wraps the existing
  `RibbonCustomizationSession`, JSON serializer and runtime publication for
  MAUI customization UI/demo surfaces.

Validation:

- `dotnet test tests/NeraSpreadSheet.Maui.Tests/NeraSpreadSheet.Maui.Tests.csproj --no-restore`
  passed locally on 2026-09-03 with **34/34** tests.

## Current status

## RIBBON-MAUI-003 checkpoint

Implemented:

- Added loaded `NeraSpreadSheet.Maui.Windows.RibbonSmoke` app.
- The smoke creates a real MAUI Windows window, attaches
  `NeraMauiRibbonView` and `NeraMauiBarPresenter`, verifies native handlers,
  activates Ribbon commands, routes a Bar shortcut through
  `NeraMauiShortcutBinding`, applies Ribbon customization and resets it.
- Added the Ribbon smoke publish/run steps to the MAUI Windows CI job.

Validation:

- `dotnet publish tests/NeraSpreadSheet.Maui.Windows.RibbonSmoke/NeraSpreadSheet.Maui.Windows.RibbonSmoke.csproj -c Release -f net10.0-windows10.0.19041.0 -r win-x64 --self-contained false -p:NeraMauiTargetFrameworks=net10.0-windows10.0.19041.0 -o artifacts/maui-windows-ribbon-smoke`
  passed locally.
- `scripts/run-maui-windows-smoke.ps1` passed locally when `DOTNET_ROOT`,
  `DOTNET_ROOT_X64` and `PATH` pointed at the locally installed .NET 10 SDK
  under the user profile. The marker reported `status=success`.

## Current status

## RIBBON-MAUI-004 checkpoint

Validation:

- `scripts/verify-architecture.ps1` passed locally.
- `dotnet build NeraSpreadSheet.slnx` passed locally with **0 warnings** and
  **0 errors**.
- `dotnet test NeraSpreadSheet.Core.slnx` passed locally with **1212/1212**
  tests.
- `dotnet test tests/NeraSpreadSheet.Maui.Tests/NeraSpreadSheet.Maui.Tests.csproj --no-restore`
  passed locally with **34/34** tests.
- Loaded MAUI Windows Ribbon smoke passed locally with marker
  `{"status":"success"}` after setting `DOTNET_ROOT`, `DOTNET_ROOT_X64` and
  `PATH` to the locally installed .NET 10 SDK.

## Current status

`RIBBON-MAUI` is locally complete and ready for exact-head GitHub CI. Do not
mark the PR Ready until the GitHub matrix confirms this head.
