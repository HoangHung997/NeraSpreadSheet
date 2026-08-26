# Function Extension SDK v1.0 contract

- Eager/versioned registry: 282 names.
- AST/reference-aware: 34 names.
- Dynamic-array unique: 20 names.
- Total built-ins: **336 / at least 538**.

F015 adds twenty pure deterministic names through `StandardFormulaFunctions.CreateAll()` and the existing descriptor/version-resolution path. Functions that require logical range shape use `FormulaFunctionArgumentCountPolicy.LogicalArguments`; scalar functions continue through the shared factory. No parallel registry or platform-specific evaluator is introduced.

Function identity, dependency capture, security classification and bounded resource behavior remain engine-owned. `SUMSQ` and `PRODUCT` were pre-existing registry names and were not registered twice.
