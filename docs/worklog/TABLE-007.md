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
- Tests/runtime: chưa chạy ở checkpoint kế hoạch. Desktop local không sử dụng.
- Gaps: T1–T3 còn OPEN; chưa có LO artifact hoặc runtime mới.
- Rollback: revert các commit lane sau base; không migration/package mới.
- File/desktop release: source còn active; desktop không giữ lease.
- Bước tiếp theo: triển khai shared acceptance guard và split native bindings.
