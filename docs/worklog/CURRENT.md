# Current Work Handoff

- Ngày cập nhật: 2026-08-19
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Implementation commit đã xác minh: `5ce73280c2a7cbb4aee4563b7c1597781fc1cdc5`
- GitHub Actions: run `32230805548`, CI `#406`, kết luận `success`
- Tài liệu nguồn sự thật: `docs/current-status.md`
- Quyết định renderer: `docs/adr/0004-direct2d-skia-backends.md`
- Contract sparse styles: `docs/whole-axis-style-contract.md`

## Đã hoàn thành trong mốc hiện tại

### Skia display-list renderer

- Triển khai renderer Skia cho toàn bộ command hiện có: fill rectangle, line, text, nested display list, clip và translation.
- Giữ nested-reference semantics, đồng thời kiểm tra stack clip/translation cân bằng qua các display list lồng nhau.
- Chuyển sang API text hiện hành của SkiaSharp và giữ command-level clip cho text/wrap.
- Thay cache typeface không giới hạn bằng `BoundedLruCache` mặc định 64 entry.
- Cache cả default fallback trên Linux nhưng không dispose tài nguyên global do Skia sở hữu.
- Bổ sung hit/miss/eviction, successful/failed frame và executed-command diagnostics.
- Bổ sung DPI overload và bảo toàn chính xác `SKCanvas.SaveCount` của caller sau cả success lẫn exception.
- Frame lỗi vẫn ném ra ngoài, nhưng không để rò transform/clip sang frame kế tiếp.

### MAUI GPU/touch host baseline

- Thay MAUI stub bằng public `NeraSpreadsheetView : SKGLView`.
- Control dùng một native GPU surface, không tạo control riêng cho từng ô.
- Bind `Workbook`, tạo `SpreadsheetSession`/viewport engine và dùng cùng display-list composer với desktop.
- Có continuous pan, wheel, pinch zoom theo anchor, tap selection, hit test, overscan, theme và diagnostics.
- `UseNeraSpreadSheet()` đăng ký SkiaSharp cùng platform handler.
- Thêm CI job cài workload `maui-windows` và build target Windows thật.
- Sửa lỗi CI restore: bỏ global `TargetFramework` override làm project reference `net10.0` mất assets target; chuyển sang target-aware implicit restore của lệnh build.

### Xác minh renderer và host

- Raster test xác nhận nested translation/clip và pixel ngoài clip không bị ghi.
- Line/text tạo raster content và tái dùng typeface.
- Wrapped text không thoát command clip.
- Cache dung lượng 2 xác nhận hit, miss và eviction.
- DPI 2x map đúng logical pixels và phục hồi save depth.
- Unsupported command giữa frame xác nhận exception, diagnostics và canvas recovery.
- Invalid DPI bị từ chối trước khi thay đổi canvas hoặc failure counter.
- Core/Linux, full Windows, desktop GPU/runtime smoke và MAUI Windows build đều xanh tại CI `#406`.

## Trạng thái HEAD sau mốc đã xác minh

Commit `4c169c7ff306591651df588643bee7e48b72db2f` chỉ mở `InternalsVisibleTo("NeraSpreadSheet.OpenXml")` để bắt đầu exact sparse style serialization. Đây là bước chuẩn bị, chưa được ghi nhận là full XLSX style round-trip.

## Giới hạn có chủ ý

- MAUI mới có source + Windows compile gate; chưa có loaded native Window/runtime smoke.
- Chưa có Android/iOS/Mac Catalyst build matrix hoặc lifecycle/context-loss test.
- Skia renderer là thread-affine, nhận caller-owned canvas; platform host chịu trách nhiệm sở hữu và phục hồi GPU context.
- Basic XLSX chưa round-trip complete style table hoặc sparse row/column style metadata.
- PR tiếp tục Draft; không merge nếu exact-head CI đỏ hoặc chưa xác định.

## File trọng tâm

- `src/NeraSpreadSheet.Rendering.Skia/SkiaDisplayListRenderer.cs`
- `src/NeraSpreadSheet.Rendering.Skia/SkiaRendererDiagnostics.cs`
- `src/NeraSpreadSheet.Rendering.Skia/NeraSpreadSheet.Rendering.Skia.csproj`
- `tests/NeraSpreadSheet.Rendering.Skia.Tests/SkiaDisplayListRendererTests.cs`
- `src/NeraSpreadSheet.Maui/NeraSpreadsheetView.cs`
- `src/NeraSpreadSheet.Maui/NeraSpreadSheetMauiAppBuilderExtensions.cs`
- `src/NeraSpreadSheet.Maui/NeraSpreadSheet.Maui.csproj`
- `.github/workflows/ci.yml`
- `Directory.Packages.props`
- `docs/third-party-notices.md`
- `docs/current-status.md`

## Bước tiếp theo duy nhất

Hoàn thành **XLSX style-table và exact sparse row/column style round-trip** từ mốc chuẩn bị `4c169c7f...`:

1. ánh xạ fonts, fills, borders, alignment, number formats và protection vào style table có deduplication ổn định;
2. ghi/đọc direct cell styles cùng sparse row/column style spans mà không materialize toàn logical axis;
3. bảo toàn thứ tự composition của row/column patches và direct-cell override;
4. giữ unknown OpenXml parts khi chưa cần sửa;
5. thêm round-trip, no-flattening, merge-anchor, structural-history và malformed-input tests;
6. chỉ cập nhật trạng thái hoàn thành khi Core/Windows/MAUI exact-head CI đều xanh.
