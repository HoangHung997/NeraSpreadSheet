# Function Extension SDK v1.0 contract

Tài liệu này định nghĩa versioned function-extension contract đã được validate của NeraSpreadSheet.

## 1. Descriptor contract

Mỗi function khai báo namespace/name identity, implementation version, minimum host API, aliases, logical argument bounds, capabilities, volatility/state, security, dependency policy và argument-error policy. Current host API: `1.0`.

## 2. Registry behavior

- Thread-safe registration và lookup.
- Exact và highest-compatible version resolution.
- Side-by-side versions, explicit replacement và unregister fallback.
- Global name/alias conflict rejection.
- Legacy `IFormulaFunction` adaptation.
- Một authoritative built-in aggregation path.

## 3. Invocation

Invocation arguments giữ scalar values hoặc range source identity, shape và row-major values. Unsupported scalar/range/array combinations bị từ chối trước evaluator. Argument counting dùng logical hoặc flattened policy theo metadata.

Engine capture range dependency trước invocation. F007 sử dụng contract này để holiday ranges trong NETWORKDAYS/WORKDAY tham gia affected-only recalculation mà không khai báo hidden dependency.

## 4. Built-in milestone

Eager/versioned registry chứa **238 names**:

- 92 original flattened-value functions;
- 11 Statistical Foundation functions;
- 39 Advanced Statistical functions;
- 56 financial functions;
- 19 engineering functions;
- 12 database functions;
- 9 later date/business-calendar/locale functions ngoài financial count.

Broader subsystem bổ sung 18 AST/reference-aware và 5 dynamic-array names, tổng **261 built-ins**.

F007 metadata:

- `NETWORKDAYS`, `NETWORKDAYS.INTL`, `WORKDAY`, `WORKDAY.INTL` expose scalar + range capability, deterministic/pure, scalar-returning và logical-argument-counted.
- Range capability chỉ dành cho holiday argument; evaluator vẫn enforce scalar start/end/days/weekend.
- `NUMBERVALUE` scalar-only, deterministic, scalar-returning, logical-argument-counted và `ContextReadOnly`.
- `IFormulaLocaleEvaluationContext` là optional deterministic context contract; không phải external-state permission.

## 5. Failure và resource policy

Registration reject incompatible API, unsupported capabilities, invalid bounds, conflicts, duplicate exact versions without replacement và disallowed external state. Evaluation reject unsupported argument kinds. Family implementations trả explicit spreadsheet errors cho invalid domains, budgets hoặc non-finite results.

F007 caps:

- holiday arguments: 2.000.000 values;
- NUMBERVALUE text: 1.000.000 characters;
- business-day shift: bounded by DateTime domain và logarithmic search iterations.

## 6. Shared services và test counts

- `FinancialDateMath` là internal shared financial service, không phải registry thứ hai.
- `BusinessDayCalendarMath` là internal shared calendar service, không phải registry thứ hai.
- Formula count regressions đọc `BuiltInFormulaTestCounts.EagerVersioned`; mỗi batch cập nhật một hằng authoritative.

## 7. Pending

- Plugin manifests, discovery/loading/unloading.
- Publisher signatures và trust policy.
- Third-party isolation và quotas.
- Formula-text version pinning.
- NuGet/plugin packaging và compatibility tooling.
- Third-party array return/spill integration.
- External-state permission prompts và auditing.

## 8. Gates

SDK changes yêu cầu version ordering, resolution, conflict/replacement/unregister, API/capability/security rejection, range identity, dependency policy, legacy adaptation và shared built-in count regressions, sau đó là complete hosted matrix.

PR #1 giữ Draft trong khi exact-head CI mới nhất đỏ hoặc unknown.
