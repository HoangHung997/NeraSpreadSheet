# F006 — Odd-Last Yield and Date Compatibility Functions

## Scope

Exactly five new public functions:

1. `ODDLYIELD`
2. `DATEDIF`
3. `DAYS360`
4. `ISOWEEKNUM`
5. `WEEKNUM`

## Implementation

- Commit: `c43bf362054110940f149a144546c4bba13387e3`.
- Families: `OddLastYieldFormulaFunctions` và `DateCompatibilityFormulaFunctions`.
- Shared financial date service: `FinancialDateMath` tiếp tục cung cấp bounded quasi-coupon ratios và EOM-preserving month shifts.
- Registration vẫn qua `StandardFormulaFunctions.CreateAll()`.
- Registry count: 228 → 233 eager/versioned names.
- Complete built-ins: 251 → 256.
- Financial functions: 55 → 56.
- Tất cả năm descriptors scalar-only, deterministic/pure và logical-argument-counted.

## Regression table

| Function | Validation | Result |
|---|---|---|
| ODDLYIELD | Microsoft example 4.52% và ODDLPRICE round trip | Pass |
| DATEDIF | Y/M/D/MD/YM/YD references, case-insensitive units và MD legacy negative edge | Pass |
| DAYS360 | US NASD, European method và reversed signed interval | Pass |
| ISOWEEKNUM | ISO week-year boundary và regular reference | Pass |
| WEEKNUM | Return types 1, 2, 11–17, 21 và invalid type | Pass |
| Domains | Date order, rate/price/redemption, unit, range misuse và coercion | Pass |
| Metadata | Identity/version/API/capability/volatility/security | Pass |
| Registry | 233 eager/versioned names | Pass |
| Formula suite | 224/224 | Pass |
| Hosted matrix | CI #874 | Pass |

## ODDLYIELD contract decisions

- Strict date order: `last_coupon < settlement < maturity`.
- Frequency truncate rồi phải là `1`, `2`, `4`; basis truncate rồi phải trong `0..4`.
- Dùng cùng theoretical coupon boundary on-or-after maturity và ba coupon-period ratios như `ODDLPRICE`.
- Rate không âm; price và redemption dương.
- `ODDLYIELD` là exact algebraic inverse của `ODDLPRICE`, không cần unbounded root solver.
- Finite negative yield được giữ nếu equation tạo ra; non-finite result fail closed.

## DATEDIF contract decisions

- Dates normalize về whole dates.
- Unit text trim và normalize uppercase.
- Supported units: `Y`, `M`, `D`, `MD`, `YM`, `YD`.
- Start date lớn hơn end date trả `#NUM!`.
- `Y`/`M` là completed units; `D` là actual whole days.
- `MD` giữ Lotus/Excel legacy residual-day behavior và có thể trả âm ở month-end edge cases.
- `YM` bỏ years; `YD` bỏ years và dùng clamped anniversary cho leap/month-end dates.

## DAYS360 contract decisions

- Optional method false/omitted dùng US NASD 30/360.
- Method true dùng European 30/360.
- US rule xử lý last-day-of-month start/end theo published NASD behavior.
- European rule cap day 31 về 30.
- Reversed intervals đổi dấu thay vì bị từ chối.

## Week-number contract decisions

- `ISOWEEKNUM` dùng ISO 8601 week-year.
- `WEEKNUM` hỗ trợ system-one return types `1`, `2`, `11..17`.
- `WEEKNUM(...,21)` dùng ISO system two.
- Invalid return type trả `#NUM!`.

## Validation

CI #874, run `32818957096`, passed:

- zero-warning/zero-error Core và Windows builds;
- 224/224 formula tests và toàn bộ Core solution tests;
- architecture verification;
- Windows desktop GPU runtime smoke;
- Android, iOS và Mac Catalyst builds;
- MAUI Windows build, handler resolution và loaded Table-filter/runtime/scale smokes.

## Next batch

F007: `NETWORKDAYS`, `NETWORKDAYS.INTL`, `WORKDAY`, `WORKDAY.INTL`, `NUMBERVALUE`.
