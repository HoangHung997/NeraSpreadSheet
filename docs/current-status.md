# NeraSpreadSheet current implementation status

## Formula subsystem — closed

| Counter | Value |
|---|---:|
| Eager/versioned | 468 |
| AST/reference-aware | 40 |
| Dynamic-array unique | 38 |
| **Total** | **546 / 546 locked catalog names** |
| Formula regressions before hardening | 514/514 |
| Formula/hardening tests after Q001 | 518/518 |
| Completed formula cycles | F001-F019 |

The formula catalog is now considered complete. New formula names are no longer a standing roadmap item; they may be added only if a future compatibility audit identifies a concrete missing name that is worth reopening the catalog for.

## Q001 — differential and fuzz hardening foundation

Q001 adds four deterministic hardening gates without adding any formula functions:

- checked-in scalar differential corpus with stable case IDs and exact expected outcomes;
- 1,000 seeded generated arithmetic expressions compared with an independent reference oracle;
- 250 seeded cell-reference expressions compared with an independent value/dependency model;
- 2,000 seeded malformed formulas that must never escape as ordinary unhandled exceptions.

Local validation after Q001:

- formula/hardening tests: **518/518**;
- complete Core solution: **1079/1079**;
- architecture verification: **passed**;
- analyzer-clean build: **0 warnings, 0 errors**.

Workbook/editing, dependency graph, Tables/AutoFilter, WPF/WinForms/MAUI hosts, XLSX preservation and print/PDF foundations remain validated. The next work item is Q002: workbook/editing state-model fuzzing and OpenXML round-trip differential corpus. Production blockers after that remain charts/pivots UI, packaging/API compatibility, plugin trust/isolation, security/recovery, localization/accessibility and broad visual corpora.
