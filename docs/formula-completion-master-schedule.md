# NeraSpreadSheet Master Formula Completion Schedule

Public batches F001–F014 retain their historical sizes. Starting with F015, each public batch contains exactly **20 new function names** and completes only after implementation, regression, documentation and exact-head hosted CI are green.

| Counter | Value |
|---|---:|
| Eager/versioned | 282 |
| AST/reference-aware | 34 |
| Dynamic-array unique | 20 |
| **Total functions** | **336 / at least 538** |
| Formula tests | 304 |
| Complete | F001–F015 |
| Next | F016: 20 new names after duplicate/catalog audit |

F014: `ATANH`, `SINH`, `COSH`, `TANH`, `COMBIN`, `COMBINA`, `FACT`, `FACTDOUBLE`, `GCD`, `LCM`.

F015:

```text
MROUND CEILING FLOOR CEILING.PRECISE FLOOR.PRECISE ISO.CEILING
MULTINOMIAL SERIESSUM SUMPRODUCT SQRTPI SUMX2MY2 SUMX2PY2 SUMXMY2
BASE DECIMAL ARABIC ROMAN ISEVEN ISODD ISNONTEXT
```

`SUMSQ` and `PRODUCT` were already present before F015 and are not counted again.

Remaining work proceeds in 20-name batches through matrix/advanced math, higher-order/LET/LAMBDA, text/regex, statistics, compatibility, engineering special, information and external-provider pools, followed by the Microsoft/OpenFormula catalog delta audit. The locked target is a minimum and may increase after the final audit.
