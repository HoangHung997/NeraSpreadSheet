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

The eager/versioned registry contains **228 names**:

- 92 original flattened-value functions;
- 11 Statistical Foundation functions;
- 39 Advanced Statistical functions;
- 55 financial functions;
- 19 engineering functions;
- 12 database functions.

The broader subsystem adds 18 AST/reference-aware and five dynamic-array names, totaling **251 built-ins**.

Financial SDK metadata:

- `NPV`, `IRR`, `XNPV`, `XIRR`, `FVSCHEDULE`, and `MIRR` expose range capability.
- Other current financial functions, including all five F005 names, are scalar-only.
- All current financial descriptors are deterministic/pure, scalar-returning and logical-argument-counted.
- `FVSCHEDULE` and `MIRR` use engine-captured dependencies; security/calendar functions declare no hidden dependency.

## 5. Failure policy

Registration rejects incompatible APIs, unsupported capabilities, invalid bounds, conflicts, duplicate exact versions without replacement and disallowed external state. Evaluation rejects unsupported argument kinds. Family implementations return explicit spreadsheet errors for invalid domains, budgets or non-finite results.

## 6. Shared services and test counts

`FinancialDateMath` is an internal platform-neutral service reused by regular and odd coupon/security built-ins, not a second registry. Formula registry-count regressions read `BuiltInFormulaTestCounts.EagerVersioned`, so each batch updates one authoritative test constant. F005 advances this constant from 223 to 228 and the formula suite from 214 to 219 passing tests.

## 7. Pending

- Plugin manifests, discovery/loading/unloading.
- Publisher signatures and trust policy.
- Third-party isolation and quotas.
- Formula-text version pinning.
- NuGet/plugin packaging and compatibility tooling.
- Third-party array return/spill integration.
- External-state permission prompts and auditing.

## 8. Gates

SDK changes require version ordering, resolution, conflict/replacement/unregister behavior, API/capability/security rejection, range identity, dependency policy, legacy adaptation and shared built-in count regressions followed by the complete hosted matrix.

PR #1 remains Draft while exact-head CI is red or unknown.
