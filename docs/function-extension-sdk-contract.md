# Function Extension SDK v1.0 contract

This document defines NeraSpreadSheet's validated versioned function-extension contract. It is platform-neutral and independent from OpenXml and UI hosts.

## 1. Identity and versions

Each versioned function declares:

- stable namespace/name identity;
- semantic implementation version;
- minimum host API version;
- aliases;
- minimum/maximum logical arguments;
- argument-count policy;
- capabilities;
- volatility/state classification;
- security classification;
- dependency policy;
- argument-error propagation policy.

Current host API: `1.0`.

## 2. Registry behavior

- Thread-safe registration and lookup.
- Exact version lookup.
- Highest compatible version selection.
- Side-by-side versions under one identity.
- Exact replacement only when explicitly requested.
- Unregister with fallback to next highest version.
- Global name/alias ownership and deterministic conflict rejection.
- Bounded versions per identity.
- Legacy `IFormulaFunction` registration through a `LEGACY` adapter.

## 3. Capabilities and invocation

Capabilities distinguish scalar/range/array arguments and scalar/array return. Invocation arguments are immutable and preserve scalar value or range source identity, shape and row-major values. Descriptor validation rejects unsupported array features, incompatible host APIs and disallowed external-state functions before registration.

Argument-count policy is either:

- logical arguments; or
- flattened values for legacy-compatible aggregate-style functions.

## 4. State and security

Volatility classifications include deterministic, volatile and external-state. Security classifications distinguish pure, context-read-only and external-state functions. The default registry policy allows current built-ins and safe extensions but fails closed on unsupported/external-state capability combinations.

Volatility metadata exists; automatic workbook volatile scheduling remains pending.

## 5. Dependencies

A descriptor chooses engine-captured dependencies only or permits additional function-declared dependencies. Returned dependencies are merged with expression/range dependencies and deduplicated before entering the graph.

## 6. Built-in registry milestone

The validated eager/versioned built-in registry contains **144 names**:

- 92 original flattened-value functions;
- 11 statistical functions;
- 10 financial functions;
- 19 engineering functions;
- 12 database functions.

All new engineering/database functions use SDK v1 descriptors. Engineering is scalar-only. Database functions preserve logical range identity and set `propagateArgumentErrors=false` so matched-row semantics can inspect errors selectively.

The broader formula subsystem adds 18 AST/reference-aware and five dynamic-array names, totaling **167 built-in names**.

## 7. Compatibility and failure policy

Registration rejects:

- minimum host API newer than current host;
- unsupported capability combinations;
- invalid argument bounds;
- conflicting name/alias ownership;
- duplicate exact versions without explicit replacement;
- external-state functions under the default restrictive policy.

Evaluation rejects unsupported argument kinds before invocation. Function exceptions are converted through the engine's fail-closed error boundary; they do not escape into UI hosts.

## 8. Deliberately pending

- Plugin manifest/package format.
- Discovery/loading/unloading.
- Publisher signatures and trust policy.
- Process/AppDomain/WASM isolation and resource quotas.
- Formula-text version pinning.
- NuGet/plugin packaging and API compatibility tooling.
- Third-party array return contract and host spill integration.
- External state permission prompts and auditing.

## 9. Validation gates

SDK changes require version ordering, exact/highest resolution, conflict/replacement/unregister behavior, API/capability/security rejection, range identity/shape, additional dependency, legacy adapter and built-in descriptor/count regressions, followed by the complete hosted matrix.

PR #1 remains Draft while exact-head CI is red or unknown.
