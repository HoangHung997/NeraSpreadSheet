# Function Extension SDK v1.0 contract

This document defines NeraSpreadSheet's validated versioned function-extension contract.

## 1. Descriptor contract

Each function declares namespace/name identity, implementation version, minimum host API, aliases, logical argument bounds, capabilities, volatility/state, security, dependency policy and argument-error policy. Current host API: `1.0`.

## 2. Registry behavior

- Thread-safe registration and lookup.
- Exact and highest-compatible version resolution.
- Side-by-side versions, explicit replacement and unregister fallback.
- Global name/alias conflict rejection.
- Legacy `IFormulaFunction` adaptation.
- One authoritative built-in aggregation path.

## 3. Invocation

Invocation arguments preserve scalar values or range source identity, shape and row-major values. Unsupported scalar/range/array combinations are rejected before evaluator execution. Argument counting is logical or flattened-value according to metadata.

## 4. Built-in milestone

The eager/versioned registry contains **208 names**:

- 92 original flattened-value functions;
- 11 Statistical Foundation functions;
- 39 Advanced Statistical functions;
- 35 financial functions;
- 19 engineering functions;
- 12 database functions.

The broader subsystem adds 18 AST/reference-aware and five dynamic-array names, totaling **231 built-ins**.

Financial SDK metadata:

- `NPV`, `IRR`, `XNPV`, `XIRR` expose scalar/range arguments.
- All other current financial functions, including the five F001 maturity-security functions, are scalar-only.
- All current financial descriptors are deterministic/pure, scalar-returning and logical-argument-counted.
- Calendar and maturity-security functions declare no hidden or volatile dependency.
- Invalid basis/date ordering/value domains fail closed inside invocation.

## 5. Failure policy

Registration rejects incompatible APIs, unsupported capabilities, invalid bounds, conflicting names/aliases, duplicate exact versions without replacement and disallowed external state. Evaluation rejects unsupported argument kinds; evaluator exceptions remain inside the fail-closed engine boundary. Family implementations return explicit spreadsheet errors for invalid domains, budgets or non-convergence.

## 6. Shared implementation services

SDK descriptors remain separate from implementation services. `FinancialDateMath` is an internal platform-neutral service reused by coupon and maturity-security built-ins, not a second registry or an OpenXml/UI dependency.

## 7. Pending

- Plugin manifests, discovery/loading/unloading.
- Publisher signatures and trust policy.
- Third-party isolation and quotas.
- Formula-text version pinning.
- NuGet/plugin packaging and compatibility tooling.
- Third-party array return/spill integration.
- External-state permission prompts and auditing.

## 8. Gates

SDK changes require version ordering, resolution, conflict/replacement/unregister behavior, API/capability/security rejection, range identity, dependency policy, legacy adaptation and built-in descriptor/count regressions followed by the complete hosted matrix.

PR #1 remains Draft while exact-head CI is red or unknown.
