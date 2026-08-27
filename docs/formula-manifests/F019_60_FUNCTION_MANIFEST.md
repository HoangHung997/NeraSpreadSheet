# F019 — 60-function implementation manifest

## Locked cycle

- Base head: `d50332d51f9b42b5dd10f89baba90e69ccee428b`.
- Functions before: **486**.
- Formula tests before: **454**.
- Group sizes: **A=20, B=20, C=20**.
- Duplicate audit: **60/60 names are new** against the F018 eager/AST/dynamic surface.
- Status values: `pending`, `implemented`, `tested`, `failed`.
- Commit plan:
  - A: `feat(formulas): add F019 group A compatibility functions (20 functions)`.
  - B: `feat(formulas): add F019 group B statistics matrix and external functions (20 functions)`.
  - C: `feat(formulas): add F019 group C higher-order external functions and finalize cycle (20 functions)`.

| Function | Group | Implementation file | Test file | Test method | Status | Important edge cases | Commit |
|---|:---:|---|---|---|---|---|:---:|
| `DAYSINMONTH` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `Daysinmonth_Contract_IsValidated` | `tested` | leap February; date coercion; invalid date | A |
| `DAYSINYEAR` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `Daysinyear_Contract_IsValidated` | `tested` | leap year 366; date coercion | A |
| `EASTERSUNDAY` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `Eastersunday_Contract_IsValidated` | `tested` | Gregorian computus; year bounds | A |
| `ISLEAPYEAR` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `Isleapyear_Contract_IsValidated` | `tested` | 1900/2000 century rules | A |
| `MONTHS` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `Months_Contract_IsValidated` | `tested` | interval vs calendar-month mode; reversed dates | A |
| `WEEKNUM_EXCEL2003` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `WeeknumExcel2003_Contract_IsValidated` | `tested` | Sunday-first Excel-2003 numbering | A |
| `WEEKNUM_OOO` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `WeeknumOoo_Contract_IsValidated` | `tested` | configurable first day/minimum days | A |
| `WEEKS` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `Weeks_Contract_IsValidated` | `tested` | interval vs Monday calendar crossings | A |
| `WEEKSINYEAR` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `Weeksinyear_Contract_IsValidated` | `tested` | ISO 52/53 weeks | A |
| `YEARS` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `Years_Contract_IsValidated` | `tested` | interval vs calendar-year crossings | A |
| `ROT13` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `Rot13_Contract_IsValidated` | `tested` | ASCII letters only; preserve nonletters | A |
| `RAWSUBTRACT` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `Rawsubtract_Contract_IsValidated` | `tested` | 2..254 args; left-to-right; range/scalar coercion | A |
| `CURRENT` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `Current_Contract_IsValidated` | `tested` | current cell value context; no recursion | A |
| `FORMULA` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `Formula_Contract_IsValidated` | `tested` | reference required; missing formula | A |
| `WEEKNUM_ADD` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `WeeknumAdd_Contract_IsValidated` | `tested` | LibreOffice compatibility alias; return-type mapping | A |
| `BINOM.DIST.RANGE` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `BinomDistRange_Contract_IsValidated` | `tested` | integer trials; lower/upper bounds; p endpoints | A |
| `EUROCONVERT` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `Euroconvert_Contract_IsValidated` | `tested` | fixed euro rates; triangulation; precision | A |
| `INFO` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `Info_Contract_IsValidated` | `tested` | supported deterministic info types; unknown type | A |
| `PHONETIC` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `Phonetic_Contract_IsValidated` | `tested` | metadata provider; fallback original text | A |
| `FILTERXML` | A | `F019DateTextAndCompatibilityFormulaFunctions.cs` | `F019GroupADateTextCompatibilityTests.cs` | `Filterxml_Contract_IsValidated` | `tested` | valid XML/XPath; no DTD/external entities; scalar result | A |
| `FORECAST.ETS` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `ForecastEts_Contract_IsValidated` | `tested` | sorted timeline; duplicate handling; seasonality limits | B |
| `FORECAST.ETS.CONFINT` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `ForecastEtsConfint_Contract_IsValidated` | `tested` | confidence 0..1; interval horizon | B |
| `FORECAST.ETS.SEASONALITY` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `ForecastEtsSeasonality_Contract_IsValidated` | `tested` | detect periodicity; bounded search | B |
| `FORECAST.ETS.STAT` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `ForecastEtsStat_Contract_IsValidated` | `tested` | stat_type validation; bounded diagnostics | B |
| `GROWTH` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `Growth_Contract_IsValidated` | `tested` | positive known_y; regression shape; optional known_x/new_x | B |
| `LINEST` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `Linest_Contract_IsValidated` | `tested` | linear regression coefficients; const/stats flags | B |
| `LOGEST` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `Logest_Contract_IsValidated` | `tested` | positive y; log regression; overflow | B |
| `MAXIFS` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `Maxifs_Contract_IsValidated` | `tested` | criteria shape equality; no matches | B |
| `MINIFS` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `Minifs_Contract_IsValidated` | `tested` | criteria shape equality; no matches | B |
| `MINVERSE` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `Minverse_Contract_IsValidated` | `tested` | square matrix; singular matrix; max cells | B |
| `MMULT` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `Mmult_Contract_IsValidated` | `tested` | inner dimension equality; finite result; max cells | B |
| `MODE.MULT` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `ModeMult_Contract_IsValidated` | `tested` | ties; no duplicates; vertical spill | B |
| `RANDARRAY` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `Randarray_Contract_IsValidated` | `tested` | rows/cols bounds; min/max; integer mode; volatile | B |
| `TEXTSPLIT` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `Textsplit_Contract_IsValidated` | `tested` | row/column delimiters; ignore_empty; pad_with | B |
| `TREND` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `Trend_Contract_IsValidated` | `tested` | linear prediction shapes; optional const | B |
| `IMAGE` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `Image_Contract_IsValidated` | `tested` | external image provider; size mode validation | B |
| `DETECTLANGUAGE` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `Detectlanguage_Contract_IsValidated` | `tested` | external language provider; unavailable provider | B |
| `TRANSLATE` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `Translate_Contract_IsValidated` | `tested` | source/target language; external provider | B |
| `WEBSERVICE` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `Webservice_Contract_IsValidated` | `tested` | external provider; URL validation; size cap | B |
| `STOCKHISTORY` | B | `F019StatisticsMatrixAndExternalFormulaFunctions.cs` | `F019GroupBStatisticsMatrixExternalTests.cs` | `Stockhistory_Contract_IsValidated` | `tested` | external provider; dates/interval/headers/properties | B |
| `BYCOL` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Bycol_Contract_IsValidated` | `pending` | lambda arity 1; one result per column; spill | C |
| `BYROW` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Byrow_Contract_IsValidated` | `pending` | lambda arity 1; one result per row; spill | C |
| `MAKEARRAY` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Makearray_Contract_IsValidated` | `pending` | positive dimensions; lambda(row,col); max cells | C |
| `MAP` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Map_Contract_IsValidated` | `pending` | same-shape arrays; lambda arity matches arrays | C |
| `REDUCE` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Reduce_Contract_IsValidated` | `pending` | left-to-right accumulator; lambda(acc,value) | C |
| `SCAN` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Scan_Contract_IsValidated` | `pending` | intermediate accumulator spill | C |
| `LAMBDA` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Lambda_Contract_IsValidated` | `pending` | parameter validation; standalone returns calc error equivalent | C |
| `LET` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Let_Contract_IsValidated` | `pending` | name/value pairs; lexical shadowing; final expression | C |
| `ISOMITTED` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Isomitted_Contract_IsValidated` | `pending` | lambda omitted sentinel only | C |
| `CALL` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Call_Contract_IsValidated` | `pending` | external add-in provider only; blocked by default | C |
| `REGISTER.ID` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `RegisterId_Contract_IsValidated` | `pending` | external add-in provider only; blocked by default | C |
| `CUBEKPIMEMBER` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Cubekpimember_Contract_IsValidated` | `pending` | cube provider; external-state failure | C |
| `CUBEMEMBER` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Cubemember_Contract_IsValidated` | `pending` | cube provider; tuple validation | C |
| `CUBEMEMBERPROPERTY` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Cubememberproperty_Contract_IsValidated` | `pending` | cube provider; property validation | C |
| `CUBERANKEDMEMBER` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Cuberankedmember_Contract_IsValidated` | `pending` | cube provider; rank >=1 | C |
| `CUBESET` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Cubeset_Contract_IsValidated` | `pending` | cube provider; set expression | C |
| `CUBESETCOUNT` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Cubesetcount_Contract_IsValidated` | `pending` | cube set handle/provider | C |
| `CUBEVALUE` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Cubevalue_Contract_IsValidated` | `pending` | cube provider; member tuple arguments | C |
| `RTD` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Rtd_Contract_IsValidated` | `pending` | realtime provider; topic arguments | C |
| `COPILOT` | C | `F019HigherOrderAndExternalFormulaEngine.cs` | `F019GroupCHigherOrderExternalTests.cs` | `Copilot_Contract_IsValidated` | `pending` | AI provider; explicit external-state context; no silent network | C |

## Validation ledger

| Item | Before | After | Status |
|---|---:|---:|---|
| Total functions | 486 | 546 | pending |
| Formula tests | 454 | 514 | A checkpoint 474/474; B checkpoint 494/494; final pending |
| Group A CLI gate | 0/20 | 20/20 | build 0 warnings/errors; filtered 20/20; full formula 474/474 |
| Group B CLI gate | 0/20 | 20/20 | build 0 warnings/errors; filtered 20/20; full formula 494/494 |
| Group C CLI gate | 0/20 | — | pending |
| Full Core tests | — | — | pending |
| Architecture verification | — | — | pending |
| Exact-head GitHub CI | — | — | not started |
