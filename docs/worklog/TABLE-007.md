# TABLE-007 — lane B

- Branch: `feature/table-007-editor-corpus`; PR lane chưa tạo.
- Base clean đã xác minh: `2e8482c25a44797a479b276ae26f472811a0a81e`.
- Implementation checkpoint đã push: `9c584bb36c840b447c3095db9095b468588c6ccb`;
  đang sửa native CI, chưa release/exact-final-head green.
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
- Gaps: T1/T2 còn native CI và Apple hardware-key evidence; T3 corpus đã có,
  final regression/exact-head CI đang chờ. CI actual SDK phải đọc log (global
  requested .302 + latestFeature có thể chọn .400), không suy ra từ config.
- Rollback: revert các commit lane sau base; không migration/package mới.
- File/desktop release: source còn active; desktop không giữ lease.
- Bước tiếp theo: push followup checkpoint rồi tự dispatch ba existing workflows
  theo quyền root đã cấp (verify remote HEAD/duplicates; auth chỉ trong memory),
  đọc raw failure/artifact và hoàn thiện overlay trên SHA cuối bao gồm tài liệu.
