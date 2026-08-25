# Function Extension SDK v1.0 contract

- Eager/versioned registry: 242 names.
- AST/reference-aware: 30 names.
- Dynamic-array unique: 14 names.
- Total built-ins: **286 / at least 538**.

F011 adds eager `LOOKUP` and `PERCENTOF`. The remaining F011 names stay engine-owned because they require lazy references, current workbook metadata or spill shape. No parallel registry is introduced.
