# RIBBON-010 Customization SDK handoff

- Checkpoint: `RIBBON-010`.
- Owner: Codex task `RIBBON-010 Customization SDK`.
- Branch: `feature/ribbon-010-customization`.
- Base integration SHA: `f75e2f103598cbbe7b5c22f92c3ab8dd755ef8c3`.
- Owned files/directories: `src/NeraSpreadSheet.Ribbon.Core` customization; Ribbon customization presenters/bindings in WPF, WinForms and MAUI; Ribbon-only tests/smokes; this contract/worklog.
- Expected implementation commits: host-neutral SDK and persistence; cross-host presenters/smokes; validation/docs.
- Explicit exclusions: Table/Filter code and the shared files `docs/current-status.md`, `docs/worklog/CURRENT.md`, `docs/worklog/RIBBON_TABLE_FILTER_UX.md`.

Implementation and validation evidence will be appended before handoff.

## Implementation

- Implementation commit: `eb08b0f95176b1a23e01ccf0b09a112bdc562dac`.
- Added one grouped `RibbonCommandCatalog`, including registered commands not yet
  placed on a Ribbon, without duplicate stable identities.
- Extended the existing immutable customization path with custom tab/group
  creation, rename/remove/reorder, cross-group command placement, large/small
  sizing and QAT add/remove/reorder.
- Added transactional preview/commit/cancel/reset, atomic application-policy
  enforcement and cross-host WPF/WinForms/MAUI APIs.
- Upgraded Ribbon JSON to schema v2 while reading legacy-v0 and v1. Unknown
  optional-module tab/group/command/QAT IDs survive round-trip.
- Retained public constructor signatures from the v1 SDK for binary compatibility.
- Updated WPF/WinForms native dialogs with Apply/Cancel automation identities and
  disabled editing affordances for policy-locked entries. MAUI uses the same
  session through `NeraMauiRibbonCustomizationBinding`.

## Key files

- `src/NeraSpreadSheet.Ribbon.Core/RibbonCustomizationSdk.cs`
- `src/NeraSpreadSheet.Ribbon.Core/RibbonCustomization.cs`
- `src/NeraSpreadSheet.Ribbon.Core/RibbonCustomizationSession.cs`
- `src/NeraSpreadSheet.Ribbon.Core/RibbonCustomizationJsonSerializer.cs`
- `src/NeraSpreadSheet.Ribbon.Core/RibbonRuntimeController.cs`
- `src/NeraSpreadSheet.Wpf/NeraRibbonCustomizationDialog.cs`
- `src/NeraSpreadSheet.WinForms/NeraRibbonCustomizationDialog.cs`
- `src/NeraSpreadSheet.Maui/NeraMauiRibbonCustomizationBinding.cs`
- `docs/ribbon-deep-customization-contract.md`

## Local validation

- Required .NET SDK `10.0.302` installed in a temporary directory; repository
  `global.json` remained unchanged.
- Core solution restore/build: passed, **0 warnings / 0 errors**.
- Core solution tests: **1360/1360 passed** (including Commands **101/101** and
  OpenXML **93/93**).
- Focused loaded WPF/WinForms customization smoke: **2/2 passed**.
- MAUI Windows customization binding: **2/2 passed**. The installed workload set
  is attached to SDK 10.0.201, so this host-only gate used its MSBuild/vstest;
  host-neutral and desktop gates used the required SDK 10.0.302.
- Loaded MAUI Windows Ribbon app: success with marker
  `structural-preview-cancel-hide-reset`. First execution hit the pre-existing
  split-button native-focus timing assertion before RIBBON-010 ran; immediate
  rerun of the same published binary passed every smoke stage.
- Architecture verification: passed.
- SDK packaging metadata verification: passed.
- Diff whitespace, owned-path and potential-secret scans: passed. No Table,
  Filter, OpenXML or forbidden shared worklog/status file was changed.

## Exact-head GitHub gates

- Full CI run `33933834709` / #1314: **success**.
- iOS accessibility run `33933836720` / #135: **success**.
- Q003C/OpenXML run `33933839228` / #132: **success**.

## Risk, rollback and remaining limits

- Profile v2 is forward-only for old binaries; applications that may roll back to
  the v1 SDK should retain a v1 profile backup. Rollback code by reverting
  `eb08b0f95176b1a23e01ccf0b09a112bdc562dac`.
- Customization remains bounded to 1 MiB, JSON depth 64 and 10,000 nodes. It runs
  only when opening/editing/applying Ribbon chrome, never on worksheet scroll or
  render frames, and creates no control per cell.
- This lane does not implement Table/Filter behavior and does not claim any such
  capability.

## Next step

Integration owner should review and cherry-pick the implementation and handoff
commits onto `feature/bootstrap-architecture-v0.1`; do not merge PR #1 and do not
mark the shared board DONE until exact-head integration CI is green.
