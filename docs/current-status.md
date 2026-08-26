# NeraSpreadSheet current implementation status

## Formula snapshot

| Counter | Value |
|---|---:|
| Eager/versioned | 282 |
| AST/reference-aware | 34 |
| Dynamic-array unique | 20 |
| **Total** | **336 / at least 538** |
| Tests | 304/304 |
| Complete batches | F001–F015 |
| Batch size from F015 | 20 new names |

F015 adds `MROUND`, `CEILING`, `FLOOR`, `CEILING.PRECISE`, `FLOOR.PRECISE`, `ISO.CEILING`, `MULTINOMIAL`, `SERIESSUM`, `SUMPRODUCT`, `SQRTPI`, `SUMX2MY2`, `SUMX2PY2`, `SUMXMY2`, `BASE`, `DECIMAL`, `ARABIC`, `ROMAN`, `ISEVEN`, `ISODD` and `ISNONTEXT`.

`SUMSQ` and `PRODUCT` were already implemented before F015, so they are excluded from the new-name count. Logical range shape is preserved for `SUMPRODUCT`, `SERIESSUM` and pairwise array functions; bounded value counts and exact-integer limits fail closed with spreadsheet errors.

The workbook/editing, dependency graph, rules/Tables/AutoFilter, WPF/WinForms/MAUI render hosts, XLSX preservation and print/PDF foundations remain validated. Production blockers still include catalog breadth, charts/pivots UI, packaging/API compatibility, plugin trust/isolation, security/fuzzing, recovery, localization/accessibility and broad differential/visual corpora.

Full F015 contract: `docs/advanced-math-compatibility-contract.md`.

Next: F016 with 20 new names selected after duplicate and catalog audit.
