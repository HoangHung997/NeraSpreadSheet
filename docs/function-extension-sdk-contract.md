# Function Extension SDK v1.0 contract

Tài liệu này định nghĩa validated versioned function-extension contract của NeraSpreadSheet.

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

Invocation arguments giữ scalar values hoặc range source identity, shape và row-major values. Unsupported scalar/range/array combinations bị reject trước evaluator execution. Argument counting là logical hoặc flattened-value theo metadata.

## 4. Built-in milestone

Eager/versioned registry có **233 names**:

- 92 original flattened-value functions;
- 11 Statistical Foundation functions;
- 39 Advanced Statistical functions;
- 56 financial functions;
- 4 F006 date-compatibility functions;
- 19 engineering functions;
- 12 database functions.

Broader subsystem thêm 18 AST/reference-aware và 5 dynamic-array names, tổng **256 built-ins**.

F006 SDK metadata:

- `ODDLYIELD`, `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM` là scalar-only.
- Tất cả deterministic/pure, scalar-returning và logical-argument-counted.
- Không function nào đọc clock, file, network hoặc external state.
- Date-only normalization và day/week semantics nằm trong formula layer, không nằm trong host UI.

Financial SDK metadata:

- `NPV`, `IRR`, `XNPV`, `XIRR`, `FVSCHEDULE`, `MIRR` expose range capability.
- Current financial functions còn lại scalar-only.
- Tất cả current financial descriptors deterministic/pure và scalar-returning.
- `FVSCHEDULE` và `MIRR` dùng engine-captured dependencies; security/calendar/date functions không có hidden dependency.

## 5. Failure policy

Registration reject incompatible APIs, unsupported capabilities, invalid bounds, conflicts, duplicate exact versions without replacement và disallowed external state. Evaluation reject unsupported argument kinds. Family implementations trả explicit spreadsheet errors cho invalid domains, budgets hoặc non-finite results.

## 6. Shared services và test counts

`FinancialDateMath` là internal platform-neutral service được coupon/security built-ins tái sử dụng, không phải registry thứ hai. Date compatibility là một family đăng ký qua cùng aggregation path. Formula registry-count regressions đọc `BuiltInFormulaTestCounts.EagerVersioned`, nên mỗi batch chỉ cập nhật một authoritative constant.

## 7. Pending

- Plugin manifests, discovery/loading/unloading.
- Publisher signatures và trust policy.
- Third-party isolation và quotas.
- Formula-text version pinning.
- NuGet/plugin packaging và compatibility tooling.
- Third-party array return/spill integration.
- External-state permission prompts và auditing.

## 8. Gates

SDK changes yêu cầu version ordering, resolution, conflict/replacement/unregister behavior, API/capability/security rejection, range identity, dependency policy, legacy adaptation và shared built-in count regressions, sau đó là complete hosted matrix.

PR #1 giữ Draft khi exact-head CI red hoặc unknown.
