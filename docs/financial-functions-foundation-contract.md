# Financial Functions Foundation contract

This document defines the validated financial-function family owned by NeraSpreadSheet. Excel and LibreOffice are compatibility references only; they are not runtime dependencies.

## 1. Architecture boundary

- All functions register through Function Extension SDK API `1.0`.
- Descriptors use namespace `NERA.BUILTIN`, implementation version `1.0.0` and logical argument counting.
- `StandardFormulaFunctions.CreateAll()` is the single built-in aggregation path.
- `FinancialDateMath` is the shared source for basis, regular coupon dates, coupon-period day counts, security year fractions and bounded quasi-coupon ratios.
- Root solvers, schedules and numerical primitives are deterministic, bounded and fail closed.

## 2. Registered functions

Fifty-five financial names are registered:

- annuities/roots: `PV`, `FV`, `PMT`, `NPER`, `RATE`;
- periodic/irregular cash flows: `NPV`, `IRR`, `XNPV`, `XIRR`;
- payments: `IPMT`, `PPMT`, `CUMIPMT`, `CUMPRINC`, `ISPMT`;
- depreciation: `SLN`, `SYD`, `DB`, `DDB`, `VDB`, `AMORLINC`, `AMORDEGRC`;
- scalar rate/growth: `EFFECT`, `NOMINAL`, `RRI`, `PDURATION`;
- calendar/day-count: `YEARFRAC`, `COUPDAYBS`, `COUPDAYS`, `COUPDAYSNC`, `COUPNCD`, `COUPPCD`, `COUPNUM`;
- maturity securities F001: `ACCRINTM`, `DISC`, `INTRATE`, `RECEIVED`, `PRICEDISC`;
- advanced maturity securities F002: `YIELDDISC`, `PRICEMAT`, `YIELDMAT`, `ACCRINT`, `FVSCHEDULE`;
- regular coupon bonds and MIRR F003: `PRICE`, `YIELD`, `DURATION`, `MDURATION`, `MIRR`;
- treasury bills and fractional dollars F004: `TBILLEQ`, `TBILLPRICE`, `TBILLYIELD`, `DOLLARDE`, `DOLLARFR`;
- odd-coupon securities F005: `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE`.

The eager/versioned registry contains **228 names**. Together with 18 AST/reference-aware and five dynamic-array names, the built-in subsystem recognizes **251 names**.

## 3. Coercion and errors

- Required scalars accept finite number/date values and invariant numeric/date text where shared coercion permits it.
- Financial dates are reduced to date-only values.
- Basis, frequency, AMOR period and DOLLAR denominator are truncated toward zero before validation.
- Unsupported range use or failed scalar coercion returns `#VALUE!`.
- Invalid date order, basis/frequency, required value domains, zero/non-finite denominators, non-finite results or budgets return `#NUM!`.
- A DOLLAR denominator that truncates below one returns `#DIV/0!`; a negative denominator returns `#NUM!`.
- Formula errors propagate before invocation.

## 4. Retained foundations

- `YIELDDISC`, `PRICEMAT` and `YIELDMAT` reuse shared year fractions; price/yield pairs have inverse regressions.
- `ACCRINT` uses a bounded first-interest-anchored quasi-coupon schedule.
- `FVSCHEDULE` accepts scalar/range schedules and caps traversal at 2,000,000 values.
- `PRICE`, `YIELD`, `DURATION` and `MDURATION` share one maturity-anchored regular coupon state.
- `YIELD` uses log-domain bisection capped at 256 iterations.
- `MIRR` preserves cash-flow positions, captures range dependencies and caps input at 2,000,000 positions.
- Treasury-bill and DOLLAR contracts from F004 remain unchanged.

## 5. F005 French depreciation contract

### Common arguments

```text
(cost, date_purchased, first_period, salvage, period, rate, [basis])
```

Common rules:

- `cost > 0`;
- `0 <= salvage <= cost`;
- `date_purchased <= first_period`;
- `period >= 0` after truncation;
- `rate > 0`;
- basis is one of `0`, `1`, `3`, `4`; basis `2` is rejected;
- dates are date-only and results must remain finite.

### `AMORLINC`

Let:

```text
first = cost × rate × YEARFRAC(date_purchased, first_period, basis)
full  = cost × rate
limit = cost - salvage
```

- period 0 returns the nonnegative prorated `first` depreciation;
- following full periods return `full` while depreciation remains below `limit`;
- the next period returns only the final nonnegative residual;
- all later periods return zero.

### `AMORDEGRC`

Let `life = 1/rate`. Supported coefficient regions are:

```text
3 <= life <= 4  → 1.5
5 <= life <= 6  → 2.0
life > 6        → 2.5
```

`life < 3` and `4 < life < 5` return `#NUM!`. First-period and subsequent depreciation amounts are rounded to whole currency units away from zero. Period traversal is capped at 100,000.

## 6. F005 odd-first coupon contract

### Shared state

`ODDFPRICE` and `ODDFYIELD` use:

```text
(settlement, maturity, issue, first_coupon,
 rate, price_or_yield, redemption, frequency, [basis])
```

Rules:

- strict date order: `issue < settlement < first_coupon < maturity`;
- frequency is `1`, `2` or `4` after truncation;
- basis is `0..4` after truncation;
- rate is nonnegative and redemption is positive;
- the regular tail from `first_coupon` to maturity must align exactly with the coupon frequency;
- issue→first-coupon, issue→settlement and settlement→first-coupon ratios are split across theoretical coupon periods;
- every schedule/ratio traversal is capped at 100,000 coupon periods.

### `ODDFPRICE`

Let `coupon = 100 × rate/frequency`. Clean price is:

```text
discounted prorated first coupon
+ discounted regular coupons and redemption
- accrued odd-period coupon interest
```

Discount exponents use the same quasi-coupon ratios and one log-periodic-yield base.

### `ODDFYIELD`

`ODDFYIELD` is the inverse of the exact `ODDFPRICE` equation. It solves in log-periodic-yield space using:

- a finite fixed bracket;
- at most 256 bisection iterations;
- convergence by price residual or bracket width;
- finite exponent/result guards;
- no unbounded Newton retry.

Published-reference and price/yield round-trip regressions are mandatory.

## 7. F005 odd-last coupon contract

### `ODDLPRICE`

```text
(settlement, maturity, last_interest,
 rate, yld, redemption, frequency, [basis])
```

Rules:

- `last_interest < settlement < maturity`;
- rate/yield are nonnegative and redemption is positive;
- frequency is `1`, `2` or `4`; basis is `0..4`;
- the theoretical coupon boundary is the first frequency-aligned date on or after maturity;
- last-interest→settlement, last-interest→maturity and settlement→maturity are measured as bounded coupon-period ratios.

Let `coupon = 100 × rate/frequency`. Price is:

```text
(redemption + coupon × last_to_maturity_periods)
/ (1 + yld/frequency × settlement_to_maturity_periods)
- coupon × last_to_settlement_periods
```

The denominator must be finite and nonzero.

## 8. SDK metadata

All 55 financial descriptors are deterministic/pure and scalar-returning. `NPV`, `IRR`, `XNPV`, `XIRR`, `FVSCHEDULE`, and `MIRR` accept ranges; all F005 functions are scalar-only. Calendar/security functions declare no hidden or volatile dependency.

## 9. Automated validation

F005 promotion requires:

1. published references for `AMORLINC`, `AMORDEGRC`, `ODDFPRICE`, `ODDFYIELD` and `ODDLPRICE`;
2. odd-first price/yield short-period and long-period round trips;
3. AMOR period-zero, full-period, exhausted-period, basis, life and domain tests;
4. odd-coupon date-order, rate/yield/price/redemption, frequency/basis and scalar-capability tests;
5. bounded quasi-coupon traversal and bounded yield convergence;
6. shared registry count at 228 eager names and 219 passing formula tests;
7. complete Core/architecture/Windows/Android/iOS/Mac Catalyst/MAUI Windows matrix.

## 10. Deliberately pending

- F006 `ODDLYIELD` and date/week compatibility functions;
- business-day/holiday conventions;
- external Excel/LibreOffice differential corpus and financial fuzzing.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
