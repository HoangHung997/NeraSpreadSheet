# NeraSpreadSheet roadmap

Project progress is tracked with the fixed weighted rubric in [`docs/project-progress.md`](docs/project-progress.md). The current roadmap implementation score is **79.84%**, reported as **80%**.

- [x] Sparse workbook, editing, dependency calculation and structural transforms.
- [x] Fractional scrolling and WPF/WinForms/MAUI renderer hosts.
- [x] XLSX preservation, Tables/AutoFilter, printing and PDF foundations.
- [x] Formula implementation closed: **546 / 546 locked catalog names** across F001-F019; catalog is considered complete unless a future compatibility audit intentionally reopens it.
- [x] Q001 differential/fuzz hardening foundation: locked scalar corpus, deterministic arithmetic oracle fuzz, dependency-model fuzz and malformed-formula crash fuzz.
- [x] Q002 workbook/editing state-model fuzz plus OpenXML round-trip differential corpus.
- [x] Q003A analytics foundation + shared vector rendering: chart/pivot models, session editing/Undo-Redo and shared DisplayList rendering.
- [ ] **Q003B ACTIVE** — floating analytics interaction layer. Placement, viewport mapping, overlay rendering, select/move/resize, normalized keyboard, MAUI touch routing, split/freeze integration and host smoke gates are implemented. Remaining bounded work is native platform accessibility exposure for WPF, WinForms and MAUI.
- [ ] Persist charts/drawings/pivots and related placement metadata through workbook/OpenXML packages after Q003B interaction semantics are stable.
- [ ] Expand differential/visual corpora across rendering, printing and file compatibility.
- [ ] Packaging/API compatibility and Function Extension SDK distribution.
- [ ] Plugin trust/isolation, security hardening and recovery.
- [ ] Localization and accessibility completion beyond the Q003B analytics-native bridge.
- [ ] Final acceptance/evidence/release hardening.
