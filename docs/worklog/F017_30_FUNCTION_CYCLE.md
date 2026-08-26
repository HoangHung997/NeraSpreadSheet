# F017 — 30-function cycle

## Scope

F017 is the first cycle under the 30-function process: A/B/C contain ten new names each, and each group must pass its own CLI gate before the next group starts.

## Group gates

- A: analyzer-clean formula build, 10/10 group tests, 374/374 formula tests.
- B: analyzer-clean formula build, 10/10 group tests, 384/384 formula tests.
- C: analyzer-clean formula build; one incorrect Z-test expectation was isolated by exact method name and corrected; 10/10 group tests and 394/394 formula tests passed.

## Final counters

- Eager/versioned: 342 → 372.
- Total built-ins: 396 → 426.
- Formula tests: 364 → 394.

## Final gate

Local final gate passed: Core build 0 warnings/errors, 955/955 Core-solution tests and `scripts/verify-architecture.ps1`. Push A/B/C together, run one exact-head CI and keep PR #1 Draft and unmerged.
