# Đợt hoàn thiện Table / Filter / Ribbon / UX — 05/09/2026

## Phạm vi và mốc xuất phát

Người dùng yêu cầu chia worktree tiếp tục đến khi hoàn thành. Đợt này thay thế
quy định tạm dừng mở wave sau nghiệm thu ở các handoff lịch sử. Mục tiêu 100%
là hoàn tất acceptance của Table / Filter / Ribbon / UX trong delivery plan,
không phải tương thích 100% mọi tính năng của Microsoft Excel hoặc toàn SDK.
Không tự chuyển acceptance chưa kiểm chứng thành ngoại lệ để báo DONE.

- Integration branch: `feature/bootstrap-architecture-v0.1`; PR #1 Draft/open.
- Exact green baseline: `2e8482c25a44797a479b276ae26f472811a0a81e`.
- Full CI `33966917191`, iOS `33966917101`, Q003C `33966917091`: success,
  đủ bảy job đúng SHA. Core 1505/1505, Windows 105/105, MAUI 44/44.
- Handoff và artifacts: [PR #1 comment](https://github.com/HoangHung997/NeraSpreadSheet/pull/1#issuecomment-5551912649).
- Baseline đã có 226 Ribbon PNG / 128 layout records, 4 MAUI PNG và 18 nupkg.
  Chúng không thay thế nghiệm thu trên HEAD kết hợp mới.

## Ba lane độc lập

Tất cả task mới dùng `gpt-6-astra`, `xhigh`, worktree riêng từ baseline trên.
Không tự tạo thêm agent/task. Đã xác minh cả ba task chạy đúng base/model/effort.

| Lane | Công việc | Trạng thái |
| --- | --- | --- |
| A — UX-007 | Keyboard, focus, accessibility; hoàn thiện localization/chrome/customization còn thiếu của UX-006 | ACTIVE |
| B — TABLE-007 | Shared structured-formula editor cho split/MAUI; corpus do LibreOffice thật tạo | ACTIVE |
| C — PERF-008-HARNESS | Harness, stress tests, paired baseline trên CI runner riêng | ACTIVE |

| Lane | Task ID | Branch |
| --- | --- | --- |
| A | `01a071e8-82ec-76c2-8c96-d97faa31e40c` | `feature/ux-007-keyboard-a11y` |
| B | `01a071e8-82eb-7722-8e51-adb2bcf7dd1c` | `feature/table-007-editor-corpus` |
| C | `01a071e8-82eb-7722-8e51-ada88da6d05b` | `feature/perf-008-harness` |

### Quyền sửa A

- Ribbon.Core, Bars.Core và các Ribbon/Bar/customization presenter/binding/chrome
  của WPF, WinForms, MAUI; Commands chỉ presentation/localization, không Table
  mutation handlers hoặc formula/calculation.
- Table/Filter popup, paging input, accessibility và MAUI AutoFilter/Table host
  chrome; không thay đổi filter/sort/workbook semantics.
- Ribbon preview/capture và test Ribbon/customization/filter-focus tương ứng.
- `tests/NeraSpreadSheet.Maui.Windows.RibbonSmoke/SmokePage.cs` và
  `tests/NeraSpreadSheet.Maui.Windows.TableFilterSmoke/SmokePage.cs` thuộc A;
  không sửa generic Windows.Smoke editor page dự kiến dành B.
- Ngoại lệ chuyển quyền **A là writer duy nhất `.github/workflows/ci.yml`**
  đến handoff/release, chỉ thêm upload `maui-windows-ribbon-ux007` cho
  `artifacts/maui-windows-ribbon-smoke/ux007-*.png` sau loaded Ribbon smoke,
  `if: always()` / `if-no-files-found: error`. Commit cùng producer 9 captures;
  không đổi triggers/SDK/jobs/gates khác. Root không sửa file này khi A giữ.
- Contract riêng `docs/ux-007-keyboard-accessibility-contract.md`, worklog
  `docs/worklog/UX-007.md`; có thể cập nhật contract UX-006/keyboard liên quan.
- **A giữ duy nhất desktop lease** cho synthetic native UI smoke/capture.
  Đọc đầy đủ Computer Use skill/guidance trước thao tác. Không sửa workbook thật
  của người dùng, không đổi thiết lập hệ thống để giả lập bằng chứng phần cứng.

### Quyền sửa B

- Editing formula assistant/editor contracts; editor/input của standalone WPF,
  WinForms và `NeraSpreadsheetSplitAdorner` / `NeraSpreadsheetSplitSurface`;
  MAUI spreadsheet view/editor overlay, không Table/AutoFilter host chrome của A.
- Corpus Table/OpenXml và converter fixes nếu có regression chứng minh; giữ
  native Table identity, preservation boundaries và session transaction hiện có.
- Test structured-editor, split editor, MAUI editor, producer corpus riêng.
- Contract riêng `docs/table-007-editor-corpus-contract.md`, worklog
  `docs/worklog/TABLE-007.md`; contract native Table/split editor liên quan.
- Không điều khiển desktop local khi A giữ lease. Dùng headless/CI; xin chuyển
  lease qua coordinator nếu thực sự cần native runtime trên máy này.
- Đã cấp B độc quyền tạo `.github/workflows/table-007-libreoffice.yml` và
  `scripts/table-007-libreoffice.py` để actual Calc headless tạo synthetic XLSX,
  version/hash/privacy provenance. Existing workflows vẫn thuộc root.
- Đã chuyển B độc quyền `tests/NeraSpreadSheet.Maui.Windows.Smoke/SmokePage.cs`
  và helper mới `Table007EditorSmoke.cs` trong cùng project: bọc view bằng host
  dùng canonical session.Editor, chạy editor regression trước runtime stress cũ.
  Không bỏ gates cũ/csproj; không dùng chung RibbonSmoke/TableFilterSmoke page A.
- Đã chuyển B độc quyền
  `src/NeraSpreadSheet.Maui/NeraSpreadSheetMauiAppBuilderExtensions.cs` chỉ để
  register internal reused cell-editor handler. Giữ UseSkiaSharp và existing
  Mac Catalyst SKGLView handler workaround; A không sửa registration file này.
  Native Apple key handling phải giữ IME/composition và delegate unhandled keys;
  Apple build không thay native editor keyboard smoke.
- Đã chuyển B ba native hooks: `SmokePage.cs` trong
  `tests/NeraSpreadSheet.Maui.Android.AnalyticsSmoke`,
  `tests/NeraSpreadSheet.Maui.iOS.AnalyticsSmoke`,
  `tests/NeraSpreadSheet.Maui.MacCatalyst.AnalyticsSmoke`; mỗi project thêm
  `Table007EditorSmoke.cs`. Chỉ thêm editor phase/result evidence, restore
  focus/layout/selection và giữ mọi analytics assertion/gate cũ. Không đổi
  workflow/csproj, không nới timeout để né lỗi. Editor fail phải fail smoke.
  Native InsertText/MarkedText không thay Apple hardware OS-keyboard evidence.
- B được chuyển `src/NeraSpreadSheet.Viewport/SpreadsheetViewportEngine.cs`
  chỉ extract layout computation dùng chung Compose/public ComputeLayout và
  test mới `tests/NeraSpreadSheet.Viewport.Tests/Table007EditorGeometryTests.cs`.
  Không đổi scroll/cache/composition hoặc duplicate metrics. Layout-only không
  tạo display list/recalculate, nhưng `EnsureMetrics` hiện có thể capture
  snapshot khi active Table filters; không hứa snapshot-free cho filtered sheet.
  Giữ visibility/freeze/merge/fractional geometry và snapshot reuse, test với
  Compose.Layout; MAUI fallback theo real host size/zoom không giả GPU thành công.

### Quyền sửa C

- `benchmarks/`, harness/test mới có tên riêng PERF-008, script
  `scripts/run-perf-008*` và workflow mới `.github/workflows/perf-008.yml`.
- Contract riêng `docs/perf-008-acceptance-contract.md`, worklog
  `docs/worklog/PERF-008.md`; được cập nhật performance budget bằng số đo thật.
- Không sửa production code trong khi A/B active; không sửa existing CI workflow,
  project references hoặc test của A/B. Chuyển quyền qua coordinator nếu phát
  hiện bottleneck cần sửa production sau integration.
- Chuẩn bị paired baseline/candidate trên runner riêng; không lấy local timing
  lúc ba lane build đồng thời làm bằng chứng. Lưu raw output, input/output
  fingerprints, SDK/runner/config và độ dao động; threshold từ baseline đo lại.
- Hoàn tất harness chưa có nghĩa PERF-008 DONE: phải chạy lại trên HEAD kết hợp
  A+B và xử lý hồi quy trước nghiệm thu.
- Checkpoint C đã nhận: baseline A/A calibration trước candidate, paired AB/BA
  có cùng harness overlay hash, noisy run trả INCONCLUSIVE; Program.cs thuộc
  benchmarks và new PERF008 Windows smoke file thuộc C, không sửa csproj.
- Audit C ghi nhận cache hiện giữ requested pages, chưa eviction: phải đo và
  nêu source/default distinct cap, số trang và memory/disposal; không gọi cache
  constant-bounded khi chỉ native UI page đang bounded.
- C preflight run `33971846930` tại `413ab07a` dừng trước đo vì actual SDK
  resolve 10.0.400 thay vì exact 10.0.302 của harness. Không có statistical
  samples và không phải production regression. C isolate DOTNET_INSTALL_DIR
  trong workflow riêng; không đổi global.json hoặc existing CI.
- C complete run `33972169896` tại `2c8c4e2c`: source report native 2/2 PASS,
  paired 10/11 PASS, completion tiny-batch baseline noise INCONCLUSIVE. Artifact
  `9971302162` giữ đầy đủ; không gọi INCONCLUSIVE là no-regression acceptance.
- Root chấp thuận một protocol correction trước baseline mới: toggle4096 ops/
  warmup128, completion32768/1024, cachedPage262144/4096, target batch khoảng
  20ms trở lên để giảm timer/scheduling noise; Ribbon/open/search giữ nguyên.
  Giữ nguyên thresholds/statistics/datasets; freeze revision rồi chạy lại TOÀN
  A/A và AB/BA, không ghép/chọn samples. Run cũ không xóa; P1/P3 vẫn OPEN.

## Checkpoint vận hành

- Coordination commit `847ff4beec70a05ab4f4f15be9e4d52e82ae7ac7` đã xanh ba
  workflows: full `33971257042`, iOS `33971257063`, Q003C `33971256987`.
  Đây là docs-only code-equivalent baseline, chưa tích hợp implementation mới.
- B producer run `33971871140` tại `acee9aba` success; actual LibreOffice 24.2.7
  theo source handoff. Corpus compatibility/privacy còn được B kiểm thử, không
  đóng T3 chỉ vì producer chạy được.
- B đã nhận actual producer artifact `9971160827` và verify hash. Calc bỏ
  calculated-column/totals/style metadata, cần ghi producer difference thật.
  Empty AutoFilter full Table range gồm totals row đang bị importer reject;
  B làm narrow normalization có regression/negative tests, không nới predicate/
  sort/opaque-content validation hoặc dựng metadata để giả parity.
- B source checkpoint `cf680688741addd7675ae1a4320c07125df0a5e4` đã push:
  báo Core 1522/1522, desktop builds 0/0; source chưa release, B tiếp tục
  negative/lifecycle/native runtime hooks. Root đã dispatch full/iOS/Q003C
  bằng configured Git authentication và REST, không cài gh/đổi triggers;
  checkpoint CI không thay final source hoặc combined acceptance.
- SDK version trong global.json là 10.0.302 với `rollForward: latestFeature`.
  Không suy ra actual SDK của existing CI từ requested version; các lane ghi
  actual version trong logs. Controlled performance dùng exact isolated SDK.
- Root đã audit sơ bộ command/demo: 49 session commands, 35/35 headless
  catalog/Table/Ribbon tests PASS; R1/R2 vẫn OPEN. Preview Open hiện mở một grid
  window không có full shell và worksheet selector là ComboBox, cần xử lý sau
  A release sample ownership. Xem [release audit](RELEASE-009_COMMAND_AUDIT.md).

## Single writer và tích hợp

- Root coordinator sở hữu CURRENT, current-status, AI_COORDINATION, delivery
  plan, file wave này; existing CI workflows, solution/shared props/project files
  và `TableRibbonIntegrationTests.cs`. Ngoại lệ ci.yml đã chuyển riêng cho A ở
  trên; các lane không ghi file root sở hữu hoặc file lane khác đang giữ.
- Mỗi file chỉ một writer. Không tự cherry-pick/merge/rebase lane bên cạnh; chỉ
  commit/push nhánh của mình. Không force-push, không merge PR #1/main/develop.
- Cần file ngoài phạm vi: báo exact paths + lý do, tiếp tục phần không vướng;
  đợi coordinator ghi chuyển quyền trước khi sửa. Không coi việc đọc trạng thái
  cũ là lock hoặc dùng cùng file MD cho nhiều tác nhân đồng thời ghi.
- Mỗi lane ghi worklog riêng: base/HEAD, commits, tests, exact-head CI, gaps,
  rollback và xác nhận release files/desktop. Báo handoff cho coordinator.
- Coordinator review diff và tests, ghép từng lane đã release, chạy combined
  build/regression/architecture/pack/runtime và ba workflows đúng SHA cuối
  (kể cả commit docs). Source-green không thay combined-green.
- Đĩa local hạn chế: reuse caches, không cài workload mới hoặc tạo nhiều bản
  self-contained không cần thiết. Không xóa artifact/worktree của tác nhân khác.

## Checklist nghiệm thu cố định

Trạng thái khởi tạo là OPEN; từng mục phải có commit/test/artifact để chuyển PASS.

| ID | Bằng chứng cần đạt | Owner | Trạng thái |
| --- | --- | --- | --- |
| U1 | Keyboard-only tab/group/QAT/menu/popup; shortcut không chạy hai lần; focus restore, Esc/Enter đúng phạm vi | A | OPEN |
| U2 | Native accessibility names/roles/states/focus cho Ribbon/Table/Filter; screen-reader smoke thực tế trên nền tảng hỗ trợ | A | OPEN |
| U3 | Vietnamese/English resources hoàn chỉnh trong surface được hỗ trợ, host isolation, light/dark/high-contrast và Picker mở | A | OPEN |
| U4 | MAUI customization shell thực, responsive full/narrow window, persisted add/remove/reorder và cancel/undo đúng contract | A | OPEN |
| U5 | Physical DPI/multi-monitor/touch acceptance có bằng chứng thật; phần cứng chưa có phải báo chưa nghiệm thu | A/root | OPEN |
| T1 | Structured reference assistance chạy thật trong WPF/WinForms split editor và MAUI reused overlay | B | OPEN |
| T2 | Enter commit, Alt+Enter newline, Esc cancel, full-cell layout/clipping; Table rename/stale selection/caret safety; bounded metadata-only suggestions | B | OPEN |
| T3 | LibreOffice-produced synthetic workbook, producer version/hash/privacy, repeated load/edit/save/reopen so với Excel/Nera corpus | B | OPEN |
| P1 | Reproducible baseline/candidate runner, raw latency/allocation/bounds evidence cho Ribbon/filter/table | C | OPEN |
| P2 | Resize/theme/customization/popup/dispose stress, subscriptions/memory, large filter bounded paging; không rebuild/recalc khi scroll | C | OPEN |
| P3 | Đo lại toàn bộ trên combined A+B; không hồi quy ngoài budget được chứng minh, không thay thế bằng source benchmark | root/C | OPEN |
| R1 | Audit toàn command surface: enabled commands có handler thật, disabled nêu lý do; không stub giả capability | root | OPEN |
| R2 | Win11 x64 demo tích hợp surface đã hỗ trợ, synthetic workbook/checklist, screenshots và known limitations | root | OPEN |
| R3 | NuGet pack + isolated consumer smoke trên cùng source; cung cấp artifact thử nghiệm, không tự publish feed công khai | root | OPEN |
| R4 | Architecture/privacy/regressions/native runtimes và exact-final-HEAD GitHub Actions xanh, docs/handoff đúng trạng thái | root | OPEN |

Giới hạn sản phẩm đã khai báo (Power Query/VBA/add-ins/OLAP, unsupported color/icon
sort execution, mixed opaque conditional-format preservation, `dataDxfId`
preserve-only) vẫn phải được nêu rõ trong demo/SDK; không hứa parity toàn Excel.
Các giới hạn đó không cho phép bỏ qua acceptance U1–R4 nêu trên. Nếu cần thiết bị,
quyền mới hoặc quyết định sản phẩm để đóng một gate, coordinator báo đúng blocker
và xin người dùng thay vì tự xác nhận PASS.

## Chuỗi tiếp tục sau wave

1. A/B/C thực hiện song song trong phạm vi ownership.
2. Root review và integrate từng lane ready; chạy exact-head combined gates.
3. C chạy final performance trên combined code; root đóng command/demo/consumer
   acceptance. Chỉ giao follow-up bounded mới nếu còn mục OPEN.
4. Cung cấp demo và NuGet artifacts để test, báo checklist PASS/OPEN. Chỉ báo
   hoàn thành 100% phần Table/Filter/Ribbon/UX khi không còn gate OPEN/FAIL.
