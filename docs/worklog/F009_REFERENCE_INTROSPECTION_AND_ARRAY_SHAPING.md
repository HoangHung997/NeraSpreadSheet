# F009 — Reference Introspection and Array Shaping

| Function | Result | Status |
|---|---|---|
| `COLUMN` | Current-cell/static reference/horizontal spill column identity | Complete |
| `COLUMNS` | Scalar/reference/dynamic-array column shape | Complete |
| `DROP` | Positive/negative/omitted dimension shaping | Complete |
| `EXPAND` | Shape expansion, default/custom padding và budget | Complete |
| `FORMULATEXT` | Exact formula metadata, lazy reference và self-reference | Complete |

- Implementation head: `bb332e65291776fea05e52ce8433db9e6b1ac810`.
- CI #882: success.
- Formula tests: **239/239**.
- Build: **0 warnings, 0 errors**.
- Counts: 239 eager + 23 AST/reference-aware + 9 dynamic = **271**.
- **Tổng số hàm: 271 / tối thiểu 538 hàm mục tiêu hiện đã khóa.**
- Next: F010 — `GETPIVOTDATA`, `GROUPBY`, `HSTACK`, `HYPERLINK`, `INDIRECT`.
