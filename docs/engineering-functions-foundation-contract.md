# Engineering Functions Foundation contract

This document defines the validated first-generation engineering-function behavior of NeraSpreadSheet. Excel and LibreOffice are compatibility references only and are not runtime dependencies.

## 1. Architecture boundary

- Engineering functions are deterministic/pure SDK v1 functions.
- Namespace: `NERA.BUILTIN`; implementation version: `1.0.0`; host API: `1.0`.
- Scalar arguments only, scalar result, logical argument counting.
- Range arguments are rejected by capability validation rather than flattened.
- Parsing and output are culture-neutral.
- UI hosts and OpenXml adapters do not implement engineering semantics.

## 2. Supported functions

### Comparison helpers

- `DELTA(number1, [number2])` returns `1` when finite coerced values are equal, otherwise `0`; default second value is `0`.
- `GESTEP(number, [step])` returns `1` when `number >= step`, otherwise `0`; default step is `0`.

### Bit operations

- `BITAND`, `BITOR`, `BITXOR`;
- `BITLSHIFT`, `BITRSHIFT`.

Inputs are truncated toward zero and must be within `0..2^48-1`. Shift magnitude is bounded to `53`. Negative shift amounts reverse direction. Left-shift overflow above `2^48-1` returns `#NUM!`.

### Decimal to base

- `DEC2BIN`, `DEC2OCT`, `DEC2HEX`.

Numbers are truncated toward zero. Positive values use optional `places` from `1` through `10`. Negative values use full fixed-width two's-complement output:

| Function | Width | Signed range |
|---|---:|---:|
| `DEC2BIN` | 10 bits / 10 digits | `-512..511` |
| `DEC2OCT` | 30 bits / 10 digits | `-536870912..536870911` |
| `DEC2HEX` | 40 bits / 10 digits | `-549755813888..549755813887` |

### Base to decimal

- `BIN2DEC`, `OCT2DEC`, `HEX2DEC`.

Input may be text or a scalar coercible to a finite integer string. Maximum length is ten digits. A maximum-width representation with the sign bit set is interpreted as a signed two's-complement value.

### Cross-base conversion

- `BIN2OCT`, `BIN2HEX`;
- `OCT2BIN`, `OCT2HEX`;
- `HEX2BIN`, `HEX2OCT`.

The source is parsed under its fixed-width signed convention, then validated against the target's signed range. Positive output may use `places`; negative output always uses the target's full ten-digit representation.

## 3. Coercion and errors

- Shared scalar finite numeric coercion applies where supported.
- Bit/decimal inputs are truncated toward zero.
- Invalid characters, excessive digits, out-of-range values, invalid `places`, excessive shifts and shift overflow return `#NUM!`.
- Unsupported argument kind, nonnumeric scalar or arity mismatch returns `#VALUE!` through engine/SDK validation.
- Argument formula errors propagate before invocation.

## 4. Determinism and dependencies

All functions are deterministic and pure. Scalar cell references are captured by the formula engine. These functions declare no hidden dependencies and read no clock, filesystem, network or external state.

## 5. Resource policy

- No work proportional to worksheet size.
- Base strings are limited to ten digits.
- Bit width and shift magnitude are explicitly bounded.
- Checked arithmetic fails closed on overflow.
- Output is one finite number or text no longer than ten characters.

## 6. Deliberately pending

- Complex-number functions.
- `CONVERT` and a versioned unit catalog.
- Bessel, error and complementary-error functions.
- Locale-specific digit/grouping acceptance.
- Complete external producer coercion differences.
- Differential Excel/LibreOffice corpus and boundary fuzzing.

## 7. Validation gates

Promotion requires descriptor identity/version/capability tests; DELTA/GESTEP tests; bit-width, shift and overflow tests; signed conversion/places/cross-base tests; scalar dependency regressions; and the complete Core, architecture, Windows and MAUI matrix.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
