# TABLE-007 — lane B

- Branch: `feature/table-007-editor-corpus`; PR lane chưa tạo.
- Base clean đã xác minh: `2e8482c25a44797a479b276ae26f472811a0a81e`.
- Implementation: đang triển khai; exact-final-head CI chưa chạy.
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
- Tests: Editing guard 9/9; producer corpus 8/8 trước thêm negative extensions;
  full Core regression đã pass (chi tiết count trong log local). WPF/WinForms
  và desktop test project build .302 0/0. MAUI Windows host/runtime smoke project
  build fallback .201 0/0; chưa chạy loaded runtime local vì lease thuộc A.
- Repro/fix: Calc empty autoFilter full Table range gồm totals bị reject tại
  OpenXmlTableCodec. Chỉ normalize empty/no attrs/no sort exact Table rectangle;
  predicate/opaque/wrong geometry vẫn reject. Calc bỏ calculated/totals/style
  metadata; không tái tạo để giả parity. Cell formulas/values còn đúng qua edit.
- Gaps: T1/T2 còn native CI và Apple hardware-key evidence; T3 corpus đã có,
  final regression/exact-head CI đang chờ. CI actual SDK phải đọc log (global
  requested .302 + latestFeature có thể chọn .400), không suy ra từ config.
- Rollback: revert các commit lane sau base; không migration/package mới.
- File/desktop release: source còn active; desktop không giữ lease.
- Bước tiếp theo: push source checkpoint cho root dispatch native CI, đọc failure
  để hoàn thiện overlay và chạy lại final SHA bao gồm tài liệu.
