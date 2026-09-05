# F002 — Advanced Maturity Security Functions

## Scope

Exactly five new public functions:

1. `YIELDDISC`
2. `PRICEMAT`
3. `YIELDMAT`
4. `ACCRINT`
5. `FVSCHEDULE`

## Implementation

- Commit: `70051299a1531016ce82df981a49753f09d1d8a6`.
- Family: `AdvancedMaturitySecurityFormulaFunctions`.
- Registration remains through `StandardFormulaFunctions.CreateAll()`.
- Shared `FinancialDateMath` basis/calendar service.
- Registry count regression centralized in `BuiltInFormulaTestCounts`.

## Regression table

| Function | Validation | Result |
|---|---|---|
| YIELDDISC | Published discounted-yield reference and PRICEDISC relation | Pass |
| PRICEMAT | Published maturity-interest price reference | Pass |
| YIELDMAT | Published yield reference and PRICEMAT inverse | Pass |
| ACCRINT | Three published basis-0 examples plus calc_method after first interest | Pass |
| FVSCHEDULE | Range, blank, dependency and nonnumeric rejection | Pass |
| Domains | Date order, basis, frequency, rate/price and scalar capabilities | Pass |
| Metadata | Identity/version/API/capability/volatility/security | Pass |
| Registry | 213 eager/versioned names | Pass |
| Formula suite | 204/204 | Pass |

## Resource decisions

- ACCRINT coupon traversal: maximum 100,000 periods.
- FVSCHEDULE schedule values: maximum 2,000,000.
- Non-finite multiplication or invalid financial denominator fails closed.

## Next batch

F003: `PRICE`, `YIELD`, `DURATION`, `MDURATION`, `MIRR`.
