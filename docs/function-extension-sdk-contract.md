# Function Extension SDK v1.0 contract

- Eager/versioned registry: 372 names.
- AST/reference-aware: 34 names.
- Dynamic-array unique: 20 names.
- Total built-ins: **426 / at least 538**.

F017 registers 30 deterministic names through `StandardFormulaFunctions.CreateAll()` and the existing descriptor/version-resolution path. No parallel registry or platform-specific evaluator is introduced.

Scalar and range capabilities are declared explicitly; logical argument counting preserves source shape for statistical ranges. Legacy aliases delegate to existing versioned targets, while new discrete and hypothesis functions use bounded engine-owned numerics.

The F017 manifest records group, implementation file, test file, exact test method, status, edge cases and owning commit for every name.
