# F016 — 60-function manifest

- Base: `36f2c5f5f87e0eee3a90d292193d93570524dff8`
- A: `dd7bedd8c11ab89fff9283501a5b91960e709d89`
- B: `95e5d8421bc1353b2177094cef9e55d94afe4f9b`
- C: current corrected cycle head
- Functions: **336 → 396**; eager: **282 → 342**; tests: **304 → 364**
- Status values: pending / implemented / tested / failed. Final rows are `tested` after named preflight.

| Function | Group | Implementation | Test file | Named test | Status | Edge case | Commit |
|---|:---:|---|---|---|---|---|:---:|
| `COMPLEX` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `Complex_WithRealImaginaryAndSuffix_ReturnsCanonicalText` | tested | i/j suffix; zero parts; invalid suffix | A |
| `IMABS` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `ImAbs_WithThreeFourComplex_ReturnsFive` | tested | magnitude; malformed complex text | A |
| `IMAGINARY` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `Imaginary_WithComplexInput_ReturnsImaginaryCoefficient` | tested | i/j suffix; real scalar gives zero | A |
| `IMARGUMENT` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `ImArgument_WithFirstQuadrantComplex_ReturnsQuarterPi` | tested | quadrants; zero gives #DIV/0! | A |
| `IMCONJUGATE` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `ImConjugate_WithComplexInput_ReturnsConjugate` | tested | sign inversion; suffix preserved | A |
| `IMCOS` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `ImCos_AtZero_ReturnsOne` | tested | zero identity; finite-result guard | A |
| `IMCOSH` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `ImCosh_AtZero_ReturnsOne` | tested | zero identity; overflow guard | A |
| `IMCOT` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `ImCot_AtQuarterPi_ReturnsOne` | tested | tangent pole gives #NUM! | A |
| `IMCSC` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `ImCsc_AtHalfPi_ReturnsOne` | tested | sine pole gives #NUM! | A |
| `IMCSCH` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `ImCsch_AtAsinhOne_ReturnsOne` | tested | hyperbolic-sine pole gives #NUM! | A |
| `IMDIV` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `ImDiv_WithTwoComplexValues_ReturnsQuotient` | tested | zero denominator; mixed i/j; definite assignment fixed | A |
| `IMEXP` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `ImExp_AtZero_ReturnsOne` | tested | zero identity; overflow gives #NUM! | A |
| `IMLN` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `ImLn_AtEulerNumber_ReturnsOne` | tested | logarithm of zero gives #NUM! | A |
| `IMLOG10` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `ImLog10_AtOneHundred_ReturnsTwo` | tested | zero; complex logarithm branch | A |
| `IMLOG2` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `ImLog2_AtEight_ReturnsThree` | tested | zero; complex logarithm branch | A |
| `IMPOWER` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `ImPower_WithOnePlusISquared_ReturnsTwoI` | tested | zero to nonpositive power; finite result | A |
| `IMPRODUCT` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `ImProduct_WithConjugatePair_ReturnsTwo` | tested | scalar/range; suffix agreement; overflow | A |
| `IMREAL` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `ImReal_WithComplexInput_ReturnsRealCoefficient` | tested | pure imaginary gives zero | A |
| `IMSEC` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `ImSec_AtZero_ReturnsOne` | tested | cosine pole; finite result | A |
| `IMSECH` | A | `ComplexEngineeringFormulaFunctions.PartA.cs` | `ComplexEngineeringFormulaFunctionGroupATests.cs` | `ImSech_AtZero_ReturnsOne` | tested | hyperbolic-cosine pole; finite result | A |
| `IMSIN` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `ImSin_AtZero_ReturnsZero` | tested | zero identity; suffix preserved | B |
| `IMSINH` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `ImSinh_AtZero_ReturnsZero` | tested | zero identity; overflow guard | B |
| `IMSQRT` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `ImSqrt_OfNegativeOne_ReturnsI` | tested | principal branch; negative real input | B |
| `IMSUB` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `ImSub_WithTwoComplexValues_ReturnsDifference` | tested | cancellation; mixed i/j | B |
| `IMSUM` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `ImSum_WithTwoComplexValues_ReturnsSum` | tested | scalar/range; suffix agreement; overflow | B |
| `IMTAN` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `ImTan_AtZero_ReturnsZero` | tested | zero identity; finite-result guard | B |
| `BETADIST` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `BetaDist_LegacyName_ReturnsCumulativeProbability` | tested | cumulative mapping; optional bounds | B |
| `BETAINV` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `BetaInv_LegacyName_ReturnsQuantile` | tested | probability domain; optional interval | B |
| `BINOMDIST` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `BinomDist_LegacyName_ReturnsCumulativeProbability` | tested | integer successes/trials; cumulative flag | B |
| `CHIDIST` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `ChiDist_LegacyName_ReturnsRightTailProbability` | tested | right-tail mapping; x/df domains | B |
| `CHIINV` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `ChiInv_LegacyName_ReturnsRightTailQuantile` | tested | right-tail inverse; probability/df domains | B |
| `COVAR` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `Covar_LegacyName_ReturnsPopulationCovariance` | tested | equal shapes; population denominator | B |
| `EXPONDIST` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `ExponDist_LegacyName_ReturnsCumulativeProbability` | tested | lambda positive; cumulative/density | B |
| `FDIST` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `FDist_LegacyName_ReturnsRightTailProbability` | tested | right tail; positive degrees of freedom | B |
| `FINV` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `FInv_LegacyName_ReturnsRightTailQuantile` | tested | right-tail inverse; probability domain | B |
| `GAMMADIST` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `GammaDist_LegacyName_ReturnsCumulativeProbability` | tested | positive shape/scale; cumulative flag | B |
| `GAMMAINV` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `GammaInv_LegacyName_ReturnsQuantile` | tested | probability endpoints; shape/scale domains | B |
| `LOGINV` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `LogInv_LegacyName_ReturnsLogNormalQuantile` | tested | probability domain; normal primitive tolerance 5e-8 | B |
| `LOGNORMDIST` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `LogNormDist_LegacyName_ReturnsCumulativeProbability` | tested | x positive; normal primitive tolerance 5e-8 | B |
| `MODE` | B | `ComplexEngineeringFormulaFunctions.PartB.cs / LegacyStatisticalAliasFormulaFunctions.cs` | `ComplexAndLegacyStatisticalFormulaFunctionGroupBTests.cs` | `Mode_LegacyName_ReturnsSmallestMostFrequentValue` | tested | ties choose smallest; no repeat gives #N/A | B |
| `AVEDEV` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `AveDev_WithSymmetricValues_ReturnsMeanAbsoluteDeviation` | tested | empty set; stable absolute deviations | C |
| `AVERAGEA` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `AverageA_WithBooleanAndText_CountsCompatibilityValues` | tested | text=0; Boolean=1/0; blanks ignored | C |
| `DEVSQ` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `DevSq_WithSymmetricValues_ReturnsSquaredDeviationSum` | tested | empty set; compensated squared deviations | C |
| `GEOMEAN` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `GeoMean_WithPositiveValues_ReturnsGeometricMean` | tested | strictly positive; log-space | C |
| `HARMEAN` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `HarMean_WithPositiveValues_ReturnsHarmonicMean` | tested | zero/negative; reciprocal overflow | C |
| `KURT` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `Kurt_WithOneThroughFive_ReturnsSampleExcessKurtosis` | tested | n>=4; zero variance | C |
| `MAXA` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `MaxA_WithBooleanNegativeAndText_ReturnsOne` | tested | A coercion; blanks ignored | C |
| `MINA` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `MinA_WithBooleanNegativeAndText_ReturnsNegativeTwo` | tested | A coercion; blanks ignored | C |
| `SKEW` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `Skew_WithSymmetricValues_ReturnsZero` | tested | sample n>=3; zero variance | C |
| `SKEW.P` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `SkewP_WithSymmetricPopulation_ReturnsZero` | tested | population n>=3; zero variance | C |
| `STDEVA` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `StdevA_WithBooleanAndNumbers_ReturnsSampleDeviation` | tested | A coercion; sample n>=2 | C |
| `STDEVPA` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `StdevPa_WithBooleanAndNumbers_ReturnsPopulationDeviation` | tested | A coercion; population n>=1 | C |
| `VARA` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `VarA_WithBooleanAndNumbers_ReturnsSampleVariance` | tested | A coercion; sample n>=2 | C |
| `VARPA` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `VarPa_WithBooleanAndNumbers_ReturnsPopulationVariance` | tested | A coercion; population n>=1 | C |
| `TRIMMEAN` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `TrimMean_WithTwentyPercent_RemovesOneValueFromEachTail` | tested | percent [0,1); even trim count | C |
| `PERCENTILE.EXC` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `PercentileExc_WithValidExclusiveRank_ReturnsInterpolatedValue` | tested | k(n+1) rank; excluded endpoints | C |
| `QUARTILE.EXC` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `QuartileExc_WithSecondQuartile_ReturnsMedian` | tested | quartile 1..3; exclusive interpolation | C |
| `RANK.AVG` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `RankAvg_WithTie_ReturnsAverageRank` | tested | average ties; ascending/descending | C |
| `PERCENTRANK.INC` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `PercentRankInc_WithMiddleValue_ReturnsOneHalf` | tested | inclusive endpoints; duplicates; significance 1..15 | C |
| `PERCENTRANK.EXC` | C | `DescriptiveCompatibilityStatisticalFormulaFunctions*.cs` | `DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests.cs` | `PercentRankExc_WithMiddleValue_ReturnsOneHalf` | tested | exclusive ranks; duplicates; significance 1..15 | C |

## Validation

- 60/60 new names; no duplicate or alias collision.
- A/B/C deterministic preflight: **20/20, 20/20, 20/20**.
- Exact corrected HEAD must pass build/analyzers, **364/364 formula tests**, all Core tests, architecture verification and all host jobs.

## Resolved failures

- `IMDIV` (A): `CS0177`; `right` could be skipped by short-circuit `||`. Fixed by initializing `right` in replacement A.
- History reconstruction: a full B tree reintroduced old A; B/C rebuilt from per-group deltas.
- `LOGINV` (B): expected 1, actual 1.0000000375996139; inherited bounded inverse-normal error. Named tolerance set to `5e-8`.
- `LOGNORMDIST` (B): expected 0.5, actual 0.5000000150000002; inherited bounded normal-CDF error. Named tolerance set to `5e-8`.

Failure handling is surgical by named test/compiler location and owning A/B/C commit; never rollback all 60 functions.
