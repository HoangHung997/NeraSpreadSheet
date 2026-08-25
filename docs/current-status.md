# NeraSpreadSheet current implementation status

Tài liệu này là nguồn sự thật cho nhánh phát triển hiện tại.

## Product rules

- SDK độc lập, không phụ thuộc runtime Excel, LibreOffice hoặc DevExpress.
- Formula/reference/dynamic-array semantics platform-neutral.
- Parser sở hữu syntax; scalar engine sở hữu reference identity/laziness; dynamic engine sở hữu shape/spill.
- Mọi traversal, projection và resource-sensitive path đều bounded và fail closed.

## Formula snapshot

| Chỉ số | Giá trị |
|---|---:|
| Eager/versioned built-ins | **239** |
| AST/reference-aware | **23** |
| Dynamic-array unique names | **9** |
| Tổng built-ins | **271** |
| Formula tests | **239/239** |
| Financial functions | **56** |
| Batch hoàn thành | **F001–F009** |

**Tổng số hàm: 271 / tối thiểu 538 hàm mục tiêu hiện đã khóa.** P11 Microsoft/OpenFormula delta có thể tăng mục tiêu.

## F009 — reference introspection và array shaping

- `COLUMN` — không đối số dùng current formula cell; reference nhiều cột trả horizontal spill; static geometry không đọc cell values.
- `COLUMNS` — trả số cột của scalar/reference/dynamic array và giữ lazy `CHOOSE`.
- `DROP` — bỏ hàng/cột đầu hoặc cuối bằng chỉ số dương/âm; hỗ trợ omitted dimension; zero hoặc bỏ hết dimension trả `#CALC!`.
- `EXPAND` — mở rộng shape, mặc định pad `#N/A`, hỗ trợ custom pad; target nhỏ hơn source trả `#VALUE!`; output trên 1.000.000 cells trả `#NUM!`.
- `FORMULATEXT` — đọc exact formula metadata của top-left reference, hỗ trợ lazy selected reference và self-reference; target không có formula trả `#N/A`.

Full contract: `docs/reference-introspection-and-array-shaping-contract.md`.

## Whole-project snapshot

- Sparse workbook, editing, structural transforms, CF, validation, Tables/AutoFilter và Undo/Redo đã có.
- Formula engine, Function SDK, 271 built-ins và nine dynamic-array names đã có automated gates.
- Fractional scrolling, WPF/WinForms/MAUI GPU hosts, XLSX preservation, streaming text, printing/PDF đã có.
- Production blockers còn gồm formula/catalog breadth, charts/pivots, packaging/API policy, plugin trust/isolation, security/fuzzing, recovery, localization/accessibility và differential/visual corpora.

## Next implementation work

F010: `GETPIVOTDATA`, `GROUPBY`, `HSTACK`, `HYPERLINK`, `INDIRECT`.

## Validation state

F009 exact implementation head `bb332e65291776fea05e52ce8433db9e6b1ac810` qua CI #882 với zero warnings/errors, architecture verification và **239/239 formula tests**. PR #1 vẫn Draft và chưa merge.
