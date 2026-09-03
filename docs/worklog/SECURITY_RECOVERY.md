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
| `SECURITY-RECOVERY-002` | Done for first eight patches | Add focused hardening tests and fixes for the highest-risk bounded surfaces. |
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

First eight hardening patches are integrated and exact-head validated. The lane
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
- Added session-level XLSX save recovery and consolidated document, workbook
  and session final package writes through a shared internal recovery helper.
- Covered document-level, workbook-level and session-level save-failure paths
  with streams that throw on the first package write and verify that existing
  destination bytes survive unchanged.
- Added a preserve-unknown worksheet-reference replacement gate: replacing a
  preserved worksheet with a new same-count worksheet is rejected before
  destination bytes are mutated.
- Added strict percent-escape validation for package graph URI text before
  decoding, so malformed escaped relationship targets, part URIs and
  relationship type URIs are rejected while common Excel hyperlink, file,
  relative, fragment and safe escaped part/type forms remain accepted.
- Added a package-archive relationship scan before OpenXML SDK load so every
  ZIP part name and `.rels` entry is checked for malformed escaped part URIs,
  relationship type URIs and relationship targets, including relationships the
  SDK would not materialize into the workbook graph.
- Local validation uses the repo-local .NET SDK 10.0.302 install: Core solution
  1226/1226 and OpenXML 79/79 passed.

## Exact-head evidence

Commit: `7f87a9bc2d7e8cb2d26b5b66210f1fa35005d839`.

- Full CI: #1278 / run `33811795200` -- success.
- iOS analytics accessibility gate: #94 / run `33811795075` -- success.
- Q003C/OpenXML gate: #91 / run `33811795185` -- success.
- `sdk-packages` artifact from CI #1278: ID `9915090019`, digest
  `sha256:df48cc5bef76c5dca04f9cddccbd38b855846319a1157c4f2e15cc4355d91c2d`,
  expires `2026-12-02T22:10:50Z`.

## Next candidate surfaces

- OpenXML package graph validation: extend archive-level validation only if a
  real fixture exposes another compatibility-safe gap beyond ZIP part and
  relationship entry scanning.
- Preserve-unknown recovery: extend atomic rejection coverage only if a new
  mismatched envelope case is found beyond worksheet topology replacement.
- Host/session failure containment: add narrowly scoped tests for renderer or
  interaction recovery only where failures can corrupt workbook/session state.
