# Đợt TABLE-006-NATIVE / UX-006 — 05/09/2026

## Cập nhật hiện hành — cả hai lane đã release, coordinator tích hợp

A final integration `ca69da45` đã xanh đủ bảy job. B final source `292a48d1`
xanh full `33965918117`, iOS `33965919539`, Q003C `33965921194`, đủ bảy job;
đã RELEASE ALL OWNED FILES kể cả transfer legacy TableHost/CI upload step.
Không còn source writer hoặc native desktop owner active. Coordinator đã ghép
bốn commits B lên A thành `0e770e52`, không conflict/path overlap, giữ source
paths của cả hai lane. [Combined integration record](UX_INTEGRATION_20260905.md).
Build/Core/combined exact-final-HEAD CI sau docs phải qua trước bàn giao.
Không tạo thêm task trong wave; các quyền/trạng thái bên dưới là lịch sử.

## Lịch sử — A đã handoff, B khi còn tiếp tục

A final `4ae7731f` xanh full `33958874774`, iOS `33958876307`, Q003C
`33958877741`, đủ bảy job. Coordinator ghép năm commits thành `1aaba747`,
không conflict; final integration CI là cổng riêng. Xem
[integration record](TABLE_NATIVE_INTEGRATION_20260905.md).
A đã release toàn bộ files/desktop. **Desktop hiện độc quyền B**, sau các
lượt transfer có xác nhận trên PR #1 và source worklogs; quyền ban đầu bên
dưới chỉ là lịch sử, không cho A tự chạy native local lại.

Các transfer file đã chấp thuận và không có hai writer cùng lúc:

- A: OpenXmlConditionalFormattingCodec.cs và OpenXmlPackagePreserver.cs cho
  native General/Table dxf; không thay Core catalog hoặc mixed opaque CF merge.
- B: MAUI NeraSpreadsheetTableHost.cs/Keyboard.Windows.cs cho presentation;
  một upload-artifact step trong ci.yml sau loaded TableFilterSmoke, chỉ
  ux006-*.png, always/error nếu thiếu. Không đổi job/gates/timeouts/retries.
- Coordinator là writer integration/status/CURRENT/board; source B vẫn từ
  `2bc00eb6`, chưa nhập vì đang sửa WPF capture/MAUI theme tại `f9fc3724`.

Không tạo thêm task trong wave; TABLE-006 chưa đóng toàn bộ và UX-006 chưa
được nghiệm thu. Quyền ban đầu và scope chi tiết tiếp tục được giữ dưới đây.

## Baseline và quyết định

Người dùng yêu cầu tiếp tục phần dở và mở worktree cho phần mới có thể chạy
song song. Hai lane bắt đầu từ integrated final HEAD
`2bc00eb667da2f2c5afda1024ab753ac638d85d4`, xanh full #1334 / `33954450148`,
iOS #155 / `33954450152`, Q003C #152 / `33954450150`, đủ bảy job.
Giữ yêu cầu model trước đó: `gpt-6-astra`, `thinking: xhigh`; không tự đổi model.
PR #1 giữ Draft/open/unmerged. Không thêm Avalonia hoặc phát hành demo/NuGet
trong đợt này.

TABLE-005/TABLE-RIBBON-012 đã hoàn tất defined scope. TABLE-006 còn native
structured-reference editor wiring và corpus có provenance. UX-006 được khởi
động sớm phần chrome/localization độc lập; không sửa Table/formula semantics
và không bỏ combined CI khi nghiệm thu.

## Hai lane độc quyền

| Lane | Checkpoint | Branch dự kiến | Trạng thái |
| --- | --- | --- | --- |
| A | TABLE-006-NATIVE — editor và producer corpus | `feature/table-006-native-compat` | HANDED OFF / RELEASED; integrated `1aaba747` |
| B | UX-006 — visual system và localization | `feature/ux-006-visual-localization` | HANDED OFF / RELEASED; imported `0e770e52` |

Đã xác nhận task có turn `inProgress`, đúng model/effort/base và worklog claim:

- A: `01a070ae-bcfe-7562-acad-9c53287433d3`.
- B: `01a070af-9093-7303-a811-821cde640467`.

Hai task cũ TABLE-RIBBON-012 và TABLE-006 headless đã idle/completed; không
đánh thức hoặc giao lại quyền ghi của wave cũ. Không tạo thêm task trong wave.

### A — Tiếp tục TABLE-006

- Dùng `SpreadsheetFormulaEditingAssistant` và `FormulaReferenceAnalyzer`
  overload có workbook/address đã tích hợp; không tạo parser/model/history khác.
- Nối Table/column completion, chọn item bằng chuột/bàn phím, provisional drag
  replacement và precedent highlights vào editor thật. WPF là đường end-to-end
  hiện hữu cần hoàn thiện trước; audit WinForms/MAUI và nối qua host/editor hiện
  hữu khi có. Capability nền còn thiếu phải có evidence/handoff, không gắn stub
  rồi tuyên bố ba host đều hoàn chỉnh.
- Giữ Enter commit, Alt+Enter newline, Escape cancel, nguyên hình chữ nhật cell,
  sparse/visible bounds, không recalculate hoặc thêm history khi chỉ gợi ý/kéo.
- Kiểm tra same/cross-sheet, #This Row, tên Unicode/ký tự escape, rename/delete
  lúc popup đang mở, empty/partial/unsupported fragments và Convert Undo/Redo.
- Corpus nhỏ do Excel/LibreOffice/Nera thực sự tạo hoặc lưu; ghi producer,
  version, recipe, hash, nguồn/quyền sử dụng và expected semantics. Không gọi
  synthetic OpenXML là native producer evidence. Mọi dữ liệu là dữ liệu mẫu;
  quét/loại metadata cá nhân, external links và path trước khi commit fixtures.
- Dùng Computer Use đúng skill cho thao tác Windows; chỉ tạo workbook mới,
  không sửa/lưu/đóng workbook cá nhân đang mở. Không tự cài hoặc chạy phần mềm
  mới tải nếu cần thêm chấp thuận. Có thể dùng fixture upstream có nguồn/quyền
  rõ và bằng chứng producer; thiếu producer phải ghi gap, không giả provenance.
- Sở hữu Table-specific Core/Editing/Formulas/OpenXml, formula-editor và input
  của `NeraSpreadsheetControl*`, `NeraSpreadsheetSurface*`, split editor, MAUI
  `NeraSpreadsheetView*`/editor binding. Không sửa Ribbon/Bar/Filter chrome.
- Tests/benchmarks: Core/Editing/Formulas/OpenXml và formula-editor native tests;
  fixture directory riêng. Được sửa OpenXml.Tests project để include fixtures.
  Project/CI chung khác phải xin transfer; không đụng Commands.Tests Ribbon files.
- Chỉ cập nhật `docs/worklog/TABLE-006-NATIVE.md` và
  `docs/table-native-compatibility-contract.md`.

### B — UX-006 độc lập

- Audit rồi hoàn thiện typography/spacing/corners/focus/hover/pressed/checked/
  disabled của Ribbon/Bar/Table Design/Filter chrome ở bốn palette; giữ dense
  ba hàng/caption đáy, responsive overflow và custom host overrides hiện hữu.
- Việt hóa có dấu, dùng resource localization/fallback có thể kiểm thử; đổi
  culture không làm mất stable IDs, key tips, shortcut, customization JSON,
  command behavior hoặc history. Không đổi command ID để dịch caption.
- Tái sử dụng Iconography catalog; không tải/copy icon Excel. Không thêm model
  hoặc theme engine song song. Giữ chữ/viền/icon để state không chỉ dựa vào màu.
- Sở hữu Ribbon.Core, Bars.Core, presentation localization trong Commands,
  Ribbon/Bar/Customization/TableFilter/PagedFilter presenters/bindings và chrome
  của WPF/WinForms/MAUI (gồm `NeraSpreadsheetAutoFilterHost*` của MAUI).
  Loại trừ toàn bộ spreadsheet formula editor/input/surface mà A sở hữu.
- Sở hữu WPF sample `RibbonPreviewWindow*`, Ribbon capture script và tests
  Ribbon/Bar/Filter presentation. Không đổi calculation/point-mode implementation
  được sample gọi. `TableRibbonIntegrationTests.cs` giữ coordinator ownership.
- Resource chỉ trong phạm vi SDK control; không ghi đè resource ứng dụng nhúng.
  Có thể dùng resource framework sẵn có; package dependency/project/CI chung
  cần decision note và transfer trước khi sửa.
- Chỉ cập nhật `docs/worklog/UX-006.md` và
  `docs/ux-006-visual-localization-contract.md`.

## Khóa tài nguyên và điều phối

1. Coordinator là writer duy nhất của integration branch, CURRENT, status,
   board, delivery plan và file wave này. Worker chỉ push nhánh riêng.
2. Mỗi task claim exact base/owned paths trong worklog riêng trước khi sửa.
   File không rõ owner: WAITING, gửi coordinator; không cùng sửa rồi resolve sau.
3. **Desktop local chỉ lane A dùng trong wave này.** B không mở Excel, preview,
   local desktop smoke hoặc capture có thể giành focus. B chạy headless build/
   tests local và native smoke/capture trong GitHub CI trên runner riêng. Muốn
   desktop local phải được coordinator transfer sau khi A xác nhận đã dừng.
4. Các worker không build vào output của integration hoặc worktree khác. Dùng
   SDK/workload/cache đã có, tránh publish self-contained/trùng artifact lớn.
   Nếu dung lượng thấp, báo coordinator, không xóa worktree/artifact của người khác.
5. B không đổi API Table đang được A nối. A không sửa chrome/localization đang
   được B làm. Cross-boundary patch cần trao quyền rõ bằng message và worklog.
6. Không thay external demo hoặc workbook người dùng; không push fixtures chứa
   dữ liệu cá nhân. Không tự merge/mark Ready PR #1.

## Cổng và handoff

- Mỗi source: build/analyzers 0/0, regression modules/full Core, architecture/
  packaging, exact-final-HEAD full CI/iOS/Q003C và mọi job success.
- A: loaded editor input smoke, fixture Save-Load-Save/schema/identity/value/
  Undo-Redo, producer provenance; thiếu bằng chứng không đánh dấu whole TABLE-006 DONE.
- B: loaded three-presenter/keyboard/theme/localization tests, capture wide/
  narrow và scale 1/1.25/1.5/2; tách physical DPI với logical/raster evidence.
  Nếu đổi render/layout performance, đo trước/sau cùng harness.
- Handoff gửi coordinator: source final SHA/base/delta, tests, CI URLs,
  file ownership release, giới hạn và rollback. Không chờ nhau để push source;
  coordinator chỉ tích hợp sau review và chạy combined exact-head gates.
- Coordinator xem A trước, B sau; chỉ delta sau baseline `2bc00eb6`, không lấy
  lại các commit đã tích hợp. Không tự mở checkpoint phụ thuộc tiếp khi chưa xanh.
