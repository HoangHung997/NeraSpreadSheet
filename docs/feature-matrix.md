# NeraSpreadSheet feature matrix

| Area | Current validated capability | Next |
|---|---|---|
| Workbook/editing | Excel-size sparse sheets, structural transforms, selection, clipboard, Undo/Redo; Q002 deterministic cell/history and structural state-model fuzz | axis grouping and richer native spill UX |
| Formula surface | **546 / 546 locked catalog names**; catalog closed | compatibility audit only when evidence warrants reopening |
| Formula hardening | Q001 locked scalar corpus + deterministic arithmetic/dependency/malformed-input fuzz | broaden cross-engine/reference-file corpus |
| Math/statistics/engineering | locked catalog coverage with deterministic regression suite | differential corpus expansion only |
| Reference/lookup | reference-aware AST, 3-D-sensitive infrastructure, INDEX/MATCH/XLOOKUP/HLOOKUP/VLOOKUP and advanced references | broader 3-D/algebra corpus |
| Dynamic arrays | 38 unique names including higher-order arrays, matrix/statistical spills | native spill UX and visual regression corpus |
| Finance/database | broad financial/database coverage | differential/fuzz corpus expansion |
| Rendering/hosts | fractional scrolling; WPF/WinForms/MAUI GPU hosts; Q003A shared analytics vectors; Q003B floating overlay/select/move/resize and desktop/MAUI host interaction gates | native analytics accessibility bridge, visual corpus and hardware budgets |
| Analytics | Q003A chart/pivot foundation; Q003B placement, viewport mapping, pointer/keyboard/touch, split/freeze integration and shared accessibility projection | native WPF/WinForms/MAUI accessibility exposure, then workbook/OpenXML drawing/chart/pivot persistence |
| XLSX/print/PDF | unknown-part preservation, Q002 sparse/extreme round-trip differential corpus, pagination, preview and PDF | charts/drawings/pivot metadata persistence and broader reference-file corpus |
| Tables/filters | Tables/AutoFilter model, native/paged presenters and loaded MAUI Table-filter smoke | broader native UX/persistence edge corpus |
| Hardening | architecture + hosted CI gates; Q001/Q002 deterministic fuzz; exact-head multi-host GPU/runtime/analytics smokes | security fuzz, packaging/isolation/recovery and performance budgets |
| Packaging/API | source/build surface and Function Extension SDK foundations | distribution/versioning/API compatibility gates |
| Localization/accessibility | shared analytics accessibility model and existing semantic metadata | native analytics bridge plus broader localization/accessibility completion |
