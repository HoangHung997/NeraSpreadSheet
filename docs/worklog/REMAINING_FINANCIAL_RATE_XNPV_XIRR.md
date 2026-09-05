# RATE, XNPV and XIRR milestone

## Scope

This milestone completes the coherent root-solved financial batch without mixing it with coupon calendars, bond conventions or accelerated depreciation.

Implemented:

- `RATE`;
- `XNPV`;
- `XIRR`.

## Why these functions were grouped

All three depend on the same difficult concerns:

- valid rate domain `rate > -1`;
- bounded nonlinear root search;
- root selection near a user-supplied guess;
- numerical scaling at extreme rates;
- deterministic convergence/resource failure;
- cash-flow sign and schedule validation.

Keeping the batch limited to these three allowed one solver policy, one error policy and one focused regression suite.

## Implementation contracts

### RATE

- Defaults: `fv = 0`, `type = 0`, `guess = 0.1`.
- `nper > 0`.
- Payment type truncates toward zero and must be 0 or 1.
- Search happens in `log(1 + rate)` space.
- Zero-rate limits are explicit.
- Positive and negative transformed-rate branches use different scaling to avoid unnecessary overflow.

### XNPV

- Values/dates counts must match.
- Actual day offsets use a 365-day denominator.
- Dates are truncated finite spreadsheet serials.
- No date may precede the first date.
- Compensated summation is used.

### XIRR

- Requires both positive and negative cash flows.
- Uses the same actual-day/365 schedule as `XNPV`.
- Exponential terms are normalized before summation.
- Multiple discovered roots are ordered by distance from `guess`.

## Solver limits

- transformed domain: `[-40, 40]`;
- maximum scan segments: 512;
- maximum bisection iterations: 256;
- maximum schedule pairs: 100,000;
- maximum term evaluations: 20,000,000.

The solver accepts a value already inside tolerance before changing a bracket. Resource exhaustion or non-convergence returns `#NUM!`.

## Regression evidence

The suite locks:

1. `RATE(1,-110,100) = 0.1`;
2. the exact zero-rate payment limit;
3. irregular-day `XNPV` with two captured range dependencies;
4. `XIRR` → `XNPV = 0` round trips;
5. nearest-guess selection for the known two-root schedule `[-100, 230, -132]`;
6. invalid period/type/guess/rate/date/order/shape/sign cases;
7. SDK descriptors and registry count 186.

Local Core restore/build/tests passed on the assembled branch head. Hosted exact-head CI remains the final promotion gate.

## Counts after the milestone

- eager/versioned: 186;
- AST/reference-aware: 18;
- dynamic-array: 5;
- complete built-in subsystem: 209.

## Next batch boundary

The next stable batch is intentionally limited to:

- `CUMIPMT`;
- `CUMPRINC`;
- `DB`;
- `DDB`;
- `VDB`.

Coupon/date helpers, day-count bases and bond price/yield/duration will be implemented only after that batch, because they require a separate shared calendar/convention layer.
