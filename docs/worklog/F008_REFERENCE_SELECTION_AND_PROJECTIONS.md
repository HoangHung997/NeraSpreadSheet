# F008 — Reference Selection and Dynamic Projections

## Scope

Exactly five new public functions:

1. `ADDRESS`
2. `AREAS`
3. `CHOOSE`
4. `CHOOSECOLS`
5. `CHOOSEROWS`

## Implementation

- Exact implementation head: `775a24dfa2fa9dc059896d5445179077b4ffe641`.
- Families/paths: `ReferenceSelectionFormulaFunctions`, `ReferenceSelectionFormulaEngine`, `DynamicArrayProjectionFormulaFunctions`.
- Parser additions: `MissingArgumentNode`, `ReferenceUnionNode`.
- Registration remains through `StandardFormulaFunctions.CreateAll()` for eager/versioned names.
- Registry count: 238 → 239 eager/versioned names.
- AST/reference-aware: 18 → 20.
- Dynamic-array unique names: 5 → 7.
- Complete built-ins: 261 → 266.
- Financial functions remain 56.

## Regression table

| Function | Validation | Result |
|---|---|---|
| ADDRESS | A1/R1C1, abs 1–4, missing args, quoted sheet, bounds | Pass |
| AREAS | Cell/range/union and CHOOSE-selected reference | Pass |
| CHOOSE | Truncation, lazy branch, selected range in SUM, spill bridge | Pass |
| CHOOSECOLS | Ordered, duplicate, negative and dynamic indexes | Pass |
| CHOOSEROWS | Ordered, duplicate, negative and range indexes | Pass |
| Dependencies | Selector + selected range only; projection source/index ranges | Pass |
| Domains | Zero/out-of-range, non-reference, range misuse, unsupported union | Pass |
| Metadata | ADDRESS identity/version/API/capability/volatility/security | Pass |
| Registry | 239 eager/versioned names | Pass |
| Formula suite | 234/234 | Pass |
| Hosted matrix | CI #880 | Pass |

## ADDRESS contract decisions

- Required row/column and optional abs_num truncate toward zero.
- Worksheet row/column limits are enforced.
- A1 and R1C1 text modes supported.
- Missing optional arguments are represented explicitly in AST.
- Sheet names are quoted only when required; embedded quotes are escaped.
- No cell dependency is created.

## AREAS contract decisions

- Static cell/range has one area.
- Parenthesized comma-separated references produce a reference union.
- Geometry counting does not evaluate static cell values.
- AREAS over CHOOSE evaluates selector only, then inspects selected reference.
- Non-reference input returns `#VALUE!`.

## CHOOSE contract decisions

- Maximum 254 values.
- Scalar selector truncates toward zero and must lie in 1..N.
- Only selected branch is evaluated.
- Selected range retains exact source identity when passed to range-aware functions.
- Top-level selected range/supported nested array can spill through the dynamic engine.
- Selector-array behavior is deliberately pending.

## CHOOSECOLS/CHOOSEROWS contract decisions

- Source supports ranges, scalars and supported nested arrays.
- Index inputs support scalars, ranges and supported nested arrays.
- Index values are flattened row-major and truncate toward zero.
- Negative values count from the end.
- Zero/out-of-range returns `#VALUE!`.
- Duplicates and requested ordering are preserved.
- Output is capped at 1.000.000 cells.

## Validation

CI #880, run `32831433700`, passed:

- zero-warning/zero-error Core build;
- 234/234 formula tests and complete Core solution tests;
- architecture verification;
- Windows desktop build/tests and GPU runtime smoke;
- Android, iOS and Mac Catalyst builds;
- MAUI Windows build, handler resolution and loaded Table-filter/runtime/scale smokes.

## Next batch

F009: `COLUMN`, `COLUMNS`, `DROP`, `EXPAND`, `FORMULATEXT`.
