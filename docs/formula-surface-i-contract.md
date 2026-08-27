# Formula Surface I contract

- Eager/versioned: 427.
- AST/reference-aware: 37.
- Dynamic-array unique: 22.
- Complete subsystem: **486 / at least 538 names**.
- Formula tests: 454.
- F018 cycle: **60 new names**, split A/B/C as 20/20/20 with a green CLI gate after every group.

F018 preserves subsystem boundaries: text/statistical/engineering scalar-range functions use the versioned registry; `CELL`, `ISFORMULA` and `ISREF` use reference-aware AST evaluation; `MUNIT` and `FREQUENCY` use the spill engine. Volatile random functions declare volatility, matrix/spill results are bounded, and regex uses a finite timeout.

Manifest and per-function edge cases: `docs/formula-manifests/F018_60_FUNCTION_MANIFEST.md`.
