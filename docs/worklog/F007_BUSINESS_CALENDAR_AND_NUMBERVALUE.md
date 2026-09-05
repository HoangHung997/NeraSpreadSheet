# F007 — Business Calendar and NUMBERVALUE

## Scope

Exactly five new public functions:

1. `NETWORKDAYS`
2. `NETWORKDAYS.INTL`
3. `WORKDAY`
4. `WORKDAY.INTL`
5. `NUMBERVALUE`

## Implementation

- Initial commit: `acc65b46b4aa1d729baacb7768960a1dbecc66e5`.
- Weekend/separator hardening: `7bc9d78c24f172c7c014d510d65e73be9ac4fd0c`.
- Exact analyzer-clean implementation head: `95748373b9dde1f0faffe2c61d2ad1262cff7532`.
- Families/services: `BusinessCalendarFormulaFunctions`, `BusinessDayCalendarMath`, `BusinessWeekendMask`, `IFormulaLocaleEvaluationContext`.
- Registration vẫn qua `StandardFormulaFunctions.CreateAll()`.
- Registry count: 233 → 238 eager/versioned names.
- Complete built-ins: 256 → 261.
- Financial functions remain 56.

## Regression table

| Function | Validation | Result |
|---|---|---|
| NETWORKDAYS | Inclusive 22-day reference và signed reverse -21 | Pass |
| NETWORKDAYS.INTL | Numeric weekend code, seven-character mask, all-weekend zero | Pass |
| WORKDAY | Published 151-day references, holiday range, negative và zero offsets | Pass |
| WORKDAY.INTL | Single-day weekend code references | Pass |
| NUMBERVALUE | Explicit/context separators, whitespace, multi-character separator và percent suffixes | Pass |
| Dependencies | Holiday range source retained exactly | Pass |
| Domains | Invalid weekend, masks, dates, offsets, holidays, separators và range misuse | Pass |
| Metadata | Identity/version/API/capability/volatility/security | Pass |
| Registry | 238 eager/versioned names | Pass |
| Formula suite | 229/229 | Pass |
| Hosted matrix | CI #878 | Pass |

## Calendar contract decisions

- Numeric weekend code truncate rồi phải thuộc `1..7` hoặc `11..17`.
- Weekend string có đúng bảy `0/1` values theo Monday→Sunday.
- NETWORKDAYS counts inclusive và reversed intervals đổi dấu.
- WORKDAY days truncate toward zero; zero trả start date.
- Blank holidays bị bỏ qua; duplicates deduplicate; weekend holidays không bị trừ lại.
- Holiday range dependencies do formula engine capture từ source identity.
- Holiday values capped at 2.000.000.
- Counting dùng whole-week arithmetic; shifting dùng bounded binary search, không per-day traversal.

## NUMBERVALUE contract decisions

- Explicit separator dùng ký tự đầu tiên.
- Omitted separators đọc `IFormulaLocaleEvaluationContext`; fallback invariant `.`/`,`.
- Whitespace bị bỏ qua.
- Multiple decimal separators hoặc group separator sau decimal trả `#VALUE!`.
- Multiple trailing `%` được áp dụng lũy tiến.
- Empty text trả 0.
- Text capped at 1.000.000 characters.
- Descriptor scalar-only, deterministic và `ContextReadOnly`.

## Validation

CI #878, run `32824453543`, passed:

- zero-warning/zero-error Core build;
- 229/229 formula tests và toàn bộ Core solution tests;
- architecture verification;
- Windows desktop build/tests và GPU runtime smoke;
- Android, iOS và Mac Catalyst builds;
- MAUI Windows build, handler resolution và loaded Table-filter/runtime/scale smokes.

## Next batch

F008: `ADDRESS`, `AREAS`, `CHOOSE`, `CHOOSECOLS`, `CHOOSEROWS`.
