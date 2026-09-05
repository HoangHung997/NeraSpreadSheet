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
Local: 18 package/consumer và 12 shared native transport in-memory fixtures PASS; all three plan modes
and PowerShell parser PASS; architecture/packaging metadata/diff/privacy checks PASS.
Build/publish/native chưa chạy local do disk constraint. CI exact implementation
HEAD sẽ được ghi khi commit và run được xác minh; không dùng baseline green thay.

## OPEN

Shared Windows/Mac launcher parameters đang chờ root/B; iOS vẫn chưa release.
Root đã release riêng Android223; owned workflow/wrapper nối Android native,
actual execution vẫn OPEN tới CI success. Build manifests không tự nâng runtime
thành PASS; runtime-verification riêng chỉ có sau own cohort validator. Whole B,
native editor và P3 còn chờ.
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

Source `f80078381f1b74c2e6e8dd56dd0d0dd283eb27f5`, MAUI run `33987543995`:
Windows/iOS/Android consumer SUCCESS; Mac build 0 warnings/errors nhưng fixed
bundle basename discovery không tìm đúng một app. Thay bằng unique actual
CFBundleIdentifier qua built Info.plist; appName được ghi và kiểm với build
manifest. Năm producers/assembler vẫn SUCCESS. Existing Q33987598805,
packages33987600233, demo33987601758 SUCCESS; full33987595198/iOS33987596708
còn chạy tại checkpoint này, không dùng thay final exact-head gates.

`f8007838` cuối đã xanh đủ năm existing gates ở các run đã nêu: full33987595198,
iOS33987596708, Q33987598805, packages33987600233, demo33987601758. Đây không
thay exact-head gates của Android wiring mới.

`bcebe527e338d0153e092c7928c2bc36595aec76`, run33987907632: ba consumer
Windows/iOS/Android SUCCESS; Mac chọn đúng bundle, build0/0 rồi inventory guard
FAIL. Chụp/kiểm inventory dùng chung Python scanner để giữ hidden-file/internal-link
semantics và appName; vẫn đọc lại actual payload để đối chiếu manifest, không bỏ
hash/missing/extra guards. Diagnostic chỉ bounded relative file names.

## Partial Android transport release và integration exclusion

Nhận đúng root `22338c79568af9106d9c6fda660180f1203940cd` qua path-limited
apply_patch; import riêng `dfd31be98dc64150bd34fcb13c1aa336058eb4b5`. Ba shared
files giữ nguyên và vẫn root-owned:

- run-maui-android-smoke.sh blob `f4ec92e07071e0b08b27ec5bf116a8bd1c27a809`.
- verify-native-smoke-result.py blob `86ef8ccd2f4d543c98a8f02c2a45fecd9b1341d1`.
- test-native-smoke-result.py blob `9cada9e7b01d06e5d46f220deb4cb103d3db75e8`.

**Khi ghép C vào root: bỏ qua import commit dfd31be9 và ba shared files**, giữ
parser/tests root mới hơn; không cherry-pick chúng đè lên iOS framing fix.
Android source transport proof do root: job101364379179/source223 và trước đó
101363322182. C gọi explicit APK/bundle/tag/prefix/fresh-result sau verify-app,
không retry/fallback; sau helper0 còn bắt own source/version/feed/nonce/target,
required assembly versions, public postconditions và >=3 completed frames.
Generic legacy min2 không phải consumer acceptance. Chưa có Android native run
ở source wiring này; Windows/iOS/Mac native, native editor/fullB/P3 vẫn OPEN.

## Bước tiếp theo duy nhất

Push/check cohort Android native sau importdfd và owned wiring; xem Mac inventory
và Android actual marker, giữ mọi failed/missing/cohort mismatch làm gate đỏ.
