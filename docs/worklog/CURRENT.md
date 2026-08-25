# Current Work Handoff

- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` into `develop` — Draft, unmerged
- F008 exact implementation head: `775a24dfa2fa9dc059896d5445179077b4ffe641`
- F008 implementation CI: #880, run `32831433700` — success
- Formula tests: `234/234`
- Eager/versioned built-ins: `239`
- AST/reference-aware built-ins: `20`
- Dynamic-array built-ins: `7`
- Complete built-ins: `266`
- Financial functions: `56`
- Source of truth: `docs/current-status.md`
- Master schedule: `docs/formula-completion-master-schedule.md`

## F008 — reference selection và array projection

| Function | Result | Status |
|---|---|---|
| `ADDRESS` | A1/R1C1 reference text với abs modes và sheet prefix | Complete |
| `AREAS` | Static/union/CHOOSE-selected reference area count | Complete |
| `CHOOSE` | Lazy scalar/reference selection và selected-range identity | Complete |
| `CHOOSECOLS` | Ordered/duplicate/negative column projection | Complete |
| `CHOOSEROWS` | Ordered/duplicate/negative row projection | Complete |

Key gates:

- ADDRESS A1/R1C1, abs modes, missing arguments, quoted sheets và worksheet bounds.
- Parser missing-argument node và parenthesized reference-union node.
- AREAS static geometry without value dependencies.
- AREAS(CHOOSE(...)) selector-only dependency behavior.
- CHOOSE fractional truncation, lazy unselected branches, selected scalar/range dependency và dynamic spill bridge.
- CHOOSECOLS/CHOOSEROWS scalar/range/dynamic indexes, negative indices, duplicates và requested ordering.
- Projection output cap 1.000.000 cells.
- Build zero warnings/errors, 234/234 formula tests và architecture verification.
- CI #880 exact implementation head passed the complete hosted matrix.

## Whole-project snapshot

- Sparse workbook, editing, structural transforms, rules, Tables/AutoFilter và Undo/Redo foundations complete.
- Parser/AST/dependency graph, SDK API 1.0, 266 built-ins và seven dynamic-array names validated.
- Fractional pixel scrolling, WPF/WinForms/MAUI GPU hosts, XLSX preservation, streaming text, pagination và PDF validated.
- Major production blockers remain formula/catalog breadth, charts/pivots, packaging/API policy, plugin trust/isolation, security/fuzzing, recovery, localization/accessibility và broad differential/visual corpora.

## Documentation/handoff gate

This handoff commit synchronizes README, roadmap, current status, feature matrix, reference/dynamic/SDK contracts, master schedule and F008 worklog. Public F008 completion requires the exact documentation-head hosted CI to remain green.

## Next five — F009

1. `COLUMN`.
2. `COLUMNS`.
3. `DROP`.
4. `EXPAND`.
5. `FORMULATEXT`.

F009 must lock current-cell context, reference shape/introspection, formula-text access, DROP negative/zero semantics, EXPAND shape/padding and dependency capture. PR remains Draft; do not merge while a newer exact-head CI is red or unknown.
