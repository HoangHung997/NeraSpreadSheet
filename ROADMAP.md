# NeraSpreadSheet roadmap

Project progress is tracked with the fixed weighted rubric in [`docs/project-progress.md`](docs/project-progress.md). The current roadmap implementation score is **83.08%**, reported as **83%**.

- [x] Sparse workbook, editing, dependency calculation and structural transforms.
- [x] Fractional scrolling and WPF/WinForms/MAUI renderer hosts.
- [x] XLSX preservation, Tables/AutoFilter, printing and PDF foundations.
- [x] Formula implementation closed: **546 / 546 locked catalog names** across F001-F019; catalog is considered complete unless a future compatibility audit intentionally reopens it.
- [x] Q001 differential/fuzz hardening foundation: locked scalar corpus, deterministic arithmetic oracle fuzz, dependency-model fuzz and malformed-formula crash fuzz.
- [x] Q002 workbook/editing state-model fuzz plus OpenXML round-trip differential corpus.
- [x] Q003A analytics foundation + shared vector rendering: chart/pivot models, session editing/Undo-Redo and shared DisplayList rendering.
- [x] Q003B floating analytics interaction + native accessibility: desktop, MAUI Windows, Android, iOS and Mac Catalyst runtime gates are closed.
- [x] Q003C managed analytics/chart OpenXML persistence: session metadata and standard managed chart/drawing materialization are locked by round-trip tests.
- [x] Q003D standard Excel PivotTable/PivotCache **package preservation**: an existing schema-valid standard pivot graph survives repeated preserved session save cycles without being silently claimed as a Nera-managed pivot.
- [ ] Extend pivot interoperability beyond preservation: standard pivot creation from Nera pivots, semantic import, cache records, refresh/calculation equivalence, destination-cell modeling and slicers/timelines.
- [x] Modular Ribbon/Bars customization, JSON persistence, command presentation/runtime, native WPF/WinForms presenters, customization dialogs and keyboard shortcuts.
- [ ] Complete `RIBBON-MAUI` presenter, customization/input mapping and loaded runtime smoke without introducing per-cell controls.
- [ ] Expand drawing/media and differential/visual corpora across rendering, printing and file compatibility.
- [ ] Packaging/API compatibility and Function Extension SDK distribution.
- [ ] Plugin trust/isolation, security hardening and recovery.
- [ ] Localization and accessibility completion beyond the Q003B analytics-native bridge.
- [ ] Final acceptance/evidence/release hardening.
