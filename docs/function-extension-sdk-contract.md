# Versioned Function Extension SDK contract

This document defines the first validated extension-function SDK for NeraSpreadSheet. The SDK is owned by Nera; Excel, LibreOffice and third-party libraries are compatibility references only.

## 1. Architecture boundary

- `FormulaFunctionDescriptor` declares the contract of one extension function.
- `FormulaFunctionIdentity` gives the function a stable namespace and primary formula name.
- `FormulaFunctionVersion` versions the implementation independently from the host API.
- `FormulaFunctionApiVersion` versions the host/extension invocation contract. The current API is `1.0`.
- `VersionedFormulaFunctionRegistry` validates, stores and resolves extension functions.
- `FormulaFunctionInvocation` preserves logical arguments and their scalar/range/array identity.
- `FormulaValueCoercion` is the public shared conversion surface for built-in and extension functions.
- `NeraFormulaEngine` captures ordinary source dependencies and invokes compatible extensions.
- Platform hosts, OpenXml adapters and renderers do not implement extension-function semantics.

The legacy `IFormulaFunction`, `IFormulaFunctionRegistry` and `BuiltInFormulaFunctionRegistry.Register(IFormulaFunction)` surfaces remain available. They are adapted into SDK descriptors rather than removed.

## 2. Stable identity and versions

A function identity contains:

- a normalized namespace, such as `ACME.ESTIMATING`;
- a normalized formula name, such as `MATERIALCOST`.

Identity comparison is case-insensitive through canonical uppercase normalization. Names are limited to ASCII letters, digits, underscore and dot, and must begin with a letter or underscore. Namespaces additionally allow hyphen.

`FormulaFunctionVersion` is a three-part non-negative semantic value: major, minor and patch. The registry can hold several versions for one identity when registration explicitly uses `AllowSideBySide`.

Resolution rules:

1. formula text resolves by primary name or alias;
2. the highest registered version for that identity is selected;
3. an exact identity/version can be resolved through the registry API;
4. removing the highest version exposes the next lower registered version;
5. aliases must remain identical across all versions of one identity.

A formula cell does not yet pin a function version in its text. Version pinning and package manifests remain future SDK work.

## 3. Host API compatibility

Each descriptor declares its minimum host API. Registration succeeds only when:

- the major API version equals the host major version;
- the requested minor version is not newer than the host minor version.

The current host API is `1.0`. A function requiring `2.0` is rejected during registration, before it can appear in formula evaluation.

## 4. Capability declarations

Capabilities are explicit flags:

- scalar arguments;
- range arguments;
- array arguments;
- scalar return;
- array return.

The default v1 registry policy supports scalar/range arguments and scalar returns. Array capabilities are declared in the type system but are rejected by the default registry until extension-array invocation is integrated with the dynamic-array materialization engine.

Failing unsupported capabilities at registration prevents a plugin from appearing valid and then failing only when a workbook is calculated.

## 5. Logical arguments versus flattened values

The SDK distinguishes two argument-count policies.

### Logical arguments

This is the default for new versioned extensions. `FUNCTION(A1:A10, 5)` has two logical arguments:

1. one range argument carrying its source dependency and ten values;
2. one scalar argument.

Minimum/maximum argument validation uses the logical count.

### Flattened values

This policy preserves the historical built-in and legacy behavior. Range values are flattened before argument-count validation. For example, `DATE(A1:A3)` continues to supply three values, while `ABS(A1:A2)` continues to fail because it supplies two flattened values to a one-value function.

Built-ins and legacy adapters explicitly declare `FlattenedValues`; new SDK functions default to `LogicalArguments`.

## 6. Invocation model

`FormulaFunctionInvocation` exposes an immutable list of `FormulaFunctionArgument` objects.

An argument records:

- kind: scalar, range or array;
- immutable values;
- source dependency for a range;
- immutable `FormulaArrayValue` for a future array-capable invocation.

The engine records source dependencies before invoking the extension. A range-aware extension therefore sees both its values and the exact worksheet/range identity, without reparsing formula text.

`FlattenValues()` is available for functions intentionally using the historical flattened model.

## 7. Volatility and state classification

A descriptor declares volatility:

- `Deterministic`;
- `Volatile`;
- `ExternalState`.

It also declares security/state access:

- `Pure`;
- `ContextReadOnly`;
- `ExternalState`.

The default policy:

- permits deterministic and volatile functions;
- permits up to context-read-only access;
- rejects external-state functions.

Built-in `TODAY` and `NOW` are described as volatile, context-read-only functions. Automatic volatile recalculation scheduling is still pending; metadata is now available for that future scheduler.

This SDK does not load assemblies out of process, sandbox arbitrary code or grant filesystem/network access. External plugin loading, signing and isolation remain release-hardening work.

## 8. Dependency policy

Two dependency policies exist:

- `EngineCapturedOnly`: the extension must not return additional dependencies;
- `FunctionMayDeclareAdditional`: the extension may add dependencies to those already captured from formula arguments.

Returning additional dependencies under `EngineCapturedOnly` throws an implementation-contract error rather than silently discarding them.

Additional dependencies are merged into the evaluation result and enter the normal workbook dependency graph.

## 9. Error and coercion policy

A descriptor chooses whether argument errors propagate before invocation.

When propagation is enabled, the first error in scalar/range values is returned with the matching `FormulaErrorCode`. A function that needs to inspect errors, such as an information function, disables automatic propagation.

`FormulaValueCoercion` is public and provides deterministic helpers for:

- number;
- integer;
- Boolean;
- DateTime/OLE serial;
- text;
- finite-number output;
- explicit errors.

Extensions should use this surface instead of creating incompatible blank, Boolean, date or text conversion rules.

## 10. Registry conflicts and limits

Registration conflict policies:

- `Reject`: no existing version for the identity is allowed;
- `AllowSideBySide`: add a different version for the same identity;
- `ReplaceExactVersion`: replace only the exact identity/version.

Primary names and aliases are globally owned by one identity inside a registry. A second identity cannot claim an existing alias.

The default registry limits one identity to eight versions. Registry reads and writes are synchronized, and descriptor/argument collections exposed to callers are immutable or detached copies.

## 11. Built-in and legacy compatibility

The 92 eager built-in functions are now described through the same SDK metadata:

- namespace `NERA.BUILTIN`;
- implementation version `1.0.0`;
- host API `1.0`;
- scalar/range arguments;
- scalar return;
- flattened-value argument counting.

Legacy functions registered through `Register(IFormulaFunction)` receive:

- namespace `LEGACY`;
- version `0.0.0`;
- scalar/range arguments;
- scalar return;
- flattened-value counting;
- context-read-only classification.

Existing source code can continue registering and resolving legacy functions.

## 12. Deliberately pending

- formula-text version pinning;
- package/plugin manifests;
- assembly discovery and dependency loading;
- signatures, trust stores and publisher policy;
- out-of-process isolation or sandboxing;
- array-returning plugin integration with spill ownership;
- async/external data functions;
- automatic volatile recalculation scheduling;
- localization and regional aliases;
- API binary-compatibility tooling and NuGet release packaging.

## 13. Required validation

Promotion requires:

1. built-in metadata and legacy registration tests;
2. API/capability/security rejection tests;
3. alias and conflict tests;
4. exact and highest-version resolution tests;
5. range-identity and dependency tests;
6. additional dependency policy tests;
7. flattened versus logical argument-count compatibility tests;
8. existing scalar, dynamic-array, workbook, Windows and MAUI regression matrix.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
