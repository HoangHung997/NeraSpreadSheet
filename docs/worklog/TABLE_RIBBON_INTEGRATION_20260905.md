# Tích hợp TABLE-005 / TABLE-RIBBON-012 / TABLE-006 — 05/09/2026

## Mốc và phạm vi

- Coordinator tích hợp trên `feature/bootstrap-architecture-v0.1`, PR #1
  Draft/open/unmerged, base `develop`; không phát hành hoặc merge PR.
- Base tích hợp: `6d1beca735acc869b1e5b3ea1e2e3794a02644e7`.
- A: `488c61ea..d283a55bf2d9ca9bc107c7ada05c8a0c4763e511`, sáu commit,
  gồm đúng một bản nhập ba commit TABLE-005.
- B: chỉ `cf923db2..7f73a97dce349d934f86dc4a13dad22d200acdeb`, ba commit;
  không lấy lại TABLE-005. Hai source HEAD sạch và bằng remote khi review.
- Cả chín commit cherry-pick không conflict. Mốc nhập cuối `de26921d`,
  regression/loaded smoke bổ sung tại **`e29acb44bc058e91a27c9dcc35a6979909d4dd5b`**.
- Coordinator không viết lại production implementation. Bổ sung kiểm chứng
  điểm nối command/runtime của A với Convert-to-range fix của B.

| Lane | Source commit | Integration commit |
| --- | --- | --- |
| A / TABLE-005 import | `1c6faead` | `2ad4e7be` |
| A / TABLE-005 docs | `b206dcdf` | `98ec0a9d` |
| A / TABLE-005 handoff | `8957f42a` | `ae948bb8` |
| A / Table Design UX | `facd85b0` | `6ed5f07b` |
| A / capture geometry | `d82e2504` | `f6bbe8b5` |
| A / handoff | `d283a55b` | `5131ff63` |
| B / compatibility | `79f72968` | `77acde8f` |
| B / identity/sort hardening | `c7b31253` | `89b62cb3` |
| B / Convert-to-range | `7f73a97d` | `de26921d` |

## Đã nối

- Production registry có 49 command, gồm 19 Table command; contextual tab,
  style gallery, totals, QAT/key tips/shortcuts dùng một dispatcher và một
  `SpreadsheetSession.Tables` mutation boundary.
- WPF preview thu thập tham số/validation/cancel qua callback SDK; selection,
  worksheet switch, disposed callbacks và bounded thumbnails dùng chung state.
- Nhận toàn bộ delta Table/XLSX identity, preservation, totals, structured
  references và sparse metadata của lane B. Không thêm package/model riêng.
- Convert-to-range đổi tham chiếu Table đích thành A1 trước khi bỏ metadata,
  giữ giá trị và history. Test qua lệnh Ribbon kiểm tra calculated column,
  cross-sheet/mixed formulas, Undo/Redo và dependency sau khi sửa ô nguồn.
- Native WPF dialog smoke kiểm tra giá trị/formula ngay sau Convert và sau
  Undo; không còn chỉ kiểm tra Table count như source presentation ban đầu.

## Kiểm chứng checkout kết hợp

| Gate | Kết quả local |
| --- | --- |
| Full solution + WPF sample Release | 0 warning / 0 error |
| Full Core solution | **1470/1470**, 0 skipped |
| Modules trong Core | Commands 122; Core 134; Editing 276; Formulas 535; OpenXml 133 |
| MAUI presenter/handler tests | **43/43**, 0 skipped |
| Loaded MAUI Windows Ribbon smoke | success, **3 frames**, Table selection binding/commands/customization |
| Full Windows desktop runtime suite | **76/77**, 0 skipped; chi tiết dưới |
| WPF loaded capture + command/dialog smoke | success, **177 ảnh / 128 layout snapshots** |
| Architecture / SDK packaging verifier | pass / pass |
| Core SDK pack | **18 nupkg**, chưa publish NuGet |

Windows local có đúng một failure:
`PublicWpfScrollBarDragMovesOnlyBottomRightPaneAndPersistsState` dừng tại
`Assert.IsTrue(window.Activate())`, trước hành vi SDK cần đo. Đây là cùng điểm
foreground failure đã ghi ở checkpoint trước; không bỏ/nới test và không dùng
lý do đó để thay thế Windows CI ở combined HEAD. Raw TRX giữ trong artifacts.

Ảnh synthetic ở `artifacts/table-ribbon-integration/captures`; đã review Home
1024 sáng, Table Design 1920 sáng, 1024 tối và tương phản tối. Manifest phân biệt logical
surface 1920 với native window bị OS giới hạn; không coi raster scale hoặc
loaded offscreen geometry là physical multi-monitor DPI acceptance.

Local Core/WPF dùng SDK 10.0.302; MAUI dùng SDK 10.0.201 có workload hiện hữu
từ working directory ngoài `global.json`. Không sửa SDK pin của repository;
remote CI vẫn dùng 10.0.302. Raw paths/logs/workbooks cá nhân không commit.

Benchmark lane B đã review: toggle+Undo 2.045/2.018 µs, completion
130.6/133.8 ns với 0/100.000 unrelated cells, allocation tương ứng
8.408/648 B mỗi operation. Đây là số của source lane B, chưa đo lại trên
combined HEAD, không phải trước/sau hoặc SLO. Integration không thay algorithm
render/scroll; giữ nguyên harness để tái lập ở checkpoint performance.

## GitHub evidence

Hai source final HEAD đã được coordinator kiểm tra trực tiếp đủ workflow/jobs:

- A `d283a55b`: full `33952589400`, iOS `33952590755`, Q003C `33952592209`.
- B `7f73a97d`: full `33952739465`, iOS `33952741334`, Q003C `33952742689`.

Combined implementation `e29acb44` đã **success**, attempt 1, đủ bảy job:

- [Full CI #1333 / 33953936497](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/33953936497).
- [iOS #154 / 33953936520](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/33953936520).
- [Q003C #151 / 33953936475](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/33953936475).

Windows job `101273695300` đạt **77/77**, build **0/0**, capture **177/128**;
không tái hiện local foreground failure. MAUI Windows/Android/Apple và loaded
smokes cũng success. Đây là combined evidence, không dùng source CI thay thế.
Commit tài liệu cuối cũng phải được kiểm tra tại chính SHA của nó; final SHA
và run URLs được ghi ở
[PR handoff](https://github.com/HoangHung997/NeraSpreadSheet/pull/1#issuecomment-5550451701)
sau khi đủ ba workflow xanh. Không dùng implementation parent để kết luận
documentation descendant đã xanh.

## Giới hạn và công việc chưa đóng

- TABLE-005 và TABLE-RIBBON-012 **DONE for defined scope** ở combined
  implementation `e29acb44` đã xanh; final documentation gates vẫn bắt buộc.
- TABLE-006 **chưa đóng toàn checkpoint**: thiếu corpus Excel/LibreOffice native
  có provenance và native editor point-mode/completion/highlight wiring cho API
  structured-reference mới. Các tests synthetic không thay thế hai việc này.
- Giữ giới hạn multi-area selector/nested incomplete fragments, query/XML Table,
  array Table formulas, unsupported totals/custom styles theo TABLE-006 contract.
- WPF sample dialogs không tự thành dialog mặc định cho mọi app host. Không
  thêm Avalonia, không repack demo ngoài repository, không publish NuGet.
- UI screenshot vẫn cần nghiệm thu thẩm mỹ/localization và physical DPI/a11y
  ở các checkpoint UX sau; không tuyên bố giao diện tương đương toàn bộ Excel.

## File trọng tâm, rollback và bước kế tiếp

- `tests/NeraSpreadSheet.Commands.Tests/TableRibbonIntegrationTests.cs`;
  `samples/NeraSpreadSheet.Wpf.Sample/RibbonPreviewWindow.TableDesignCapture.cs`;
  `src/NeraSpreadSheet.Editing/SpreadsheetTableController.Design.cs`;
  contracts `table-ribbon-integration`, `table-compatibility-hardening` và
  `table-contextual-design` trong `docs/`.
- Rollback bằng commit revert ngược chuỗi tích hợp sau `6d1beca7`, giữ nguyên
  lịch sử/source worktrees; không reset/force-push, không cần migration.
- Bước tiếp theo duy nhất: xác minh cả ba workflow và mọi job trên HEAD cuối
  của nhánh tích hợp, rồi bàn giao checkpoint trên PR #1; không tự mở task mới.
