# NeraSpreadSheet feature matrix

| Area | Current validated capability | Next |
|---|---|---|
| Workbook/editing | Excel-size sparse sheets, structural transforms, selection, clipboard and Undo/Redo | axis grouping and native spill UX |
| Formula surface | **486 / at least 538 functions**, 454 separately named formula tests; F018 used 60 names in A/B/C with per-group CLI gates | remaining catalog delta + final Microsoft/OpenFormula audit |
| Math | arithmetic, logs, trig/hyperbolic, combinatorics, rounding, vector math, radix/Roman, error functions, determinant and volatile random functions | remaining matrix/advanced math catalog |
| Statistics | descriptive/order statistics, regression, distributions, confidence, F/Z/chi-square/t tests, legacy compatibility | differential corpus and remaining delta |
| Engineering | complex functions, fixed-width conversions, bit operations, `CONVERT`, ERF/ERFC and Bessel surface | remaining engineering special functions |
| Information | parity/non-text plus `CELL`, `ERROR.TYPE`, `ISFORMULA`, `ISREF`, `TYPE` | remaining metadata/data-type functions |
| Reference/lookup | ADDRESS/AREAS/CHOOSE/COLUMN/COLUMNS/FORMULATEXT/INDIRECT/OFFSET/ROW/ROWS/SHEET/SHEETS plus INDEX/MATCH/XLOOKUP/HLOOKUP/VLOOKUP | 3-D references and full algebra |
| Dynamic arrays | 22 unique names including MUNIT and FREQUENCY | higher-order and remaining projection families |
| Lookup/logical | LOOKUP/XMATCH/XLOOKUP plus lazy IF/IFERROR/IFNA/SWITCH paths | LET/LAMBDA and remaining advanced lookup |
| Finance | 56 functions | differential/fuzz corpus |
| Database | 12 aggregates | expression criteria/indexing |
| Rendering | fractional scrolling and WPF/WinForms/MAUI GPU hosts | hardware budgets/accessibility |
| XLSX/print/PDF | preservation, pagination, preview and PDF | charts/drawings/full metadata |
| Hardening | architecture and hosted CI gates | packaging, isolation, security and recovery |
