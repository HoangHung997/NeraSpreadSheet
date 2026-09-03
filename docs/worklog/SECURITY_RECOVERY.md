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
| `SECURITY-RECOVERY-002` | Done for first three patches | Add focused hardening tests and fixes for the highest-risk bounded surfaces. |
| `SECURITY-RECOVERY-003` | In progress | Update docs and collect local plus exact-head GitHub evidence. |

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

First three hardening patches are integrated and exact-head validated. The lane
remains active for additional bounded trust/recovery coverage before moving to
localization/a11y completion.

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
- Added the same seekable/readable destination recovery to workbook-level XLSX
  saves, including both normal generated packages and preserve-unknown output.
- Covered both document-level and workbook-level save-failure paths with a
  stream that throws on the first package write and verifies that existing
  destination bytes survive unchanged.
- Local test execution is blocked in this workspace session because only .NET
  SDK 8.0.424 is on PATH while `global.json` requires 10.0.302. GitHub CI will
  be the validation source for this batch.

## Exact-head evidence

Commit: `e819c9b26c2136cc6fd9d08c8e4711f6129c888b`.

- Full CI: #1254 / run `33798063286` -- success.
- iOS analytics accessibility gate: #60 / run `33798063047` -- success.
- Q003C/OpenXML gate: #57 / run `33798063208` -- success.
- `sdk-packages` artifact from CI #1254: ID `9910001536`, digest
  `sha256:e47762e21f3cd60cc7a30eaa1c9461d7dffc770739ba0a5e58e316d96edd0ff0`,
  expires `2026-12-02T19:43:07Z`.

## Next candidate surfaces

- OpenXML package graph validation: add compatibility-safe tests around escaped
  relationship target edge cases without blocking valid Excel hyperlink/file
  references.
- Preserve-unknown recovery: verify invalid or mismatched preserved envelopes
  reject atomically without mutating workbook state.
- Host/session failure containment: add narrowly scoped tests for renderer or
  interaction recovery only where failures can corrupt workbook/session state.
