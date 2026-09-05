# RELEASE-009 MAUI — Handoff lane C

- Branch: `feature/release-009-maui-packages`.
- Base: `50cb357a00d6bb8a6b134cdeebce624a09bd1b21`, root verified five gates.
- PR #1 giữ Draft/unmerged; lane này chưa tạo PR hoặc sửa integration branch.
- PERF branch/source giữ nguyên `fe01586468d455c2ec26cc084e523c32c4c31baa`.

## Đã triển khai, chưa nghiệm thu runtime

Producer/assembler, public isolated consumer/platform glue, workflow build stage,
ADR0008/contract mới. Không sửa SDK csproj, production, existing workflows,
launchers hoặc shared status/CURRENT/wave. Scope grant ở wave root 06/09.

Producer/assembler commit: `feb12d5f`.
Local: 15 in-memory package/consumer negative fixtures PASS; all three plan modes
and PowerShell parser PASS; architecture/packaging metadata/diff/privacy checks PASS.
Build/publish/native chưa chạy local do disk constraint. CI exact implementation
HEAD sẽ được ghi khi commit và run được xác minh; không dùng baseline green thay.

## OPEN

Shared Windows/Mac launcher parameters đang chờ root/B; Android/iOS shared
launcher extraction do root. Chưa nối native execution, marker verification hoặc
native editor. Build-only manifests luôn ghi runtime OPEN. Whole B/P3 còn chờ.
Run `33986092369`, source `4a7cf6fbc5c2b284d7f8bfbee351efe7c0e30347`: fixtures
và cả năm producers SUCCESS. Actual SDK10.0.302 build/pack và informational
version checks qua Windows/Android/iOS/Mac/neutral. Canonical pack tạo được
nupkg nhưng metadata-equivalence guard FAIL; consumer jobs chưa chạy. Tiếp theo
normalize equivalent canonical TFM spelling (không bỏ group/version checks) và
thêm diagnostic chỉ dependency/framework components để xác định delta còn lại.

## Bước tiếp theo duy nhất

Push consumer/workflow checkpoint tiếp theo `feb12d5f` rồi kiểm workflow build
stage ở đúng SHA; sửa lỗi thực tế theo failed job, không đổi runtime OPEN thành PASS.
