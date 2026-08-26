# NeraSpreadSheet feature matrix

| Area | Current validated capability | Next |
|---|---|---|
| Workbook/editing | Excel-size sparse sheets, structural transforms, selection, clipboard and Undo/Redo | axis grouping and native spill UX |
| Formula surface | **426 / at least 538 functions**, 394 separately named formula tests; 30-name A/B/C cycles with per-group CLI gates | F018 after duplicate/catalog audit |
| Math | arithmetic, logs, trigonometry, hyperbolic, combinatorics, rounding, vector math, radix and Roman conversions | matrix and remaining advanced math catalog |
| Statistics | descriptive/order statistics, regression, advanced and discrete distributions, confidence, F/Z tests, modern and legacy compatibility | remaining compatibility and differential corpus |
| Engineering | 45 names including fixed-width conversions, bit operations and complex-number functions | `CONVERT` and remaining engineering special functions |
| Information | parity and non-text predicates plus existing information foundation | remaining information and error-introspection functions |
| Reference | ADDRESS/AREAS/CHOOSE/COLUMN/COLUMNS/FORMULATEXT/INDIRECT/OFFSET/ROW/ROWS/SHEET/SHEETS | 3-D references and full algebra |
| Dynamic arrays | 20 unique names including TOCOL/TOROW/TRIMRANGE/VSTACK/WRAPCOLS/WRAPROWS | remaining array and higher-order families |
| Lookup/logical | LOOKUP/XMATCH plus lazy IF/IFERROR/IFNA/SWITCH paths | advanced lookup and LET/LAMBDA |
| Finance | 56 functions | differential/fuzz corpus |
| Database | 12 aggregates | expression criteria/indexing |
| Rendering | fractional scrolling and WPF/WinForms/MAUI GPU hosts | hardware budgets/accessibility |
| XLSX/print/PDF | preservation, pagination, preview and PDF | charts/drawings/full metadata |
| Hardening | architecture and hosted CI gates | packaging, isolation, security and recovery |
