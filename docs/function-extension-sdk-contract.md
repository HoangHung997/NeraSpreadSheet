# Versioned Function Extension SDK contract

This document defines Function Extension SDK API `1.0` for NeraSpreadSheet. The SDK is owned by Nera; Excel, LibreOffice and third-party libraries are compatibility references only.

## 1. Architecture boundary

- `FormulaFunctionDescriptor` declares one function contract.
- `FormulaFunctionIdentity` gives a stable namespace and formula name.
- `FormulaFunctionVersion` versions implementation independently from the host API.
- `FormulaFunctionApiVersion` versions the invocation contract; current API is `1.0`.
- `VersionedFormulaFunctionRegistry` validates, stores and resolves functions.
- `FormulaFunctionInvocation` preserves logical scalar/range/array identity.
- `FormulaValueCoercion` is the shared conversion surface.
- `NeraFormulaEngine` captures source dependencies and invokes compatible functions.
- Platform hosts, OpenXml and renderers do not implement function semantics.

Legacy `IFormulaFunction` and `IFormulaFunctionRegistry` remain source-compatible through descriptors/adapters.

## 2. Identity and versions

Identity contains a normalized namespace and primary name. Comparison is case-insensitive through uppercase normalization. Names are bounded and validated.

`FormulaFunctionVersion` is a non-negative major/minor/patch value. Side-by-side registration is explicit. Resolution selects the highest version by name/alias, while exact identity/version resolution remains available.

Removing the highest version exposes the next lower version. Aliases must be stable across versions. Formula text does not yet pin a version.

## 3. Host API compatibility

Registration succeeds only when the requested host API major equals the host major and the requested minor is not newer. API `2.0` is rejected by an API `1.0` host before formula evaluation.

## 4. Capability and state declarations

Capabilities:

- scalar arguments;
- range arguments;
- array arguments;
- scalar return;
- array return.

State metadata:

- volatility: deterministic, volatile or external-state;
- security/state: pure, context-read-only or external-state.

The default v1 policy permits scalar/range arguments, scalar returns, deterministic/volatile behavior and at most context-read-only access. It rejects external-state and array-capable extensions until isolated loading and dynamic-array extension integration exist.

## 5. Logical versus flattened arguments

### Logical arguments

Default for new SDK functions. `FUNCTION(A1:A10,5)` contains two arguments: one range retaining source identity/ten values and one scalar.

The eleven Statistical Functions Foundation functions use this policy so range data remains distinct from percentile/rank/index control arguments.

### Flattened values

Historical compatibility policy. Range values are flattened before arity validation. The original 92 eager functions and legacy adapters use this behavior.

The eager built-in registry now contains:

- 92 flattened-value functions;
- 11 logical-argument statistical functions;
- total 103 versioned built-in descriptors.

## 6. Invocation and dependencies

`FormulaFunctionInvocation` exposes immutable `FormulaFunctionArgument` objects recording kind, values, optional source dependency and future array value.

The engine records ordinary source dependencies before invocation. A descriptor may choose:

- `EngineCapturedOnly`;
- `FunctionMayDeclareAdditional`.

Returning undeclared additional dependencies fails the implementation contract rather than silently discarding them.

## 7. Error and coercion policy

Descriptors choose whether argument errors propagate before invocation. Functions that inspect errors disable propagation; ordinary numeric/statistical functions enable it.

`FormulaValueCoercion` provides deterministic helpers for numbers, integers, Boolean, DateTime/OLE serial, text, finite output and explicit errors. Extensions should use this surface rather than inventing incompatible rules.

## 8. Registry conflicts and limits

Conflict policies:

- reject;
- allow side-by-side versions;
- replace exact version.

Primary names and aliases are globally owned by one identity. One identity is limited to eight versions by the default registry. Reads/writes are synchronized; descriptor and argument collections are immutable or detached.

## 9. Built-in and legacy metadata

All 103 eager built-ins use namespace `NERA.BUILTIN`, implementation version `1.0.0` and host API `1.0`.

- The original 92 declare scalar/range arguments, scalar return and flattened counting.
- The eleven statistical functions declare scalar/range arguments, scalar return and logical counting.
- `TODAY`/`NOW` are volatile/context-read-only.
- Statistical functions are deterministic/pure and bounded to two million numeric/date values per invocation.

Legacy functions receive namespace `LEGACY`, version `0.0.0`, flattened counting and context-read-only classification.

## 10. Deliberately pending

- formula-text version pinning;
- plugin/package manifests and assembly discovery;
- signatures, trust stores and publisher policy;
- dependency loading and API binary-compatibility tooling;
- out-of-process isolation/sandboxing;
- array-returning extension integration;
- async/external data functions;
- automatic volatile scheduling;
- localization/regional aliases;
- NuGet/source-link release packaging.

## 11. Required validation

Promotion requires:

1. built-in descriptor counts and legacy compatibility;
2. API/capability/security rejection;
3. alias/conflict/version resolution;
4. logical versus flattened arity;
5. range identity and dependency capture;
6. additional-dependency policy;
7. Statistical Functions descriptors through the same SDK surface;
8. the complete scalar, dynamic-array, workbook, Windows and MAUI matrix.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
