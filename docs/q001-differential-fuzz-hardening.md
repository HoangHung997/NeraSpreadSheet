# Q001 — Differential and fuzz hardening foundation

Q001 starts the post-formula phase. It deliberately adds no formula names.

## Locked gates

1. `TestData/differential-scalar-v1.json`
   - checked-in stable corpus;
   - case IDs survive refactors;
   - exact number/boolean/text/error expectations.
2. `SeededArithmeticDifferentialFuzz_OneThousandExpressionsMatchIndependentOracle`
   - seed `0x4E455241`;
   - 1,000 generated expressions;
   - independent generator/oracle computes expected values without invoking NeraSpreadSheet evaluation.
3. `SeededDependencyFuzz_RandomCellExpressionsMatchReferenceValuesAndDependencies`
   - seed `0x4450465A`;
   - 250 generated reference expressions;
   - compares both numeric result and dependency-address set.
4. `MalformedFormulaFuzz_TwoThousandInputsNeverEscapeAsUnhandledExceptions`
   - seed `0x46555A5A`;
   - 2,000 malformed inputs;
   - any ordinary unhandled exception is a deterministic regression with seed/case/formula in the assertion message.

## Local evidence

- Formula/hardening suite: **518/518 passed**.
- Core solution: **1079/1079 passed**.
- Architecture verification: **passed**.
- Analyzer warnings/errors: **0/0**.

## Next — Q002

Build a reference-model fuzz harness for workbook/editing mutations and an OpenXML round-trip corpus. Q002 should validate structural transforms, Undo/Redo invariants, sparse-state preservation, shared strings/styles/formulas, unknown-part preservation and deterministic save-load-save behavior.
