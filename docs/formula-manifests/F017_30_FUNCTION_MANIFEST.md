# F017 — 30-function implementation manifest

## Locked cycle

- Base head: `26778e3c585164656fc1484fe95b8b5586f601c3`.
- Functions before: **396**.
- Eager/versioned before: **342**.
- Formula tests before: **364**.
- Group sizes: **A=10, B=10, C=10**.
- Duplicate audit: **30/30 names are new** at cycle lock.
- Status values: `pending`, `implemented`, `tested`, `failed`.
- Commit plan:
  - A: `feat(formulas): add F017 group A legacy aliases (10 functions)`.
  - B: `feat(formulas): add F017 group B compatibility statistics (10 functions)`.
  - C: `feat(formulas): add F017 group C distributions and tests (10 functions)`.

The manifest is created before implementation. Each row owns one separately named regression. CLI build, the owning test class and the complete formula suite must be green before moving to the next group.

| Function | Group | Implementation file | Test file | Test method | Status | Important edge cases | Commit |
|---|:---:|---|---|---|---|---|:---:|
| `NORMDIST` | A | `LegacyStatisticalAliasFormulaFunctions.Part2.cs` | `F017GroupALegacyStatisticalAliasTests.cs` | `NormDist_LegacyName_MatchesModernNormalDistribution` | `tested` | cumulative/density; standard deviation > 0 | A |
| `NORMINV` | A | `LegacyStatisticalAliasFormulaFunctions.Part2.cs` | `F017GroupALegacyStatisticalAliasTests.cs` | `NormInv_LegacyName_MatchesModernNormalInverse` | `tested` | probability open interval; standard deviation > 0 | A |
| `NORMSDIST` | A | `LegacyStatisticalAliasFormulaFunctions.Part2.cs` | `F017GroupALegacyStatisticalAliasTests.cs` | `NormSDist_LegacyName_UsesCumulativeStandardNormal` | `tested` | implicit cumulative TRUE; finite scalar only | A |
| `NORMSINV` | A | `LegacyStatisticalAliasFormulaFunctions.Part2.cs` | `F017GroupALegacyStatisticalAliasTests.cs` | `NormSInv_LegacyName_MatchesModernStandardNormalInverse` | `tested` | probability open interval | A |
| `POISSON` | A | `LegacyStatisticalAliasFormulaFunctions.Part2.cs` | `F017GroupALegacyStatisticalAliasTests.cs` | `Poisson_LegacyName_MatchesModernPoissonDistribution` | `tested` | nonnegative integer events; positive mean | A |
| `WEIBULL` | A | `LegacyStatisticalAliasFormulaFunctions.Part2.cs` | `F017GroupALegacyStatisticalAliasTests.cs` | `Weibull_LegacyName_MatchesModernWeibullDistribution` | `tested` | x >= 0; positive alpha/beta; density/cumulative | A |
| `RANK` | A | `LegacyStatisticalAliasFormulaFunctions.Part2.cs` | `F017GroupALegacyStatisticalAliasTests.cs` | `Rank_LegacyName_MatchesRankEqWithTies` | `tested` | ties; ascending/descending; range identity | A |
| `PERCENTILE` | A | `LegacyStatisticalAliasFormulaFunctions.Part2.cs` | `F017GroupALegacyStatisticalAliasTests.cs` | `Percentile_LegacyName_MatchesInclusivePercentile` | `tested` | k in [0,1]; interpolation; empty input | A |
| `QUARTILE` | A | `LegacyStatisticalAliasFormulaFunctions.Part2.cs` | `F017GroupALegacyStatisticalAliasTests.cs` | `Quartile_LegacyName_MatchesInclusiveQuartile` | `tested` | quartile 0..4; interpolation | A |
| `FORECAST` | A | `LegacyStatisticalAliasFormulaFunctions.Part2.cs` | `F017GroupALegacyStatisticalAliasTests.cs` | `Forecast_LegacyName_MatchesForecastLinear` | `tested` | equal-sized numeric pairs; zero x variance | A |
| `STDEV` | B | `StatisticalCompatibilityFormulaFunctions.GroupB.cs` | `F017GroupBStatisticalCompatibilityTests.cs` | `Stdev_LegacyName_MatchesSampleStandardDeviation` | `tested` | sample requires at least two values | B |
| `STDEVP` | B | `StatisticalCompatibilityFormulaFunctions.GroupB.cs` | `F017GroupBStatisticalCompatibilityTests.cs` | `StdevP_LegacyName_MatchesPopulationStandardDeviation` | `tested` | population requires at least one value | B |
| `VAR` | B | `StatisticalCompatibilityFormulaFunctions.GroupB.cs` | `F017GroupBStatisticalCompatibilityTests.cs` | `Var_LegacyName_MatchesSampleVariance` | `tested` | sample denominator n-1 | B |
| `VARP` | B | `StatisticalCompatibilityFormulaFunctions.GroupB.cs` | `F017GroupBStatisticalCompatibilityTests.cs` | `VarP_LegacyName_MatchesPopulationVariance` | `tested` | population denominator n | B |
| `TINV` | B | `StatisticalCompatibilityFormulaFunctions.GroupB.cs` | `F017GroupBStatisticalCompatibilityTests.cs` | `TInv_LegacyName_MatchesTwoTailedInverse` | `tested` | probability (0,1]; positive degrees of freedom | B |
| `TDIST` | B | `StatisticalCompatibilityFormulaFunctions.GroupB.cs` | `F017GroupBStatisticalCompatibilityTests.cs` | `TDist_LegacyName_SelectsOneOrTwoTailDistribution` | `tested` | x >= 0; tails only 1 or 2; df truncation | B |
| `CONFIDENCE` | B | `StatisticalCompatibilityFormulaFunctions.GroupB.cs` | `F017GroupBStatisticalCompatibilityTests.cs` | `Confidence_LegacyName_MatchesConfidenceNorm` | `tested` | alpha open interval; positive sigma; size >= 1 | B |
| `CONFIDENCE.NORM` | B | `StatisticalCompatibilityFormulaFunctions.GroupB.cs` | `F017GroupBStatisticalCompatibilityTests.cs` | `ConfidenceNorm_ReturnsNormalMarginOfError` | `tested` | two-sided alpha; sample size truncation | B |
| `CONFIDENCE.T` | B | `StatisticalCompatibilityFormulaFunctions.GroupB.cs` | `F017GroupBStatisticalCompatibilityTests.cs` | `ConfidenceT_ReturnsStudentTMarginOfError` | `tested` | size >= 2; two-tailed inverse | B |
| `PROB` | B | `StatisticalCompatibilityFormulaFunctions.GroupB.cs` | `F017GroupBStatisticalCompatibilityTests.cs` | `Prob_SumsProbabilityMassWithinClosedInterval` | `tested` | equal shapes; probabilities in [0,1]; total approximately 1 | B |
| `BINOM.INV` | C | `DiscreteAndHypothesisStatisticalFormulaFunctions.GroupC.cs` | `F017GroupCDiscreteAndHypothesisTests.cs` | `BinomInv_ReturnsSmallestSuccessCountMeetingProbability` | `tested` | trials truncation; alpha [0,1]; bounded binary search | C |
| `NEGBINOM.DIST` | C | `DiscreteAndHypothesisStatisticalFormulaFunctions.GroupC.cs` | `F017GroupCDiscreteAndHypothesisTests.cs` | `NegBinomDist_ReturnsMassAndCumulativeProbability` | `tested` | failures >= 0; successes >= 1; p in [0,1] | C |
| `HYPGEOM.DIST` | C | `DiscreteAndHypothesisStatisticalFormulaFunctions.GroupC.cs` | `F017GroupCDiscreteAndHypothesisTests.cs` | `HypGeomDist_ReturnsMassAndCumulativeProbability` | `tested` | finite population bounds; feasible sample successes | C |
| `F.TEST` | C | `DiscreteAndHypothesisStatisticalFormulaFunctions.GroupC.cs` | `F017GroupCDiscreteAndHypothesisTests.cs` | `FTest_ReturnsTwoTailedVarianceProbability` | `tested` | each sample >= 2; nonzero variances; symmetric result | C |
| `Z.TEST` | C | `DiscreteAndHypothesisStatisticalFormulaFunctions.GroupC.cs` | `F017GroupCDiscreteAndHypothesisTests.cs` | `ZTest_ReturnsOneTailedProbabilityWithKnownOrEstimatedSigma` | `tested` | sample nonempty; positive sigma; estimated sigma needs n >= 2 | C |
| `CRITBINOM` | C | `DiscreteAndHypothesisStatisticalFormulaFunctions.GroupC.cs` | `F017GroupCDiscreteAndHypothesisTests.cs` | `CritBinom_LegacyName_MatchesBinomInv` | `tested` | legacy alias; alpha endpoints | C |
| `NEGBINOMDIST` | C | `DiscreteAndHypothesisStatisticalFormulaFunctions.GroupC.cs` | `F017GroupCDiscreteAndHypothesisTests.cs` | `NegBinomDist_LegacyName_MatchesModernMass` | `tested` | legacy mass-only signature | C |
| `HYPGEOMDIST` | C | `DiscreteAndHypothesisStatisticalFormulaFunctions.GroupC.cs` | `F017GroupCDiscreteAndHypothesisTests.cs` | `HypGeomDist_LegacyName_MatchesModernMass` | `tested` | legacy mass-only signature | C |
| `FTEST` | C | `DiscreteAndHypothesisStatisticalFormulaFunctions.GroupC.cs` | `F017GroupCDiscreteAndHypothesisTests.cs` | `FTest_LegacyName_MatchesModernFTest` | `tested` | two-tailed alias; range shape | C |
| `ZTEST` | C | `DiscreteAndHypothesisStatisticalFormulaFunctions.GroupC.cs` | `F017GroupCDiscreteAndHypothesisTests.cs` | `ZTest_LegacyName_MatchesModernZTest` | `tested` | optional sigma; range shape | C |

## Validation ledger

| Item | Before | After | Status |
|---|---:|---:|---|
| Eager/versioned functions | 342 | 372 | count audit passed |
| Total functions | 396 | 426 | count audit passed |
| Formula tests | 364 | 394 | 30 distinct tests and full formula suite passed |
| Group A CLI gate | 0/10 | 10/10 | build 0 warnings/errors; filtered and full formula suites passed |
| Group B CLI gate | 0/10 | 10/10 | build 0 warnings/errors; filtered and full formula suites passed |
| Group C CLI gate | 0/10 | 10/10 | exact failed test filtered and repaired; build and full formula suite passed |
| Full Core tests | — | 955/955 | passed by local CLI |
| Architecture verification | — | passed | local PowerShell CLI |
| Exact-head GitHub CI | — | — | not started |

Failure handling is surgical: filter the exact named test, repair only its owning A/B/C implementation, and never roll back the whole 30-function cycle.
