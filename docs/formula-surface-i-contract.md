# Formula Surface I contract

- Eager/versioned: 282.
- AST/reference-aware: 34.
- Dynamic-array unique: 20.
- Complete subsystem: **336 / at least 538 names**.
- Formula tests: 304.
- Public batch size from F015: 20 new names.

F015 adds twenty deterministic built-ins through the authoritative registry. Scalar rounding and conversion functions use shared coercion; `MULTINOMIAL`, `SERIESSUM`, `SUMPRODUCT` and the `SUMX*` family preserve logical argument/range shape. Value traversal is capped at 1,000,000 items, radix text at 255 characters and exact-integer operations at 2^53−1. Unsupported domains fail closed with spreadsheet errors.

`SUMSQ` and `PRODUCT` already existed and were not counted again.

See `docs/advanced-math-compatibility-contract.md`.
