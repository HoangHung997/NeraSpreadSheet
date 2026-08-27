# NeraSpreadSheet current implementation status

## Overall roadmap implementation progress

The fixed weighted roadmap rubric in [`project-progress.md`](project-progress.md) currently evaluates to **79.84%**, reported as **80%**. This is an implementation-roadmap score, not a claim that NeraSpreadSheet implements 80% of every Microsoft Excel feature and not a production-readiness percentage.

## Formula subsystem — closed

| Counter | Value |
|---|---:|
| Eager/versioned | 468 |
| AST/reference-aware | 40 |
| Dynamic-array unique | 38 |
| **Total** | **546 / 546 locked catalog names** |
| Formula/hardening tests | **518/518** |
| Completed formula cycles | F001-F019 |

The formula catalog is considered complete. New names are added only if a compatibility audit supplies concrete evidence worth reopening the catalog.

## Q001 — differential and fuzz hardening — DONE

Q001 locked deterministic scalar, arithmetic, dependency and malformed-input fuzz gates. Exact-head CI **#924** passed.

## Q002 — workbook/editing + OpenXML differential hardening — DONE

Q002 added deterministic `SpreadsheetSession` state-model fuzz, structural row/column model fuzz with atomic boundary rejection, and sparse/extreme OpenXML save-load-save differential corpora while retaining unknown-part/package preservation gates. Exact-head CI **#932** passed.

## Q003A — analytics foundation + shared vector rendering — DONE

Q003A added chart models/projection (Column, Bar, Line, Pie), pivot models/projection (Sum, Count, Average, Minimum, Maximum), per-worksheet analytics editing with Undo/Redo, shared DisplayList analytics rendering and common polygon rendering across Skia/Direct2D/WPF/WinForms. Exact-head CI **#958** passed.

## Q003B — floating analytics interaction — ACTIVE

Implementation checkpoint: `35f96669eb79eb798cffbb17d42a07967db4ca54`.
Exact-head CI **#1025 — success**.

Implemented and validated:

- host-neutral analytics placement state and viewport mapping;
- floating chart/pivot overlay composition;
- select, move and resize interaction with shared Undo/Redo commit semantics;
- normalized keyboard intent/editing and accessibility projection model;
- WPF and WinForms pointer/keyboard host integration;
- MAUI primary-touch ownership with secondary-touch isolation;
- scroll/freeze/split integration and deterministic interaction tests;
- Windows desktop analytics interaction smoke;
- loaded MAUI Windows analytics smoke covering touch movement, selection, accessibility metadata, Undo/Redo and input-state isolation;
- synchronized placement-map access so GPU rendering may safely read snapshots while UI input commits placement changes.

The loaded MAUI analytics gate exposed a smoke-harness reentrancy defect rather than a transform-commit defect. Immediate post-release validation proved the placement commit was correct. The smoke then performed Undo/Redo inside `PaintSurface`; invalidations could start another completion validator while the first validator was intentionally in the undone state. Completion validation is now single-flight, so reentrant frames cannot interpret that transient Undo state as a failed commit.

### Current exact-head validation at the Q003B checkpoint

- build/analyzers: **0 warnings, 0 errors**;
- complete Core solution: **1150/1150 passed**;
- Core: **110/110**;
- Editing: **209/209**;
- Interaction: **20/20**;
- Rendering.Spreadsheet: **118/118**;
- Rendering.Skia: **14/14**;
- Viewport: **56/56**;
- Formulas: **518/518**;
- OpenXML: **56/56**;
- architecture verification: **passed**;
- Windows hosts + desktop GPU runtime smoke: **passed**;
- MAUI Android: **passed**;
- MAUI iOS + Mac Catalyst: **passed**;
- MAUI Windows build + handler-resolution + loaded Table-filter/runtime/analytics/scale smokes: **passed**.

### Remaining Q003B work

The bounded remaining gap is native accessibility exposure, not the host-neutral accessibility model itself:

- WPF AutomationPeer/native child exposure for floating analytics items;
- WinForms AccessibleObject/native child exposure;
- MAUI platform-native semantics/accessibility bridge and host smoke coverage.

Chart/drawing/pivot workbook/OpenXML persistence remains deliberately deferred until the Q003B interaction layer is closed.
