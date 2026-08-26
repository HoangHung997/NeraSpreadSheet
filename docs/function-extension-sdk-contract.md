# Function Extension SDK v1.0 contract

- Eager/versioned registry: 342 names.
- AST/reference-aware: 34 names.
- Dynamic-array unique: 20 names.
- Total built-ins: **396 / at least 538**.

F016 registers 60 deterministic names through `StandardFormulaFunctions.CreateAll()` and the existing descriptor/version-resolution path. No parallel registry or platform-specific evaluator is introduced.

Complex and descriptive functions declare scalar/range capabilities explicitly and use logical argument counting where source shape must be preserved. Legacy statistical aliases adapt arguments and invoke the existing versioned targets, so solver, domain and dependency behavior remains centralized.

Function identity, dependency capture, security classification and bounded-resource behavior remain engine-owned. The F016 manifest records implementation file, test method, status, edge cases and owning A/B/C commit for every name.
