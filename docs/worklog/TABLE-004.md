# TABLE-004 — Table Style Engine

## Claim

- Checkpoint: `TABLE-004`.
- Owner: Codex task `TABLE-004 Table Style Engine`.
- Branch: `feature/table-004-style-engine`.
- Base integration SHA: `f75e2f103598cbbe7b5c22f92c3ab8dd755ef8c3`.
- Implementation SHA: `3a459320ef7192f5843dcd6d3bfb0a56ae7698ea`.
- PR: chưa tạo; PR #1 không bị sửa hoặc merge.
- Baseline CI được giao: full CI #1312 / run 33931524467; iOS #133 /
  run 33931524461; Q003C/OpenXML #130 / run 33931524543.
- Excluded shared files giữ nguyên: `docs/current-status.md`,
  `docs/worklog/CURRENT.md`, `docs/worklog/RIBBON_TABLE_FILTER_UX.md`.

## Status

`COMPLETE` — model/resolver, shared rendering, desktop filter visuals,
OpenXML round-trip/preservation, tests, loaded smoke và benchmark đã hoàn tất.

## Đã hoàn thành

- Thêm workbook-owned Table style catalog với 60 built-in identity ổn định,
  custom style, validation, preview 12 x 12 và snapshot isolation.
- Thêm workbook theme, RGB/theme color cùng tint/shade giữ alpha; resolve style
  một lần vào contract dùng chung cho mọi renderer.
- Khóa precedence: whole table, row stripe, column stripe, first/last column,
  header/totals, direct/axis formatting, conditional formatting.
- Compose fill/font/border chỉ trên visible + overscan; lookup/cache hot path không
  tạo closure theo ô và không materialize logical worksheet.
- WPF, WinForms, Direct2D và Skia dùng cùng display-list style; WPF/WinForms/MAUI
  filter button dùng cùng resolved Table-style visual.
- Import/export custom `tableStyles`, `tableStyleElement`, DXF và workbook theme;
  giữ theme+tint semantics, remap DXF, schema-validate và preserve unsupported
  style markup khi `PreserveUnknownParts=true`.
- Contract chi tiết nằm tại `docs/table-style-engine-contract.md`.

## File trọng tâm

- `src/NeraSpreadSheet.Core/TableStyles.cs`
- `src/NeraSpreadSheet.Core/WorksheetSnapshot.cs`
- `src/NeraSpreadSheet.Rendering.Spreadsheet/SpreadsheetDisplayListComposer.cs`
- `src/NeraSpreadSheet.Rendering.Spreadsheet/SpreadsheetTableStyleVisuals.cs`
- `src/NeraSpreadSheet.OpenXml/OpenXmlTableStyleCodec.cs`
- `src/NeraSpreadSheet.OpenXml/OpenXmlWorkbookThemeCodec.cs`
- `src/NeraSpreadSheet.OpenXml/OpenXmlDifferentialStyleRemapper.cs`
- `benchmarks/NeraSpreadSheet.Benchmarks/TableStyleComposeBenchmarks.cs`

## Build, test và runtime evidence

- .NET SDK 10.0.302: `dotnet build NeraSpreadSheet.Core.slnx -c Release`
  thành công, 0 warning / 0 error.
- .NET SDK 10.0.302: `dotnet test NeraSpreadSheet.Core.slnx -c Release
  --no-build` thành công 1.366/1.366 tests.
- Focused TABLE-004: Core 6/6; Rendering.Spreadsheet 3/3; Skia raster 1/1;
  OpenXML repeated round-trip/preservation 2/2.
- Loaded Windows runtime smoke: 2/2, gồm WPF pixel render và WinForms GDI+
  pixel render cộng Direct2D execution diagnostics.
- Full Windows rendering suite: 70/71; test không liên quan
  `PublicWpfScrollBarDragMovesOnlyBottomRightPaneAndPersistsState` dừng tại
  `Assert.IsTrue(window.Activate())` do foreground window của môi trường, trước
  khi đi vào product code.
- MAUI Windows build bằng SDK 10.0.201 có workload: thành công, 0 warning /
  0 error. SDK 10.0.302 cục bộ dùng cho solution không có workload
  `maui-tizen`, nên không dùng được cho MAUI trên máy này.
- `verify-architecture.ps1`: passed.
- `verify-packaging-sdk.ps1`: passed.
- `git diff --check`: passed.
- Scan toàn bộ changed files cho secret, token và đường dẫn máy cá nhân: passed.
- Không có file Ribbon hoặc ba shared status/worklog bị thay đổi.

## Benchmark

BenchmarkDotNet ShortRun, .NET 10.0.10, viewport 1200 x 800, overscan 128,
120 frame, cache viewport tắt, worksheet Table 1.048.576 x 20:

| Trường hợp | Mean / 120 frame | Managed allocation / 120 frame |
|---|---:|---:|
| Không Table style | 19,70 ms | 44,58 MB |
| Table style đầy đủ | 51,46 ms | 74,21 MB |

Kết quả tương đương khoảng 0,43 ms và 0,62 MB mỗi styled frame trên máy đo;
allocation và command count phụ thuộc viewport, không phụ thuộc 1.048.576 hàng.
ShortRun chỉ có ba iteration nên số thời gian dùng làm smoke/baseline, không phải
ngưỡng thống kê ổn định.

## Giới hạn và rollback

- 60 built-in có đúng identity/gallery grouping và theme-aware semantic của
  Nera, nhưng palette hiện là approximation có hệ thống, chưa phải corpus
  pixel-perfect của từng Excel built-in.
- `FilterButton` là extension renderer nội bộ, không phải
  `tableStyleElement` chuẩn SpreadsheetML nên không export sang OpenXML.
- Unsupported producer-specific Table-style element chỉ được giữ nguyên trong
  preservation mode; không bị giả lập thành semantic Core khác.
- Rollback an toàn: revert implementation SHA và commit handoff TABLE-004; thay
  đổi không có migration dữ liệu hoặc package dependency mới.

## Bước tiếp theo duy nhất

Sau khi exact-head CI của branch xanh, mở PR tích hợp
`feature/table-004-style-engine` vào `develop` và dùng implementation SHA ở
trên làm mốc review; không merge PR #1.
