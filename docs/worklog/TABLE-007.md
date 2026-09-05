# TABLE-007 — lane B

- Branch: `feature/table-007-editor-corpus`; PR lane chưa tạo.
- Base clean đã xác minh: `2e8482c25a44797a479b276ae26f472811a0a81e`.
- Product checkpoint đã push: `9bf24af9a44ce25da4826edb2f6039203f5416f1`;
  desktop21-path slice đã partial release, wholeB vẫn HOLD. Diagnostic checkpoint
  `7fa83d27813c9620f6394dc7c099b88c71281c2d` có completed baseline-page partition:
  variant analytics PASS, original native gates đỏ. One-off variant đã đóng;
  historical commit giữ exact patch và evidence. Không có active variant mới.
- Đã đọc kiến trúc, status/CURRENT, wave, Table native/structured/split contracts,
  editor/corpus tests và TableCompatibility benchmark.
- Root cho phép riêng workflow `table-007-libreoffice.yml` và producer script;
  existing workflows/project/shared status vẫn thuộc root.
- Checkpoint plan: dùng Session.Editor hiện có, shared safe assistant boundary;
  MAUI wrapper host giữ view GPU và một overlay; Calc thật chạy ở CI.
- Producer commits: `2e76d544` plan/workflow/script, `b2b942b6` isolated push
  trigger, `acee9aba` URI privacy audit fix. Producer run `33971871140` success,
  artifact `9971160827`; Calc 24.2.7.2, package 4:24.2.7-0ubuntu0.24.04.6.
- Fixture SHA-256: `531D092BD1A4D1B89EB5C99900515BC3E6E78158B828DB6365E10D8891B864CE`.
  Chỉ core metadata/timestamps sanitize; native Table/worksheet/styles payload
  hashes được kiểm tra từ producer manifest. Không đổi nhãn producer.
- Source checkpoint đang hoàn thiện: safe acceptance guard dùng chung; native
  split popups/full-cell clips/point-mode/highlights; MAUI reused editor shell và
  native keyboard adapters. Root transfer MAUI Windows SmokePage/helper và
  NeraSpreadSheetMauiAppBuilderExtensions chỉ cho Apple editor registration.
- Tests: Editing guard 9/9; producer corpus 13/13 gồm negative extensions;
  Core checkpoint 1522/1522; Viewport sau layout extraction 66/66 và MAUI
  headless 44/44. WPF/WinForms
  và desktop test project build .302 0/0. MAUI Windows host/runtime smoke project
  build fallback .201 0/0; chưa chạy loaded runtime local vì lease thuộc A.
- Repro/fix: Calc empty autoFilter full Table range gồm totals bị reject tại
  OpenXmlTableCodec. Chỉ normalize empty/no attrs/no sort exact Table rectangle;
  predicate/opaque/wrong geometry vẫn reject. Calc bỏ calculated/totals/style
  metadata; không tái tạo để giả parity. Cell formulas/values còn đúng qua edit.
- CI tại cf680688: full `33973005182`, iOS `33973006560`, Q003C `33973008084`.
  Core/Android checkpoint success; full chưa green. WPF hidden standalone draft
  rỗng bị analyzer reject khi split editor giữ session edit: thêm formula-only
  guard và loaded regression empty/literal/partial draft cho cả desktop host.
  Apple override nullability đã sửa theo compiler; Windows native Enter đã qua,
  Alt+Enter assertion thêm normalization line ending và raw draft diagnostics,
  chưa gọi runtime đó là PASS trước rerun.
- Coordinator transfer bổ sung: ViewportEngine chỉ extract ComputeLayout dùng
  EnsureMetrics hiện hữu, không duplicate layout engine trong Compose; test
  Table007EditorGeometryTests; ba MAUI Android/iOS/Mac SmokePage và helper riêng.
  Native editor phase chạy trước analytics, giữ các gate cũ, restore selection,
  dispose wrapper. Android dùng native DispatchKeyEvent; Apple dùng native
  InsertText/marked-text, không chứng minh hardware PressesBegan/OS keyboard.
- Geometry regression: fractional offsets, hidden/frozen rows/columns, merge,
  worksheet switch, filtered snapshot refresh/reuse và no display-list/recalc.
  MAUI dùng real host bounds khi chưa có usable GPU frame; không giả GPU PASS.
- Followup source review sau ac96e6cc: editor giữ pane bắt đầu khi active pane
  đổi; pane bị bỏ mới fallback pane hiện tại. Point-mode dừng khi mất capture,
  cancel giải phóng capture; completion xóa provisional highlights cũ. Loaded
  desktop regression thêm cross-pane Table range, moved caret và capture loss;
  project build .302 vẫn 0 warnings/errors, runtime đợi CI.
- MAUI bounds refresh cập nhật font theo zoom; host resize hook được detach khi
  dispose. Windows loaded smoke kiểm tra full raw width/clip/font khi zoom rồi
  restore dimensions/scroll/zoom. Apple smoke nhập ký tự qua native InsertText
  để đi qua native text notifications. Chưa gọi các runtime bổ sung là PASS.
- CI source `35cedeaad7dd045b40b474a04b00df3ef6c38f5a`: full `33977571678`,
  iOS `33977573066`, Q003C `33977574262`. Q003C/Core/Windows hosts/MAUI Windows/
  Android PASS. Desktop 107/107, MAUI headless 44/44, Core 1532/1532; native
  Windows OS Enter/AltEnter/Escape, full-width clip/zoom và Android native keys
  ghi table007Editor=true. CI actual SDK 10.0.400 theo logs.
- Apple còn FAIL: iOS BeginEdit quá sớm khi view size=-1; Mac process signal 11
  trong editor phase sau GPU draw-core-success, chưa có managed failure/result.
  Followup iOS chờ SizeChanged thật (bounded 5s trong timeout cũ); Mac thêm các
  mốc native input/marked-text/cleanup. Host dùng một RectangleGeometry và chỉ
  cập nhật khi rect đổi, tránh invalidation vô điều kiện mỗi frame; chưa kết
  luận đó là nguyên nhân crash trước runtime mới. Không đổi native gate/timeout.
- CI `3e9239ab3282a45bd6c52fb9fb62a8c240857458`: full `33978509220`,
  iOS `33978510465`, Q003C `33978511735`. iOS đã PASS sau real-layout wait;
  Core/Windows desktop/Android PASS. Mac trace xác định Enter/commit/undo/reopen
  đều qua, signal 11 trước mốc sau SetMarkedText; chưa chứng minh API đó là
  nguyên nhân vì có await/native setup trước lời gọi. Thêm plain
  UIKit baseline probe tạm (không MAUI handler/workbook callback) để phân biệt
  lỗi native API với editor; vẫn giữ probe trên reused editor, không dùng baseline
  thay acceptance. Native first-responder/selection được validate trước gọi.
- MAUI Windows tại3e fail-fast trước marker cả hai attempts hiện hữu. Clip không
  còn gắn geometry rỗng trong constructor; chỉ gắn sau có cell geometry hợp lệ,
  như thời điểm35 trước đây, vẫn reuse khi rect đổi. Chưa xác nhận root cause.
- Root transfer riêng `scripts/run-maui-maccatalyst-smoke.sh`: sau failure chờ
  report của đúng PID/process/run tối đa10s; chỉ in selected exception/termination/
  thread frames đã lọc UUID/path, bỏ raw report dump. Không đổi cleanup scope,
  launch/runtime timeout, success criteria hoặc exit status. Bash syntax và
  synthetic Python audit PID/name/privacy/two-part IPS parsing đã PASS.
- CI exact `ee675078`: full `33979429092` FAIL Mac và MAUI Windows; iOS
  `33979430234`, Q003C `33979431250`, Core/desktop/Android PASS. Mac chưa tới
  baseline-before-marked; không có matching current-process IPS sau bounded10s.
  Bổ sung mốc sau await, constructor/text/attach/focus/selection của plain UIKit
  để xác định native operation; baseline không thay probe editor hay gate cũ.
- MAUI Windows vẫn0xc0000409 trước result cả hai attempts tại ee; không gọi đó
  là startup noise. Product delta Windows từ35 chỉ clip geometry reuse/mutation:
  followup gán RectangleGeometry mới chỉ khi Rect đổi, dùng native property-map
  path của35 đãPASS, vẫn tránh per-frame allocation khi geometry không đổi.
  Windows page/helper thêm stage trace gắn đúng result path, không ghi draft/
  đường dẫn/thông tin máy. Root đã transfer riêng Windows runner: failure reader
  chỉ đọc8KiB/64labels đầu đúng resultPath của attempt, whitelist literal stages,
  không thay retry/timeout/result/frame gates/cleanup. PowerShell syntax và
  synthetic stage allowlist/privacy audit PASS.
- Followup architecture verification và diff whitespace PASS. Không heavy build/
  loaded local vì disk/desktop lease; compilation/native regression phải qua CI.
- CI exact4c: full `33980048742` FAIL Mac/MAUI Windows; iOS `33980050483`,
  Q003C `33980052256`, Core/desktop/Android PASS. Mac chỉ tới editor-opened,
  chưa native-ready sau await60ms; không gọi InsertText/SetMarkedText. Unified
  log ghi native focus trước NSWindow didCreateScene, RTI chưa có XPC endpoint.
  Followup queue editor phase qua awaited Dispatcher.DispatchAsync để initial
  GPU paint/window attachment stack thoát trước khi mở/focus native control;
  chưa xác nhận đây là nguyên nhân signal11. Giữ toàn bộ IME/analytics gates.
  C review read-only độc lập xác nhận selection trong PaintSurface có thể gọi
  nested DrawCore qua MainThread inline trước outer flush; dispatcher isolation
  xử lý caller của lane B, không sửa Mac renderer thuộc root.
- Windows4c cả2attempts tới table-editor-complete và smoke-editor-verified:
  OS keys/history/stale caret/full-width clip/zoom assertions đều qua. Fast-fail
  xảy ra SAU editor phase, nên không gọi startup noise hoặc editor PASS đầy đủ.
  Followup thêm literal stages cho pinch/pan/wheel/resize/surface recreation để
  xác định original stress phase gây failure; giữ nguyên hành vi và gates cũ.
- CI source3f: full `33980860391` FAIL Mac/Windows; iOS `33980861653`,
  Q003C `33980863021`, Core/desktop/Android PASS. Mac không tới editor-enter
  trong queued callback; GPU first/subsequent draw đều tới flush/present success.
  C review loại trừ first Clip/Focus/IME vì BeginEdit chưa chạy; chưa tìm native
  misuse của editor từ source. Followup giữ original bare view lúc initial attach,
  queued callback mới bọc SAME view/session trong editor host; thêm create/attach
  stages, giữ mọi editor/analytics assertion. Không sửa renderer của root.
- Windows3f cả2attempts tới after-surface-reinsert của chu kỳ đầu, trước frame
  tái tạo. Followup thêm handler-changed/loaded/recreated-frame và focus category
  (none/surface/editor/other), không ghi values/path/device IDs. Focus hoặc
  stale native unload chưa phải nguyên nhân được chứng minh.
- Root cấp riêng `table-007-native-diagnostics.yml`: hai hosted jobs chạy đúng
  existing Windows.Smoke/MacCatalyst.AnalyticsSmoke và runner/SDK/workload/timeouts
  hiện hữu; push chỉ branch B. Probe iterations dùng workflow hẹp; final source
  vẫn phải xanh cả ba existing workflows cộng workflow chẩn đoán đúng HEAD.
  Không thêm dependency parser vào repo; môi trường không có PyYAML/node yaml.
  Workflow review thủ công, PowerShell syntax/diff/architecture PASS; GitHub
  parser và native builds phải xác nhận workflow mới trước khi dùng evidence.
- Diagnostic exact243 run `33981545011` accepted/builds PASS, cả hai native jobs
  FAIL. Mac vẫn signal11 trước host-attach-enter: editor subtree chưa được tạo,
  nên first Clip/Focus/IME không có đường gọi. C không tìm misuse của editor
  qua source; renderer reentrancy/lifetime vẫn là exposure chưa chứng minh fatal.
- Windows243 cả2attempts: native surface loaded trước remove, focus NONE;
  old handler clear và new handler/create/reinsert trả về, new surface vẫn
  unloaded, không tới next Loaded/recreated-frame. Không hỗ trợ focused-view
  hypothesis. Đã đề xuất root minimal renderer-depth probe và native XAML
  UnhandledException→existing failure boundary để lấy inner stack; chưa sửa
  những ranh giới đó trước khi được root transfer/confirm.
- Root đã transfer riêng Mac handler cho probe, chưa cho sửa scheduler/dispose.
  Followup ghi tối đa96 lifecycle stages/renderer với draw depth, pending,
  disposed/main-thread flags tại request/callback/size/disconnect/dispose và
  DrawSafely enter/finally. Không đổi quyết định scheduling hay giải phóng.
- Windows native UnhandledException được root cho phép đi vào existing Fail:
  giữ inner exception objects ở boundary, JSON chỉ xuất tối đa4 exception types/
  HResult +16 method frames mỗi cấp, không raw message/source paths/arguments.
  Không set Handled, không swallow/success fallback; unsubscribe khi Dispose.
  Architecture/diff/PowerShell parser PASS; extracted formatter compile và
  synthetic message/path/UUID exclusion + inner type retention audit PASS.
  Compilation/native runtime còn phụ thuộc next diagnostic CI.
- Diagnostic9c `33982202648`: Mac FAIL trước wrapper creation; tất cả7 draw
  enter/exit depth1 cân bằng, main=True/disposed=False, không disconnect/dispose
  (30 lifecycle records, dưới cap96). Chưa có evidence nested draw cho fatal này.
  Windows FAIL functional assertion committed Table formula value, chưa tới
  recreation nên không dùng lượt này để kết luận native XAML crash đã hết.
- Root và C phát hiện WPF lifecycle bug độc lập: Session.ActivateWorksheet hủy
  Editor trước ActiveWorksheetChanged, CancelEditor cũ early-return để native
  overlay/popup còn tồn tại. Fix luôn cleanup UI, chỉ Focus khi thực sự cancel;
  state-null không chọn ô cũ/không mutate history/cells trong sheet mới.
  Table007EditorLifecycleSmokeTests có2 loaded STA cases với actual changed
  native draft/popup; kiểm tra direct activation cleanup trước caller cleanup
  và external canonical cancel + moved selection. Incremental desktop test
  project build SDK10.0.302 PASS 0warnings/errors (24s); native runtime chờ CI,
  không chạy local do lease A. Root giữ sample tab-click regression riêng.
- Partial release WPF: source `49e1debeaa6187c91546d23c6ac63f96d9c10c60`,
  full run `33982772337`, Windows job `101350852141` SUCCESS, desktop109/109,
  zero skipped; build/runtime/capture PASS. Root nhận riêng delta WPF control và
  Table007EditorLifecycleSmokeTests, không nhận toàn commit có B-only docs.
  Hai source/test paths dùng APIs baseline2e; toàn lane B vẫn HOLD.
- Diagnostic49 `33982749901`: Windows cả hai attempts qua toàn editor assertions,
  vẫn fast-fail sau reinsert surface đầu tiên, trước Loaded/recreated frame;
  không có native XAML exception marker. Mac vẫn signal11 trước editor wrapper
  creation, draw depth1/disposedFalse, không matching crash report trong10s.
  Full49 Android fail native accessibility root child count sau editor phase;
  chưa coi đó là transient hoặc hạ gate. Core/Windows desktop PASS.
- Root cấp paired same-run diagnostic: hai hosted jobs checkout thêm pinned
  base2e vào subdirectory, dùng cùng SDK/workloads/current sanitized runners;
  chạy baseline rồi candidate với outcomes/result labels riêng. Candidate vẫn
  chạy sau baseline failure nhưng mọi failed step giữ job FAIL, không thêm
  retry/timeout/assertion bypass. Baseline không thay candidate acceptance.
- WinForms bounded lifecycle followup theo grant root: CancelEditor luôn gọi
  HideEditor (đã bao gồm reset candidates), chỉ Focus/return true khi thực sự
  hủy draft. Native regression riêng có2 STA cases: activation tự đóng draft/
  suggestions ngay và external canonical cancel + moved selection; không đổi
  cells/history/selection. Không sửa hai WPF paths đã release. Incremental
  desktop test build SDK10.0.302 PASS0warnings/errors,25.7s; runtime đợi CI.
- Paired workflow source `c6de63379b16718e67be24df6ba7bfb4a9296e9d` đã push;
  run `33983361424` được GitHub parser chấp nhận, hai jobs đang thực thi baseline.
- Partial release WinForms: exact64 full `33983522435`, Windows job
  `101352853547` SUCCESS, desktop111/111 zero skipped, build0/0/runtime/capture
  PASS. Root nhận riêng WinForms control delta và new lifecycle test (base2e
  APIs), không cherry-pick B docs. Đóng writer hai paths; whole lane vẫn HOLD.
- Paired c6 `33983361424`: Windows baseline/candidate PASS (candidate
  table007Editor=true,59frames,3recreations); baseline cần existing retry sau
  native fast-fail attempt1. Chưa chứng minh nguồn fatal hoặc fix. Mac baseline
  PASS/candidate FAIL cùng job/toolchain: signal11 trước editor host creation,
  tám balanced draw pairs depth1, không dispose/IPS. Khoanh delta B, chưa cause.
- Android49 có race validation khả thi từ event order: SDK bridge đọc projection
  trước smoke PaintSurface callback, UI thread insert analytics đồng thời; callback
  có thể thấy fresh model2nodes nhưng native snapshot từ đầu frame cũ. Guard
  chỉ sửa SmokePage: bỏ validation state-1; sau transaction publish frame floor
  trước state1, skip frame in-flight và yêu cầu frame kế tiếp rồi giữ mọi native
  provider/identity/bounds/action assertions. Counter dùng Interlocked/Volatile;
  không readiness polling hoặc bridge fallback. Android64 trước guard PASS,
  không gọi race đã hết hoặc guard đã validated trước exact-source runtime.
- Paired64 `33983495915`: baseline Windows/Mac PASS, cả hai candidates FAIL;
  Windows lặp lại after-surface-reinsert trước Loaded ở cả2attempts. Kết hợp c6
  candidatePASS vẫn chỉ chứng minh intermittent failure chưa xác định cause.
- Root cấp native stderr probe chỉ synthetic Mac smoke: FileMode.CreateNew,
  file sandbox riêng run/PID với identity header; dup2 chỉ fd2 của chính process
  trước attach. Runner failure-only đọc<=64KiB, whitelist<=64 native method
  frames, không message/path/UUID/address/register/value/raw dump hoặc upload.
  Cleanup xác minh run/PID/header/owner/freshness rồi unlink đúng một file; không
  đổi gates/timeouts/attempts, signing/OS/debugger/scheduler. Native stderr audit
  PASS (two frame formats/ObjC/privacy/cap/identity/freshness/bounded read/cleanup),
  bash syntax/architecture/diff PASS. Native compile/capture còn đợi CI.
- Probe0e: full `33984152897` Android guard build/native PASS, Core/desktop
  PASS. Mac full job101354516838 và pair `33984121857` job101354435125 cùng
  compilePASS/runtimeFAIL signal11, trước host attach; stderr installed/matched
  đúng process/run nhưng chưa có frame qua whitelist, IPS absent. Không suy ra
  stderr rỗng: followup chỉ thêm numeric byte/line counts và literal header/format
  presence. File không upload và cleanup sau lượt cũ, không thể đọc lại raw.
- Bounded Mac callback isolation: dùng existing DispatchAsync(Action), callback
  chỉ ghi entry và bắt đầu separate NoInlining async phase; await cả dispatch
  và actual editor task trước analytics. Giữ queue/order/assertions/timeouts;
  không renderer/scheduler/model mới. Tách entry của callback khỏi editor phase
  để phân biệt activation/creation, chưa xác nhận fix hay nguyên nhân.
- Gaps: T1/T2 còn native CI và Apple hardware-key evidence; T3 corpus đã có,
  final regression/exact-head CI đang chờ. CI actual SDK phải đọc log (global
  requested .302 + latestFeature có thể chọn .400), không suy ra từ config.
- Rollback: revert các commit lane sau base; không migration/package mới.
- File/desktop release: source còn active; desktop không giữ lease.
- Probe cc7596e7 `33984604032`: Mac baseline PASS/candidate FAIL trước cả
  tiny Action callback entry; captured stderr đúng identity có664bytes/5lines,
  không native/managed header hoặc address-frame lines, không matching IPS.
  Windows cùng lượt baseline PASS/candidate FAIL. Chưa xác định native cause.
- Root cấp đúng một transient Mac variant: checkout cùng github.sha sau hai
  cohorts gốc, chỉ bỏ Apple editor handler registration block; in exact diff,
  giữ original assertions/toolchain/timeout/current runner. Candidate tracked
  không đổi; failure gốc giữ job FAIL bất kể variant outcome. Không dùng variant
  làm acceptance, publish artifact hoặc production fallback. Workflow/log freeze
  trước product cleanup/API để giữ experiment inputs cùng source.
- Registration experiment freeze `4bef0f0ec1e72c17cd9ba6899fd5c15922182845`,
  narrow run `33985385938` DONE: Windows baseline/candidate PASS; Mac baseline
  PASS, candidate và registration variant compilePASS/runtimeFAIL trước Action
  entry. Exact removal diff guard PASS. Variant có matching current-process IPS:
  SIGSEGV tại libobjc realizeClassWithoutSwift, NSClassFromString, AppKit
  NSTextInputContext, UIKitMacHelper UINSInputView/UINSSceneView/window creation.
  Chưa biết class đang realize hoặc delta gây lỗi; không suy renderer cause.
  Candidate stderr664B/5lines, variant669B/5lines, không symbolized fd2 frames.
  Đóng transient variant steps sau bounded experiment; giữ product registration
  và paired diagnostic/full acceptance gates. Product delta không thay inputs4bef.
- Root cấp tiếp WPF public draft/caret bridge và idempotent split/MAUI cleanup;
  standalone WPF main writer mở lại. Đã triển khai snapshot native TextBox và
  notifications ở owner; UpdateEditorDraft không focus/history/restart, atomic
  selection validation, FocusEditor riêng; existing lifecycle route split facade.
  Canonical end dọn WPF UI ngay, hidden standalone không dựng bounds/highlights
  từ shared split state. Không thêm Core draft model hoặc editor controller.
- Native regressions mới:8 WPF bridge/validation/focus/history/lifecycle cases
  (standalone/split),2 WinForms split cleanup cases; MAUI Windows/Android helper
  giữ native editor, external canonical cancel rồi moved selection/host cleanup,
  xác minh candidates/text/cells/history. Local incremental desktop test build
  SDK10.0.302 PASS0warnings/errors (initial test alias collision đã sửa); chưa
  chạy local runtime vì lease A. Architecture/diff PASS; native CI còn chờ.
- Partial desktop release source `9bf24af9a44ce25da4826edb2f6039203f5416f1`:
  full `33985950156`, Windows job101359345450 SUCCESS, desktop121/1210skip,
  build0/0/GPU runtime/capture PASS; Core và Android full SUCCESS. Narrow
  `33985947947` Windows job101359337293 baseline/candidate PASS, candidate
  table007Editor=true/58frames gồm canonical-cancel cleanup. Mac vẫn FAIL.
  Release21 desktop/shared-assistant/test paths theo manifest/patch riêng
  `artifacts/table-007-desktop-bridge-9bf24af9`; base root782890b6, patchSHA256
  `eb48e632d1eab79b3d49c5d0f43869584414ba4ef34fc30e446829c78f7441e7`.
  Không chứa source ngoài lane, MAUI/Viewport/corpus/docs hoặc lặp49/64 hunks.
  Đóng writer21paths; root review/integration gates vẫn riêng. WholeB HOLD.
- Root cấp bounded read-only generated registrar audit trong paired Mac job:
  đọc own baseline/candidate build outputs, class/base/selector declarations,
  relative file hashes, SDK/workload và evaluated properties. Chỉ generated
  response flags mới là effective mode evidence; missing metadata/linker roots
  phải ghi unavailable. Không rebuild/install/debugger/production flags/variant.
  JSON output<=32KiB, không raw file/binlog/path/UUID upload. Synthetic parser
  audit signatures/type allowlist/privacy/flags/output bound PASS; architecture
  và whitespace PASS. Hosted metadata collection còn đợi lượt mới.
- Generated audit d1e19023 `33986427219`, Mac job101360705616: baselinePASS,
  candidateFAIL, metadata stepPASS. SDK10.0.400/arm64/target26.0/min15.0 giống;
  evaluated TrimMode=partial, workload10.0.0/10.0.100, không có effective registrar
  flags/rsp/linker root graph. Không đồng nhất workload version với resolved NuGet.
  registrar.h cùng135 declarations; shared MauiTextView:UITextView có19 signatures
  giống nhau. registrar.mm114→115 thêm CellTextView:MauiTextView với insertText:
  và pressesBegan:withEvent: đúng shape; không thấy wrong base/selector từ đó.
  Windows job101360705755 baselinePASS/candidateFAIL cả2attempts sau first
  reinsert; product giống9bf từngPASS, intermittent lifetime failure vẫn OPEN.
- Followup cùng read-only grant lấy targeted implementation/runtime-call names,
  class-map metadata type tokens/flags và resolved package names/versions; không
  in method body, assembly UUID, source path hoặc raw metadata. Không sửa native
  registration/renderer/keyboard/timeout dựa vào type-count difference đơn lẻ.
- Audit eca8a6d2 `33986930666`, Mac job101362141244: baselinePASS/candidateFAIL,
  metadata stepPASS; Windows candidate cũngFAIL. Shared MauiTextView implementation
  hash `4182b68c858f1edd8dba5f8123b825e2478ee1ba81c51e926beefe93ffcc1e0f`
  giống hệt,19signatures/runtime calls/index190/flags3 giữ; UITextView index189/
  flags0 giữ. CandidateCellTextView index248/flags3/token609 với đúng2selectors;
  token references thay đổi không tự chứng minh invalid metadata. Cả hai resolve
  Microsoft.Maui.Core/Controls10.0.20 và SkiaSharp.Views.Maui.Controls4.151.1.
  Không thấy sai shared implementation/base/selector từ các facts này.
- Root đã cấp đúng một isolated same-source variant sau audit eca8a6d2:
  bỏ mapping cùng Apple handler class khỏi compile, xác minh generated class
  absent trước native launch; giữ all assertions và candidate failure. Patch
  review-only `artifacts/table-007-remove-apple-subclass-variant.patch`, SHA256
  `cfc3b7cec04b46b822dd9eb6398b3b43d13bf4fe97ce9047049699e2f08e8fcf`.
  Tracked native source không đổi. Patch được giữ đúng bytes trong scripts để
  hosted checkout áp dụng reproducibly; guard SHA/đúng hai paths và generated
  registrar class absence phải PASS trước launch. Không đổi original candidate,
  retries, runtime assertions/timeouts hoặc dùng variant làm acceptance.
- Class-exclusion c187fe80 `33987516524`, Mac job101363710590: baselinePASS,
  candidateFAIL; variant build/patch/absence guardPASS nhưng runtimeFAIL trước
  Action/host entry. Variant registrar.h hash giống baseline
  `f62948e90c080c8324010c1f0eacfed0edc46ae811d6fe58a53a3af590c8c819`,
  literal CellTextView vắng khỏi generated.h/.mm. Matching variant IPS vẫn ở
  realizeClassWithoutSwift/NSClassFromString/NSTextInputContext/UINSScene.
  Subclass presence không cần để tái hiện failure này; không suy subclass fix.
  Đóng one-off variant steps/checked-in patch, giữ historical commit/evidence,
  original product registration và paired native gates. Windows candidateFAIL.
- Root đã nhận exact21 source blobs tại ce1a00d2 và ghép A navigation ở
  22338c79568af9106d9c6fda660180f1203940cd. Root sở hữu/fix ShowReferenceHighlights
  ở split composers và full-cell WPF Measure; public CurrentFormula* metadata/help
  split forwarding còn OPEN tại root. B không sửa/reimport các released paths.
- Root yêu cầu read-only source map trước probe tiếp: base2e full33966917191,
  Apple101308559859 SUCCESS (result success/10frames). cf680688 Apple101324733106
  dừng ở iOS build, Mac skipped; ac96 không có exact-head run được liệt kê.
  First-known native failure35cedeaa full33977571678/Apple101336874097 buildPASS,
  nativeFAIL sau analytics-create-enter/draw-core-success, launchd signal11.
  Không có early matching IPS nên không đồng nhất instruction với c187.
- Delta ban đầu: cf thêm editor host/handler/bounds; ac sửa Apple compile, thêm
  shared ComputeLayout và Mac wrapper/editor-before-analytics;35 chỉ thêm host
  resize/font refresh và native InsertText thay Text setter. Class-excluded c187
  còn page async/dispatch trước analytics, shared Compose layout extraction,
  compiled editor host/bounds/assistant, sau đó là renderer-depth/fd2 probes.
  Trước current Action marker chỉ layout và probes có đường chạy; editor subtree
  chưa tạo. Probes sau35 không cần để tạo initial failure. Android/Windows key
  adapters bị loại bởi platform guards; renderer scheduling/disposal không đổi.
- Source byte-equality audit PASS cho global.json/Directory.Build.props/package
  versions, MAUI project/smoke project, MauiProgram/SmokeTrace/Program/AppDelegate/
  Info.plist giữa base và c187. Generated registrar có thể đổi reachability dù
  file config giống; không coi source/evaluated equality là effective mode proof.
- Source partition đề xuất (chưa được cấp/chạy): thay riêng Mac SmokePage bằng
  exact base2e blob trong isolated current-candidate checkout; giữ product và
  original failure, baseline analytics assertions. Variant không chạy editor,
  không được coi là editor acceptance. Nó phân biệt page startup/reachability
  với product delta còn lại, không tự chứng minh một dòng lỗi. Cũng bỏ later fd2
  hook; first failure35 đã không có hook. Cần đọc generated metadata sau build.
  Review-only patch/JSON artifacts/table-007-baseline-page-partition-proposal.*,
  SHA256 bf026f39611378559bb8b2a4d0dfb1096891a44e639d4a79c19e43f8fb28a83b;
  git apply --check PASS, không áp dụng lên source tracked.
- Đính chính native logging proposal: Apple objc4 objc-env.h public option là
  OBJC_PRINT_CLASS_SETUP, ánh xạ internal PrintConnecting; OBJC_PRINT_CONNECTING
  không phải public option đã xác minh. Không bật flag nào; root ưu tiên source
  partition assessment trước runtime logging, chưa cấp probe tiếp.
- Root đã cấp đúng ONE baseline-page partition với patchSHA ở trên, same-current
  checkout; before blob89bc459fa5c86b523e96bcd22f19a496e2151df6 và after blob
  28bd663814d338a7a9564b852e6f4c5eb31dd664 bắt buộc exact, dừng nếu khác.
  Workflow đóng class-exclusion variant cũ và thêm duy nhất partition này sau
  baseline/original candidate; metadata đọc bounded trước launch. Custom handler,
  native subclass/product/SDK/renderer/flags/signing và current runner giữ nguyên.
  Không chạy editor trong diagnostic baseline page; baseline analytics assertions
  không đổi và original failed steps giữ jobFAIL. Không nhận TABLE007 acceptance.
- Local partition checks: three embedded Python blocks compile; positive và
  negative guards cho frozen source/hash/path/afterblob PASS; seven Mac shell
  blocks bash syntax PASS. Approved patch git apply --check/hash PASS; original
  src/tests không đổi, architecture PASS. Native build/runtime chỉ chạy hosted
  narrow CI; không heavy/local loaded run trong desktop lease của A.
- Windows c187 job101363710716 đã đọc lại: cả2attempts qua editor assertions tới
  first after-surface-reinsert; không recreated-frame-enter/native-unhandled
  marker, có focus-none. Intermittent reattach failure vẫn OPEN.
- Baseline-page7fa83d27 narrow33988816930 DONE: Mac101367217303 baselinePASS,
  original candidateFAIL trước Action/host entry; variant patch/build/audit/run
  PASS. Baseline và variant đều10frames/2native elements/chart+pivot activation
  verified. Không tính C# marker trong printed removal diff là runtime entry.
  Windows101367217496 baselinePASS/candidateFAIL cả2attempts sau first reinsert,
  không recreated-frame/XAMLexception. Original failed native gates vẫn giữ FAIL.
- Candidate/variant generated registrar.h byte-identical
  f514f42d81b51c37c699e327153275b3665d8226e19a64e8700d9e1a377413ba;
  native CellTextView vẫn hiện diện với2selectors, shared MauiTextView19selectors.
  Targeted implementation hashes4182b68c/879814ad và class-map indices/tokens/flags
  đều giống. Hai registrar.mm cùng1054529bytes nhưng full hashes khác; không
  tuyên bố toàn generated metadata giống. SDK/evaluatedprops/packages giữ như
  paired audit trước. Kết quả thu hẹp page startup/reachability cluster, chưa
  chứng minh một dòng lỗi hoặc native editor acceptance. Đóng completed one-off.
- Đề xuất read-only intermediate tiếp (chưa cấp/chạy): từ baseline page vừaPASS
  chỉ chuyển existing analytics method sang async và thêm original awaited
  Dispatcher Action với constant entry marker trước analytics, không editor
  references/fd2 hook. Cùng original product/handler/source frozen trước89bc459f,
  expected intermediate blob0ff170b2483d5137b7e8518fdbb3e2d59d2f42c5.
  Review-only artifacts/table-007-baseline-dispatch-partition-proposal.*,
  patchSHA6996d3c42d01f96423698ece2b03c4f9ae123c188036eb41b190357698eb01e5,
  git apply --check PASS. NếuFAIL: async dispatch/control-flow/reachability đủ
  tái hiện mà không editor phase; nếuPASS: editor-related roots/body vẫn OPEN.
  Cả hai không thay editor acceptance; chỉ chạy khi root cấp đúng patch mới.
- Bước tiếp theo duy nhất: nhận quyết định root về prepared dispatcher-only
  baseline-page partition; giữ21 desktop paths frozen và wholeB HOLD, không
  tự mở thêm variant hoặc bật runtime flags.
