# PACKAGING-SDK worklog

## Goal

Validate package/versioning/API readiness before moving into
security/recovery hardening and the final Windows 11 demo lane.

## Starting checkpoint

- Branch: `feature/bootstrap-architecture-v0.1`.
- PR #1 remains Draft, open and unmerged.
- Formula, Q001, Q002, Q003A, Q003B, Q003C, Q003D, `RIBBON-MAUI`,
  `PIVOT-OPENXML-STANDARD` and `DRAWING-MEDIA-COMPAT` are closed for their
  defined scopes.

## Batch plan

| Batch | Status | Scope |
|---|---|---|
| `PACKAGING-SDK-001` | Done | Inventory project/package metadata, target frameworks, packable projects and samples. |
| `PACKAGING-SDK-002` | Done locally | Add package/version/API compatibility checks that fit the existing build. |
| `PACKAGING-SDK-003` | In progress | Update docs and collect local plus exact-head GitHub evidence. |

## Scope

- NuGet/package metadata consistency.
- Target framework and sample package-readiness checks.
- Public API compatibility guardrails where practical inside the repo.

## Out of scope

- Publishing packages.
- Marking PR #1 ready or merging it.
- Trust/isolation/recovery hardening.
- Final Windows 11 demo app integration.

## Current status

`PACKAGING-SDK` is implemented locally and awaiting remote integration plus
exact-head GitHub evidence.

## Inventory

- `NeraSpreadSheet.Core.slnx` packs 17 SDK packages from `src/` and excludes
  tests, samples and benchmarks.
- Existing common metadata already covered author, company, repository URL,
  repository type and `VersionPrefix` `0.1.0`.
- Four packable SDK projects inferred package IDs implicitly and now declare
  them explicitly:
  - `NeraSpreadSheet.Commands`;
  - `NeraSpreadSheet.Ribbon.Core`;
  - `NeraSpreadSheet.Bars.Core`;
  - `NeraSpreadSheet.DataGrid.Core`.
- Baseline `dotnet pack` succeeded but each package warned that no README was
  included.

## Implementation

- Added common NuGet project URL, readme and tags in `Directory.Build.props`.
- Pack the root `README.md` into each SDK package at the package root.
- Added explicit package IDs for the four remaining SDK projects.
- Added `scripts/verify-packaging-sdk.ps1` to validate:
  - required common package metadata;
  - explicit `PackageId` and `Description` on every `src` project;
  - target framework declarations on every `src` project;
  - tests, samples and benchmarks stay non-packable and do not declare package
    IDs.
- Added CI steps to run the packaging metadata verifier, pack
  `NeraSpreadSheet.Core.slnx`, and upload the generated `.nupkg` files as the
  `sdk-packages` artifact.

## Local validation

- `dotnet build NeraSpreadSheet.slnx --no-restore`: passed, 0 warnings,
  0 errors.
- `dotnet build NeraSpreadSheet.Core.slnx -c Release --no-restore`: passed,
  0 warnings, 0 errors.
- `dotnet test NeraSpreadSheet.Core.slnx -c Release --no-build`: 1216/1216
  passed.
- `scripts/verify-architecture.ps1`: passed.
- `scripts/verify-packaging-sdk.ps1`: passed.
- `dotnet pack NeraSpreadSheet.Core.slnx -c Release --no-build`: produced
  17 SDK packages.
- Package inspection confirmed all 17 generated `.nupkg` files contain
  `README.md`.

## Exact-head status

Pending. The lane is not closed until the remote integration commit and its
exact-head GitHub Actions evidence are green.
