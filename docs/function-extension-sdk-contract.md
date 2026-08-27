# Function Extension SDK v1.0 contract

- Eager/versioned registry: 427 names.
- AST/reference-aware: 37 names.
- Dynamic-array unique: 22 names.
- Total built-ins: **486 / at least 538**.

F018 adds 55 eager/versioned functions through `StandardFormulaFunctions.CreateAll()`, three reference-aware functions through the existing AST engine, and two dynamic-array functions through the existing spill engine. No platform-specific formula registry is introduced.

Scalar/range capabilities, volatility, logical argument counting and resource bounds remain explicit. `RAND` and `RANDBETWEEN` are marked volatile; reference-introspection functions capture exact source dependencies; dynamic matrix/frequency output observes the one-million-cell array cap.

The F018 manifest records group, implementation file, test file, exact test method, status, edge cases and owning commit for every name.
