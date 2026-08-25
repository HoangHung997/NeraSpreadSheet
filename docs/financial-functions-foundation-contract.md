# Financial Functions Foundation contract

Tài liệu này định nghĩa họ financial functions đã được validation trong NeraSpreadSheet. Excel và LibreOffice chỉ là compatibility references; không phải runtime dependencies.

## 1. Architecture boundary

- Tất cả functions đăng ký qua Function Extension SDK API `1.0`.
- Descriptors dùng namespace `NERA.BUILTIN`, implementation version `1.0.0` và logical argument counting.
- `StandardFormulaFunctions.CreateAll()` là built-in aggregation path duy nhất.
- `FinancialDateMath` là shared source cho basis, coupon dates, coupon-period day counts, security year fractions và quasi-coupon ratios.
- Root solvers, schedules và numerical primitives đều deterministic, bounded và fail closed.

## 2. Registered financial functions

**56 financial names** đang được đăng ký:

- annuities/roots: `PV`, `FV`, `PMT`, `NPER`, `RATE`;
- periodic/irregular cash flows: `NPV`, `IRR`, `XNPV`, `XIRR`;
- payments: `IPMT`, `PPMT`, `CUMIPMT`, `CUMPRINC`, `ISPMT`;
- basic depreciation: `SLN`, `SYD`, `DB`, `DDB`, `VDB`;
- scalar rate/growth: `EFFECT`, `NOMINAL`, `RRI`, `PDURATION`;
- calendar/day-count: `YEARFRAC`, `COUPDAYBS`, `COUPDAYS`, `COUPDAYSNC`, `COUPNCD`, `COUPPCD`, `COUPNUM`;
- maturity securities F001: `ACCRINTM`, `DISC`, `INTRATE`, `RECEIVED`, `PRICEDISC`;
- advanced maturity securities F002: `YIELDDISC`, `PRICEMAT`, `YIELDMAT`, `ACCRINT`, `FVSCHEDULE`;
- regular coupon bonds và MIRR F003: `PRICE`, `YIELD`, `DURATION`, `MDURATION`, `MIRR`;
- treasury bills và fractional dollars F004: `TBILLEQ`, `TBILLPRICE`, `TBILLYIELD`, `DOLLARDE`, `DOLLARFR`;
- French depreciation và odd coupons F005: `AMORLINC`, `AMORDEGRC`, `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE`;
- odd-last inverse F006: `ODDLYIELD`.

Eager/versioned registry hiện có **233 names**. Cộng 18 AST/reference-aware và 5 dynamic-array names, subsystem nhận diện **256 built-ins**.

## 3. Coercion và errors

- Required scalars nhận finite number/date values và invariant numeric/date text nếu shared coercion cho phép.
- Financial dates được giảm về date-only values.
- Basis, frequency, period và DOLLAR denominator truncate hướng về zero trước validation.
- Unsupported range use hoặc failed scalar coercion trả `#VALUE!`.
- Invalid date order, basis/frequency, required value domains, zero/non-finite denominators, non-finite results hoặc exhausted budgets trả `#NUM!`.
- DOLLAR denominator truncate dưới một trả `#DIV/0!`; denominator âm trả `#NUM!`.
- Formula errors propagate trước invocation.

## 4. Maturity-security equations

Let `Y(a,b,basis) = YEARFRAC(a,b,basis)`.

### `YIELDDISC`

```text
(redemption - price) / (price × Y(settlement,maturity,basis))
```

### `PRICEMAT` / `YIELDMAT`

Hai functions dùng cùng maturity value, accrued interest và settlement-to-maturity fractions và là algebraic inverses.

### `ACCRINT`

Coupon candidates neo vào `first_interest`, giữ end-of-month behavior và bị cap ở 100,000 coupon periods.

### `FVSCHEDULE`

```text
principal × Π(1 + schedule_rate)
```

Schedule có thể scalar hoặc range và cap ở 2,000,000 values.

## 5. Regular coupon và MIRR contract

`PRICE`, `YIELD`, `DURATION`, `MDURATION` cùng derive frequency, remaining coupon count, days before settlement, days to next coupon và days in coupon period từ maturity-anchored state.

### `PRICE`

Clean price bằng discounted coupon/redemption cash flows trừ accrued coupon interest.

### `YIELD`

Là inverse của đúng `PRICE` equation, giải trong log-periodic-yield domain bằng bounded bisection tối đa 256 iterations.

### `DURATION` / `MDURATION`

`DURATION` là Macaulay weighted average time; `MDURATION = DURATION / (1 + yld/frequency)`.

### `MIRR`

- scalar/range capable và capture dependencies;
- giữ nguyên positional cash-flow timing;
- yêu cầu ít nhất một positive và một negative participating cash flow;
- finance/reinvest rate lớn hơn `-1`;
- cap 2,000,000 positions;
- dùng log-domain aggregation và compensated summation.

## 6. Treasury-bill và DOLLAR contract

Let `DSM` là actual whole-day settlement-to-maturity interval, không quá một calendar year.

```text
TBILLPRICE = 100 × (1 - discount × DSM / 360)
TBILLYIELD = (100 - price) × 360 / (price × DSM)
TBILLEQ    = 365 × discount / (360 - discount × DSM)
```

Discount phải dương. `TBILLYIELD` price phải dương. Finite signed equation results được giữ; zero/non-finite denominators fail closed.

For DOLLAR conversions:

```text
denominator = TRUNC(fraction)
scale       = 10 ^ CEILING(LOG10(denominator))
whole       = TRUNC(value)
part        = value - whole
DOLLARDE    = whole + part × scale / denominator
DOLLARFR    = whole + part × denominator / scale
```

## 7. French depreciation contract

### `AMORLINC`

- prorated first period;
- full depreciation periods;
- một bounded final residual;
- zero sau khi asset đã depreciated hết.

### `AMORDEGRC`

- useful-life coefficient theo reciprocal of rate;
- whole-unit rounding từng period;
- traversal cap 100,000 periods;
- cost/rate dương và salvage trong `[0,cost]`.

Supported AMOR bases: `0`, `1`, `3`, `4`.

## 8. Odd-coupon contract

### Odd-first state

`ODDFPRICE` và `ODDFYIELD` dùng strict order:

```text
issue < settlement < first_coupon < maturity
```

Tail từ first coupon đến maturity phải frequency-aligned. Odd first period được chia thành bounded quasi-coupon ratios.

### Odd-last state

`ODDLPRICE` và `ODDLYIELD` dùng strict order:

```text
last_coupon < settlement < maturity
```

Cả hai derive:

```text
last_coupon_to_settlement_periods
last_coupon_to_maturity_periods
settlement_to_maturity_periods
```

trên theoretical coupon boundary đầu tiên on-or-after maturity.

Let:

```text
coupon      = 100 × rate / frequency
A           = last_coupon_to_settlement_periods
DC          = last_coupon_to_maturity_periods
DSC         = settlement_to_maturity_periods
```

Then:

```text
ODDLPRICE = (redemption + coupon × DC) /
            (1 + (yield/frequency) × DSC) - coupon × A

ODDLYIELD = frequency ×
            ((redemption + coupon × DC) /
             (price + coupon × A) - 1) / DSC
```

`ODDLYIELD` là exact algebraic inverse của validated `ODDLPRICE` equation. Rate phải không âm; price và redemption phải dương; finite negative yield được giữ nếu equation tạo ra.

## 9. SDK metadata

Tất cả 56 financial descriptors deterministic/pure và scalar-returning. `NPV`, `IRR`, `XNPV`, `XIRR`, `FVSCHEDULE`, `MIRR` có range capability; các current security/calendar functions còn lại scalar-only. Financial/date functions không khai báo hidden hoặc volatile dependencies.

## 10. Automated validation

Promotion qua F006 yêu cầu:

1. published/reference values cho financial functions;
2. price/yield inverse và reconciliation tests;
3. date order, basis/frequency, value-domain và coercion tests;
4. resource/convergence caps;
5. descriptor/capability tests và shared registry count 233;
6. complete Core/architecture/Windows/Android/iOS/Mac Catalyst/MAUI Windows matrix.

## 11. Deliberately pending

- business-day/holiday conventions;
- external Excel/LibreOffice financial/date differential corpus và fuzzing;
- locale-aware parsing;
- release-level numerical compatibility sign-off.

PR #1 giữ Draft khi một exact-head CI mới hơn red hoặc unknown.
