# Current Work Handoff

- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` into `develop` — Draft, unmerged
- F003 exact implementation head: `48012398a3a020bfb12829bee46cfa88bc1c7fed`
- F003 hosted CI: #866 — success
- F004 exact compatibility-correction head: `b836976733acbfc50696aa096d53547bcad856c7`
- F004 hosted CI: #870 — success
- Formula tests: `214/214`
- Eager/versioned built-ins: `223`
- Complete built-ins: `246`
- Financial functions: `50`
- Source of truth: `docs/current-status.md`
- Master schedule: `docs/formula-completion-master-schedule.md`

## F003 — regular coupon bond and MIRR

| Function | Result | Status |
|---|---|---|
| `PRICE` | Shared regular-coupon clean-price equation | Complete |
| `YIELD` | Bounded inverse of PRICE | Complete |
| `DURATION` | Macaulay present-value duration | Complete |
| `MDURATION` | Modified-duration reconciliation | Complete |
| `MIRR` | Position-preserving range/scalar modified IRR | Complete |

Key gates:

- PRICE/YIELD published reference and nested round trips.
- DURATION/MDURATION published references and reconciliation.
- MIRR range dependency, blanks, signs, rates and 2,000,000-value cap.
- CI #866 exact head passed the full hosted matrix.

## F004 — treasury bills and fractional dollars

| Function | Result | Status |
|---|---|---|
| `TBILLEQ` | Bond-equivalent treasury-bill yield | Complete |
| `TBILLPRICE` | Price per 100 face value | Complete |
| `TBILLYIELD` | Treasury-bill yield from price | Complete |
| `DOLLARDE` | Fractional-dollar to decimal-dollar conversion | Complete |
| `DOLLARFR` | Decimal-dollar to fractional-dollar conversion | Complete |

Key contracts:

- Actual whole-day `DSM` with a one-calendar-year upper boundary.
- Overflow-safe maximum-date handling, covered by a year-9999 regression.
- Extreme positive discounts preserve finite signed `TBILLPRICE` and `TBILLEQ` results; only zero/non-finite equivalent-yield denominators fail closed.
- `TBILLYIELD` still requires a positive price and may return a finite negative yield for a price above 100.
- DOLLAR denominator truncation and distinct `#NUM!`/`#DIV/0!` domains.
- Signed DOLLAR round trips and published 16/32 denominator references.
- CI #870 exact compatibility-correction head passed Core, architecture, Windows/GPU, Android, iOS, Mac Catalyst and MAUI Windows loaded smokes.

## Documentation/handoff gate

This handoff update synchronizes current status, feature matrix, financial contract and F004 worklogs with the compatibility correction. Public F004 completion requires the exact documentation-head hosted CI to be green.

## Next five — F005

1. `AMORLINC`.
2. `AMORDEGRC`.
3. `ODDFPRICE`.
4. `ODDFYIELD`.
5. `ODDLPRICE`.

F005 must introduce a bounded AMOR depreciation state and explicit odd-first-coupon schedule/quasi-coupon contracts before the public names are promoted. PR remains Draft; do not merge while a newer exact-head run is red or unknown.
