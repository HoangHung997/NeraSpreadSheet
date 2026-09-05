# Business Calendar and NUMBERVALUE contract

Tài liệu này định nghĩa behavior đã được validate cho F007. Excel và LibreOffice là compatibility references; NeraSpreadSheet không phụ thuộc runtime của các sản phẩm đó.

## 1. Architecture boundary

- `BusinessDayCalendarMath` sở hữu business-day counting và shifting platform-neutral.
- `BusinessWeekendMask` sở hữu weekend codes và Monday-first mask semantics.
- `IFormulaLocaleEvaluationContext` cung cấp deterministic decimal/group defaults cho locale-sensitive functions.
- `StandardFormulaFunctions.CreateAll()` vẫn là built-in aggregation path duy nhất.
- Holiday range dependencies được formula engine capture từ source range trước khi evaluator chạy.
- Không host UI nào được tự triển khai calendar hoặc locale-number semantics.

## 2. Public functions

```text
NETWORKDAYS(start_date,end_date,[holidays])
NETWORKDAYS.INTL(start_date,end_date,[weekend],[holidays])
WORKDAY(start_date,days,[holidays])
WORKDAY.INTL(start_date,days,[weekend],[holidays])
NUMBERVALUE(text,[decimal_separator],[group_separator])
```

Tất cả dùng namespace `NERA.BUILTIN`, implementation version `1.0.0`, API `1.0`, logical argument counting và scalar return.

Calendar functions expose range capability chỉ để nhận holiday range; start/end/days/weekend vẫn phải là scalar. `NUMBERVALUE` scalar-only và security classification `ContextReadOnly`.

## 3. Weekend contract

Numeric weekend value được truncate toward zero rồi phải thuộc:

```text
1..7   = seven adjacent two-day weekend patterns
11..17 = seven single-day weekend patterns
```

String weekend mask có đúng bảy ký tự `0` hoặc `1`, theo thứ tự:

```text
Monday Tuesday Wednesday Thursday Friday Saturday Sunday
```

- `1` nghĩa là weekend/non-workday.
- `0000011` tương đương Saturday/Sunday.
- `1111111` hợp lệ cho `NETWORKDAYS.INTL` và tạo 0 workdays.
- `1111111` bị từ chối cho `WORKDAY.INTL` vì không tồn tại ngày có thể đạt target.
- Numeric code ngoài domain trả `#NUM!`; malformed string mask trả `#VALUE!`.

## 4. Holiday contract

- Holiday argument có thể là scalar hoặc range.
- DateTime, finite OLE Automation serial và invariant date text được chấp nhận.
- Blank/empty holiday values bị bỏ qua.
- Duplicate dates được deduplicate.
- Holiday rơi vào configured weekend không bị trừ lần hai.
- Holiday ranges giữ source dependency và tham gia affected-only recalculation.
- Tối đa 2.000.000 holiday values; vượt giới hạn trả `#NUM!`.
- Invalid nonblank holiday value trả `#VALUE!`; out-of-range serial trả `#NUM!`.

## 5. NETWORKDAYS contract

NETWORKDAYS và NETWORKDAYS.INTL đếm inclusive cả start và end nếu ngày đó là workday.

```text
result = workdays in ordered inclusive interval - unique workday holidays
```

Nếu start lớn hơn end, hệ thống đảo interval và đổi dấu kết quả. Counting dùng whole-week arithmetic cộng tối đa sáu remainder days, không quét toàn bộ khoảng ngày.

## 6. WORKDAY contract

- `days` được truncate toward zero.
- `days = 0` trả start date nguyên ngày.
- Positive days tìm workday sau start; negative days tìm workday trước start.
- Weekend và holiday đều bị loại.
- Shifting dùng bounded binary search trên khoảng ngày và week-based count; không lặp một lần cho mỗi calendar day.
- Date/result ngoài `DateTime` domain hoặc offset không biểu diễn được trả `#NUM!`.

## 7. NUMBERVALUE contract

Khi separator được truyền, chỉ ký tự đầu tiên tham gia parse. Khi bị bỏ qua:

1. đọc `IFormulaLocaleEvaluationContext.DecimalSeparator` và `GroupSeparator` nếu context cung cấp;
2. nếu không, dùng invariant `.` decimal và `,` group.

Rules:

- whitespace bị bỏ qua, kể cả giữa các chữ số;
- tối đa một decimal separator;
- group separators trước decimal bị bỏ qua;
- group separator sau decimal trả `#VALUE!`;
- decimal và group separator giống nhau trả `#VALUE!`;
- trailing `%` được áp dụng lũy tiến: một dấu chia 100, hai dấu chia 10.000;
- percent nằm giữa text hoặc text không parse được trả `#VALUE!`;
- empty text trả 0;
- text dài hơn 1.000.000 ký tự trả `#NUM!`;
- non-finite result trả `#NUM!`.

## 8. Automated validation

F007 promotion yêu cầu:

1. inclusive, signed và custom-weekend NETWORKDAYS references;
2. positive, negative, zero và international WORKDAY references;
3. holiday duplicate/weekend/blank handling và exact range dependency;
4. explicit, multi-character, context-default, whitespace và percent NUMBERVALUE cases;
5. invalid codes/masks/separators, range misuse, out-of-range dates và resource limits;
6. descriptor/capability/security tests;
7. registry count 238 eager/versioned và formula suite 229/229;
8. exact-head Core/architecture/Windows/Android/iOS/Mac Catalyst/MAUI Windows matrix.

## 9. Deliberately pending

- broader locale and holiday differential corpus;
- provider-backed regional holiday calendars;
- advanced lookup/reference and array-selection F008;
- final Microsoft/OpenFormula catalog audit.
