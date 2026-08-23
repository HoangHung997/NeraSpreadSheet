# Engineering + Database Functions Foundation milestone

## Validated implementation head

- Implementation commit: `ba7d0ce079c451f6390f5aafcb0cf861ccad0caa`
- GitHub Actions: CI `#819`, run `32651011596`, success
- PR #1 remains Draft and unmerged into `develop`.

## Implemented source surface

### Engineering

- Comparison helpers: `DELTA`, `GESTEP`.
- Bounded bit operations: `BITAND`, `BITOR`, `BITXOR`, `BITLSHIFT`, `BITRSHIFT`.
- Decimal conversion: `DEC2BIN`, `DEC2OCT`, `DEC2HEX`.
- Base-to-decimal: `BIN2DEC`, `OCT2DEC`, `HEX2DEC`.
- Cross-base: `BIN2OCT`, `BIN2HEX`, `OCT2BIN`, `OCT2HEX`, `HEX2BIN`, `HEX2OCT`.
- Fixed-width signed conversion, places, input/target range and overflow checks.

### Database

- `DSUM`, `DCOUNT`, `DCOUNTA`, `DAVERAGE`.
- `DMAX`, `DMIN`, `DPRODUCT`, `DGET`.
- `DSTDEV`, `DSTDEVP`, `DVAR`, `DVARP`.
- Header/field resolution, AND/OR criteria-table compilation, shared wildcard criteria and bounded scans.
- Stable sum/variance/deviation algorithms.
- Database/field/criteria dependency capture and affected-only recalculation.

### SDK integration

- All 31 functions use namespace `NERA.BUILTIN`, version `1.0.0`, host API `1.0`.
- Engineering descriptors are deterministic/pure scalar-only logical-argument functions.
- Database descriptors retain scalar/range arguments, logical argument count and selective argument-error inspection.
- Built-in registry count is 144; complete built-in subsystem count is 167.

## Automated regressions

Engineering tests cover:

- SDK descriptors;
- DELTA/GESTEP defaults;
- bit truncation and 48-bit limits;
- signed shift direction and overflow;
- positive/negative radix conversions;
- fixed-width sign-bit interpretation;
- places and target-range errors;
- scalar-only capability rejection.

Database tests cover:

- descriptors and logical range identity;
- field by header/index;
- AND within criteria rows and OR across rows;
- wildcard/tilde, blank rows and duplicate criteria headers;
- all aggregate families and DGET cardinality;
- sample/population variance/deviation;
- dependencies and affected recalculation;
- malformed shapes/headers/fields and resource budgets.

## Hosted validation

CI #819 passed Core, architecture, full Windows, desktop GPU, Android, iOS, Mac Catalyst and MAUI Windows build/handler/loaded-smoke gates.

## Deliberately pending

- Complex engineering functions and a unit-conversion catalog.
- Bessel/error/special functions.
- Formula-expression database criteria.
- Locale-specific criteria and radix parsing.
- Database indexing/incremental query plans.
- Cube/external database functions.
- Advanced statistics/distributions and remaining finance.
- External compatibility corpora, target-hardware performance and fuzzing.

## Next implementation order

1. Advanced Statistical Functions Foundation.
2. Covariance/correlation/regression.
3. Distribution and inverse-distribution primitives.
4. Remaining finance.
5. Advanced lookup/dynamic arrays.
6. Plugin packaging/isolation, drawings/charts, advanced data and release hardening.
