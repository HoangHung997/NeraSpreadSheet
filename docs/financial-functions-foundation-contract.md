# Financial Functions Foundation contract

This document defines the validated financial-function family owned by NeraSpreadSheet. Excel and LibreOffice are compatibility references only; they are not runtime dependencies.

## 1. Architecture boundary

- All functions register through Function Extension SDK API `1.0`.
- Descriptors use namespace `NERA.BUILTIN`, implementation version `1.0.0` and logical argument counting.
- `StandardFormulaFunctions.CreateAll()` is the single built-in aggregation path.
- `FinancialDateMath` is the shared source for basis, coupon dates, coupon-period day counts and security year fractions.
- Root solvers, schedules and numerical primitives are deterministic, bounded and fail closed.

## 2. Registered functions

Forty financial names are registered:

- annuities/roots: `PV`, `FV`, `PMT`, `NPER`, `RATE`;
- periodic/irregular cash flows: `NPV`, `IRR`, `XNPV`, `XIRR`;
- payments: `IPMT`, `PPMT`, `CUMIPMT`, `CUMPRINC`, `ISPMT`;
- depreciation: `SLN`, `SYD`, `DB`, `DDB`, `VDB`;
- scalar rate/growth: `EFFECT`, `NOMINAL`, `RRI`, `PDURATION`;
- calendar/day-count: `YEARFRAC`, `COUPDAYBS`, `COUPDAYS`, `COUPDAYSNC`, `COUPNCD`, `COUPPCD`, `COUPNUM`;
- maturity securities F001: `ACCRINTM`, `DISC`, `INTRATE`, `RECEIVED`, `PRICEDISC`;
- advanced maturity securities F002: `YIELDDISC`, `PRICEMAT`, `YIELDMAT`, `ACCRINT`, `FVSCHEDULE`.

The eager/versioned registry contains **213 names**. Together with 18 AST/reference-aware and five dynamic-array names, the built-in subsystem recognizes **236 names**.

## 3. Coercion and errors

- Required scalars accept finite number/date values and invariant numeric/date text where shared coercion permits it.
- Financial dates are reduced to date-only values.
- Basis/frequency are truncated toward zero before validation.
- Unsupported range use or failed scalar coercion returns `#VALUE!`.
- Invalid date order, basis/frequency, required value domains, denominators, non-finite results or budgets return `#NUM!`.
- Formula errors propagate before invocation.

## 4. F002 equations

Let `Y(a,b,basis) = YEARFRAC(a,b,basis)`.

### `YIELDDISC(settlement,maturity,price,redemption,[basis])`

```text
(redemption - price) / (price × Y(settlement,maturity,basis))
```

Price and redemption must be positive; settlement precedes maturity.

### `PRICEMAT(settlement,maturity,issue,rate,yld,[basis])`

```text
DIM = Y(issue,maturity,basis)
A   = Y(issue,settlement,basis)
DSM = Y(settlement,maturity,basis)
price = 100 × (1 + rate × DIM) / (1 + yld × DSM)
        - 100 × rate × A
```

Rate/yield are nonnegative. The discount denominator must be strictly positive.

### `YIELDMAT(settlement,maturity,issue,rate,price,[basis])`

This is the algebraic inverse of `PRICEMAT`:

```text
maturity_value = 100 × (1 + rate × DIM)
accrued         = 100 × rate × A
yld = (maturity_value / (price + accrued) - 1) / DSM
```

Price must be positive. `PRICEMAT`/`YIELDMAT` round trips are mandatory.

### `ACCRINT(issue,first_interest,settlement,rate,par,frequency,[basis],[calc_method])`

- issue precedes settlement and first-interest date;
- rate/par are positive;
- frequency is 1, 2 or 4;
- basis is 0..4;
- `calc_method` defaults TRUE;
- quasi-coupon candidates are generated from the first-interest anchor, preserving end-of-month behavior;
- each segment contributes `accrued_days / normal_coupon_days`;
- result is `par × rate/frequency × sum(segment_fractions)`;
- search is capped at 100,000 coupon periods.

For `calc_method=FALSE`, accrual starts at first interest only when settlement is after that date; pre-first-interest compatibility examples still accrue from issue.

### `FVSCHEDULE(principal,schedule)`

```text
principal × Π(1 + schedule_rate)
```

- schedule may be scalar or range;
- blank schedule cells are zero rates;
- every nonblank schedule value must be numeric;
- dependencies are engine captured;
- at most 2,000,000 schedule values are accepted.

## 5. SDK metadata

All 40 financial descriptors are deterministic/pure and scalar-returning. `NPV`, `IRR`, `XNPV`, `XIRR`, and `FVSCHEDULE` accept ranges; other current financial functions are scalar-only. Calendar/security functions declare no hidden or volatile dependency.

## 6. Automated validation

F002 promotion requires:

1. official references for `YIELDDISC`, `PRICEMAT`, `YIELDMAT` and three `ACCRINT` cases;
2. `PRICEMAT`/`YIELDMAT` inverse round trip;
3. `FVSCHEDULE` range, blank, text-rejection and dependency tests;
4. date/basis/frequency/value domain tests;
5. scalar/range capability and descriptor checks;
6. shared registry-count constant at 213 eager names;
7. complete Core/architecture/Windows/Android/iOS/Mac Catalyst/MAUI Windows matrix.

## 7. Deliberately pending

- F003 fixed-coupon price/yield/duration and MIRR;
- treasury, AMOR and odd-first/odd-last coupon functions;
- business-day/holiday conventions;
- external Excel/LibreOffice differential corpus and financial fuzzing.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
