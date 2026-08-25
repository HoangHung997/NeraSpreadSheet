# Dynamic Arrays Foundation contract

## Architecture

- `FormulaArrayValue` là immutable rectangular row-major result.
- `NeraDynamicArrayFormulaEngine` đánh giá supported array functions.
- `DynamicArrayWorkbookCalculationEngine` quản lý spill ownership, dependency và bounded stabilization.
- Host UI không triển khai lại spill semantics.

Complete subsystem hiện có **271 names**: 239 eager/versioned, 23 AST/reference-aware và 9 dynamic-array unique names.

## Safety

- Shape phải dương và rectangular.
- Output tối đa 1.000.000 cells.
- Spill collision trả `#SPILL!`.
- Stabilization tối đa 8 passes.
- Source/shape/padding dependencies được capture chính xác.

## Supported dynamic functions

`SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE`, `CHOOSECOLS`, `CHOOSEROWS`, `DROP`, `EXPAND`.

`CHOOSE` và `COLUMN` có dynamic spill bridge nhưng được tính trong AST/reference-aware family để tránh duplicate public names.

## F009 shaping semantics

- `DROP(array,rows,[columns])`: dương bỏ đầu, âm bỏ cuối; omitted dimension giữ nguyên; zero/xóa hết trả `#CALC!`.
- `EXPAND(array,rows,[columns],[pad_with])`: target phải không nhỏ hơn source; default pad `#N/A`; blank/missing dimension dùng source dimension; cap 1.000.000 cells.
- `COLUMN(reference)` có thể phát horizontal vector cho multi-column reference.
- Output changes trigger dependent-only recalculation.

## Pending

`A1#`, `@`, array constants, HSTACK/VSTACK, TAKE, TOCOL/TOROW, wrap families, LET/LAMBDA, native spill UX, broad differential/fuzz corpus và array-returning extensions.

PR #1 remains Draft while newer exact-head CI is red or unknown.
