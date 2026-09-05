# F014 — Hyperbolic, combinatorics and integer functions

## Delivered

`ATANH`, `SINH`, `COSH`, `TANH`, `COMBIN`, `COMBINA`, `FACT`, `FACTDOUBLE`, `GCD`, `LCM`.

## Validation

- Implementation head: `fe22926f161c616229e1cc302c4f83d1aafdcf8b`.
- Implementation exact-head CI: #905.
- Build/analyzers: 0 warnings, 0 errors.
- Formula tests: 284/284.
- Architecture verification and Windows/Android/iOS/Mac Catalyst/MAUI host gates passed.

## Locked boundaries

- `ATANH` uses the open unit interval.
- Non-finite hyperbolic outputs fail with `#NUM!`.
- Combination traversal is bounded at 1,000,000 iterations.
- `FACT` is bounded at 170 and `FACTDOUBLE` at 300.
- `GCD`/`LCM` validate every argument and use the exact-integer boundary below `2^53`.
- All ten names use the authoritative eager/versioned registration path.

## Snapshot

- Eager/versioned: 262.
- AST/reference-aware: 34.
- Dynamic-array unique: 20.
- Total functions: **316 / at least 538**.
- Formula tests: 284/284.

## Next

F015: `MROUND`, `CEILING`, `FLOOR`, `CEILING.PRECISE`, `FLOOR.PRECISE`, `ISO.CEILING`, `MULTINOMIAL`, `SERIESSUM`, `SUMSQ`, `SUMPRODUCT`.
