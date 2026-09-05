# Đợt hoàn thiện Table / Filter / Ribbon / UX — 05/09/2026

## Chuyển quyền tiếp tục tại baseline tích hợp 50cb357a — 06/09

Mục này thay thế quyền sửa lịch sử của A/C bên dưới. Root đã xác minh đủ
năm workflow **success** tại `50cb357a00d6bb8a6b134cdeebce624a09bd1b21`:
full `33984177819`, iOS `33984177815`, Q003C `33984177818`, Windows packages
`33984174136`, published demo `33984234305`. Đây chưa phải whole B acceptance.

Tái sử dụng ba task/worktree hiện hữu, giữ GPT-6 Astra / xhigh. A và C tạo
nhánh mới từ đúng baseline trên sau khi kiểm tra clean tracked tree; giữ
nguyên nhánh đã bàn giao, không reset/rebase hoặc xóa artifacts. Không tạo
thêm task/worktree. Không local heavy build/native lease khi đĩa C gần đầy.

### A — RELEASE-009 navigation shell

- Nhánh mới `feature/release-009-navigation-shell`.
- Writer duy nhất trong WPF sample: `RibbonPreviewWindow.cs`,
  `RibbonPreviewWindow.WorksheetTabs.cs`, `RibbonPreviewWindow.Capture.cs`,
  các partial mới `RibbonPreviewWindow.Navigation.cs` và
  `RibbonPreviewWindow.SplitShell.cs`.
- Tests: `RibbonLoadedWorkbookSmokeTests.cs` và mới
  `RibbonWorksheetNavigationSmokeTests.cs` trong Windows.Rendering.Tests.
- Tài liệu riêng: `docs/release-009-navigation-shell-contract.md` và
  `docs/worklog/RELEASE-009-NAVIGATION.md`.
- Bổ sung grant localization: A giữ duy nhất
  `src/NeraSpreadSheet.Commands/PresentationStrings.resx` và
  `PresentationStrings.en.resx` để thêm đúng hai key automation name
  `Cuộn ngang trang tính` / `Cuộn dọc trang tính`, English tương ứng
  `Scroll worksheet horizontally` / `Scroll worksheet vertically`.
  Native names phải theo runtime localization change; test trong owned
  RibbonWorksheetNavigationSmokeTests, giữ resource parity gate hiện hữu.
  Không mở rộng sang command mutation hoặc thay các resource không liên quan.
- Gắn hai standalone worksheet scrollbars bằng API/extent hiện có, coalesce
  thumb input theo frame, giữ fractional offsets và adaptive extent floor.
  Loaded split dùng đúng session/stored topology, không force Both hoặc ghi
  far cells; chỉ một input host/scrollbar topology hoạt động tại một thời điểm.
- Không sửa SDK/Scrolling/Viewport/Editing hoặc existing CI/shared docs. Formula
  bar chờ public draft/caret bridge B; không dựng editor model trong sample.
  Lifecycle split cần B release trước khi gọi full editor acceptance hoàn tất.
- Existing regression + actual loaded shell native CI/captures bắt buộc; không
  lấy headless tests làm native proof hoặc chạy heavy builds ở local.

### C — RELEASE-009 MAUI package matrix

- Nhánh mới `feature/release-009-maui-packages`.
- Writer cho workflow mới `release-009-maui-packages.yml`; scripts mới
  `build-release-009-maui-shard.ps1`, `assemble-release-009-maui-packages.ps1`,
  `run-release-009-maui-consumer.ps1`, các fixture tests tên RELEASE-009 MAUI;
  `eng/release-009-maui/` và `tests/NeraSpreadSheet.Packaged.Maui.Smoke/`.
- Reserve ADR `docs/adr/0008-maui-cross-host-package-assembly.md`; contract riêng
  `docs/release-009-maui-package-consumer-contract.md`, worklog riêng
  `docs/worklog/RELEASE-009-MAUI.md`.
- Một exact source/version/toolchain cohort, neutral packages và bốn TFM
  producers, assemble canonical MAUI package qua NuGet pack; kiểm payload và
  dependency groups, không đưa bốn partial cùng ID/version vào consumer feed.
  Consumer public API, ngoài checkout, PackageReference-only/cache cô lập;
  mỗi platform phải chạy đúng package/app hash và fresh runtime marker.
- Không sửa production/csproj SDK, existing workflows hoặc runner scripts B
  đang giữ. Có thể chuẩn bị và kiểm assembler/consumer/build matrix song song;
  đề xuất exact launcher parameterization riêng để root chuyển quyền sau B,
  không copy runner rồi âm thầm chạy source smoke cũ hoặc bỏ runtime gates.
- Không public feed; Windows-only pack không phải multi-target proof. Source
  matrix và final combined whole-B matrix là hai checkpoint khác nhau. P3 vẫn
  chờ full B, không đổi perf protocol hoặc lấy package build làm performance.

### B và root

B tiếp tục native/editor/corpus. Sau current bounded Mac probe, B được nhận
lại WPF `NeraSpreadsheetControl.cs` (slice 49 đã tích hợp), cùng các owned
formula/split files để bổ sung public bridge draft/caret/notifications và
Begin/Commit/Cancel trên chính editor hiện hữu. API phải có tests/docs, giữ
validation, một Undo, focus/caret và cleanup khi canonical editor đã hủy.
Chưa thay scheduler từ giả thuyết; numeric stderr counts và callback activation
isolation chỉ là diagnostic, không phải production fix hoặc runtime proof.
MAUI external-cancel cleanup và split cleanup cần native regressions riêng.

Cho B một biến thể diagnostic riêng trong Mac paired job sau candidate gốc:
checkout cùng source ở thư mục tạm riêng của run, chỉ bỏ registration của
`NeraCellEditorHandler` để dùng default handler, không sửa tracked candidate.
Giữ toàn bộ editor/analytics assertions và runtime timeouts; ghi rõ variant
không phải acceptance. Baseline/candidate gốc vẫn chạy và failure vẫn làm job
fail dù variant tiến xa hơn hoặc success. Không upload/publish variant package
hoặc gọi đó là production fix. Báo exact source delta và entry stages để root
quyết định bước tiếp, không mở nhiều biến thể hoặc rerun-until-green.

Sau matched IPS của run `33985385938`, cho B thêm read-only diagnostics trong
paired Mac workflow đang sở hữu: đối chiếu generated registrar/linker metadata
của đúng baseline/candidate đã build trong job, đặc biệt native editor subclass.
Chỉ đọc outputs hiện có bằng công cụ sẵn trên runner, không build biến thể,
install tooling, debugger/OS changes hoặc sửa production. Xuất bounded summary
tối đa 32 KiB gồm logical type/selector/signature/flags, source/toolchain và
relative output hashes; nếu output không có thì báo unavailable, không suy đoán.
Không upload raw generated files/binlog/paths/UUID/native dumps. Native runtime
failure và existing acceptance gates giữ nguyên; metadata không thay runtime.

Root giữ sample Commands/command audit, tất cả shared docs/CI ngoài các quyền
mới ghi rõ, review và tích hợp tuần tự. A/C báo đường dẫn cần thêm trước sửa;
không ghi cùng CURRENT hoặc worklog của lane khác. CI đúng HEAD cuối và tất cả
acceptance OPEN dưới đây vẫn giữ nguyên, không tự giảm phạm vi để báo 100%.

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
| A — UX-007 | Keyboard, focus, accessibility; hoàn thiện localization/chrome/customization còn thiếu của UX-006 | SOURCE RELEASED / INTEGRATED; hardware/combined OPEN |
| B — TABLE-007 | Shared structured-formula editor cho split/MAUI; corpus do LibreOffice thật tạo | ACTIVE |
| C — PERF-008-HARNESS | Harness, stress tests, paired baseline trên CI runner riêng | SOURCE RELEASED / INTEGRATED; P3 OPEN |

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
- Chuyển B writer duy nhất `scripts/run-maui-maccatalyst-smoke.sh` chỉ cho
  diagnostic sau failure: chờ tối đa 10 giây cho crash report của process/run
  vừa exit, in sanitized exception/termination/thread frames để khoanh lỗi.
  Giữ success criteria, launch/runtime timeout và exit status; không retry để
  được xanh. Không in/upload raw report có Machine ID, đường dẫn người dùng,
  UUID thiết bị hoặc environment. Không đổi cleanup/phạm vi filesystem hiện có.
- Chuyển B writer duy nhất `scripts/run-maui-windows-smoke.ps1` chỉ để đọc
  bounded stage sidecar của đúng resultPath/attempt khi process thất bại,
  whitelist stage labels, không workbook values/paths/environment. Giữ retry
  count/criteria, timeout, frame/success gates và cleanup cũ; trace không được
  thay result JSON hoặc biến failure thành success. Không retry đến khi xanh.
- Cho B tạo riêng `.github/workflows/table-007-native-diagnostics.yml` để
  rút ngắn vòng PROBE: chỉ Windows.Smoke và MacCatalyst.AnalyticsSmoke trên
  hosted runners, giữ setup/scripts/timeouts/attempts và tất cả assertions của
  hai project như full CI. Push chỉ nhánh B, không sửa ci.yml. Có thể chạy
  diagnostic trước trong vòng probe; final source vẫn bắt buộc ba existing
  workflows/bảy jobs và diagnostic xanh đúng HEAD. Không thay final gates.
- Chuyển B writer duy nhất
  `src/NeraSpreadSheet.Maui/NeraMacCatalystSKGLViewHandler.cs` cho diagnostic
  bounded render-depth/queued/drawing/disposed stage, không đổi scheduling,
  dispose hoặc success criteria trong probe. Trace chỉ stage/depth/flags,
  không dữ liệu workbook/đường dẫn/identity thiết bị. B báo evidence trước khi
  đề xuất production fix; root không sửa file này đến khi B release.
- Windows SmokePage được bắt native Application.UnhandledException để ghi
  failure stage và exception type/stack đã sanitize vào existing failure
  result; không set Handled, không nuốt lỗi, không success fallback. Chỉ đăng
  ký trong smoke, unsubscribe Dispose; giữ runner retries/timeouts/assertions.
  Loại workbook values/đường dẫn/UUID khỏi diagnostic, không upload raw dump.
- Cho B một paired baseline/candidate diagnostic trong cùng hai hosted jobs
  của `table-007-native-diagnostics.yml`: baseline cố định
  `2e8482c25a44797a479b276ae26f472811a0a81e` checkout ở subdirectory riêng,
  build bằng cùng SDK/workload và chạy tuần tự trước candidate bằng current
  sanitized runner scripts. Không sửa baseline, OS/signing/security hoặc dùng
  debugger bypass; labels/results tách biệt. Giữ runtime timeouts/attempts/
  assertions, không continue-on-error hoặc success fallback. Candidate vẫn chạy
  để thu evidence nếu baseline fail, nhưng job failure được giữ; baseline không
  thay candidate acceptance hoặc final full/iOS/Q003C gates.
- Cho B probe Mac stderr của chính synthetic smoke process bằng dup2(fd2)
  trong owned SmokePage trước native attachment. Chỉ file temp riêng current
  process/run trong app sandbox, không truncate file của lượt/instance khác.
  Runner chỉ đọc failure-only tối đa64KiB, xuất tối đa64 symbolized method
  frames qua whitelist và bỏ paths/UUID/addresses/registers/locals/values;
  không upload raw stderr, dump hoặc artifacts chứa dữ liệu native chưa lọc.
  Cleanup chỉ exact current-run file đã xác minh. Giữ Console/result criteria,
  timeout/attempts; không debugger attach, OS/signing/security hoặc scheduler
  changes. Redirect chỉ để thu evidence, không thay success gate hay kết luận fix.

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
- Approval count cuối đến sau C đã push revision v2 `97f20261`, run
  `33973443725`. Root thấy phase measurement đã completed nên không cancel;
  v2 được lưu riêng, không trộn. C freeze v3 theo counts cuối đã chốt trước khi
  đọc candidate raw; giữ cả v1/v2 dù kết quả nào.
- C final candidate `3cefe685` đã dispatch full `33974253095`, iOS
  `33974254655`, Q003C `33974255889`, isolated `33974159142` đúng SHA.
  Source chưa release: root review thấy Measure hash precomputed output trước
  batch, chưa recapture kết quả thực sau batch. C được yêu cầu bổ sung before/
  after postcondition ngoài measured window + negative drift regression, giữ
  v3 counts/thresholds/datasets và archive các run cũ; new final SHA cần gate lại.
- Exact-final perf run `33974159142` tại `3cefe685`: C báo INCONCLUSIVE 4/11,
  native 2/2 PASS, 0 skip. Giữ artifact `9971867019`, không dùng implementation
  `726ace80` green thay final. Root không retest commit còn correctness gap;
  sau guard fix chạy full matrix ở new HEAD, nếu còn noise chỉ cho một bounded
  full exact-HEAD retest, không đổi policy/counts hoặc rerun-until-green. Nếu
  vẫn noisy giữ P1/P3 OPEN và yêu cầu controlled-runner decision.

## Checkpoint vận hành

- 06/09: R3 Windows source `4534e231` đã PASS run `33979253272`, artifact
  `9973277152` root verify. A/B chưa integrated, MAUI/combined R3 vẫn OPEN.
  A release desktop sau local native capture AccessDenied; final producer
  chuyển CI, không đổi quyền/né lỗi. B checkpoint `ee675078` đang exact gates.
- A/B có thể tự dispatch existing workflows trên nhánh mình bằng configured
  Git authentication, giữ secret trong memory, kiểm remote SHA và duplicate
  runs; báo IDs/exact-head evidence. Không cần chờ root cho mỗi checkpoint,
  không thay đổi workflow triggers hoặc tự tạo credential.

- C final `fe015864` đã được root xác minh bốn workflow đúng SHA xanh, review
  corrected output guards và ghép bảy commits sạch thành `4e42a584`.
  [Integration/evidence](PERF_008_INTEGRATION_20260905.md); P3 chưa chạy combined.
- A source `1c855249` đang CI; producer HCLight open-Picker cần sửa thêm. A đã
  release sample paths, nhưng tiếp tục giữ production/ci.yml và reacquire
  desktop độc quyền để kiểm chứng. B/C/root không dùng native local.
- B `35cedeaa` xanh desktop/Windows MAUI/Android, còn Apple failures. Source
  `3e9239ab` thêm layout-ready wait và stage diagnostics, cần gate đúng SHA mới.
- Root thêm RELEASE-009 package consumer qua workflow riêng; CI pending,
  không đụng ci.yml A đang giữ. Local chỉ plan/parser/architecture/metadata.
- Dung lượng C khoảng 130 MB; dừng local heavy builds, không cleanup workaround.
  Heartbeat trong task root kiểm tra mỗi 15 phút, không tạo task trùng/đổi model,
  im lặng khi chưa có thay đổi đáng chú ý. Các capacity interruptions được
  tiếp tục trên task/model cũ, không thay worktree hoặc ghi đè source.

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
- R3 audit: artifact hiện chỉ có 18 core packages, chưa có WPF/WinForms/MAUI/
  Direct2D nupkg. Packable metadata hoặc source project build không thay
  isolated PackageReference consumer/loaded host proof; R3 vẫn OPEN.

## Single writer và tích hợp

- A final24d130c6 đã release toàn bộ production/tests/docs/ci.yml; root nhận
  bảy commits sạch đến79fb8aa9 và verify mọi A-owned path byte-equivalent.
  Exact source full33983346342/iOS33983348485/Q003C33983350512 xanh7jobs.
  Root review final images và delta resource/keyboard/customization, không
  thay bằng CI parent. A không còn writer/desktop lease; root giữ ci.yml.
- B đã release thêm riêng WinForms delta64c04f5f: NeraSpreadsheetControl.cs
  và Table007WinFormsEditorLifecycleSmokeTests.cs, Windows job101352853547 /
  run33983522435 PASS111/111,0skip. Root kiểm parent blob khớp, nhận đúng hai
  source/test paths bằng apply_patch, không lấy B docs. Như WPF49, không nhập
  trùng source/test64 khi nhận whole B; docs delta64 chưa tích hợp. Native B
  source còn HOLD, không coi partial releases là whole source-green.

- B đã release riêng delta hai file WPF tại
  `49e1debeaa6187c91546d23c6ac63f96d9c10c60`: NeraSpreadsheetControl.cs và
  Table007EditorLifecycleSmokeTests.cs. Exact Windows job `101350852141` /
  full run `33982772337` success, 109/109 native tests, 0 skip; root review
  source/test độc lập base và nhận bằng path-limited apply_patch, không lấy B
  docs hoặc code MAUI đang fail. Đây là ngoại lệ slice có bounded evidence,
  không công nhận toàn B source-green; root exact combined gates vẫn bắt buộc.
  Khi ghép whole B sau này không nhập trùng hai source/test hunks của 49; docs
  delta trong commit 49 chưa được nhận. B vẫn giữ các file/lane chưa release.

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
