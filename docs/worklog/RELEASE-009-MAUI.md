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
Local: 18 in-memory package/consumer negative fixtures PASS; all three plan modes
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

Run `33986414763`, source `e64367ad7bcb1f12f36270616b6336b88c780592`: năm
producers/fixtures SUCCESS; assembler chỉ khác `requireLicenseAcceptance`.
Đã normalize missing/default false theo NuGet ManifestMetadata, vẫn phân biệt true
và từ chối Boolean invalid/duplicate; giữ payload/dependency/framework guards.
Consumer dùng atomic frame count, chủ động request frame qua UI khi đợi (SDK tự
tắt loop khi scrolling idle), đợi completed frame sau resize và stable idle GPU
snapshot. Public PaintSurface thực tế được gọi sau TryCompleteFrame; source review
này không phải native failure đã tái hiện. Kiểm chứng build/runtime còn chờ CI.

Source `4415b890631e1f29da52f7d803efd4658392dfa8`, run `33986789725`: cả năm
producers và canonical assembler SUCCESS. NuGet default normalization đã qua
actual pack; bốn consumer build jobs đang chạy. Đã thêm `verify-app` so file set,
size/hash với build manifest, reject missing/extra/changed/path escape trước
launcher; build script cũng gọi guard này. Android log có cùng explicit prefix
như console. Android transport chỉ chứng minh completed marker, không OS exit code.

Consumer results4415: Android/iOS/Mac NU1605 (implicit Controls10.0.0 thấp hơn
Skia minimum10.0.20); Windows restore/asset isolation PASS, publish NETSDK1112.
Fix: explicit Controls/MauiVersion từ evaluated producer metadata; Windows
framework-dependent/untrimmed smoke và cùng Configuration ở restore/publish.
ADR giải thích dependency đã có; không suppress warnings/downgrade checks.
Feed hash bao gồm evaluated MAUI dependency versions cùng canonical package hashes.

Source `823e913e8581e5062a8e37e322c95c4565f6f11a`, run `33987175419`: năm
producers/canonical assembler SUCCESS. Windows consumer `101363202376` SUCCESS,
kể cả isolated assets/publish/app hash. Apple restore/public consumer compilation
tới analyzer, CA1711 cho AppDelegate: đổi managed type thành PlatformApplication,
giữ native Register name. Android build SUCCESS nhưng script gặp scalar Count
khi chỉ có một APK: bọc toàn if-expression trong array để giữ 0/1/many semantics.
Không sửa SDK hoặc bỏ analyzers; runtime vẫn OPEN.

## Bước tiếp theo duy nhất

Push/check cohort sau `823e913e` với hai fixes Apple naming/PowerShell collection;
chỉ nối launcher sau root release và giữ runtime OPEN tới actual evidence.
