# Formula Surface I contract

- Eager/versioned: 342.
- AST/reference-aware: 34.
- Dynamic-array unique: 20.
- Complete subsystem: **396 / at least 538 names**.
- Formula tests: 364.
- Formula-cycle size from F016: **60 new names**, split A/B/C as 20/20/20.

F016 adds 26 complex engineering functions, 14 legacy statistical compatibility names and 20 descriptive/ranking statistical functions through the authoritative registry. Every public name has a separately named regression in its owning A/B/C test class.

Complex parsing and canonical formatting are centralized in `ComplexFormulaMath`; mixed `i`/`j` suffixes and non-finite results fail closed. Legacy statistical names delegate to existing modern numerical implementations without duplicating solvers. Descriptive collectors are capped at 2,000,000 values and preserve logical scalar/range coercion rules.

Manifest and per-function edge cases: `docs/formula-manifests/F016_60_FUNCTION_MANIFEST.md`.
