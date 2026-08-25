# Formula Surface I contract

Tài liệu này định nghĩa validated scalar/reference formula behavior.

## Architecture boundary

- Parser/AST sở hữu syntax, missing arguments và reference unions.
- `NeraFormulaEngine` sở hữu scalar/reference evaluation, lazy branches, dependencies và errors.
- `NeraDynamicArrayFormulaEngine` sở hữu array shape, projection và spill results.
- `IFormulaReferenceIntrospectionContext` cung cấp current-cell/formula metadata mà không buộc host UI triển khai semantics.
- Eager built-ins chỉ đi qua `StandardFormulaFunctions.CreateAll()`.

## Counts

- Eager/versioned: **239**.
- AST/reference-aware: **23**.
- Scalar/reference total: **262**.
- Dynamic-array unique names: **9**.
- Complete subsystem: **271 names**.
- Locked target: **tối thiểu 538 names**.

## F009 behavior

- `COLUMN()` dùng current formula-cell column; reference nhiều cột có horizontal spill.
- `COLUMNS` đọc shape của scalar/reference/dynamic array.
- `FORMULATEXT` đọc exact formula metadata, hỗ trợ selected reference và self-reference.
- Static geometry không tạo value dependency.
- `FORMULATEXT` tạo exact target dependency để affected recalculation cập nhật đúng.
- `DROP`/`EXPAND` dùng dynamic engine và existing spill ownership.

Full contract: `docs/reference-introspection-and-array-shaping-contract.md`.

## Errors và budgets

- Invalid reference/coercion/shape trả `#VALUE!`.
- DROP zero hoặc xóa toàn bộ dimension trả `#CALC!`.
- FORMULATEXT target không có formula hoặc unavailable trả `#N/A`.
- Array output trên 1.000.000 cells trả `#NUM!`.
- Formula text được giới hạn 8.192 ký tự.

## Pending

F010: `GETPIVOTDATA`, `GROUPBY`, `HSTACK`, `HYPERLINK`, `INDIRECT`; sau đó remaining reference/projection, LET/LAMBDA, full text/statistics/engineering/compatibility/external providers.

PR #1 giữ Draft khi exact-head CI mới nhất đỏ hoặc unknown.
