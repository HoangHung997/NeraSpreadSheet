# NeraSpreadSheet roadmap

Project progress is tracked with the fixed weighted rubric in [`docs/project-progress.md`](docs/project-progress.md). The current roadmap implementation score is **79.84%**, reported as **80%**.

- [x] Sparse workbook, editing, dependency calculation and structural transforms.
- [x] Fractional scrolling and WPF/WinForms/MAUI renderer hosts.
- [x] XLSX preservation, Tables/AutoFilter, printing and PDF foundations.
- [x] Formula implementation closed: **546 / 546 locked catalog names** across F001-F019; catalog is considered complete unless a future compatibility audit intentionally reopens it.
- [x] Q001 differential/fuzz hardening foundation: locked scalar corpus, deterministic arithmetic oracle fuzz, dependency-model fuzz and malformed-formula crash fuzz.
- [x] Q002 workbook/editing state-model fuzz plus OpenXML round-trip differential corpus.
- [x] Q003A analytics foundation + shared vector rendering: chart/pivot models, session editing/Undo-Redo and shared DisplayList rendering.
- [ ] **Q003B ACTIVE** — floating analytics interaction layer. Desktop, MAUI Windows, Android and Mac Catalyst native accessibility gates are implemented. The only bounded gap is loaded iOS/VoiceOver runtime validation of the real native accessibility container.
- [ ] Persist charts/drawings/pivots and related placement metadata through workbook/OpenXML packages after Q003B interaction semantics are stable.
- [x] Modular Ribbon/Bars customization, JSON persistence, command presentation/runtime, native WPF/WinForms presenters, customization dialogs and keyboard shortcuts.
- [ ] Complete `RIBBON-MAUI` presenter, customization/input mapping and loaded runtime smoke without introducing per-cell controls.
- [ ] Expand differential/visual corpora across rendering, printing and file compatibility.
- [ ] Packaging/API compatibility and Function Extension SDK distribution.
- [ ] Plugin trust/isolation, security hardening and recovery.
- [ ] Localization and accessibility completion beyond the Q003B analytics-native bridge.
- [ ] Final acceptance/evidence/release hardening.
