# Financial Functions Foundation contract

This document defines the validated financial-function family owned by NeraSpreadSheet. Excel and LibreOffice are compatibility references only; they are not runtime dependencies.

## 1. Architecture boundary

- All functions register through Function Extension SDK API `1.0`.
- Descriptors use namespace `NERA.BUILTIN`, implementation version `1.0.0` and logical argument counting.
- `StandardFormulaFunctions.CreateAll()` is the single built-in aggregation path.
- `FinancialDateMath` is the shared source for basis, coupon dates, coupon-period day counts and security year fractions.
- Root solvers, schedules and numerical primitives are deterministic, bounded and fail closed.

## 2. Registered functions

Fifty financial names are registered:

- annuities/roots: `PV`, `FV`, `PMT`, `NPER`, `RATE`;
- periodic/irregular cash flows: `NPV`, `IRR`, `XNPV`, `XIRR`;
- payments: `IPMT`, `PPMT`, `CUMIPMT`, `CUMPRINC`, `ISPMT`;
- depreciation: `SLN`, `SYD`, `DB`, `DDB`, `VDB`;
- scalar rate/growth: `EFFECT`, `NOMINAL`, `RRI`, `PDURATION`;
- calendar/day-count: `YEARFRAC`, `COUPDAYBS`, `COUPDAYS`, `COUPDAYSNC`, `COUPNCD`, `COUPPCD`, `COUPNUM`;
- maturity securities F001: `ACCRINTM`, `DISC`, `INTRATE`, `RECEIVED`, `PRICEDISC`;
- advanced maturity securities F002: `YIELDDISC`, `PRICEMAT`, `YIELDMAT`, `ACCRINT`, `FVSCHEDULE`;
- regular coupon bonds and MIRR F003: `PRICE`, `YIELD`, `DURATION`, `MDURATION`, `MIRR`;
- treasury bills and fractional dollars F004: `TBILLEQ`, `TBILLPRICE`, `TBILLYIELD`, `DOLLARDE`, `DOLLARFR`.

The eager/versioned registry contains **223 names**. Together with 18 AST/reference-aware and five dynamic-array names, the built-in subsystem recognizes **246 names**.

## 3. Coercion and errors

- Required scalars accept finite number/date values and invariant numeric/date text where shared coercion permits it.
- Financial dates are reduced to date-only values.
- Basis/frequency and DOLLAR denominator are truncated toward zero before validation.
- Unsupported range use or failed scalar coercion returns `#VALUE!`.
- Invalid date order, basis/frequency, required value domains, denominators, non-finite results or budgets return `#NUM!`.
- A DOLLAR denominator that truncates below one returns `#DIV/0!`; a negative denominator returns `#NUM!`.
- Formula errors propagate before invocation.

## 4. F002 equations retained

Let `Y(a,b,basis) = YEARFRAC(a,b,basis)`.

### `YIELDDISC(settlement,maturity,price,redemption,[basis])`

```text
(redemption - price) / (price × Y(settlement,maturity,basis))
```

### `PRICEMAT` / `YIELDMAT`

`PRICEMAT` and `YIELDMAT` share maturity value, accrued interest and settlement-to-maturity fractions and are algebraic inverses.

### `ACCRINT`

Coupon candidates are anchored to `first_interest`, preserve end-of-month behavior and are capped at 100,000 coupon periods.

### `FVSCHEDULE`

```text
principal × Π(1 + schedule_rate)
```

The schedule may be scalar or range and is capped at 2,000,000 values.

## 5. F003 regular coupon and MIRR contract

### Shared coupon state

`PRICE`, `YIELD`, `DURATION` and `MDURATION` all derive:

```text
frequency
remaining_coupon_count
days_before_settlement
days_to_next_coupon
days_in_coupon_period
```

from the same maturity-anchored coupon period and basis `0..4`.

### `PRICE(settlement,maturity,rate,yld,redemption,frequency,[basis])`

For each remaining coupon cash flow, discount by its fractional coupon-period exponent. The clean price is:

```text
discounted coupon/redemption cash flows - accrued coupon interest
```

Rate and yield must be nonnegative; redemption must be positive.

### `YIELD(settlement,maturity,rate,price,redemption,frequency,[basis])`

`YIELD` is the inverse of the exact `PRICE` equation. It solves in a log-transformed periodic-yield domain using a bounded bisection:

- at most 256 iterations;
- finite bracket and exponent checks;
- convergence by price residual or bracket width;
- no unbounded retry or Newton divergence.

`PRICE`/`YIELD` round trips are mandatory.

### `DURATION` / `MDURATION`

`DURATION` is the Macaulay weighted average time of discounted coupon/redemption cash flows. `MDURATION` reconciles as:

```text
MDURATION = DURATION / (1 + yld/frequency)
```

### `MIRR(values,finance_rate,reinvest_rate)`

- accepts scalar/range input and captures range dependencies;
- preserves the original position of every cash-flow value;
- requires at least two positions, one positive and one negative participating cash flow;
- finance and reinvest rates must be greater than `-1`;
- at most 2,000,000 values;
- uses log-domain aggregation and compensated summation to reduce overflow/cancellation risk.

## 6. F004 treasury-bill and DOLLAR contract

Let `DSM` be the actual number of days from settlement to maturity. Settlement must precede maturity and maturity must not be later than one calendar year after settlement.

### `TBILLPRICE(settlement,maturity,discount)`

```text
100 × (1 - discount × DSM / 360)
```

Discount and resulting price must be positive.

### `TBILLYIELD(settlement,maturity,price)`

```text
(100 - price) × 360 / (price × DSM)
```

Price must be positive. A price above 100 may therefore produce a finite negative yield.

### `TBILLEQ(settlement,maturity,discount)`

```text
365 × discount / (360 - discount × DSM)
```

Discount and denominator must be positive.

The calendar-year upper-bound check is overflow-safe even for valid dates in year 9999. Date inputs are normalized to whole dates before `DSM` is calculated.

### `DOLLARDE(fractional_dollar,fraction)`

### `DOLLARFR(decimal_dollar,fraction)`

Let:

```text
denominator = TRUNC(fraction)
scale       = 10 ^ CEILING(LOG10(denominator))
whole       = TRUNC(value)
part        = value - whole
```

Then:

```text
DOLLARDE = whole + part × scale / denominator
DOLLARFR = whole + part × denominator / scale
```

The same truncation-toward-zero rule supports signed round trips. Scale creation is finite and bounded.

## 7. SDK metadata

All 50 financial descriptors are deterministic/pure and scalar-returning. `NPV`, `IRR`, `XNPV`, `XIRR`, `FVSCHEDULE`, and `MIRR` accept ranges; other current financial functions are scalar-only. Calendar/security functions declare no hidden or volatile dependency.

## 8. Automated validation

F003/F004 promotion requires:

1. published references for PRICE/YIELD/DURATION/MDURATION/MIRR and all three treasury-bill functions;
2. `PRICE`/`YIELD`, duration/modified-duration, treasury-price/yield and DOLLAR round-trip/reconciliation tests;
3. MIRR range, position, sign, rate, dependency and resource-domain tests;
4. treasury date/order/calendar-year/discount/price and maximum-date boundary tests;
5. DOLLAR truncation, negative, zero, signed and nonnumeric tests;
6. descriptor/capability tests and shared registry count at 223 eager names;
7. complete Core/architecture/Windows/Android/iOS/Mac Catalyst/MAUI Windows matrix.

## 9. Deliberately pending

- F005 AMOR depreciation and odd-first/odd-last coupon functions;
- business-day/holiday conventions;
- external Excel/LibreOffice differential corpus and financial fuzzing.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
