# NeraSpreadSheet Master Formula Completion Schedule

Historical cycles F001–F015 retain their original sizes. Starting with F016, each formula cycle contains exactly **60 new function names** split into three sequential groups of 20: A, B and C.

The locked workflow is: manifest first; one implementation-and-test commit per group; no branch update and no GitHub CI after A or B; final registry/count audit after C; one branch update; then one exact-head CI.

| Counter | Value |
|---|---:|
| Eager/versioned | 342 |
| AST/reference-aware | 34 |
| Dynamic-array unique | 20 |
| **Total functions** | **396 / at least 538** |
| Formula tests | 364 |
| Complete cycles | F001–F016 after exact-head CI |
| Next | F017: 60 new names after duplicate/catalog audit |

## F016 groups

- A — 20 complex engineering foundations: `COMPLEX`, `IMABS`, `IMAGINARY`, `IMARGUMENT`, `IMCONJUGATE`, `IMCOS`, `IMCOSH`, `IMCOT`, `IMCSC`, `IMCSCH`, `IMDIV`, `IMEXP`, `IMLN`, `IMLOG10`, `IMLOG2`, `IMPOWER`, `IMPRODUCT`, `IMREAL`, `IMSEC`, `IMSECH`.
- B — 6 remaining complex names and 14 legacy statistical names: `IMSIN`, `IMSINH`, `IMSQRT`, `IMSUB`, `IMSUM`, `IMTAN`, `BETADIST`, `BETAINV`, `BINOMDIST`, `CHIDIST`, `CHIINV`, `COVAR`, `EXPONDIST`, `FDIST`, `FINV`, `GAMMADIST`, `GAMMAINV`, `LOGINV`, `LOGNORMDIST`, `MODE`.
- C — 20 descriptive/ranking compatibility names: `AVEDEV`, `AVERAGEA`, `DEVSQ`, `GEOMEAN`, `HARMEAN`, `KURT`, `MAXA`, `MINA`, `SKEW`, `SKEW.P`, `STDEVA`, `STDEVPA`, `VARA`, `VARPA`, `TRIMMEAN`, `PERCENTILE.EXC`, `QUARTILE.EXC`, `RANK.AVG`, `PERCENTRANK.INC`, `PERCENTRANK.EXC`.

Manifest: `docs/formula-manifests/F016_60_FUNCTION_MANIFEST.md`.

Remaining work proceeds through further 60-name cycles, followed by a Microsoft/OpenFormula delta audit. The locked target is a minimum and may increase after that audit.
