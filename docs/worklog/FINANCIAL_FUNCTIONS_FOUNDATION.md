# Financial Functions Foundation milestone

## Implementation head

- Implementation commit: `e8c349d0b969fa8c9734452573bf7e9bcfa4df28`
- GitHub Actions: CI `#809`, run `32644745950`
- PR #1 remains Draft and unmerged into `develop`.

The milestone is promoted only after the complete implementation run and a newer documentation exact-head run both conclude successfully.

## Implemented function surface

### Time value of money

- `PV`.
- `FV`.
- `PMT`.
- `NPER`.

These functions use one cash-flow sign model, payment timing `0`/`1`, finite rates greater than `-1` and explicit zero-rate branches.

### Ordered cash flows

- `NPV`.
- `IRR`.

`NPV` discounts the first retained flow at period one, preserves logical/range order, uses compensated summation and enforces a two-million-value budget.

`IRR` treats the first retained flow as period zero, requires positive and negative flows, enforces a 100,000-value budget, and uses bounded Newton plus transformed-rate bracket/bisection candidates.

### Payment decomposition

- `IPMT`.
- `PPMT`.

Periods are one-based, payment timing is validated, beginning-of-period payment one has zero interest, and principal plus interest reconciles to `PMT` within floating-point tolerance.

### Depreciation

- `SLN`.
- `SYD`.

Life and period domains are validated before calculation.

## IRR hardening

The original bounded Newton implementation could converge successfully to a root farther from `guess` after crossing attraction basins. The hardened path:

1. computes the bounded Newton candidate;
2. independently samples rate space in `log(1 + rate)` coordinates;
3. selects the sign-change interval nearest the guess in actual rate space;
4. performs bounded bisection;
5. compares converged Newton and bracket candidates by absolute distance to `guess`;
6. returns the nearer result deterministically.

Regression cash flows:

```text
17, 116, -473, 74
```

contain admissible roots near:

```text
-0.8368694674176768
 1.742625940800664
```

With guess `-0.62`, unguarded Newton crosses to the positive root; the hardened solver selects the nearer negative root. With guess `1.5`, it selects the positive root. Twenty repeated evaluations are checked for deterministic output.

## Criteria compatibility fix completed in the batch

The shared conditional-aggregate criterion matcher now tokenizes wildcards and honors tilde escaping:

- `~*` matches literal `*`;
- `~?` matches literal `?`;
- unescaped `*` and `?` retain wildcard behavior.

This fix applies to all `COUNTIF(S)`, `SUMIF(S)` and `AVERAGEIF(S)` criteria.

## SDK metadata

Every financial descriptor uses:

- identity namespace `NERA.BUILTIN`;
- version `1.0.0`;
- minimum API `1.0`;
- logical argument counting;
- deterministic volatility;
- pure security classification;
- scalar return;
- engine-captured dependencies.

`NPV` and `IRR` support scalar/range arguments. The other eight functions are scalar-only.

## Automated regression coverage

- PV/FV/PMT/NPER consistency.
- Zero-rate linear paths.
- Cash-flow sign conventions.
- NPV ordered ranges and mixed logical arguments.
- IRR ordinary convergence and multiple-root nearest-guess selection.
- IRR deterministic repetition, invalid domains and value budgets.
- IPMT/PPMT reconciliation and timing.
- SLN/SYD result and boundary checks.
- Scalar/range coercion and error propagation.
- Dependency graph and affected-only recalculation.
- Versioned descriptor metadata.
- Complete prior formula/SDK/criteria/statistics/dynamic-array regressions.

## Deliberately pending

- `RATE`, `XNPV`, `XIRR`.
- `CUMIPMT`, `CUMPRINC`, `ISPMT`.
- Bond, coupon, price, yield, treasury and day-count functions.
- DB/DDB/VDB and amortization depreciation methods.
- Currency, locale and date-basis compatibility.
- Root discovery beyond the bounded IRR strategy.
- External Excel/LibreOffice financial corpus and fuzzing.

## Next implementation order

1. Engineering and Database Functions Foundation.
2. Database criteria-table evaluation and budgets.
3. Advanced statistics and regression/distributions.
4. Remaining financial functions.
5. Advanced lookup/dynamic arrays and plugin packaging.
6. Drawings/charts, advanced data and release hardening.
