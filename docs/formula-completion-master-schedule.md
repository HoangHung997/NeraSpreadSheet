# NeraSpreadSheet Master Formula Completion Schedule

Historical cycles retain their original sizes. Starting with F017, each cycle contains exactly **30 new function names**, split into groups A/B/C of ten.

Locked workflow: manifest first; implement A, B and C separately; after every group run analyzer-clean build, the exact group tests and the complete formula suite; keep three local commits; after C run complete Core tests and architecture verification; push the three commits together; accept one exact-head GitHub CI.

| Counter | Value |
|---|---:|
| Eager/versioned | 372 |
| AST/reference-aware | 34 |
| Dynamic-array unique | 20 |
| **Total functions** | **426 / at least 538** |
| Formula tests | 394 |
| Complete cycles | F001–F017 after exact-head CI |
| Next | F018: 30 new names after duplicate/catalog audit |

## F017 groups

- A: `NORMDIST`, `NORMINV`, `NORMSDIST`, `NORMSINV`, `POISSON`, `WEIBULL`, `RANK`, `PERCENTILE`, `QUARTILE`, `FORECAST`.
- B: `STDEV`, `STDEVP`, `VAR`, `VARP`, `TINV`, `TDIST`, `CONFIDENCE`, `CONFIDENCE.NORM`, `CONFIDENCE.T`, `PROB`.
- C: `BINOM.INV`, `NEGBINOM.DIST`, `HYPGEOM.DIST`, `F.TEST`, `Z.TEST`, `CRITBINOM`, `NEGBINOMDIST`, `HYPGEOMDIST`, `FTEST`, `ZTEST`.

Manifest: `docs/formula-manifests/F017_30_FUNCTION_MANIFEST.md`.
