# ADR 0006 — NeraSpreadSheet is an independent spreadsheet product model

## Decision

NeraSpreadSheet defines its own public API, command identifiers, workbook model, calculation engine, rendering contracts and UI schema.

Excel, LibreOffice, DevExpress and other spreadsheet products may be used only as external behavioral or feature references during QA. They are not runtime dependencies and their public command identifiers must not become NeraSpreadSheet public contracts.

## Consequences

- No `.uno:*` identifiers in the Nera public command API.
- No dependency on Microsoft Office automation, LibreOffice UNO or DevExpress assemblies in Core modules.
- Compatibility adapters, if ever required, live outside Core and translate into Nera-native contracts.
- Missing spreadsheet capabilities are implemented with Nera-native naming and behavior contracts.
- Cross-platform hosts remain adapters over the same Nera workbook/interaction/rendering model.
