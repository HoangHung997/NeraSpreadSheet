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

Current implementation checkpoint: `1c700aaf7b73113d3cbbe8e8c6093bdc3fce404d`.
Exact-head CI run **33170632141 — success**.

Implemented and validated:

- host-neutral analytics placement state and viewport mapping;
- floating chart/pivot overlay composition;
- select, move and resize interaction with shared Undo/Redo commit semantics;
- normalized keyboard intent/editing and host-neutral accessibility projection model;
- WPF and WinForms pointer/keyboard host integration;
- WPF native `AutomationPeer` children for floating analytics items;
- WinForms native `AccessibleObject` children for floating analytics items;
- Windows desktop native-accessibility smoke coverage while preserving editor accessibility children;
- MAUI primary-touch ownership with secondary-touch isolation;
- MAUI view-level semantic summary/hint projection without introducing per-cell controls;
- MAUI Windows native UI Automation children layered over the existing `SKSwapChainPanel`, with stable Name, AutomationId, control role, set metadata, visible bounds and Invoke pattern exposure;
- loaded MAUI Windows native-accessibility probe that verifies the real WinUI/UIA child without taking ownership of the analytics interaction sequence;
- scroll/freeze/split integration and deterministic interaction tests;
- Windows desktop analytics interaction smoke;
- loaded MAUI Windows analytics smoke covering touch movement, selection, accessibility metadata, Undo/Redo and input-state isolation;
- synchronized placement-map access so GPU rendering may safely read snapshots while UI input commits placement changes;
- MAUI Windows scale smoke proving the accessibility layer does not disturb the logical-viewport/backing-pixel scale contract.
- Android per-item native accessibility exposure with a loaded analytics accessibility smoke;
- a shared iOS/Mac Catalyst virtual `UIAccessibilityElement` bridge over the GPU-backed view;
- loaded Mac Catalyst native-accessibility smoke covering chart/pivot children, metadata, bounds and activation-to-selection behavior.

The MAUI accessibility bridge is attached per `NeraSpreadsheetView` instance rather than through a static handler mapper. This keeps `UseNeraSpreadSheet()` handler-neutral and preserves the headless handler-resolution test contract. The native accessibility probe is deliberately observational: it verifies the native UI Automation contract but does not invoke the child from `PaintSurface`, because doing so would mutate selection before the loaded analytics smoke begins its own touch sequence.

### Current exact-head validation at the Q003B checkpoint

- build/analyzers: **0 warnings, 0 errors**;
- complete Core solution: **1153/1153 passed**;
- Core: **110/110**;
- Editing: **209/209**;
- Interaction: **20/20**;
- Rendering.Spreadsheet: **118/118**;
- Rendering.Skia: **14/14**;
- Viewport: **56/56**;
- Formulas: **518/518**;
- OpenXML: **59/59**;
- architecture verification: **passed**;
- Windows hosts + desktop GPU runtime smoke: **passed**;
- MAUI Android build + loaded analytics native-accessibility smoke: **passed**;
- MAUI iOS build: **passed**;
- MAUI Mac Catalyst build + loaded analytics native-accessibility smoke: **passed**;
- MAUI Windows build: **passed**;
- MAUI Windows handler-resolution tests: **29/29 passed**;
- loaded MAUI Windows Table-filter/runtime/analytics/scale smokes: **passed**;
- loaded MAUI Windows native analytics accessibility probe: **passed** as part of the analytics smoke.

### Remaining Q003B work

Android/TalkBack and Mac Catalyst/VoiceOver runtime gates are closed. The only bounded Q003B gap is a loaded iOS/VoiceOver runtime validation of the real native `UIView` accessibility container and its virtual chart/pivot children. A build-only iOS check and the shared Mac Catalyst runtime evidence do not substitute for that gate. Q003B therefore remains **ACTIVE** and the weighted roadmap score remains unchanged.

The branch also contains provisional analytics/drawing OpenXML persistence work with green tests. It is not used to claim Q003B completion and must not be expanded until the iOS runtime gate is resolved.

## Ribbon and Bars SDK — desktop stack integrated

Implementation checkpoint: `716db8c4765e5259a71fd304e624942fa6ae73d8`.
Combined exact-head CI run **33230558230 — success**.

Implemented and validated:

- immutable Ribbon/Bars customization for visibility, ordering and Ribbon item size;
- deterministic versioned JSON persistence with legacy-v0 migration and bounded input validation;
- command-state presentation snapshots and runtime controllers using the shared command dispatcher;
- native WPF and WinForms Ribbon, toolbar, menu and context-menu presenters;
- native WPF and WinForms customization dialogs with apply, reset, save and load flows;
- normalized shortcut resolution and WPF/WinForms keyboard bindings;
- loaded desktop smoke coverage for presentation, activation, state refresh, customization and shortcuts.

The controls are modular and can be embedded independently of `NeraSpreadsheetControl`. They do not create controls per spreadsheet cell and do not own workbook, calculation or rendering state.

Remaining Ribbon work is `RIBBON-MAUI`: MAUI presentation, customization UI/input mapping and loaded runtime smoke. PR #1 remains Draft and unmerged.
