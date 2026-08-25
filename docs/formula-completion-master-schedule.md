# NeraSpreadSheet Master Formula Completion Schedule

Each public batch now contains exactly **10 new function names** and completes only after implementation, regression, documentation and exact-head hosted CI are green.

| Counter | Value |
|---|---:|
| Eager/versioned | 242 |
| AST/reference-aware | 30 |
| Dynamic-array unique | 14 |
| **Total functions** | **286 / at least 538** |
| Formula tests | 254 |
| Complete | F001–F011 |
| Next | F012 |

F010: `GETPIVOTDATA`, `GROUPBY`, `HSTACK`, `HYPERLINK`, `INDIRECT`.

F011: `LOOKUP`, `OFFSET`, `PERCENTOF`, `PIVOTBY`, `ROW`, `ROWS`, `SHEET`, `SHEETS`, `SORTBY`, `TAKE`.

F012 next:

```text
TOCOL TOROW TRIMRANGE VSTACK WRAPCOLS WRAPROWS XMATCH IFERROR IFNA SWITCH
```

Remaining work proceeds through higher-order/LET/LAMBDA, text/regex, math/matrix, statistics, compatibility, engineering special, information and external-provider pools, then Microsoft/OpenFormula catalog delta audit. The locked target is a minimum; P11 audit may increase it.
