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
- Một authoritative eager built-in aggregation path.

## 3. Invocation

Invocation arguments giữ scalar values hoặc range source identity, shape và row-major values. Unsupported scalar/range/array combinations bị từ chối trước evaluator. Argument counting dùng logical hoặc flattened policy theo metadata.

Engine capture range dependency trước invocation. Reference-aware AST functions và dynamic-array functions dùng engine-owned paths vì cần lazy evaluation hoặc spill ownership; chúng không tạo registry thứ hai.

## 4. Built-in milestone

Eager/versioned registry chứa **239 names**:

- 92 original flattened-value functions;
- 11 Statistical Foundation functions;
- 39 Advanced Statistical functions;
- 56 financial functions;
- 19 engineering functions;
- 12 database functions;
- 9 date/business-calendar/locale functions ngoài financial count;
- 1 F008 eager reference-text function: `ADDRESS`.

Broader subsystem bổ sung **20 AST/reference-aware** và **7 dynamic-array** names, tổng **266 built-ins**.

F008 metadata:

- `ADDRESS` scalar-only, deterministic/pure, scalar-returning và logical-argument-counted.
- `AREAS`/`CHOOSE` nằm trong AST/reference-aware engine vì cần reference identity/lazy branch.
- `CHOOSECOLS`/`CHOOSEROWS` nằm trong dynamic engine vì cần array shape và spill ownership.
- Các path này dùng cùng parser, dependency model và public function-name namespace; không đăng ký duplicate eager descriptors.

## 5. Failure và resource policy

Registration reject incompatible API, unsupported capabilities, invalid bounds, conflicts, duplicate exact versions without replacement và disallowed external state. Evaluation reject unsupported argument kinds. Family implementations trả explicit spreadsheet errors cho invalid domains, budgets hoặc non-finite results.

F008 caps:

- CHOOSE value arguments: 254;
- projection output: 1.000.000 cells;
- ADDRESS row/column: worksheet limits;
- unsupported reference union as value: fail closed.

## 6. Shared services và test counts

- `FinancialDateMath`, `BusinessDayCalendarMath` và reference-selection helpers là internal shared services, không phải registry thứ hai.
- Formula count regressions đọc `BuiltInFormulaTestCounts.EagerVersioned`; F008 tăng shared eager count từ 238 lên 239.
- Complete count được báo theo unique public names: 239 eager + 20 AST/reference-aware + 7 dynamic = 266.

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
