# Worksheet AutoFilter foundation validation

- Candidate implementation commit: `847d076bb352169c89f1b6dd6912ad0c13d43293`.
- Scope: rich Table predicates, paged filter-value snapshots, Worksheet AutoFilter Core state, structural mapping, filtered-row projection and production history controller.
- Validation policy: this candidate is not promoted in `docs/current-status.md` until an exact-head pull-request CI run executes all Core, Windows and MAUI jobs successfully.
- PR #1 remains Draft and must not merge while exact-head CI is red, action-required or unknown.
- Next implementation after validation: standard worksheet `autoFilter` XLSX import/export, copy-and-patch preservation and native asynchronous page binding.
