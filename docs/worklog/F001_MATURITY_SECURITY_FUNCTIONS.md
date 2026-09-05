# F001 — Maturity Security Functions

## Scope

Exactly five new public functions:

1. `ACCRINTM`
2. `DISC`
3. `INTRATE`
4. `RECEIVED`
5. `PRICEDISC`

## Implementation

- Commit: `3ea2ae2d576e40b72e91c02ab493f1e244ffe0bd`.
- One `MaturitySecurityFormulaFunctions` family registered through `StandardFormulaFunctions.CreateAll()`.
- Shared `FinancialDateMath` basis/date primitive; no copied day-count engine.
- SDK v1 identity/version/API/capability/volatility/security descriptors.
- Scalar-only logical arguments and fail-closed spreadsheet errors.

## Regression table

| Function | Reference/relationship | Result |
|---|---|---|
| ACCRINTM | 2008-04-01 → 2008-06-15, rate .1, par 1000, basis 3 | Pass |
| ACCRINTM | omitted par defaults to 1000 | Pass |
| INTRATE | 1,000,000 → 1,014,420 over 90 actual/360 days | Pass |
| RECEIVED | 1,000,000 at discount .0575 over 90 actual/360 days | Pass |
| PRICEDISC | 14-day actual/360 reference at 5.25% | Pass |
| DISC | recovers discount from PRICEDISC output | Pass |
| Domains | date order, basis, positive values and denominator | Pass |
| Capabilities | range/text rejection and scalar metadata | Pass |
| Registry | 208 eager/versioned names | Pass |
| Formula suite | 198/198 | Pass |

## CI note

CI #859 proved Core, architecture and the full formula suite. Its Apple runner failed during checkout because DNS could not resolve `github.com`; this was before source compilation. The milestone requires a new exact-head run with all hosted jobs green before its public completion report.

## Next batch

F002: `YIELDDISC`, `PRICEMAT`, `YIELDMAT`, `ACCRINT`, `FVSCHEDULE`.
