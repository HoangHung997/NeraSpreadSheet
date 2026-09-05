# F015 — Advanced Math Compatibility

## Delivered

Twenty new names were implemented in four modules:

- Rounding: `MROUND`, `CEILING`, `FLOOR`, `CEILING.PRECISE`, `FLOOR.PRECISE`, `ISO.CEILING`, `SQRTPI`.
- Vector math: `MULTINOMIAL`, `SERIESSUM`, `SUMPRODUCT`, `SUMX2MY2`, `SUMX2PY2`, `SUMXMY2`.
- Conversion: `BASE`, `DECIMAL`, `ARABIC`, `ROMAN`.
- Information: `ISEVEN`, `ISODD`, `ISNONTEXT`.

`SUMSQ` and `PRODUCT` were found during duplicate audit and were not counted again.

## Validation

- Exact implementation head: `cfe71a1679dd636ea30408248164703d204d553d`.
- Formula registry: 282 eager/versioned names.
- Total built-ins: 336 / at least 538.
- Formula tests: 304/304.
- Build/analyzers: zero warnings and zero errors.
- Resource caps and logical range shape are covered by regression.

## Compatibility correction during review

The Roman regression was corrected to preserve supported compatibility semantics: `ARABIC("IIII")` returns 4, while invalid `ARABIC("IIV")` returns `#VALUE!`. The implementation was not weakened to satisfy an incorrect test expectation.

## Next

F016 will contain exactly twenty new names after duplicate and catalog audit.
