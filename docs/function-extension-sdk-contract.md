# Function Extension SDK v1.0 contract

- Eager/versioned registry: 242 names.
- AST/reference-aware: 34 names.
- Dynamic-array unique: 20 names.
- Total built-ins: **296 / at least 538**.

F012 does not add eager registry entries. Its ten names remain engine-owned because they require lazy error branches, lookup-array identity, array shape or spill ownership. No parallel registry is introduced.
