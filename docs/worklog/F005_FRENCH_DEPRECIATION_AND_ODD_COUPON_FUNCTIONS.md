# F005 — French Depreciation and Odd Coupon Functions

## Scope

Exactly five new public functions:

1. `AMORLINC`
2. `AMORDEGRC`
3. `ODDFPRICE`
4. `ODDFYIELD`
5. `ODDLPRICE`

## Implementation

- Commit: `bbd4e7c70e7d8426ad79843373cc3aff744d9466`.
- Families: `FrenchDepreciationFormulaFunctions` and `OddCouponFormulaFunctions`.
- Shared date service: `FinancialDateMath` now includes bounded coupon-period ratios and public internal EOM-preserving coupon month shifts.
- Registration remains through `StandardFormulaFunctions.CreateAll()`.
- Registry count: 223 → 228 eager/versioned names.
- All five descriptors are scalar-only, deterministic/pure and logical-argument-counted.

## Regression table

| Function | Validation | Result |
|---|---|---|
| AMORLINC | Published 360 reference, period zero/full/final/exhausted behavior | Pass |
| AMORDEGRC | Published 776 reference, first period 330, useful-life and rounding domains | Pass |
| ODDFPRICE | Published 113.59771747407883 reference, short and long odd-first periods | Pass |
| ODDFYIELD | Published 0.07724554159782439 reference and ODDFPRICE round trips | Pass |
| ODDLPRICE | Published 99.87828601472134 reference | Pass |
| Domains | Date order, basis/frequency, rate/yield/price/redemption, range misuse and coercion | Pass |
| Resources | 100,000 coupon/depreciation periods and 256 yield iterations | Pass |
| Metadata | Identity/version/API/capability/volatility/security | Pass |
| Registry | 228 eager/versioned names | Pass |
| Formula suite | 219/219 | Pass |
| Hosted matrix | CI #872 | Pass |

## AMOR contract decisions

- Dates are normalized to whole dates.
- Period and basis truncate toward zero.
- Supported bases are `0`, `1`, `3`, `4`.
- `AMORLINC` prorates period zero, returns full depreciation for complete periods, then one bounded final residual and zero thereafter.
- `AMORDEGRC` applies useful-life acceleration coefficients, rounds each depreciation amount to whole units away from zero and caps traversal at 100,000 periods.
- Cost/rate must be positive; salvage lies in `[0,cost]`; purchase date may not follow first-period date.

## Odd-coupon contract decisions

- ODDF strict order: `issue < settlement < first_coupon < maturity`.
- ODDL strict order: `last_coupon < settlement < maturity`.
- Frequency is annual, semiannual or quarterly; basis is `0..4`.
- The first-coupon-to-maturity tail must be frequency aligned.
- Quasi-coupon ratios are split over theoretical coupon periods using the shared day-count basis and EOM anchor.
- `ODDFPRICE` discounts a prorated first coupon plus regular coupons/redemption, then subtracts accrued odd-period interest.
- `ODDFYIELD` is the bounded log-domain inverse of the exact ODDFPRICE equation.
- `ODDLPRICE` uses last-to-settlement, last-to-maturity and settlement-to-maturity coupon-period ratios.

## Validation

CI #872 passed:

- zero-warning/zero-error Core and Windows builds;
- 219/219 formula tests and all other Core solution tests;
- architecture verification;
- Windows desktop GPU runtime smoke;
- Android, iOS and Mac Catalyst builds;
- MAUI Windows build, handler resolution and loaded Table-filter/runtime/scale smokes.

## Next batch

F006: `ODDLYIELD`, `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM`.
