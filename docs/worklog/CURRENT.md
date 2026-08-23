# Current Work Handoff

- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` into `develop` — Draft, unmerged
- Implementation head: `19e749473ce68f0b67b110ba70b37339a4c7e155`
- GitHub Actions: CI `#772`, run `32633548509`, success
- Source of truth: `docs/current-status.md`
- SDK contract: `docs/function-extension-sdk-contract.md`
- Criteria contract: `docs/conditional-aggregates-contract.md`
- Final acceptance: `docs/CODEX_FINAL_ACCEPTANCE.md`

## Batch completed: Function Extension SDK v1.0

- Stable namespace/name identity.
- Semantic implementation version and host API version.
- Current host API `1.0`.
- Exact and highest-version resolution.
- Side-by-side versions, exact replacement and unregister fallback.
- Stable aliases and deterministic name conflicts.
- Scalar/range/array and scalar/array-return capability declarations.
- Deterministic/volatile/external-state metadata.
- Pure/context-read-only/external-state classification.
- Engine-only or function-added dependency policy.
- Logical versus flattened argument-count policy.
- Immutable logical arguments retaining range source identity and values.
- Public shared coercion helpers.
- Thread-safe registry and bounded versions per identity.
- Fail-closed API/capability/external-state validation.
- Legacy `IFormulaFunction` compatibility.
- All 92 eager built-ins described as `NERA.BUILTIN` version `1.0.0`.
- `TODAY`/`NOW` marked volatile/context-read-only.

## Batch completed: Conditional Aggregates

- Shared invariant criteria parser.
- Operators `=`, `<>`, `<`, `<=`, `>`, `>=`.
- Error, Boolean, number, DateTime and text criteria.
- Case-insensitive ordinal text.
- `*`/`?` wildcards and tilde escapes.
- Blank/non-blank matching.
- `COUNTIF`, `COUNTIFS`.
- `SUMIF`, `SUMIFS`.
- `AVERAGEIF`, `AVERAGEIFS`.
- Strict positional shape equality.
- Multiple criteria combine by AND.
- Matched aggregate errors propagate; unmatched errors do not.
- Every criteria/aggregate/expression dependency enters the graph.
- Affected-only recalculation tests.
- Two-million positional-pass budget validated before enumeration.

The built-in formula subsystem now recognizes 115 names: 92 eager, 18 reference-aware and five dynamic-array names. User-registered SDK functions are additional.

## CI #772

Passed:

- Core restore/build/tests;
- architecture verification;
- SDK API/version/capability/conflict/dependency tests;
- criteria and six conditional aggregate families;
- dynamic-array and all existing formula regressions;
- full Windows tests and desktop GPU smoke;
- Android;
- iOS and Mac Catalyst;
- MAUI Windows build/handler;
- loaded Table-filter, runtime-context and scale/orientation smokes.

## Explicit limitations

- Formula text does not pin an extension version.
- No plugin package manifest, assembly discovery or publisher signature policy.
- No isolated execution for third-party code.
- Default policy rejects external-state and array-capable extensions.
- Volatile metadata exists; automatic volatile scheduling is pending.
- Criteria parsing is invariant, not locale-specific.
- Conditional aggregate ranges must be canonical cell/range references of equal shape.
- Full Excel/LibreOffice criteria/coercion corpus and indexes are pending.
- Statistical, financial, engineering and database families are pending.

## Progress after exact-head documentation validation

- Engine/viewport/renderer: about `92%`.
- Basic spreadsheet MVP: about `94–96%`.
- Professional roadmap: about `66%`.
- Production readiness: about `43–46%`.

## Next batch

1. Statistical functions foundation.
2. Financial functions foundation.
3. Engineering/database functions and criteria tables.
4. Advanced lookup/reference and dynamic-array helpers.
5. Plugin packaging/discovery/isolation and API compatibility tooling.
6. Native spill UX, drawings/charts and advanced data.
7. Release hardening and final Codex acceptance.

PR remains Draft; do not merge while a newer exact-head CI is red or unknown.
