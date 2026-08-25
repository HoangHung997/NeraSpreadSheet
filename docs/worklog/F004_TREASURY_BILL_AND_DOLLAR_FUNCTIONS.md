# F004 — Treasury Bill and Dollar Functions

## Scope

Exactly five new public functions:

1. `TBILLEQ`
2. `TBILLPRICE`
3. `TBILLYIELD`
4. `DOLLARDE`
5. `DOLLARFR`

## Implementation

- Main commit: `2d05b076cbf59912d52400440ecec422d398f625`.
- Exact hardened head: `85a3982b9c23fdbaf524d7e868c04f0701182407`.
- Family: `TreasuryBillAndDollarFormulaFunctions`.
- Registration remains through `StandardFormulaFunctions.CreateAll()`.
- All five descriptors are scalar-only, deterministic/pure and logical-argument-counted.
- Registry count: 218 → 223 eager/versioned names.

## Regression table

| Function | Validation | Result |
|---|---|---|
| TBILLPRICE | Published 98.45 reference and actual-day calendar-year boundary | Pass |
| TBILLYIELD | Published reference and reconciliation from TBILLPRICE | Pass |
| TBILLEQ | Published equivalent-yield reference and denominator domain | Pass |
| DOLLARDE | Published denominator 16/32 references, truncation and signed round trip | Pass |
| DOLLARFR | Published denominator 16/32 references, truncation and signed round trip | Pass |
| Domains | Date order, one-year bound, discount/price, range misuse, denominator and coercion | Pass |
| Maximum date | Year-9999 calendar-year validation cannot overflow `AddYears` | Pass |
| Metadata | Identity/version/API/capability/volatility/security | Pass |
| Registry | 223 eager/versioned names | Pass |
| Formula suite | 214/214 | Pass |
| Hosted matrix | CI #868 | Pass |

## Contract decisions

- Treasury-bill `DSM` is the actual whole-day difference after date-only normalization.
- Maturity may equal settlement plus one calendar year, including a 366-day leap span; any later date returns `#NUM!`.
- TBILLPRICE requires a positive discount and positive resulting price.
- TBILLYIELD requires positive price; a price above 100 may produce a finite negative yield.
- TBILLEQ requires a positive discount and strictly positive denominator.
- DOLLAR denominator is truncated toward zero.
- Negative denominator returns `#NUM!`; a truncated denominator below one returns `#DIV/0!`.
- Decimal-place scale is finite and bounded; signed conversions round trip.

## Next batch

F005: `AMORLINC`, `AMORDEGRC`, `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE`.
