# Hyperbolic, combinatorics and integer math contract (F014)

F014 adds `ATANH`, `SINH`, `COSH`, `TANH`, `COMBIN`, `COMBINA`, `FACT`, `FACTDOUBLE`, `GCD` and `LCM` as pure deterministic eager/versioned built-ins registered only through `StandardFormulaFunctions.CreateAll()`.

## Hyperbolic functions

- `ATANH` accepts only `-1 < number < 1`; endpoints and values outside the interval return `#NUM!`.
- `SINH`, `COSH` and `TANH` use deterministic platform-neutral double-precision math.
- Any non-finite numerical result returns `#NUM!`; unsupported coercion returns `#VALUE!`.

## Combinatorics and factorials

- Numeric arguments are truncated toward zero after the original value is checked for negativity and finiteness.
- `COMBIN` and `COMBINA` require non-negative inputs and `number >= number_chosen`.
- Combination evaluation uses a multiplicative algorithm with a maximum of 1,000,000 iterations and checked overflow.
- `FACT` accepts inputs through 170; `FACTDOUBLE` accepts inputs through 300. Larger finite inputs return `#NUM!`.

## GCD and LCM

- `GCD` and `LCM` accept from 1 through 255 flattened scalar/range values.
- Each value is non-negative, finite and truncated toward zero.
- Values at or above `2^53` return `#NUM!`, because they cannot be represented as exact consecutive integers in the host number type.
- `LCM` also returns `#NUM!` when the result reaches or exceeds `2^53`.
- Zero follows spreadsheet identities: `GCD(x,0)=x` and `LCM(x,0)=0`, while every argument is still validated.

All functions reuse the shared coercion and spreadsheet-error model; no WPF, WinForms or MAUI-specific implementation exists.
