# RELEASE-009 MAUI — Handoff lane C

## Desktop package consumer — 08/09 đang triển khai

- Hiện hành: branch giữ HEAD/remote `fa96692c096caab2d9b6a5c8db7d972c792d9056`,
  PR1 Draft/unmerged; **local observer patch chưa commit/push và chưa chạy native**.
  Root chỉ cho4existing paths: package Python của Mac launcher, linked fixture
  Program, own contract và worklog này. Không thay fa app/formatter/OSLog/SDK/
  shared parser/launch/cleanup/timeout/acceptance hoặc nhận root/B WIP.
- Fa source có4gates SUCCESS: iOS34183385120, Q34183386502, Windows packages
  34183388536, demo34183389821. Full34183383351 FAIL legacy Picker capture tại
  job101926823821; ảnh10039767660 theo root review chụp nền, chưa là SDK glyph bug.
  MAUI34183245138 có10/11jobs PASS: Windows1019270816779frames/child0,
  Android10192708169810frames, iOS1019270816488frames/cohort PASS. Mac101927081641
  fixture/build0/0 PASS, nhưng actual90s FAIL app-file0/unified6192;4matching stage
  constructorEntered/completed, loadedEntered, dispatchAccepted, không callback/
  run/Emit; no malformed/clipped/foreign records. Không suy nguyên nhân hay OSexit.
- Local patch thêm read-only kqueue exit watcher một numeric PID, cùng vòng90s,
  timeout0/oneevent, cap128observations +1registration. Snapshot cuối đóng băng và
  queue đóng trước Bash cleanup, không coi harnessTERM là spontaneous exit.
  Fixed flags tách ESRCH/denied/unavailable/unexpected, không đọc normal exit data.
  Stage+lifetime summary<=2KiB; PID present không thành live-original/hang proof.
  Fixture extract actual observer:43cases fake kernel/liveness PASS; actual shared
  CLI giữ lifetime/diagnostic-only ở pending2/no output, failure1/no output và
  complete marker0/output. Existing33transport+23package+13classifier PASS0skip;
  architecture/packaging/Bash/inline Python syntax/diff và frozen-boundary checks PASS.
  Compiled C# và nativeMac API chưa được chạy ở patch này; local patch/hash gửi
  root review trước mọi commit/push/probe.
- Một bước tiếp theo duy nhất: root review immutable local4path patch/hash và
  quyết định commit/push/probe grant; C không tự chạy lại cohort hoặc đổi source.
- Exact checkpoint `dd6eafda50f5e98bb51fe23a9e0312525be70868` có năm existing gates
  SUCCESS: full34182133420, iOS34182134752, Q34182135997, Windows packages34182137232,
  demo34182138742. Core1515/native desktop158 và local33+23+13 PASS0skip.
  MAUI34181795895: common fixture/5producers/assembler PASS; Windows101922679855
  đạt9frames/child exit0, Android101922679875 đạt11frames, iOS101922679851 đạt8frames,
  cùng own cohort/public postconditions. Mac101922679905 fixture/cleanup/build0/0
  PASS nhưng actual90s FAIL: app-file0/unified array rỗng. Không suy root cause.
- Root amended thêm CHỈ actual `PackageProvenance.cs` và `SmokePage.cs` vào scope:
  finite Mac stage diagnostics và public CoreFoundation.OSLog, same compact envelope
  sau actual CreateNew/Write/Flush/close. Không đổi scheduling/await/assertions/
  config/SDK/signing hoặc shared parser. Summary riêng <=2KiB chỉ fixed counts/
  booleans, exact process/time/transport nonce; private log không upload.
  Linked fixture kiểm actual finite formatter, actual launcher summarizer và actual
  strict CLI từ chối diagnostic-only dù full payload hợp lệ. Native branch chưa
  nghiệm thu; source mới phải qua whole cohort/sáu gates, không dùng dd thay.
- Root đã review/approve immutable cf4f9dac cho B import-only rồi một paired probe;
  C giữ classifier block/fixture byte-equal. Không nhận B import ghi đè launcher C.
- Branch `feature/release-009-maui-desktop-consumer` từ exact combined
  `d73cd2e2bcb7e2832e9a8b3fd071373b62cb2a0f`, baseline sáu gates xanh theo root.
  Giữ iOS5d/package8b/PERFfe branches nguyên trạng; PR1 Draft/unmerged.
- Nhận đúng Windows b50e60f9/Mac fee485b3 blobs B650 bằng apply_patch vào commit
  riêng `5906ea9fe4d27baaa310997cfb9938ee4d194934`. **PHẢI nhận commit này khi
  tích hợp C**, không skip như import-only các đợt Android/iOS trước.
- Scope root: hai launchers, wrapper/workflow, existing emission-fixture project/
  Program và ba own docs; amended thêm `test_native_stderr_classifier.py`.
  Không sửa actual Emit/SmokePage/SDK hoặc shared parser/iOS/Android helpers.
- Windows opt-in một process/attempt, bounded async stdout/stderr2MiB, actual
  child exit0; Mac reuse NSWorkspace/strict codesign, fresh container result và
  scoped complete marker, không fallback/claim OS exit. Cả hai giữ prelaunch
  verify-app rồi full cohort/public postcondition verify-runtime >=3.
- Hosted fixture mở rộng synthetic child/negative launcher cases, giữ sáu nhóm
  actual Emit và actual shared CLI roundtrip. Local33shared +23package/scanner
  PASS0skip; PowerShell/Bash/four inline Python parse, PlanOnly và architecture/
  packaging PASS. Chưa chạy fixture build/native desktop mới; không local heavy.
- Amended classifier theo root14 literal keys, chỉ fixed JSON sau same process/
  run/freshness/UID/no-link guards.13 actual-implementation tests PASS0skip.
  Diagnostic-only commit `cf4f9dac1eb8c7a45ecf366328523df834f0cd45`; immutable patch
  `6f72d9d4fa514eaab43ffc633775331e53819f3d373a9c8a288f90993c9b0b2a` againstfee485
  → after13287ae318dea2cc318329a76373611aefb6fe6a, fixtureSHA256
  `77bfb99521e522b76d7924ddb1865fb5901c4b280c1a202db7a4d3beee7084a6`.
  Root review rồi mới cấp B import; không giao full opt-in WIP hoặc B writer.
  Khi root nhận B phải skip classifier import của B để không ghi đè C launcher.
- Windows/Mac package native mới, native editor/whole B/P3/hardware vẫn OPEN.
- First experiment `55ddb62f`, MAUI34181591521: hosted common fixture SUCCESS,
  producers đang chạy tại checkpoint. Bổ sung positive-PID guard riêng package
  cleanup để không nhận0/process-group values; hosted Mac fixture extract chính
  actual cleanup function và thay kill bằng counter để kiểm invalid/positive IDs.
  New source phải chạy whole cohort/sáu gates riêng, không nhận parent CI thay.

## Native iOS consumer — checkpoint đã nhận

- Branch frozen: `feature/release-009-maui-ios-consumer`, từ đúng frozen source
  `8b7781ca44b4f9f3647c5434d02970be873d9624`; giữ nguyên nhánh package đã bàn giao.
- Source8b đã xanh6gates: full33989004197, iOS33989005769, Q33989007405,
  Windows packages33989009301, demo33989010831, MAUI33988945365 (11jobs).
  Bốn consumer build/apphash PASS; Android native10frames/source/nonce/feed/public
  postconditions PASS. Root nhận9owned commits/28paths, bỏ importdfd theo mục dưới.
- Grant mới nhận CHỈ ba blobs rootf344 qua apply_patch. Import-only commit
  `9fe954b1adedc18d81bfb2fae78156529650b365` phải được **bỏ qua khi root nhận delta**:
  run-maui-ios-smoke.sh `41b8d02bad14c812fffcda562b63c17c02336682`,
  verify-native-smoke-result.py `5fd92e2866fc7231c44c81feda3dd4dcc8c43788`,
  test-native-smoke-result.py `cf540735e2a20faa72be3a842338b4a4638d49b7`.
  Android helper giữ nguyên223; shared files vẫn root-owned immutable.
- Owned workflow/wrapper thêm iOS launch opt-in, verify-app ngay trước helper,
  fresh result path và verify-runtime source/version/feed/nonce/target/SDK versions,
  public postconditions, >=3 completed frames. Không retry/fallback hoặc sửa SDK.
  iOS proof yêu cầu simctl status0 và full strict marker; actual iOS consumer OPEN
  tới run mới. Windows/Mac native, native editor/fullB/P3 vẫn OPEN.
- Local23 package/scanner và20shared parser fixtures PASS,0skip; plan/parser,
  architecture/packaging/diff PASS. Không heavy local build/native hoặc public publish.
- Source wiring `6864459e3f0223096397f856c116ed574396301e`: full33989993991,
  legacy iOS33989995306, Q33989996719, Windows packages33989998295 và demo33989999418
  SUCCESS. MAUI33989937858 FAIL tại iOS consumer101370724447: build0warning/0error,
  native parser báo malformed-marker chars987/json-offset976/ends-object0. Năm
  producers, assembler và ba consumer còn lại SUCCESS, gồm actual Android native.
  Stream bị lỗi và việc console có full JSON hay không vẫn UNKNOWN; không có raw
  stream artifact để kết luận. Giữ nguyên failure, chưa release iOS delta.
- Đã nhận grant triển khai root-owned opt-in `app-file-v1`: fresh simulator container
  file giữ full consumer JSON, compact marker ràng buộc transport nonce và SHA256.
  Default legacy transport và own cohort/postcondition checks giữ nguyên. Owned
  Emit dùng full UTF8 bytes/CreateNew/Flush(true)/close rồi compact5fields; wrapper
  thêm fiftharg chỉ iOS. Hosted console fixture link actual Emit và synthetic cohort,
  không thêm package. Nhận test release466 trong import-only `332a79a8` (root bỏ
  qua khi tích hợp); ba blobs khớp grant và32fixtures PASS0skip. Rà soát actualCLI
  tái hiện file-mode chấp nhận compact unified fragment khi console đầy đủ:
  full196/fragment88/header-complete/exit0. Đã báo root sửa riêng file-mode trước CI;
  không sửa shared files ở C, không nhận đây là native acceptance.
- Root sửa tại `30e74befa9984c5fb4ac1ea00701dff85fe6c533`; nhận nguyên trạng
  trong import-only `e17242d0` (root cũng bỏ qua). Helper blob60420f42 không đổi;
  parser721b09db/test5bd85f0f tắt fragment reconciliation riêng file-mode.
  Local33 shared fixtures PASS0skip, gồm actualCLI regression từ chối compact
  fragment dù console/file hợp lệ. Default legacy vẫn giữ contract trước đó.
  Owned console fixture kiểm actual Emit và roundtrip qua chính shared Python CLI
  với private synthetic context; build/execution fixture này chờ hosted CI.
- Owned `f5c9cce4942232fd5373ddd49490c012fa27e98b`, MAUI33991950309:
  hosted fixture compile chặn CA1861 tại mảng tên assembly cố định. Chuyển mảng
  fixture thành static readonly; không suppress analyzer hoặc thay consumer evidence.
  Chưa chạy producer/native ở run này; final HEAD cần cohort/gates mới.

## Checkpoint package đã release — lịch sử

- Branch: `feature/release-009-maui-packages` (giữ frozen8b).
- Base: `50cb357a00d6bb8a6b134cdeebce624a09bd1b21`, root verified five gates.
- PR #1 giữ Draft/unmerged; lane này chưa tạo PR hoặc sửa integration branch.
- PERF branch/source giữ nguyên `fe01586468d455c2ec26cc084e523c32c4c31baa`.

## Đã triển khai, chưa nghiệm thu runtime

Producer/assembler, public isolated consumer/platform glue, workflow build stage,
ADR0008/contract mới. Không sửa SDK csproj, production, existing workflows,
launchers hoặc shared status/CURRENT/wave. Scope grant ở wave root 06/09.

Producer/assembler commit: `feb12d5f`.
Local: 23 package/consumer fixtures và 12 shared native transport fixtures PASS,
0 skip; gồm 5 actual-filesystem inventory fixtures nhỏ. All three plan modes
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
Generic legacy min2 không phải consumer acceptance. Source wiring
`1bbe334a61ce0edecbf4679972a888de0bd9398b`, run `33988568079` đã SUCCESS toàn
matrix: năm producers, canonical assembler, bốn consumer builds/apphash và actual
Android native public smoke/cohort validator. Windows/iOS/Mac native, native
editor/fullB/P3 vẫn OPEN; hardened link scanner tiếp theo cần exact-source CI riêng.

Theo root review sau wiring `1bbe334a61ce0edecbf4679972a888de0bd9398b`, scanner
giữ explicit link kind/target/resolved target, không flatten directory aliases;
reject absolute/escaping links, symbolic và directory-graph cycles, path collisions.
23 fixtures PASS local gồm actual hidden add/remove/equal-length content changes,
file/directory retarget tới same bytes và actual cycle/escape; không skip và không
native/build workload local. Temporary fixture cleanup chỉ trong newly-created,
resolved/checked fixture directory. Shared imported223 blobs không đổi.

## Bước tiếp theo duy nhất

Push desktop opt-in/fixture implementation và chạy whole canonical cohort cùng
sáu gates tại final HEAD; chỉ release sau cả bốn native consumers PASS.
