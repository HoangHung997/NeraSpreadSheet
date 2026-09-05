# Module Map

## Mô-đun hiện có ở M0

```text
NeraSpreadSheet.Foundation
 ├─ NeraSpreadSheet.Core
 │   ├─ NeraSpreadSheet.Formulas
 │   ├─ NeraSpreadSheet.Layout
 │   └─ NeraSpreadSheet.OpenXml
 ├─ NeraSpreadSheet.Scrolling
 ├─ NeraSpreadSheet.Commands
 │   ├─ NeraSpreadSheet.Ribbon.Core
 │   ├─ NeraSpreadSheet.Bars.Core
 │   └─ NeraSpreadSheet.DataGrid.Core
 └─ NeraSpreadSheet.Rendering.Abstractions
      ├─ NeraSpreadSheet.Rendering.Direct2D
      │    ├─ NeraSpreadSheet.Wpf
      │    └─ NeraSpreadSheet.WinForms
      └─ NeraSpreadSheet.Rendering.Skia
           └─ NeraSpreadSheet.Maui
```

Các project backend/host ở M0 mới khóa ranh giới phụ thuộc và API nền; Direct2D, DirectWrite, Skia GPU và XLSX round-trip chưa được triển khai đầy đủ.

## Mô-đun dự kiến bổ sung

```text
NeraSpreadSheet.UndoRedo
NeraSpreadSheet.Printing
NeraSpreadSheet.Pdf
NeraSpreadSheet.Ribbon.Wpf / WinForms / Maui
NeraSpreadSheet.Bars.Wpf / WinForms / Maui
NeraSpreadSheet.DataGrid.Wpf / WinForms / Maui
NeraSpreadSheet.Charts
NeraSpreadSheet.Pivot
NeraSpreadSheet.DuToan
```
