# Function Extension SDK v1.0 contract

This document defines NeraSpreadSheet's validated versioned function-extension contract. It is platform-neutral and independent from OpenXml and UI hosts.

## 1. Identity and versions

Each versioned function declares stable namespace/name identity, semantic implementation version, minimum host API, aliases, logical argument bounds, capabilities, volatility/state, security, dependency policy and error-propagation policy. Current host API: `1.0`.

## 2. Registry behavior

- Thread-safe registration and lookup.
- Exact version and highest-compatible selection.
- Side-by-side versions and explicit exact replacement.
- Unregister with fallback.
- Global name/alias ownership and deterministic conflict rejection.
- Bounded versions per identity.
- Legacy `IFormulaFunction` adapter.
- One authoritative built-in aggregation path; the public built-in registry delegates to one internal versioned registry and does not re-register families manually.

## 3. Capabilities and invocation

Capabilities distinguish scalar/range/array arguments and scalar/array returns. Invocation arguments are immutable and preserve scalar values or range source identity, shape and row-major values. Unsupported argument kinds are rejected before evaluation.

Argument counting is logical or flattened-value, according to the descriptor.

## 4. State, security and dependencies

Volatility classifications are deterministic, volatile and external-state. Security classifications are pure, context-read-only and external-state. The default policy fails closed on unsupported/external-state combinations.

Descriptors either use engine-captured dependencies only or permit additional declared dependencies. Returned dependencies are merged and deduplicated before entering the graph.

## 5. Built-in registry milestone

The eager/versioned registry contains **191 names**:

- 92 original flattened-value functions;
- 11 Statistical Foundation functions;
- 39 Advanced Statistical functions;
- 18 financial functions;
- 19 engineering functions;
- 12 database functions.

The broader formula subsystem adds 18 AST/reference-aware and five dynamic-array names, totaling **214 built-in names**.

Financial metadata:

- `NPV`, `IRR`, `XNPV`, `XIRR` expose scalar/range arguments.
- The remaining financial functions, including `CUMIPMT`, `CUMPRINC`, `DB`, `DDB`, `VDB`, are scalar-only.
- All current financial descriptors are deterministic/pure, return a scalar and use logical argument counting.
- Root and schedule resource contracts are enforced inside invocation.

## 6. Compatibility and failure policy

Registration rejects incompatible APIs, unsupported capabilities, invalid argument bounds, conflicting names/aliases, duplicate exact versions without replacement and disallowed external state.

Evaluation rejects unsupported argument kinds. Function exceptions are converted through the fail-closed engine boundary. Family implementations return explicit errors for invalid domains, exhausted budgets or non-convergence.

## 7. Deliberately pending

- Plugin manifest/package format and discovery.
- Publisher signatures and trust policy.
- Isolation and resource quotas for third-party code.
- Formula-text version pinning.
- NuGet/plugin packaging and API compatibility tooling.
- Third-party array return integration.
- External-state permission prompts and auditing.

## 8. Validation gates

SDK changes require version ordering, exact/highest resolution, conflict/replacement/unregister behavior, API/capability/security rejection, range identity/shape, dependency policy, legacy adaptation and built-in descriptor/count regressions, followed by the complete hosted matrix.

PR #1 remains Draft while exact-head CI is red or unknown.
