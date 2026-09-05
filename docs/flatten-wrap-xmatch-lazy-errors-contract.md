# F012 flatten, wrap, XMATCH and lazy-error contract

## Public functions

`TOCOL`, `TOROW`, `TRIMRANGE`, `VSTACK`, `WRAPCOLS`, `WRAPROWS`, `XMATCH`, `IFERROR`, `IFNA`, `SWITCH`.

## Array shaping

- `TOCOL` and `TOROW` support ignore modes 0–3 and row-major or column-major scanning.
- `TRIMRANGE` trims selected leading/trailing blank rows and columns; an empty result fails closed with `#CALC!`.
- `VSTACK` preserves source order and pads narrower arrays with `#N/A`.
- `WRAPCOLS` and `WRAPROWS` accept one-dimensional vectors, truncate wrap counts toward zero and use `#N/A` padding unless a custom value is supplied.
- Every produced array remains rectangular and is capped at 1,000,000 cells.

## Lookup and lazy errors

- `XMATCH` supports exact, next-smaller, next-larger and wildcard modes plus forward/reverse and binary-mode contracts.
- `IFERROR` evaluates the fallback only when the primary result is an error.
- `IFNA` evaluates the fallback only for `#N/A`.
- `SWITCH` evaluates only matching result branches and the default branch when required.
- Array-aware error replacement uses scalar broadcasting or exact-shape replacement; incompatible shapes return `#VALUE!`.

## Validation

F012 adds ten regression methods and raises the formula suite to 264/264. Build, analyzers, architecture verification and exact-head hosted CI must all remain green before the batch is marked complete.
