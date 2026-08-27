# NeraSpreadSheet Master Formula Completion Schedule

Historical cycles retain their original sizes. F018 contains exactly **60 new function names**, split into groups A/B/C of twenty, with a green local CLI gate after every group and one exact-head GitHub CI after all three commits are pushed together.

| Counter | Value |
|---|---:|
| Eager/versioned | 427 |
| AST/reference-aware | 37 |
| Dynamic-array unique | 22 |
| **Total functions** | **486 / at least 538** |
| Formula tests | 454 |
| Complete cycles | F001–F018 after exact-head CI |
| Remaining current minimum | 52 names before final catalog audit |

## F018 groups

- A — text/DBCS/regex compatibility: `ASC`, `ARRAYTOTEXT`, `BAHTTEXT`, `CONCATENATE`, `DBCS`, `DOLLAR`, `FINDB`, `FIXED`, `JIS`, `LEFTB`, `LENB`, `MIDB`, `REGEXEXTRACT`, `REGEXREPLACE`, `REGEXTEST`, `REPLACEB`, `RIGHTB`, `SEARCHB`, `TEXTAFTER`, `TEXTBEFORE`.
- B — information/statistical compatibility: `TEXT`, `VALUETOTEXT`, `ENCODEURL`, `CELL`, `ERROR.TYPE`, `ISFORMULA`, `ISREF`, `TYPE`, `GAMMA`, `GAMMALN`, `GAMMALN.PRECISE`, `GAUSS`, `PHI`, `PERMUT`, `PERMUTATIONA`, `CHISQ.TEST`, `T.TEST`, `PERCENTRANK`, `CHITEST`, `TTEST`.
- C — engineering/lookup/matrix: `CONVERT`, `ERF`, `ERF.PRECISE`, `ERFC`, `ERFC.PRECISE`, `BESSELI`, `BESSELJ`, `BESSELK`, `BESSELY`, `HLOOKUP`, `VLOOKUP`, `INDEX`, `MATCH`, `XLOOKUP`, `AGGREGATE`, `RAND`, `RANDBETWEEN`, `MDETERM`, `MUNIT`, `FREQUENCY`.

Manifest: `docs/formula-manifests/F018_60_FUNCTION_MANIFEST.md`.
