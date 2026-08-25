# Function Extension SDK v1.0 contract

## Registry và invocation

- Identity/version/API/capability/state/security/dependency/conflict contracts.
- Thread-safe registration, version resolution, replacement và unregister fallback.
- Range arguments giữ source identity và shape.
- Một authoritative eager built-in aggregation path.
- AST/reference-aware và dynamic paths dùng cùng public namespace, không tạo registry thứ hai.

## Current counts

- Eager/versioned registry: **239 names**.
- AST/reference-aware: **23 names**.
- Dynamic-array unique: **9 names**.
- Complete built-ins: **271 names**.
- Locked target: **tối thiểu 538 names**.

F009 không tăng eager registry. `COLUMN`, `COLUMNS`, `FORMULATEXT` cần reference/current-cell metadata; `DROP`, `EXPAND` cần spill ownership và array shape, nên thuộc engine-owned paths.

## Resource policy

- Formula text: tối đa 8.192 ký tự.
- Array output: tối đa 1.000.000 cells.
- Unsupported reference/array combinations fail closed.
- External state vẫn bị chặn cho tới khi có manifest, trust, isolation, timeout và audit contracts.

## Pending

Plugin discovery/loading, publisher trust, isolation/quotas, packaging/API compatibility, third-party spill integration và external-state permission/audit.

PR #1 giữ Draft trong khi exact-head CI mới nhất đỏ hoặc unknown.
