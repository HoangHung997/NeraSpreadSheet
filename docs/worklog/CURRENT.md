# Current Work Handoff

- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` into `develop` — Draft, unmerged
- F007 exact implementation head: `95748373b9dde1f0faffe2c61d2ad1262cff7532`
- F007 implementation CI: #878, run `32824453543` — success
- Formula tests: `229/229`
- Eager/versioned built-ins: `238`
- AST/reference-aware built-ins: `18`
- Dynamic-array built-ins: `5`
- Complete built-ins: `261`
- Financial functions: `56`
- Source of truth: `docs/current-status.md`
- Master schedule: `docs/formula-completion-master-schedule.md`

## F007 — business calendar và locale-number parsing

| Function | Result | Status |
|---|---|---|
| `NETWORKDAYS` | Inclusive default-weekend business-day count | Complete |
| `NETWORKDAYS.INTL` | Signed count với weekend code/mask | Complete |
| `WORKDAY` | Default-weekend business-day shifting | Complete |
| `WORKDAY.INTL` | Custom-weekend business-day shifting | Complete |
| `NUMBERVALUE` | Explicit/context locale number parsing | Complete |

Key gates:

- Published inclusive/signed NETWORKDAYS references.
- Numeric weekend codes, Monday-first masks và all-weekend behavior.
- Positive/negative/zero WORKDAY references và holiday exclusions.
- Holiday duplicate/weekend/blank normalization.
- Exact holiday range dependency capture.
- NUMBERVALUE explicit/context separators, whitespace, multi-character separators và repeated percent suffixes.
- 2.000.000 holiday-value cap, 1.000.000-character text cap và DateTime-domain bounded shifting.
- Build zero warnings/errors, 229/229 formula tests và architecture verification.
- CI #878 exact implementation head passed the complete hosted matrix.

## Whole-project snapshot

- Sparse workbook, editing, structural transforms, rules, Tables/AutoFilter và Undo/Redo foundations complete.
- Parser/AST/dependency graph, SDK API 1.0, 261 built-ins và first-generation dynamic arrays validated.
- Fractional pixel scrolling, WPF/WinForms/MAUI GPU hosts, XLSX preservation, streaming text, pagination và PDF validated.
- Major production blockers remain formula/catalog breadth, charts/pivots, packaging/API policy, plugin trust/isolation, security/fuzzing, recovery, localization/accessibility và broad differential/visual corpora.

## Documentation/handoff gate

This handoff commit synchronizes README, roadmap, current status, feature matrix, formula/SDK contracts, master schedule and F007 worklog. Public F007 completion requires the exact documentation-head hosted CI to remain green.

## Next five — F008

1. `ADDRESS`.
2. `AREAS`.
3. `CHOOSE`.
4. `CHOOSECOLS`.
5. `CHOOSEROWS`.

F008 must lock reference identity, selection laziness, negative indices, array shape propagation, spill behavior and dependency capture. PR remains Draft; do not merge while a newer exact-head CI is red or unknown.
