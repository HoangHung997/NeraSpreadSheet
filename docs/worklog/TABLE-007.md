# TABLE-007 — lane B

- Branch: `feature/table-007-editor-corpus`; PR lane chưa tạo.
- Base clean đã xác minh: `2e8482c25a44797a479b276ae26f472811a0a81e`.
- Implementation checkpoint đã push: `cf680688741addd7675ae1a4320c07125df0a5e4`;
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
- Gaps: T1/T2 còn native CI và Apple hardware-key evidence; T3 corpus đã có,
  final regression/exact-head CI đang chờ. CI actual SDK phải đọc log (global
  requested .302 + latestFeature có thể chọn .400), không suy ra từ config.
- Rollback: revert các commit lane sau base; không migration/package mới.
- File/desktop release: source còn active; desktop không giữ lease.
- Bước tiếp theo: push followup checkpoint cho root dispatch ba native workflows,
  đọc raw failure/artifact và hoàn thiện overlay trên SHA cuối bao gồm tài liệu.
