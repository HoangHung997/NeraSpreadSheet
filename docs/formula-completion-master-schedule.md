# NeraSpreadSheet Master Formula Completion Schedule

> Khóa lịch tại mốc: 24/08/2026. Đây là lịch triển khai tuần tự theo dependency, không phải cam kết thời gian. Mỗi milestone công khai gồm đúng **5 tên hàm mới** và chỉ được báo hoàn thành khi exact-head CI xanh.

## 1. Mốc xuất phát

- Pull request: `#1`, Draft, chưa merge vào `develop`.
- Exact head trước khi khóa lịch: `8105c092736cdcfc1b70842085d9a5e7db88d704`.
- Eager/versioned built-ins: `203`.
- AST/reference-aware built-ins: `18`.
- Dynamic-array built-ins: `5`.
- Tổng built-in formula subsystem: `226`.
- Hàm tài chính: `30`.
- Formula tests: `192/192` tại CI `#854`; exact documentation head xanh tại CI `#855`.

## 2. Baseline đích

Danh mục đích là hợp của:

1. danh sách worksheet functions hiện hành do Microsoft công bố cho Excel;
2. OpenDocument/OpenFormula 1.4 để khóa hành vi liên nền tảng;
3. các tên compatibility/legacy vẫn cần mở workbook cũ;
4. các hàm external, cube, data-type và web sau khi host/provider contract tương ứng tồn tại;
5. các alias được workbook XLSX/ODS thực tế sử dụng.

Tổng số đích **không hard-code** trong tài liệu này. Trước mỗi batch, registry audit tạo bốn tập:

```text
Target = tên trong baseline đã khóa
Implemented = tên đã resolve được và có contract test
Pending = Target - Implemented - Blocked
Blocked = tên đang chờ infrastructure bắt buộc
```

Một tên đã có trong registry sẽ tự động bị bỏ qua và tên `Pending` kế tiếp được kéo lên. Nhờ vậy lịch không làm lại hàm cũ, không bị sai khi Microsoft bổ sung hàm mới và không phụ thuộc vào con số ước lượng thủ công.

## 3. Quy tắc batch bắt buộc

1. Mỗi báo cáo milestone phải chứa đúng **5 public function names** mới.
2. Refactor, numerical primitive, parser, provider, manifest, fuzz corpus và tài liệu là việc hỗ trợ; không được tính thay cho một tên hàm.
3. Một alias chỉ được tính khi có registry mapping, descriptor, coercion/error contract và regression riêng; không được tăng count bằng alias rỗng.
4. Một hàm chỉ được coi là hoàn thành khi có:
   - executable implementation;
   - SDK metadata đúng;
   - result/reference tests;
   - domain/coercion/error tests;
   - dependency/resource/convergence tests khi áp dụng;
   - registry-count regression;
   - tài liệu và worklog;
   - exact-head Core/architecture/Windows/Android/iOS/Mac Catalyst/MAUI Windows CI xanh.
5. CI đỏ hoặc chưa xác định nghĩa là batch **chưa hoàn thành**; không chuyển sang batch sau.
6. PR #1 tiếp tục là Draft; không merge từng batch.
7. Sau mỗi batch, cập nhật bảng tiến độ trong tài liệu này và `docs/worklog/CURRENT.md`.
8. Khi tiếp tục công việc, tự lấy năm tên `Pending` đầu tiên; không hỏi người dùng chọn lại.
9. Nếu một batch cần infrastructure lớn, infrastructure được làm trong cùng batch nhưng báo cáo vẫn chỉ xuất hiện sau khi đủ năm hàm và CI xanh.

## 4. Sáu batch đầu đã khóa cứng

Các batch đầu dùng calendar/day-count layer vừa hoàn thành và tạo nền cho toàn bộ nhóm bond/security.

| Batch | Năm hàm | Trạng thái |
|---:|---|---|
| F001 | `ACCRINTM`, `DISC`, `INTRATE`, `RECEIVED`, `PRICEDISC` | Next |
| F002 | `YIELDDISC`, `PRICEMAT`, `YIELDMAT`, `ACCRINT`, `FVSCHEDULE` | Pending |
| F003 | `PRICE`, `YIELD`, `DURATION`, `MDURATION`, `MIRR` | Pending |
| F004 | `TBILLEQ`, `TBILLPRICE`, `TBILLYIELD`, `DOLLARDE`, `DOLLARFR` | Pending |
| F005 | `AMORLINC`, `AMORDEGRC`, `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE` | Pending |
| F006 | `ODDLYIELD`, `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM` | Pending |

Nếu audit phát hiện một tên trong bảng đã hoàn thành trước đó, tên đó bị thay bằng tên Pending đầu tiên của phase kế tiếp để batch vẫn đủ đúng năm hàm mới.

## 5. Hàng đợi ứng viên theo dependency

Sau F006, bộ chọn batch đọc các pool dưới đây theo đúng thứ tự và luôn lấy năm tên Pending đầu tiên. Tên đã có được tự động bỏ qua. Ranh giới phase không bắt buộc trùng ranh giới batch.

### P01 — Business-day, date/time và locale-number

```text
NETWORKDAYS
NETWORKDAYS.INTL
WORKDAY
WORKDAY.INTL
NUMBERVALUE
```

### P02 — Lookup, reference và dynamic-array projection

```text
ADDRESS
AREAS
CHOOSE
CHOOSECOLS
CHOOSEROWS
COLUMN
COLUMNS
DROP
EXPAND
FORMULATEXT
GETPIVOTDATA
GROUPBY
HSTACK
HYPERLINK
INDIRECT
LOOKUP
OFFSET
PERCENTOF
PIVOTBY
ROW
ROWS
SHEET
SHEETS
SORTBY
TAKE
TOCOL
TOROW
TRIMRANGE
VSTACK
WRAPCOLS
WRAPROWS
XMATCH
```

`IMAGE`, `FIELDVALUE` và `STOCKHISTORY` nằm ở phase external/data-type vì cần provider và security policy.

### P03 — LAMBDA, higher-order arrays và logical surface

```text
BYCOL
BYROW
IFS
IFERROR
IFNA
ISOMITTED
LAMBDA
LET
MAKEARRAY
MAP
REDUCE
SCAN
SWITCH
XOR
```

Phase này phải khóa lexical scope, argument binding, recursion/resource limits, lazy evaluation và array-shape propagation trước khi được coi là xanh.

### P04 — Text, regex, byte-width và conversion

```text
ARRAYTOTEXT
ASC
BAHTTEXT
CHAR
CLEAN
CONCAT
CONCATENATE
DBCS
DOLLAR
EXACT
FINDB
FIXED
JIS
LEFTB
LENB
MIDB
PHONETIC
REGEXEXTRACT
REGEXREPLACE
REGEXTEST
REPLACE
REPLACEB
RIGHTB
SEARCHB
T
TEXT
TEXTAFTER
TEXTBEFORE
TEXTSPLIT
VALUE
VALUETOTEXT
```

`DETECTLANGUAGE` và `TRANSLATE` được giữ ở phase external service. Byte-width functions phải có corpus DBCS thực tế; locale formatting không được phụ thuộc culture của tiến trình.

### P05 — Math, trigonometry, combinatorics và matrix

```text
ACOT
ACOTH
AGGREGATE
ARABIC
BASE
CEILING.MATH
CEILING.PRECISE
COMBIN
COMBINA
COT
COTH
CSC
CSCH
DECIMAL
DEGREES
EVEN
EXP
FACTDOUBLE
FLOOR.MATH
FLOOR.PRECISE
GCD
ISO.CEILING
LCM
MDETERM
MINVERSE
MMULT
MROUND
MULTINOMIAL
MUNIT
ODD
QUOTIENT
RADIANS
RANDARRAY
ROMAN
SEC
SECH
SERIESSUM
SQRTPI
SUMPRODUCT
SUMSQ
SUMX2MY2
SUMX2PY2
SUMXMY2
```

Matrix/array functions phải có shape budget, singular-matrix errors và deterministic numerical tolerances.

### P06 — Statistical tests, confidence, forecast và missing modern statistics

```text
AVEDEV
AVERAGEA
BINOM.DIST.RANGE
BINOM.INV
CHISQ.TEST
CONFIDENCE.NORM
CONFIDENCE.T
DEVSQ
F.TEST
FORECAST
FORECAST.ETS
FORECAST.ETS.CONFINT
FORECAST.ETS.SEASONALITY
FORECAST.ETS.STAT
FREQUENCY
GAMMA
GAMMALN
GAMMALN.PRECISE
GAUSS
GEOMEAN
GROWTH
HARMEAN
HYPGEOM.DIST
INTERCEPT
KURT
LINEST
LOGEST
MAXA
MAXIFS
MINA
MINIFS
MODE.MULT
NEGBINOM.DIST
PEARSON
PERCENTRANK.EXC
PERCENTRANK.INC
PERMUT
PERMUTATIONA
PHI
PROB
RSQ
SKEW
SKEW.P
STEYX
TREND
TRIMMEAN
T.TEST
Z.TEST
```

Forecast ETS cần một bounded time-series engine và không được dùng implementation nền tảng khác nhau giữa hosts.

### P07 — Compatibility/legacy names

```text
BETADIST
BETAINV
BINOMDIST
CEILING
CHIDIST
CHIINV
CHITEST
CONFIDENCE
COVAR
CRITBINOM
EXPONDIST
FDIST
FINV
FLOOR
FTEST
GAMMADIST
GAMMAINV
HYPGEOMDIST
LOGINV
LOGNORMDIST
MODE
NEGBINOMDIST
NORMDIST
NORMINV
NORMSDIST
NORMSINV
PERCENTILE
PERCENTRANK
POISSON
QUARTILE
RANK
STDEV
STDEVP
TDIST
TINV
TTEST
VAR
VARP
WEIBULL
ZTEST
```

Compatibility names ưu tiên adapter tới primitive hiện đại đã kiểm thử. Chúng vẫn phải khóa khác biệt signature, tail/cumulative mode và error behavior; không chỉ là string alias mù.

### P08 — Engineering special functions, complex numbers và unit conversion

```text
BESSELI
BESSELJ
BESSELK
BESSELY
COMPLEX
CONVERT
ERF
ERF.PRECISE
ERFC
ERFC.PRECISE
IMABS
IMAGINARY
IMARGUMENT
IMCONJUGATE
IMCOS
IMCOSH
IMCOT
IMCSC
IMCSCH
IMDIV
IMEXP
IMLN
IMLOG10
IMLOG2
IMPOWER
IMPRODUCT
IMREAL
IMSEC
IMSECH
IMSIN
IMSINH
IMSQRT
IMSUB
IMSUM
IMTAN
```

`CONVERT` cần catalog đơn vị versioned, prefix policy, dimension checking và tests không phụ thuộc locale.

### P09 — Information, introspection và reference identity

```text
CELL
ERROR.TYPE
INFO
N
NA
TYPE
ISBLANK
ISERR
ISERROR
ISEVEN
ISFORMULA
ISLOGICAL
ISNA
ISNONTEXT
ISNUMBER
ISODD
ISREF
ISTEXT
```

Những tên đã tồn tại trong core information surface sẽ bị audit bỏ qua. `CELL`/`INFO` phải có capability rõ ràng và không được đọc trạng thái host UI ngoài context contract.

### P10 — Cube, web, data types, automation và external-state functions

```text
CUBEKPIMEMBER
CUBEMEMBER
CUBEMEMBERPROPERTY
CUBERANKEDMEMBER
CUBESET
CUBESETCOUNT
CUBEVALUE
ENCODEURL
EUROCONVERT
FILTERXML
WEBSERVICE
FIELDVALUE
STOCKHISTORY
IMAGE
DETECTLANGUAGE
TRANSLATE
RTD
CALL
REGISTER.ID
COPILOT
```

Phase này chỉ bắt đầu sau khi có provider manifest, permission/trust policy, cancellation, timeout, cache, offline behavior, audit log và deterministic fake-provider tests. Hoàn thành formula contract không có nghĩa Nera phụ thuộc runtime Excel hoặc một nhà cung cấp duy nhất.

### P11 — OpenFormula-only và catalog delta

Sau các pool trên, chạy hai phép so sánh máy:

```text
MissingOpenFormula = OpenFormula-1.4-target - Registry
MissingMicrosoft = Microsoft-snapshot-target - Registry
```

Các tên còn thiếu được sắp theo dependency và tự đóng thành batch năm hàm. Phase này bao gồm các hàm OpenFormula không có tên Excel tương ứng, alias ODF, các tên mới được Microsoft thêm sau snapshot, và lỗi bỏ sót do catalog audit phát hiện.

## 6. Mẫu báo cáo bắt buộc sau mỗi năm hàm

```markdown
## Batch <ID> — <family>

| Hàm | Implementation | Reference/domain tests | Status |
|---|---|---|---|
| FUNCTION_1 | ... | ... | ✅ |
| FUNCTION_2 | ... | ... | ✅ |
| FUNCTION_3 | ... | ... | ✅ |
| FUNCTION_4 | ... | ... | ✅ |
| FUNCTION_5 | ... | ... | ✅ |

| Gate | Kết quả |
|---|---|
| Formula tests | x/x |
| Registry | trước → sau |
| Architecture | pass |
| Windows/GPU | pass |
| Android/iOS/Mac Catalyst | pass |
| MAUI Windows loaded smokes | pass |
| Exact-head CI | run / success |
| PR | Draft, unmerged |

Next five: `...`
```

Không báo giữa chừng theo kiểu “3/5 đã xong” trừ khi có lỗi thực sự cần công khai. Báo cáo milestone chỉ phát ra sau khi đủ 5/5 và exact-head xanh.

## 7. Bảng điều khiển tiến độ

| Chỉ số | Giá trị hiện tại |
|---|---:|
| Built-ins đã khóa | 226 |
| Eager/versioned | 203 |
| Financial functions | 30 |
| Formula tests | 192 |
| Batch kế tiếp | F001 |
| Hàm kế tiếp | ACCRINTM, DISC, INTRATE, RECEIVED, PRICEDISC |
| PR | Draft, unmerged |
| Target còn thiếu | Tính lại bằng catalog audit trước mỗi batch |

Bảng này được cập nhật sau mỗi batch năm hàm; không duy trì một phần trăm giả dựa trên số lượng checkbox.

## 8. Điều kiện kết thúc toàn bộ chương trình formula

Chỉ được tuyên bố “đủ các hàm” khi đồng thời thỏa:

1. `Pending = 0` cho snapshot Microsoft và OpenFormula đã khóa;
2. không còn duplicate identity/alias hoặc registration path song song;
3. compatibility aliases có differential tests;
4. external/cube/data-type functions có provider contract, fake-provider tests và fail-closed offline path;
5. parser hỗ trợ toàn bộ cú pháp cần cho LET/LAMBDA/dynamic references;
6. resource budgets khóa mọi range, array, recursion, solver và external request;
7. external Excel/LibreOffice/ODS differential corpus đạt ngưỡng đã công bố;
8. fuzzing không còn crash/hang/uncaught exception;
9. documentation, function catalog và SDK metadata đồng bộ;
10. exact-head full hosted CI xanh;
11. Codex final acceptance không còn blocker;
12. PR chỉ được chuyển khỏi Draft sau một lượt audit cuối riêng biệt.

## 9. Quy tắc tiếp tục phiên

Khi công việc được tiếp tục, không cần chọn family hay nhắc lại lịch. Quy trình mặc định là:

```text
đọc exact head
→ kiểm tra CI mới nhất
→ refresh Target/Implemented/Pending
→ lấy 5 Pending đầu tiên
→ triển khai + test + docs
→ exact-head CI
→ báo một bảng tiến độ
→ khóa next five
```

Chat không thể tự phát một tin nhắn mới khi không có lượt hội thoại đang hoạt động; vì vậy trạng thái và hàng đợi được ghi trong repository để lần tiếp tục kế tiếp tự khởi động đúng batch, không cần người dùng chỉ định lại.
