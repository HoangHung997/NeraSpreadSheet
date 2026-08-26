# F015 Advanced Math Compatibility Contract

## Scope

F015 contains exactly twenty new built-in names:

`MROUND`, `CEILING`, `FLOOR`, `CEILING.PRECISE`, `FLOOR.PRECISE`, `ISO.CEILING`, `MULTINOMIAL`, `SERIESSUM`, `SUMPRODUCT`, `SQRTPI`, `SUMX2MY2`, `SUMX2PY2`, `SUMXMY2`, `BASE`, `DECIMAL`, `ARABIC`, `ROMAN`, `ISEVEN`, `ISODD`, `ISNONTEXT`.

`SUMSQ` and `PRODUCT` existed before this batch and are deliberately excluded from the new-name count.

## Architecture

- All names remain platform-neutral and are aggregated only by `StandardFormulaFunctions.CreateAll()`.
- Scalar functions use the shared factory/coercion/error path.
- `MULTINOMIAL`, `SERIESSUM`, `SUMPRODUCT` and the `SUMX*` family use logical arguments so ranges retain source identity and shape.
- No WPF, WinForms or MAUI implementation is introduced.

## Compatibility rules

- `MROUND` requires number and multiple to have the same sign unless either is zero.
- Legacy `CEILING`/`FLOOR` preserve same-sign behavior; precise/ISO variants use the absolute significance.
- `MULTINOMIAL` truncates non-negative inputs and fails closed when the finite result cannot be represented.
- `SUMPRODUCT` requires equal logical argument lengths; non-numeric range entries contribute zero.
- Pairwise `SUMX*` functions require equal shapes and ignore non-numeric range entries consistently.
- `BASE`/`DECIMAL` support radix 2 through 36; `BASE` supports deterministic zero padding.
- `ROMAN` supports forms 0 through 4. `ARABIC` accepts supported classical and simplified forms; `ARABIC("IIII")` is 4 while invalid `IIV` returns `#VALUE!`.
- `ISEVEN` and `ISODD` truncate fractional inputs. `ISNONTEXT` returns a Boolean even when its argument is an error.

## Resource and numerical boundaries

- Logical value traversal: maximum 1,000,000 values.
- Radix/Roman text: maximum 255 characters.
- Exact integer conversion/parity boundary: 2^53−1.
- Kahan accumulation is used for series, products and pairwise sums.
- Invalid domains, overflow and unsupported shapes return spreadsheet error values rather than throwing.

## Regression gate

F015 contributes twenty test methods. Completion requires 304/304 formula tests, zero build/analyzer warnings or errors, architecture verification and all hosted platform jobs green on the exact implementation and documentation heads.
