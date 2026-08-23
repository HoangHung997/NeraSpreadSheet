# Function SDK and Conditional Aggregates milestone

## Validated implementation

- Commit: `19e749473ce68f0b67b110ba70b37339a4c7e155`
- CI: `#772`, run `32633548509`, success
- PR #1 remains Draft and unmerged.

## Function Extension SDK v1.0

Implemented:

- stable namespace/name identity;
- semantic implementation versions;
- host API compatibility, current API `1.0`;
- scalar/range/array and scalar/array-return capability declarations;
- deterministic, volatile and external-state metadata;
- pure, context-read-only and external-state classification;
- engine-captured or function-added dependency policy;
- logical versus flattened argument counting;
- aliases, conflict policy, side-by-side versions and exact lookup;
- highest compatible version resolution;
- immutable invocation arguments with range source identity;
- shared public coercion helpers;
- legacy `IFormulaFunction` registration compatibility.

The default registry accepts scalar/range inputs and scalar results, permits volatile/context-read-only functions and rejects external-state or array-capable extensions. Built-ins are described as `NERA.BUILTIN` version `1.0.0`; `TODAY` and `NOW` are volatile.

## Conditional aggregates

Implemented shared criteria parsing and:

- `COUNTIF`, `COUNTIFS`;
- `SUMIF`, `SUMIFS`;
- `AVERAGEIF`, `AVERAGEIFS`.

Criteria support comparison prefixes, invariant number/date/Boolean/error parsing, ordinal case-insensitive text, `*`/`?` wildcards, tilde escapes and blank/non-blank matching.

Functions require same-shape positional ranges, combine multiple criteria by AND, capture every source dependency, propagate matched aggregate errors, ignore unmatched errors and enforce a two-million positional-pass budget.

## Validation

CI #772 passed Core, architecture, full Windows tests, desktop GPU smoke, Android, iOS, Mac Catalyst, MAUI Windows build/handler and all loaded MAUI Windows smokes.

## Pending

- formula-text function-version pinning;
- plugin package/discovery/loading policy;
- isolated execution for third-party implementations;
- array-returning extensions;
- automatic volatile scheduling;
- locale-specific criteria compatibility;
- full external criteria corpus;
- statistical, financial, engineering and database functions.

## Progress after documentation CI

- Engine/viewport/renderer: about `92%`.
- Basic spreadsheet MVP: about `94–96%`.
- Professional roadmap: about `66%`.
- Production readiness: about `43–46%`.

## Next

1. Statistical functions foundation.
2. Financial functions foundation.
3. Engineering/database functions.
4. Advanced lookup and dynamic arrays.
5. Native spill UX, drawings/charts and advanced data.
6. Packaging, fuzzing, performance budgets and release hardening.
