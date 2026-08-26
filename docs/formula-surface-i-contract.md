# Formula Surface I contract

- Eager/versioned: 242.
- AST/reference-aware: 34.
- Dynamic-array unique: 20.
- Complete subsystem: **296 / at least 538 names**.
- Formula tests: 264.

F012 adds flattening, trimming, vertical stacking, vector wrapping, `XMATCH`, lazy scalar error handling and array-aware error replacement. Dependency capture, lazy branch selection and spill ownership remain engine-owned and platform-neutral.

See `docs/flatten-wrap-xmatch-lazy-errors-contract.md`.
