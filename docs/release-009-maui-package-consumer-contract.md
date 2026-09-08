# RELEASE-009 — MAUI package và consumer cô lập

Phạm vi: một bộ 15 neutral packages + một MAUI package chứa Windows, Android,
iOS và Mac Catalyst. Existing Windows desktop/OpenXml consumer không bị đổi.
Source của checkpoint này từ baseline tích hợp `50cb357a`; chưa nhận whole B.

Checkpoint mới đã xác minh: source `5d70be93` đủ sáu workflow SUCCESS, canonical
matrix `33992029278` đủ 11 jobs; iOS consumer thật đạt 8 frames và Android 10
frames, cùng đầy đủ provenance/public postconditions. Root nhận ba owned commits
thành `389c883d..aabd359f`, không nhận ba import-only transport cũ. Source `8b7781ca`
và 28 paths canonical assembly đã được nhận trước đó. Windows/Mac native và
combined HEAD vẫn cần gate riêng, không được thay bằng source green.
[Hồ sơ tích hợp](worklog/RELEASE-009_MAUI_INTEGRATION_20260906.md).

## Gates

1. SHA checkout bằng workflow HEAD; version gồm run/attempt/SHA. Mọi producer
   build và pack với cùng version từ đầu, kiểm informational version SDK DLL.
2. Năm shard manifests cùng SDK/source/version và closure. Hash archive và từng
   assembly phải khớp; dependency/framework groups, lib/ref folders đồng nhất.
   Metadata/chung khác bytes, thiếu TFM, case collision, traversal hoặc foreign
   version đều bị từ chối. Không upload/restore partial MAUI packages vào feed chung.
3. NuGet pack canonical giữ nguyên verified payload và framework metadata;
   feed manifest bao gồm source, target groups và stable identity của package hashes.
4. Consumer ngoài checkout có props/targets/CPM riêng, exact PackageReference,
   source mapping và cache mới. Assets không có project library hoặc cache ngoài;
   compile/runtime MAUI asset phải đúng platform trong package đã kiểm.
   Explicit Microsoft.Maui.Controls dùng exact evaluated producer version; assets
   phải khớp, không phụ thuộc default version của workload trên runner.
5. App consumer mang source/version/feed hash/target/nonce trong assembly riêng.
   Public API smoke kiểm native handler/GPU frames, controller commit/Undo/Cancel,
   filter với 20 checkbox native đã load và resize thật. Controller test không
   được gọi native draft/editor proof. Runtime kiểm assembly provenance sau khi
   dùng các capability, loại riêng consumer assembly theo identity.
6. Wrapper phải chạy `verify-app` ngay trước native launcher để kiểm toàn bộ app
   payload với build manifest; sau helper chạy `verify-runtime` cho marker mới.
   Phải kiểm schema/cohort/nonce, target, frame count và exit code khi transport
   cung cấp được. Android `am` chỉ có explicit completed marker, không cho managed
   process exit code; không ghi thành OS exit-code proof. Missing/failure marker
   không được PASS.
   Shared Windows/Mac launchers đã được B release; C nhận bounded opt-in grant,
   Android/iOS extraction và strict parser vẫn thuộc root.
   Android/iOS có wiring opt-in qua shared transport được root release, luôn giữ own gate
   tối thiểu 3 completed frames và toàn bộ public postconditions. Result được ghi
   riêng `runtime-verification.json` chỉ sau khi verifier PASS. Android đã PASS
   ở source8b; Android/iOS đã PASS ở source5d và combinedd73. Windows/Mac có wiring
   opt-in mới, acceptance vẫn OPEN tới actual consumer CI ở đúng source.
   iOS đòi simctl launch status0 và explicit marker. Consumer chọn transport
   `app-file-v1` bằng argument thứ năm của shared helper; default legacy không đổi.
   Launcher tạo fresh path trong data container thực của simulator và truyền
   `NERA_MAUI_SMOKE_PROTOCOL=native-result-file-v1`, `NERA_MAUI_SMOKE_RESULT`
   và `NERA_MAUI_SMOKE_NONCE` (32 ký tự lower-hex). Consumer serialize full JSON
   thành UTF8 bytes một lần, CreateNew/Flush(true)/close trước khi emit duy nhất
   compact marker có đúng schema/status/frameCount/transportNonce/sha256.
   SHA256 lower-hex ràng buộc toàn bộ bytes; transport nonce riêng với cohort nonce.
   Shared verifier phải kiểm full strict JSON và compact marker/nonce/hash/header;
   file đơn lẻ, fragment, conflict/failure, stale/missing/oversized/symlink evidence
   đều không thành native proof. Own verifier vẫn kiểm toàn bộ source/version/feed,
   cohort nonce/target/required assembly versions/public postconditions và >=3 frames.
   Full JSON không bị cắt bớt; raw logs và đường dẫn container không được upload.

## Desktop opt-in

Windows thêm `-ResultPath`, `-MarkerPrefix`, `-ResultProtocol app-file-v1` cùng
existing `-ExecutablePath`, `-TimeoutSeconds 75` và bắt `-MaximumAttempts 1`.
Output caller khác private payload/context, phải mới trong RUNNER_TEMP. Capture
stdout/stderr riêng bằng async reads, mỗi pipe <=2MiB và cùng process deadline;
timeout/nonzero exit luôn FAIL dù file/marker có success. Output chỉ được xuất
qua shared strict parser sau actual child ExitCode0; không in raw result/pipe.

Mac giữ hai legacy arguments rồi thêm expected bundle ID, marker prefix và
`app-file-v1`. Verify compiled Info.plist, executable và strict codesign trước
cùng NSWorkspace launcher. Dùng fresh private directory trong per-bundle Mac
container của recipe hiện hữu; chỉ app thật ghi full result mới xác minh được
đường dẫn writable. Không dùng simctl hoặc fallback/direct executable/signing flags.
Scoped unified query theo launched processID/time/prefix giữ complete envelope;
không marker thì file đơn lẻ không PASS. Query <=10s/2MiB trong result bound90s;
chỉ cleanup exact launched process, không broad file/PID search hoặc retry.
LaunchServices callback bound30s giữ nguyên.

Exit evidence: Windows `child-exit-zero-and-explicit-completed-marker`; Mac
`launchservices-started-and-explicit-completed-marker`. Mac label không tuyên bố
managed process ExitCode0. Cả hai vẫn phải qua same strict file protocol và own
source/version/feed/cohort nonce/target/assembly/public postcondition gate >=3.
New native acceptance cần whole fresh canonical cohort và sáu exact-HEAD gates.

Mac dd checkpoint chưa tạo file/marker dù LaunchServices đã trả về process.
Root cấp diagnostic-only extension trong actual consumer: 19 stage literals, mỗi
event gắn current transport nonce và hai Boolean về absolute path/parent tồn tại;
không ghi đường dẫn, exception text hay environment dump. Tối đa64 events qua
CoreFoundation.OSLog; summary hiện hữu chỉ xét <=128 scoped records, <=512chars/
record trong cùng2MiB/90s query, fixed schema/counts/booleans <=2KiB. Unknown/
foreign/duplicate/malformed data không thành stage evidence. Diagnostic prefix
khác result prefix; file + diagnostics không thể PASS. Mac opt-in ghi chính compact
result vào OSLog sau durable close, trùng nội dung console. Default/non-Mac giữ
transport cũ; native branch phải được actual hosted compile/runtime xác minh.

## Matrix và giới hạn

| Target | Host build | Probe dự kiến |
|---|---|---|
| Windows | Windows, maui-windows, win-x64 | Unpackaged WinUI executable |
| Android | Ubuntu, maui-android, JDK17, android-x64 | APK trên emulator API35 |
| iOS | macOS, maui-ios, Xcode | Debug simulator consumer của Release package |
| Mac Catalyst | macOS, maui-maccatalyst, Xcode | App bundle theo kiến trúc runner |

Apple bundle được chọn duy nhất bằng CFBundleIdentifier trong compiled Info.plist.
Một Python scanner chung chụp/kiểm app inventory, gồm hidden files và explicit
internal links. Link entries giữ kind, relative linkTarget/resolvedTarget; directory
alias không flatten descendants. Escape, absolute links, symbolic/directory cycles
và duplicate/case-colliding logical paths bị từ chối. Retarget tới bytes giống hệt
vẫn làm manifest mismatch. Manifest giữ appName/size/hash; marker runtime phải
được tạo mới từ chính build đó. Fixtures dùng thư mục synthetic vài byte riêng,
kiểm content change, hidden add/remove và actual link retarget/escape/cycles.

Android host probe giữ AOT-disabled như source gate hiện hữu. Simulator/debug
không thay device/AOT/signing/hardware acceptance. Native editor bridge chờ B
release; P3 chờ whole combined B. Không gọi workflow build-only success là R3
runtime acceptance hoặc sản phẩm hoàn chỉnh.

## Privacy và rollback

Artifact chỉ chứa package bytes, relative path/hash manifests và sanitized
summaries. Raw assets, NuGet.Config, stderr, binlog, app bundle/SDK caches,
machine paths và device identifiers không được upload. Launcher input chứa
absolute path chỉ ở RUNNER_TEMP. Source package/output debug symbols bị loại.
Không dùng workbook thật, không publish feed công khai, không local heavy build.

Local chỉ chạy `-PlanOnly`, PowerShell parser và tiny synthetic negative fixtures
`eng/release-009-maui/test_package_matrix.py`. Hosted fixtures dùng SDK10.0.302
để chạy console project `eng/release-009-maui/emission-fixture`, link trực tiếp mã
PackageProvenance.Emit với cohort giả: default full marker, payload Unicode lớn,
hash/full file/compact envelope, existing/second write, cấu hình invalid và failure.
Fixture cũng đưa output thật của Emit qua shared Python CLI và đối chiếu full
consumer payload trả về; chỉ dùng private synthetic context, không thêm parser riêng.
Không dùng fixture làm runtime acceptance hoặc thêm package dependency.
Hosted Windows fixture chạy synthetic child thật qua launcher: valid/pipe pressure,
missing/failure/nonzero/timeout/oversize/partial/nonce/hash/output/attempt rejection.
Hosted Mac fixture từ chối wrong bundle, unsigned payload, existing output và
unknown mode trước native launch. Classifier fixture13cases gọi actual diagnostic
block và kiểm identity/privacy/bounds; không dùng prototype classifier trong test.
Rollback reverse-revert desktop opt-in slice, giữ accepted iOS/Android protocol
và SDK d73; không migration dữ liệu hoặc public feed publish.
