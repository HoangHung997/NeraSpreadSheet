# Financial calendar and day-count milestone

## Validated implementation

- Implementation commit: `eeb74ad4ee596f7cb56343b8459f2311538c8243`
- GitHub Actions: CI `#854`, run `32745296544`, success
- Formula tests: `192/192`
- Pull request: `#1`, Draft, unmerged

## Scope

This batch adds one shared platform-neutral financial calendar/day-count layer and seven SDK v1 functions:

- `YEARFRAC`;
- `COUPDAYBS`;
- `COUPDAYS`;
- `COUPDAYSNC`;
- `COUPNCD`;
- `COUPPCD`;
- `COUPNUM`.

## Progress table

| Item | Validation | Status |
|---|---|---|
| Basis 0 | US NASD 30/360 incl. February/end-of-month | Pass |
| Basis 1 | Actual/Actual leap-year and multi-year denominator | Pass |
| Basis 2 | Actual/360 | Pass |
| Basis 3 | Actual/365 and `365/frequency` coupon period | Pass |
| Basis 4 | European 30/360 | Pass |
| Coupon dates | Previous/next dates from maturity anchor | Pass |
| Coupon counts | Annual/semiannual/quarterly remaining count | Pass |
| End-of-month | August-31 ↔ February-29/28 anchor | Pass |
| Exact coupon date | PCD equals settlement, NCD remains later | Pass |
| Domains | settlement/maturity, basis, frequency and scalar validation | Pass |
| Resource boundary | Search capped at 100.000 periods | Pass |
| Registry count | 203 eager/versioned, 226 total | Pass |
| Full hosted CI | Core/Windows/Android/iOS/Mac Catalyst/MAUI Windows | Pass |

## Calendar design

### Maturity anchoring

For coupon index `k`, the date is calculated from:

```text
maturity month - k × (12 / frequency)
```

The date is never calculated by repeatedly subtracting months from the prior coupon. When maturity is the last day of its month, every candidate is the last day of its target month. This avoids a short February permanently changing later August/November coupon days.

### Previous and next coupon

The bounded backward scan keeps the first candidate greater than settlement as NCD. The next candidate at or before settlement becomes PCD. Consequently:

- PCD can equal settlement;
- NCD is strictly later;
- remaining coupon count is the number of backward maturity intervals already crossed.

## Day-count design

- US NASD 30/360 includes day-31 and last-February adjustments.
- European 30/360 caps both day values at 30.
- Actual bases use calendar-day subtraction.
- Actual/Actual selects 365/366 for short spans and average covered-year length for spans longer than one year.
- YEARFRAC is signed; reversing dates reverses the result.

Coupon period outputs:

```text
basis 1: actual PCD→NCD
basis 0/2/4: 360/frequency
basis 3: 365/frequency
```

## CI evidence

### CI #852

The first probe failed at compile time because some `out` parameters were not definitely assigned on all short-circuit paths. The fix initializes the complete coupon argument tuple before parsing.

### CI #853

The implementation then built and all new functional regressions passed. Four inherited tests still expected 196 registry names; actual count was correctly 203.

### CI #854

Only those four count assertions changed to 203. Numerical references and date expectations were untouched. The final run passed 192/192 formula tests plus the complete hosted matrix.

## Explicit limitations

- Regular schedules only; odd first/last coupon periods are pending.
- No weekend/holiday/business-day adjustment.
- No external Excel/LibreOffice differential corpus yet.
- Security price/yield/duration functions are intentionally deferred until this shared layer is independently stable.

## Rollback

Revert commits from `e369b7747e840351a25148f397720b22f851792d` through `eeb74ad4ee596f7cb56343b8459f2311538c8243`, then revert the corresponding documentation lock commit. The earlier empty tree-transition commit has no file in the final tree and must not be restored as a dummy artifact.

## Next handoff

Implement the coherent discount/maturity-security batch:

- `ACCRINTM`;
- `DISC`;
- `INTRATE`;
- `RECEIVED`;
- `PRICEDISC`;
- `YIELDDISC`;
- optionally `PRICEMAT` and `YIELDMAT` after the common equations are green.
