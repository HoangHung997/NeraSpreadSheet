# Formula Surface I contract

- Eager/versioned: 468.
- AST/reference-aware: 40.
- Dynamic-array unique: 38.
- Complete subsystem: **546 / 546 locked catalog names**.
- Formula tests: 514.
- F019 cycle: **60 new names**, split A/B/C as 20/20/20 with a green CLI gate after every group.

F019 preserves subsystem boundaries: scalar compatibility and explicit external-provider functions use the versioned registry; `LET`, `LAMBDA`, `ISOMITTED` use scoped AST evaluation; regression/matrix/text-split and higher-order arrays use the spill engine. External/network/AI functions never perform silent network access and fail closed without an explicit provider context.

Manifest and per-function edge cases: `docs/formula-manifests/F019_60_FUNCTION_MANIFEST.md`.
