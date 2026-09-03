# SECURITY-RECOVERY worklog

## Goal

Harden trust, isolation and recovery paths before broader localization/a11y
completion and final Windows 11 demo packaging.

## Starting checkpoint

- Branch: `feature/bootstrap-architecture-v0.1`.
- PR #1 remains Draft, open and unmerged.
- `PACKAGING-SDK` is closed at
  `47591a8ac223f0ee5141e92bb31fca304fbe0a50` with exact-head CI #1239, iOS
  gate #42 and Q003C/OpenXML gate #39 all successful.
- Current roadmap score: `83.98% ~= 84%`.

## Batch plan

| Batch | Status | Scope |
|---|---|---|
| `SECURITY-RECOVERY-001` | Done for first pass | Inventory plugin/external input boundaries, persistence recovery paths and renderer/session failure modes. |
| `SECURITY-RECOVERY-002` | In progress | Add focused hardening tests and fixes for the highest-risk bounded surfaces. |
| `SECURITY-RECOVERY-003` | Pending | Update docs and collect local plus exact-head GitHub evidence. |

## Scope

- Plugin and extension trust boundaries that can affect workbook/session state.
- OpenXML/session persistence failure and recovery behavior.
- Renderer/host smoke failure containment where a bad package or state should
  not corrupt the workbook model.

## Out of scope

- Publishing packages.
- Marking PR #1 ready or merging it.
- Final Windows 11 demo app integration.
- Broad localization/accessibility completion beyond recovery-relevant paths.

## Current status

First-pass inventory started from the PACKAGING-SDK exact-head checkpoint.

## First-pass inventory

- Formula external-state functions already require an explicit
  `IFormulaExternalFunctionContext` and fail closed with `#N/A` when no provider
  is present.
- Formula function registry policy blocks external-state functions by default
  unless the host opts in.
- OpenXML package loading validates package graphs before import, including part
  URI, relationship identifier/type and reference target safety.
- Unknown-part preservation is opt-in and bounded by worksheet topology checks.
- Renderer recovery has Direct2D, swap-chain and WPF shared-texture stress
  coverage.

## Implementation checkpoint

- Added exception containment around external formula provider calls so a host
  provider failure returns `#N/A` instead of escaping through formula evaluation.
- Covered scalar external functions (`WEBSERVICE`, `CALL`) and the dynamic-array
  external `STOCKHISTORY` provider boundary with fail-closed tests.
- Added seekable/readable destination recovery for document-level XLSX saves:
  if final destination write or flush fails after validation, the serializer
  restores the previous destination bytes and then surfaces the original write
  failure.
- Covered the save-failure path with a stream that throws on the first package
  write and verifies that existing destination bytes survive unchanged.
- Local test execution is blocked in this workspace session because only .NET
  SDK 8.0.424 is on PATH while `global.json` requires 10.0.302. GitHub CI will
  be the validation source for this batch.
