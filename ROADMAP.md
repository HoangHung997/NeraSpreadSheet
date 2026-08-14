# Roadmap NeraSpreadSheet

## M0 — Bootstrap kiến trúc

- [x] Khóa module và dependency direction.
- [x] Workbook sparse tối thiểu.
- [x] Metric index cho hàng/cột kích thước biến đổi.
- [x] Continuous pixel scroll controller.
- [x] Display-list contract.
- [x] Host shell WPF, WinForms và MAUI.
- [x] Command, Ribbon Core, Bars Core và DataGrid Core contract.
- [x] Unit test, benchmark và CI nền.

## M1 — Workbook và viewport usable

- [ ] Immutable/read snapshot cho renderer.
- [ ] Selection model, active cell và multi-range.
- [ ] Row/column insert, delete, hide và resize.
- [ ] Merge range index.
- [ ] Undo/redo transaction.
- [ ] Dirty-region tracker.
- [ ] Display-list builder cho grid, text và selection.
- [ ] Editor overlay dùng lại một instance.

## M2 — Windows renderer hiệu năng cao

- [ ] Direct2D device/resource lifecycle.
- [ ] DirectWrite glyph/text-layout cache.
- [ ] DXGI/composition surface.
- [ ] Partial redraw và tile cache.
- [ ] WPF host interop.
- [ ] WinForms host interop.
- [ ] 60 Hz và 120 Hz benchmark trên 4K.

## M3 — Formula engine

- [ ] Tokenizer, parser và AST.
- [ ] Dependency graph incremental.
- [ ] Circular reference policy.
- [ ] Math, text, date/time, lookup và financial functions.
- [ ] Dynamic array/spill contract.
- [ ] Plugin hàm nghiệp vụ dự toán.

## M4 — XLSX và in ấn

- [ ] Open XML import/export.
- [ ] Shared strings, style và formula mapping.
- [ ] Unknown-part preservation.
- [ ] Print layout, page break và PDF pipeline.
- [ ] Round-trip compatibility corpus.

## M5 — MAUI và Skia GPU

- [ ] Skia GPU render backend.
- [ ] MAUI handler theo nền tảng.
- [ ] Touch selection handle, pinch zoom và virtual keyboard.
- [ ] Responsive command surface thay cho ribbon desktop đầy đủ.

## M6 — Control suite

- [ ] Ribbon, toolbar, menu và command schema.
- [ ] DataGrid với data model riêng.
- [ ] Theme, localization, accessibility và designer support.
- [ ] Chart, conditional formatting, image và shape.
- [ ] Pivot, slicer và module nâng cao.

## M7 — Product hardening

- [ ] API compatibility checks.
- [ ] NuGet packaging và symbols.
- [ ] Crash recovery, telemetry opt-in và support bundle an toàn.
- [ ] Alpha → Beta → RC → Production.
