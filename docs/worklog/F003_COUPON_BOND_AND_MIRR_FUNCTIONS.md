# F003 — Coupon Bond and MIRR Functions

## Scope

Exactly five new public functions:

1. `PRICE`
2. `YIELD`
3. `DURATION`
4. `MDURATION`
5. `MIRR`

## Implementation

- Main commit: `aa276e0a560029a3a7af22d948a49f1cad7ec085`.
- Exact correction head: `48012398a3a020bfb12829bee46cfa88bc1c7fed`.
- Family: `CouponBondFormulaFunctions`.
- Registration remains through `StandardFormulaFunctions.CreateAll()`.
- `PRICE`, `YIELD`, `DURATION` and `MDURATION` share one maturity-anchored coupon state.
- `MIRR` is the only range-capable function in this batch.
- Registry count: 213 → 218 eager/versioned names.

## Regression table

| Function | Validation | Result |
|---|---|---|
| PRICE | Published fixed-coupon clean-price reference | Pass |
| YIELD | Published yield reference, PRICE inverse and nested round trip | Pass |
| DURATION | Published Macaulay duration reference | Pass |
| MDURATION | Published reference and exact reconciliation to DURATION | Pass |
| MIRR | Published cash-flow reference, blank-position preservation and dependency capture | Pass |
| Domains | Date order, rate/price/redemption, frequency, basis, sign and range/scalar capabilities | Pass |
| Metadata | Identity/version/API/capability/volatility/security | Pass |
| Registry | 218 eager/versioned names | Pass |
| Formula suite | 209/209 | Pass |
| Hosted matrix | CI #866, run `32806306949` | Pass |

## Resource and numerical decisions

- Yield solver: at most 256 bisection iterations in a bounded log-periodic-yield domain.
- Coupon discounting: finite exponent checks plus compensated summation.
- MIRR input: maximum 2,000,000 positions.
- MIRR aggregates positive future value and negative present value in the log domain.
- Missing positive or negative cash-flow sign returns `#DIV/0!`.
- Invalid financial domains or non-finite results fail closed.

## Next batch

F004: `TBILLEQ`, `TBILLPRICE`, `TBILLYIELD`, `DOLLARDE`, `DOLLARFR`.
