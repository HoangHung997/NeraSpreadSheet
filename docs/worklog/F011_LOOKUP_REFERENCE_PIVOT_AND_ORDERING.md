# F011 — Lookup, reference, pivot and ordering

## Scope

1. `LOOKUP`
2. `OFFSET`
3. `PERCENTOF`
4. `PIVOTBY`
5. `ROW`
6. `ROWS`
7. `SHEET`
8. `SHEETS`
9. `SORTBY`
10. `TAKE`

## Post-green counters

- Eager/versioned: 242.
- AST/reference-aware: 30.
- Dynamic-array unique: 14.
- Total built-ins: 286.
- Formula tests: 254.
- Formula target: at least 538.

## Gates

- Exactly ten new public names and ten regression methods.
- Range/current-cell/workbook metadata dependencies.
- PERCENTOF zero denominator.
- PIVOTBY grouping, totals, filtering and relative percentage.
- SORTBY stable multi-key ordering.
- TAKE omitted/positive/negative/zero behavior.
- Core build, complete tests, architecture verification and hosted exact-head CI #899 green.

## Next ten

`TOCOL`, `TOROW`, `TRIMRANGE`, `VSTACK`, `WRAPCOLS`, `WRAPROWS`, `XMATCH`, `IFERROR`, `IFNA`, `SWITCH`.
