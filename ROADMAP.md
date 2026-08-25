# NeraSpreadSheet roadmap

`docs/current-status.md` là nguồn sự thật implementation. Một capability chỉ hoàn thành khi có source thực thi, automated tests và gate phù hợp.

## Engine và workbook

- [x] Excel-size sparse workbook, editing, clipboard, Undo/Redo và atomic structural transforms.
- [x] Fractional scrolling, freeze/split panes và WPF/WinForms/MAUI GPU hosts.
- [ ] Hide/group/outline metadata, complete axis properties và native spill UX.

## Formula engine và SDK

- [x] Parser/AST, dependency graph, shared/structured formulas và affected-only recalculation.
- [x] Function Extension SDK API `1.0` và một authoritative eager registry path.
- [x] F001–F009.
- [ ] Remaining lookup/reference/projection.
- [ ] LET/LAMBDA, higher-order arrays và recursion budgets.
- [ ] Full text/regex/byte-width, special engineering và legacy aliases.
- [ ] External/cube/data-type providers, trust/isolation và catalog delta = zero.

## Reference và dynamic arrays

- [x] Missing arguments, reference unions, lazy `CHOOSE`, selected-range identity.
- [x] `COLUMN`, `COLUMNS`, `FORMULATEXT` với current-cell/formula metadata context.
- [x] `DROP`/`EXPAND` với shape, padding, spill và giới hạn 1.000.000 cells.
- [ ] Intersection, `A1#`, `@`, array constants và full reference-return algebra.

## Product hardening

- [x] Exact-head multi-platform CI và architecture verification.
- [ ] Packaging/API compatibility, plugin isolation, security/fuzzing, recovery và release gates.
- [ ] Charts, pivots/slicers, localization, accessibility và visual differential corpus.

## Formula completion execution order

1. F001–F009: **Complete**.
2. **F010 next:** `GETPIVOTDATA`, `GROUPBY`, `HSTACK`, `HYPERLINK`, `INDIRECT`.
3. Tiếp tục dependency pools, rồi Microsoft/OpenFormula catalog audit, differential corpus, fuzzing và final acceptance.

**Tổng số hàm: 271 / tối thiểu 538 hàm mục tiêu hiện đã khóa.**

## Weighted progress after F009

- Engine/viewport/renderer foundation: khoảng `92%`.
- Basic spreadsheet MVP: khoảng `97–98%`.
- Complete professional roadmap: khoảng `85–86%`.
- Production release readiness: khoảng `63–66%`.

## Validation rule

F009 implementation head `bb332e65291776fea05e52ce8433db9e6b1ac810` build zero warnings/errors, qua architecture verification và **239/239 formula tests** trong CI #882. PR #1 giữ Draft và chưa merge; public milestone chỉ khóa sau documentation exact-head CI xanh.
