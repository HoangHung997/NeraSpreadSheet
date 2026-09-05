# Formula Surface I contract

- Eager/versioned: 468.
- AST/reference-aware: 40.
- Dynamic-array unique: 38.
- Complete subsystem: **546 / 546 locked catalog names**.
- Formula tests: 524.
- F019 cycle: **60 new names**, split A/B/C as 20/20/20 with a green CLI gate after every group.

F019 preserves subsystem boundaries: scalar compatibility and explicit external-provider functions use the versioned registry; `LET`, `LAMBDA`, `ISOMITTED` use scoped AST evaluation; regression/matrix/text-split and higher-order arrays use the spill engine. External/network/AI functions never perform silent network access and fail closed without an explicit provider context.

Compatibility hardening keeps `VLOOKUP`/`HLOOKUP` error propagation scoped to
the lookup path and selected result. Whole-column `VLOOKUP` table arrays use a
sparse used-row context while the dependency graph retains the full column
range; this supports formulas such as `DL!$B:$R` without allocating the
1,048,576-row axis.

Manifest and per-function edge cases: `docs/formula-manifests/F019_60_FUNCTION_MANIFEST.md`.
