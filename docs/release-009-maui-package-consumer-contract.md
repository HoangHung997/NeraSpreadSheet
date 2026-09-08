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

## Observer vòng đời process — lịch sử đề xuất trước source 0a

Sau fa native Mac đã ghi constructor/Loaded/dispatchAccepted nhưng chưa callback,
root chỉ cấp implementation cục bộ cho observer trong existing package receiver.
Đăng ký đúng numeric PID một lần qua Python `select.kqueue`, `KQ_FILTER_PROC`/
`KQ_NOTE_EXIT`; không observer cho child/replacement hoặc đọc normal event data
thành exit code. Registration nhận cả lỗi syscall lẫn `EV_ERROR`: ESRCH, denied,
unavailable và lỗi/record không dự kiến là các Boolean riêng. Poll không chờ
(`timeout=0`, tối đa1 event), chỉ tại biên vòng lặp hiện hữu và lần cuối.
Giới hạn128 lượt quan sát (127 thường +1 cuối), ngoài một syscall đăng ký;
không thay90s deadline, log cap2MiB/query10s hoặc shared result acceptance.

Lần cuối gọi signal0 để quan sát PID, đóng băng snapshot rồi đóng queue trước khi
Python trả về Bash cleanup. Không tính TERM của harness là app tự kết thúc.
Summary vòng đời và stage cộng lại <=2KiB, fixed schema/counts/booleans; không
PID, thời gian, process path/name, errno text, exit status, signal, raw stack/log.
Chỉ `NOTE_EXIT` hoặc quan sát PID vắng mặt thêm bằng chứng kết thúc/vắng mặt;
không khẳng định nguyên nhân, clean exit, thời điểm chính xác hoặc callback chưa
từng chạy. PID có mặt không chứng minh original instance còn sống: registration
gap/PID reuse vẫn UNKNOWN, cũng không chứng minh main thread starvation/deadlock.
Observer thiếu quyền/API, nhận dữ liệu bất thường hoặc close lỗi phải báo UNKNOWN
qua các cờ lỗi, không bypass quyền hay làm result-only/diagnostic-only PASS.

Fixtures extract chính implementation, fake kernel/probe cho immediate/later exit,
registration ESRCH/EACCES/unsupported/EV_ERROR, event sai, liveness errors/noevent,
caps/privacy và snapshot trước cleanup. CLI test giữ full valid file + diagnostics/
lifetime-only ở pending/no output. Hosted C# build và actual Mac API vẫn chưa chạy
cho local patch; root phải review exact patch/hash trước commit/push/native run.

## Trạng thái thoát Darwin — lịch sử đề xuất trước source 01a0

Source `0a6bbb265c0142c52ccea8f73278f1a19a9deb67`, canonical run `34186366468`,
đã compile/chạy observer thật: kernel EXIT và PID vắng mặt được ghi nhận trước
cleanup; Mac vẫn FAIL vì không có complete bound result. Root chỉ cấp sửa cục bộ
trong package observer/diagnostic assembly, actual extracted fixtures và hai own
docs. Remote giữ 0a; bản vá sau đây chưa commit/push hoặc chạy native/CI.

Observer đăng ký một lần `NOTE_EXIT | NOTE_EXITSTATUS`. Chỉ Darwin mới dùng
numeric `0x04000000` khi Python không expose `KQ_NOTE_EXITSTATUS`; nếu có,
constant phải là integer đúng giá trị, không nhận Boolean. Platform/API không
hỗ trợ hoặc constant sai không thử đăng ký khác. Denied/unsupported giữ riêng
các cờ vòng đời và signal0 cuối; không fallback registration hoặc retry.

Chỉ đọc `event.data` sau exact PID/filter/allowed flags, request đúng cả hai cờ
và event echo cả hai. Bare NOTE_EXIT vẫn chỉ chứng minh EXIT, data không được
đọc. EV_ERROR data chỉ là errno. Integer 0..65535 được giải mã bằng native
`os.WIFEXITED`, `WEXITSTATUS`, `WIFSIGNALED`, `WTERMSIG`; API thiếu/không hoạt
động hoặc dữ liệu/type không hợp lệ trả `unknown` và giữ bằng chứng EXIT.

Summary `nativePackageProcessLifetimeV1` giữ các trường cũ và thêm đúng hai
trường: `exitCategory` và `currentRunAssociated`. Category chỉ thuộc danh sách
`unknown`, `waitExitZero`, `waitExitNonzero`, `signalAbort`, `signalSegv`,
`signalBus`, `signalKill`, `signalTerm`, `signalOther`. Không xuất numeric status,
signal, PID, nonce, đường dẫn, thời gian, stack hoặc exception text. Tổng stage
và lifetime summaries vẫn <=2KiB.

Association dùng chính entry guards hiện hữu (CI, bundle/executable/signature,
LaunchServices success, positive PID), private context đúng schema/path tuyệt
đối/32 lower-hex nonce, và kết quả query processID/time/prefix hiện hữu. Diagnostic
summary phải đúng toàn bộ schema/stage keys, có ít nhất một nonce-matched stage,
counts là integer0..64, absolute path đã quan sát, không missing parent/rejected/
invalid/clipped data. Lần query cuối bị lỗi hoặc thiếu bằng chứng thì association
false và category `unknown`, kể cả status đã đọc được. Snapshot được đóng băng
trước cleanup; sửa input/return object hoặc gọi finish lần nữa không đổi kết quả.

`waitExitZero` chỉ là low-eight-bit exit status bằng0, không phải clean exit,
smoke PASS hay đã chạy callback. `signalAbort` không chứng minh native/managed
unhandled exception. Association không là birth identity; khoảng trống giữa
LaunchServices và đăng ký watcher/PID reuse còn UNKNOWN. Không thay once/poll
timeout0/cap128/90s deadline, launch/scheduling/log flags/SDK/shared parser hoặc
full file + compact marker/cohort/public postcondition acceptance. Không đọc IPS,
stderr mới, tìm replacement process hay đổi security/signing/entitlements.

Căn cứ là Apple XNU `f6217f891ac0bb64f3d375211650a4c1ff8ca1ea`,
`xnu-12377.1.9`, được Apple map tới macOS26.0; runner0a là macOS26.6.2/25G83,
chưa chứng minh kernel/SDK của runner khớp source này. Public header và kernel
implementation cho phép parent hoặc caller được phép signal target; `cansignal`
chỉ kiểm quyền, không gửi SIGKILL. Cùng source vẫn có man page nói child-only;
khác biệt tài liệu này và runtime compatibility phải được giữ rõ khi review.

- [Public event.h và NOTE_EXITSTATUS](https://github.com/apple-oss-distributions/xnu/blob/f6217f891ac0bb64f3d375211650a4c1ff8ca1ea/bsd/sys/event.h#L273).
- [Quyền đăng ký và event data](https://github.com/apple-oss-distributions/xnu/blob/f6217f891ac0bb64f3d375211650a4c1ff8ca1ea/bsd/kern/kern_event.c#L1116).
- [Kernel gửi wait status16bit](https://github.com/apple-oss-distributions/xnu/blob/f6217f891ac0bb64f3d375211650a4c1ff8ca1ea/bsd/kern/kern_exit.c#L2561).
- [Wait-status encoding](https://github.com/apple-oss-distributions/xnu/blob/f6217f891ac0bb64f3d375211650a4c1ff8ca1ea/bsd/sys/wait.h#L144) và [Darwin signal values](https://github.com/apple-oss-distributions/xnu/blob/f6217f891ac0bb64f3d375211650a4c1ff8ca1ea/bsd/sys/signal.h).
- [Man page cùng source](https://github.com/apple-oss-distributions/xnu/blob/f6217f891ac0bb64f3d375211650a4c1ff8ca1ea/bsd/man/man2/kqueue.2#L549).

157 fixture cases extract actual code/finalization, fake kernel/probe/native wait API với status0,
nonzero, signals/core bit, bare data poison, request/echo mismatch, unavailable
API/platform, permission/malformed/correlation failures, privacy/bounds và frozen
snapshot trước cleanup. Actual shared CLI vẫn từ chối diagnostics/status-only
dù full file hợp lệ. Fake APIs không là native proof; C# hosted compile/native
runtime cần grant riêng sau root review immutable patch/hash. Không thay render,
input hoặc workbook nên không thêm benchmark; P3/hardware vẫn là gate riêng.
Rollback bản vá này chỉ reverse bốn owned-file deltas về0a; không migration.

## Ba cờ chẩn đoán association/status — local pending sau source 01a0

Source `01a0fa6b466f8e9806c10ccae6d1976184db2ecc`, canonical run `34190050107`,
có10/11jobs SUCCESS. Mac job `101946706676` build0warnings/0errors nhưng FAIL
complete-bound-result: app-file0/unified3097, chỉ hai constructor stages được
quan sát. Watch registered/EXIT/PID absent trước cleanup, pollCount51; category
`unknown`, association false. Summary mốc chạy hợp lệ chưa cho biết context riêng
hay lượt query cuối không đạt, hoặc private classifier đã giải mã status hay chưa.
Không suy nguyên nhân native/managed, clean exit, birth identity hay path writable.

Root chỉ cấp bản vá cục bộ trong observer init/finish/final diagnostic assembly,
actual extracted fixtures và hai own docs. Remote giữ01a0, bản vá mới chưa commit/
push/native/CI. Snapshot lifetime hiện hữu thêm đúng ba Boolean:

- `privateContextVerified`: input context phải chính là Boolean true; integer1,
  string hoặc truthy object đều thành false.
- `lastScopedQuerySucceeded`: input từ collection flag hiện hữu phải chính là
  Boolean true. False có thể là chưa có query trả về hoặc query cuối không trả về
  thành công; true không tự chứng minh diagnostic/result payload hợp lệ.
- `exitStatusDecoded`: private classifier đang giữ một trong tám finite categories
  khác `unknown` tại thời điểm freeze, trước association masking. False không chứng
  minh API chắc chắn thiếu hoặc chưa từng decode. Không xuất category không được
  association, raw status, numeric signal/PID, path, nonce, thời gian hay error text.

Internal finish nhận riêng hai guard inputs, default cả hai false. Association
vẫn là valid PID + hai guards + strict diagnostic summary. Category công khai vẫn
`unknown` nếu association false, kể cả `exitStatusDecoded=true`. Ba Boolean được
đóng băng cùng snapshot trước cleanup; gọi finish thêm, sửa input hoặc return dict
không đổi dữ liệu đã lưu. Tổng hai JSON summaries tối đa1155bytes theo fixed fields,
counts64/poll128/longest category, vẫn dưới2KiB. Chúng chỉ mô tả chẩn đoán.

Không sửa classifier/registration/observe/query/launch/Swift/app/SDK/cleanup,
deadline90s, poll128, predicate PID/nonce, caps, log flags hoặc shared parser.
Query cuối thất bại vẫn mask category; không thêm grace period, fallback, query
hay thay strict full result/cohort acceptance. Native status0 không làm smoke PASS.

194 actual extracted observer/status/finalization/receiver cases kiểm bốn tổ hợp
guards, decoded/bare/missing API/malformed/permission, strict Boolean/defaults,
earlier success rồi terminal query failure ở fake90s, privacy/bounds và immutable
snapshot trước cleanup. Fixture dùng fake clock/kernel/query/wait APIs/CLI/filesystem
cho receiver; không là native proof. Actual shared CLI kiểm diagnostics/status-only
pending, explicit failure và complete marker riêng; hosted fixture còn đưa explicit
failure từ chính consumer Emit qua CLI để bảo đảm các cờ không che failure.
Local không .NET/native build; C# compile/native và CI của delta mới chưa được grant.
Rollback chỉ reverse bốn owned-file deltas về01a0; không migration hay public publish.

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
