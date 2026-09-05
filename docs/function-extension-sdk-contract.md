# Function Extension SDK v1.0 contract

- Eager/versioned registry: 468 names.
- AST/reference-aware: 40 names.
- Dynamic-array unique: 38 names.
- Total built-ins: **546 / 546 locked catalog names**.

F019 raises the eager/versioned registry to 468 names, keeps scoped/reference-aware logic in the AST engine, and raises the dynamic-array-only surface to 38 names. Built-in external-state functions are allowed only through explicit host provider contexts; the default third-party SDK policy remains fail-closed.

Scalar/range capabilities, volatility, logical argument counting and resource bounds remain explicit. `RAND` and `RANDBETWEEN` are marked volatile; reference-introspection functions capture exact source dependencies; dynamic matrix/frequency output observes the one-million-cell array cap.

The F019 manifest records group, implementation file, test file, exact test method, status, edge cases and owning commit for every name.
