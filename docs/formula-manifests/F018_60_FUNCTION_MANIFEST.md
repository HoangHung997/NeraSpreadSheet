# F018 — 60-function implementation manifest

## Cycle identity

- Base HEAD: `0c6f8d5938b50485fa93023e9311261850b7d592`.
- Functions before/after: **426 → 486**.
- Eager/versioned before/after: **372 → 427**.
- Dynamic-array unique before/after: **20 → 22**.
- Formula tests before/after: **394 → 454**.
- Groups: **A=20, B=20, C=20**.
- Duplicate audit: **60/60 names new**.

| Function | Group | Implementation file | Test file | Test method | Status | Important edge cases | Commit |
|---|:---:|---|---|---|---|---|:---:|
| `ASC` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_ASC_Compatibility` | `tested` | full-width ASCII/katakana normalization | A |
| `ARRAYTOTEXT` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_ARRAYTOTEXT_Compatibility` | `tested` | range shape; strict format; text quoting | A |
| `BAHTTEXT` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_BAHTTEXT_Compatibility` | `tested` | negative; satang rounding; large input | A |
| `CONCATENATE` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_CONCATENATE_Compatibility` | `tested` | ranges; error propagation; 32767 cap | A |
| `DBCS` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_DBCS_Compatibility` | `tested` | ASCII/katakana full-width conversion | A |
| `DOLLAR` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_DOLLAR_Compatibility` | `tested` | negative parentheses; negative decimals | A |
| `FINDB` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_FINDB_Compatibility` | `tested` | DBCS byte position; not found | A |
| `FIXED` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_FIXED_Compatibility` | `tested` | group separators; negative decimals | A |
| `JIS` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_JIS_Compatibility` | `tested` | same DBCS conversion surface | A |
| `LEFTB` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_LEFTB_Compatibility` | `tested` | DBCS byte boundary | A |
| `LENB` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_LENB_Compatibility` | `tested` | ASCII=1 byte; non-ASCII=2 bytes | A |
| `MIDB` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_MIDB_Compatibility` | `tested` | 1-based byte start; multibyte boundary | A |
| `REGEXEXTRACT` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_REGEXEXTRACT_Compatibility` | `tested` | no match; invalid regex; timeout | A |
| `REGEXREPLACE` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_REGEXREPLACE_Compatibility` | `tested` | all/specific occurrence; invalid regex | A |
| `REGEXTEST` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_REGEXTEST_Compatibility` | `tested` | case mode; invalid regex | A |
| `REPLACEB` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_REPLACEB_Compatibility` | `tested` | byte replacement boundaries | A |
| `RIGHTB` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_RIGHTB_Compatibility` | `tested` | DBCS byte boundary | A |
| `SEARCHB` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_SEARCHB_Compatibility` | `tested` | wildcards; case-insensitive; byte position | A |
| `TEXTAFTER` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_TEXTAFTER_Compatibility` | `tested` | positive/negative instance; if-not-found | A |
| `TEXTBEFORE` | A | `F018TextCompatibilityFormulaFunctions.cs` | `F018GroupATextCompatibilityTests.cs` | `F018_TEXTBEFORE_Compatibility` | `tested` | positive/negative instance; if-not-found | A |
| `TEXT` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_TEXT_Compatibility` | `tested` | numeric/date subset; invalid format | B |
| `VALUETOTEXT` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_VALUETOTEXT_Compatibility` | `tested` | strict text quoting; scalar only | B |
| `ENCODEURL` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_ENCODEURL_Compatibility` | `tested` | spaces/unicode; URI failure | B |
| `CELL` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_CELL_Compatibility` | `tested` | address/row/col/contents/type; reference identity | B |
| `ERROR.TYPE` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_ERROR_TYPE_Compatibility` | `tested` | maps spreadsheet errors; non-error #N/A | B |
| `ISFORMULA` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_ISFORMULA_Compatibility` | `tested` | reference only; formula metadata context | B |
| `ISREF` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_ISREF_Compatibility` | `tested` | cell/range/reference function vs scalar | B |
| `TYPE` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_TYPE_Compatibility` | `tested` | number/text/boolean/error/array | B |
| `GAMMA` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_GAMMA_Compatibility` | `tested` | poles; reflection; overflow | B |
| `GAMMALN` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_GAMMALN_Compatibility` | `tested` | positive domain | B |
| `GAMMALN.PRECISE` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_GAMMALN_PRECISE_Compatibility` | `tested` | positive domain | B |
| `GAUSS` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_GAUSS_Compatibility` | `tested` | normal CDF minus 0.5 | B |
| `PHI` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_PHI_Compatibility` | `tested` | normal density | B |
| `PERMUT` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_PERMUT_Compatibility` | `tested` | n/k truncation; k<=n | B |
| `PERMUTATIONA` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_PERMUTATIONA_Compatibility` | `tested` | repetition; overflow | B |
| `CHISQ.TEST` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_CHISQ_TEST_Compatibility` | `tested` | equal shapes; expected >0 | B |
| `T.TEST` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_T_TEST_Compatibility` | `tested` | tails 1/2; types 1/2/3 | B |
| `PERCENTRANK` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_PERCENTRANK_Compatibility` | `tested` | sorted interpolation; significance | B |
| `CHITEST` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_CHITEST_Compatibility` | `tested` | legacy alias behavior | B |
| `TTEST` | B | `F018InfoAndStatisticalFormulaFunctions.cs / F018ReferenceIntrospectionFormulaEngine.cs` | `F018GroupBInfoAndStatisticalTests.cs` | `F018_TTEST_Compatibility` | `tested` | legacy alias behavior | B |
| `CONVERT` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_CONVERT_Compatibility` | `pending` | unit dimension match; temperature offsets | C |
| `ERF` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_ERF_Compatibility` | `pending` | one/two-bound forms | C |
| `ERF.PRECISE` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_ERF_PRECISE_Compatibility` | `pending` | finite scalar | C |
| `ERFC` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_ERFC_Compatibility` | `pending` | complementary error function | C |
| `ERFC.PRECISE` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_ERFC_PRECISE_Compatibility` | `pending` | finite scalar | C |
| `BESSELI` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_BESSELI_Compatibility` | `pending` | order truncation; bounded series | C |
| `BESSELJ` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_BESSELJ_Compatibility` | `pending` | order truncation; bounded series | C |
| `BESSELK` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_BESSELK_Compatibility` | `pending` | x>0; bounded recurrence | C |
| `BESSELY` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_BESSELY_Compatibility` | `pending` | x>0; bounded recurrence | C |
| `HLOOKUP` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_HLOOKUP_Compatibility` | `pending` | row index; approximate/exact | C |
| `VLOOKUP` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_VLOOKUP_Compatibility` | `pending` | column index; approximate/exact | C |
| `INDEX` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_INDEX_Compatibility` | `pending` | shape; row/column bounds | C |
| `MATCH` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_MATCH_Compatibility` | `pending` | vector-only; exact/approx modes | C |
| `XLOOKUP` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_XLOOKUP_Compatibility` | `pending` | exact match; if-not-found; shape | C |
| `AGGREGATE` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_AGGREGATE_Compatibility` | `pending` | function selector; numeric ranges | C |
| `RAND` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_RAND_Compatibility` | `pending` | volatile; [0,1) | C |
| `RANDBETWEEN` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_RANDBETWEEN_Compatibility` | `pending` | volatile; inclusive bounds | C |
| `MDETERM` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_MDETERM_Compatibility` | `pending` | square matrix; pivoting; 256 cap | C |
| `MUNIT` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_MUNIT_Compatibility` | `pending` | positive size; 1M-cell cap | C |
| `FREQUENCY` | C | `F018EngineeringLookupMathFormulaFunctions.cs / F018DynamicMatrixFormulaEngine.cs` | `F018GroupCEngineeringLookupMathTests.cs` | `F018_FREQUENCY_Compatibility` | `pending` | bins; output bins+1; dynamic spill | C |

## Validation ledger

| Gate | Status |
|---|---|
| Group A CLI | passed — build 0 warnings/errors; 20/20 filtered; 414/414 formula |
| Group B CLI | passed — build 0 warnings/errors; 20/20 filtered; 434/434 formula |
| Group C CLI | pending |
| Full formula suite | pending |
| Full Core tests | pending |
| Architecture verification | pending |
| Exact-head GitHub CI | pending |
