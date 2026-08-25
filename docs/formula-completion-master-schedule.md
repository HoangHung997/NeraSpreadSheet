# NeraSpreadSheet Master Formula Completion Schedule

> Lịch tuần tự theo dependency. Mỗi milestone công khai gồm đúng **5 tên hàm mới** và chỉ hoàn thành khi implementation, regression, documentation và exact-head hosted CI đều xanh.

## 1. Trạng thái trực tiếp

| Chỉ số | Giá trị |
|---|---:|
| Eager/versioned built-ins | 238 |
| AST/reference-aware | 18 |
| Dynamic-array built-ins | 5 |
| Tổng built-ins | 261 |
| Financial functions | 56 |
| Formula tests | 229 |
| Batch hoàn thành | F001–F007 |
| Batch kế tiếp | F008 |
| PR | #1 · Draft · chưa merge |

## 2. Baseline đích

```text
Target      = Microsoft worksheet catalog snapshot
            ∪ OpenFormula 1.4 target
            ∪ compatibility names used by real XLSX/ODS files
Implemented = resolvable names with executable implementation and contract tests
Blocked     = names waiting for mandatory parser/provider/security infrastructure
Pending     = Target - Implemented - Blocked
```

Trước mỗi batch, registry audit loại mọi tên đã có và lấy năm tên `Pending` đầu tiên theo thứ tự dependency. Tổng đích không hard-code vì Microsoft/OpenFormula catalog có thể thay đổi.

## 3. Quy tắc batch

1. Báo cáo milestone chứa đúng năm public function names mới.
2. Refactor, parser, provider, test, corpus và docs không thay thế một tên hàm.
3. Alias chỉ được tính khi có mapping, descriptor, coercion/error và regression riêng.
4. Function hoàn thành khi có implementation, SDK metadata, result/domain tests và dependency/resource/convergence tests nếu áp dụng.
5. CI đỏ hoặc unknown nghĩa là batch chưa hoàn thành.
6. Sau mỗi milestone, tự khóa năm tên kế tiếp; không hỏi lại người dùng.
7. PR #1 giữ Draft trong toàn bộ chuỗi.
8. Registry-count assertions dùng một shared test constant.
9. Báo cáo sau batch phải gồm cả tiến độ **toàn bộ dự án**, không chỉ riêng formula batch.

## 4. Các batch đầu

| Batch | Năm hàm | Trạng thái |
|---:|---|---|
| F001 | `ACCRINTM`, `DISC`, `INTRATE`, `RECEIVED`, `PRICEDISC` | ✅ Complete |
| F002 | `YIELDDISC`, `PRICEMAT`, `YIELDMAT`, `ACCRINT`, `FVSCHEDULE` | ✅ Complete |
| F003 | `PRICE`, `YIELD`, `DURATION`, `MDURATION`, `MIRR` | ✅ Complete |
| F004 | `TBILLEQ`, `TBILLPRICE`, `TBILLYIELD`, `DOLLARDE`, `DOLLARFR` | ✅ Complete |
| F005 | `AMORLINC`, `AMORDEGRC`, `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE` | ✅ Complete |
| F006 | `ODDLYIELD`, `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM` | ✅ Complete |
| F007 | `NETWORKDAYS`, `NETWORKDAYS.INTL`, `WORKDAY`, `WORKDAY.INTL`, `NUMBERVALUE` | ✅ Complete |
| F008 | `ADDRESS`, `AREAS`, `CHOOSE`, `CHOOSECOLS`, `CHOOSEROWS` | **Next** |

Nếu audit thấy tên đã tồn tại, tên đó được thay bằng tên Pending đầu tiên của pool kế tiếp để batch vẫn đủ đúng năm hàm mới.

## 5. Hàng đợi theo dependency

### P02 — Lookup, reference và dynamic-array projection

F008 lấy năm tên đầu:

```text
ADDRESS
AREAS
CHOOSE
CHOOSECOLS
CHOOSEROWS
```

Phần còn lại:

```text
COLUMN COLUMNS DROP EXPAND FORMULATEXT GETPIVOTDATA GROUPBY HSTACK
HYPERLINK INDIRECT LOOKUP OFFSET PERCENTOF PIVOTBY ROW ROWS SHEET
SHEETS SORTBY TAKE TOCOL TOROW TRIMRANGE VSTACK WRAPCOLS WRAPROWS
XMATCH
```

Phase này phải khóa reference identity, scalar/reference/array capability, negative column/row indices, shape propagation và dependency capture.

### P03 — LET/LAMBDA, higher-order arrays và logical

```text
BYCOL BYROW IFS IFERROR IFNA ISOMITTED LAMBDA LET MAKEARRAY
MAP REDUCE SCAN SWITCH XOR
```

Phase này phải khóa lexical scope, lazy evaluation, recursion/resource limits và array-shape propagation.

### P04 — Text, regex, byte-width và conversion

```text
ARRAYTOTEXT ASC BAHTTEXT CHAR CLEAN CONCAT CONCATENATE DBCS DOLLAR
EXACT FINDB FIXED JIS LEFTB LENB MIDB PHONETIC REGEXEXTRACT
REGEXREPLACE REGEXTEST REPLACE REPLACEB RIGHTB SEARCHB T TEXT
TEXTAFTER TEXTBEFORE TEXTSPLIT VALUE VALUETOTEXT
```

### P05 — Math, trigonometry, combinatorics và matrix

```text
ACOT ACOTH AGGREGATE ARABIC BASE CEILING.MATH CEILING.PRECISE
COMBIN COMBINA COT COTH CSC CSCH DECIMAL DEGREES EVEN EXP
FACTDOUBLE FLOOR.MATH FLOOR.PRECISE GCD ISO.CEILING LCM MDETERM
MINVERSE MMULT MROUND MULTINOMIAL MUNIT ODD QUOTIENT RADIANS
RANDARRAY ROMAN SEC SECH SERIESSUM SQRTPI SUMPRODUCT SUMSQ
SUMX2MY2 SUMX2PY2 SUMXMY2
```

### P06 — Statistics, tests, confidence và forecast

```text
AVEDEV AVERAGEA BINOM.DIST.RANGE BINOM.INV CHISQ.TEST
CONFIDENCE.NORM CONFIDENCE.T DEVSQ F.TEST FORECAST
FORECAST.ETS FORECAST.ETS.CONFINT FORECAST.ETS.SEASONALITY
FORECAST.ETS.STAT FREQUENCY GAMMA GAMMALN GAMMALN.PRECISE GAUSS
GEOMEAN GROWTH HARMEAN HYPGEOM.DIST KURT LINEST LOGEST MAXA MAXIFS
MINA MINIFS MODE.MULT NEGBINOM.DIST PERCENTRANK.EXC
PERCENTRANK.INC PERMUT PERMUTATIONA PHI PROB SKEW SKEW.P TREND
TRIMMEAN T.TEST Z.TEST
```

### P07 — Compatibility/legacy names

```text
BETADIST BETAINV BINOMDIST CEILING CHIDIST CHIINV CHITEST CONFIDENCE
COVAR CRITBINOM EXPONDIST FDIST FINV FLOOR FTEST GAMMADIST GAMMAINV
HYPGEOMDIST LOGINV LOGNORMDIST MODE NEGBINOMDIST NORMDIST NORMINV
NORMSDIST NORMSINV PERCENTILE PERCENTRANK POISSON QUARTILE RANK
STDEV STDEVP TDIST TINV TTEST VAR VARP WEIBULL ZTEST
```

### P08 — Engineering special, complex numbers và unit conversion

```text
BESSELI BESSELJ BESSELK BESSELY COMPLEX CONVERT ERF ERF.PRECISE
ERFC ERFC.PRECISE IMABS IMAGINARY IMARGUMENT IMCONJUGATE IMCOS
IMCOSH IMCOT IMCSC IMCSCH IMDIV IMEXP IMLN IMLOG10 IMLOG2 IMPOWER
IMPRODUCT IMREAL IMSEC IMSECH IMSIN IMSINH IMSQRT IMSUB IMSUM IMTAN
```

### P09 — Information, introspection và reference identity

```text
CELL ERROR.TYPE INFO N NA TYPE ISBLANK ISERR ISERROR ISEVEN ISFORMULA
ISLOGICAL ISNA ISNONTEXT ISNUMBER ISODD ISREF ISTEXT
```

### P10 — Cube, web, data types và external state

```text
CUBEKPIMEMBER CUBEMEMBER CUBEMEMBERPROPERTY CUBERANKEDMEMBER CUBESET
CUBESETCOUNT CUBEVALUE ENCODEURL EUROCONVERT FILTERXML WEBSERVICE
FIELDVALUE STOCKHISTORY IMAGE DETECTLANGUAGE TRANSLATE RTD CALL
REGISTER.ID COPILOT
```

Phase này chỉ bắt đầu sau provider manifest, permission/trust policy, timeout, cancellation, cache, offline behavior, audit log và deterministic fake-provider tests.

### P11 — OpenFormula-only và catalog delta

```text
MissingOpenFormula = OpenFormulaTarget - Registry
MissingMicrosoft   = MicrosoftSnapshotTarget - Registry
```

Các tên còn lại được sắp theo dependency và tự chia batch năm hàm cho tới khi cả hai tập bằng 0.

## 6. Điều kiện kết thúc toàn bộ formula program

Chỉ tuyên bố đủ hàm khi:

1. `Pending = 0` cho snapshot Microsoft và OpenFormula đã khóa;
2. không còn duplicate identity/alias hoặc registration path song song;
3. compatibility aliases có differential tests;
4. external/cube/data-type functions có provider contract, fake-provider tests và offline fail-closed path;
5. parser hỗ trợ LET/LAMBDA/dynamic references cần thiết;
6. mọi solver, recursion, array, schedule, calendar và external request có resource budget;
7. external Excel/LibreOffice/ODS differential corpus và fuzzing không còn blocker;
8. exact-head hosted CI và Codex final acceptance đều xanh.

## 7. Next five

```text
ADDRESS
AREAS
CHOOSE
CHOOSECOLS
CHOOSEROWS
```
