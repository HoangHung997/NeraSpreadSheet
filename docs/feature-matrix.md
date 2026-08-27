# NeraSpreadSheet feature matrix

| Area | Current validated capability | Next |
|---|---|---|
| Workbook/editing | Excel-size sparse sheets, structural transforms, selection, clipboard and Undo/Redo | Q002 state-model fuzz; axis grouping and native spill UX |
| Formula surface | **546 / 546 locked catalog names**; catalog closed | compatibility audit only when evidence warrants reopening |
| Formula hardening | Q001 locked scalar corpus + deterministic arithmetic/dependency/malformed-input fuzz | extend corpus to workbook/OpenXML and cross-engine reference files |
| Math/statistics/engineering | locked catalog coverage with deterministic regression suite | differential corpus expansion only |
| Reference/lookup | reference-aware AST, 3-D-sensitive infrastructure, INDEX/MATCH/XLOOKUP/HLOOKUP/VLOOKUP and advanced references | broader 3-D/algebra corpus |
| Dynamic arrays | 38 unique names including higher-order arrays, matrix/statistical spills | native spill UX and visual regression corpus |
| Finance/database | broad financial/database coverage | differential/fuzz corpus |
| Rendering | fractional scrolling and WPF/WinForms/MAUI GPU hosts | hardware budgets, visual corpus and accessibility |
| XLSX/print/PDF | preservation, pagination, preview and PDF | Q002 OpenXML round-trip corpus; charts/drawings/full metadata |
| Hardening | architecture + hosted CI gates; Q001 deterministic fuzz foundation | security fuzz, packaging, isolation and recovery |
