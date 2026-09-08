# Đợt hoàn thiện Table / Filter / Ribbon / UX — 05/09/2026

## Hàng chờ bổ sung của người dùng — chưa kích hoạt

Đã tiếp nhận [Mục2 Clipboard UX và Mục3 per-sheet view state](NEXT_CLIPBOARD_WORKSHEET_VIEW_STATE.md)
từ PR#1 comment5579073325. Chỉ bắt đầu SAU công việc hiện tại hoàn tất, đọc lại
source mới trước sửa. Không đổi scope/grant của wave đang chạy, không thêm
writer/task/CI cho hai track lúc này. Cut nhiều vùng và XLSX SheetViews có ưu
tiên correctness khi đến lượt; Formula UX Avalonia của ChatGPT không bị ghi đè.

## UX-008 đã nhận — chờ exact combined gates

Root nhận source66344418 rồi258a9f92 thành afc98171 rồi52ca8a0e; cả bốn blobs
khớp nguồn final, original fixture chỉ partial. Năm source workflows SUCCESS,
root đọc actual Windows1515+186 PASS/0skip/build0/0, MAUI46 PASS, package PASS,
demo267captures/128layouts PASS. Không SDK/UI/performance changes hoặc acceptance
về actual screen reader/automatic UIA events. Successful log không xuất cached
counts, không lấy absence làm proof. Architecture/packaging/diff PASS local.
Root checkpoint commit kế tiếp cần đủ sáu combined gates; last all-six-green463.
Không nhận wholeB/C, không mở queued Clipboard/per-sheet, U2/U5/P3/hardware OPEN.

## Outcome trước tích hợp sau463 — không mở thêm phạm vi

A correction source258a9f92db09a35ee73063ec08133ad3384fb18a đã push; root kiểm
actual delta và một bộ exact-source/attempt1 full34190913369/iOS34190915221/
Q34190917515/Windows34190919620/demo34190921934. Cả năm đang chạy tại receipt,
chưa PASS/release; không sửa source/rerun/dispatch thêm trong lúc chờ.

A663 Windows101944786182: 182/186 PASS, bốn current-subtree membership FAIL
sau Next; 174 tests cũ PASS, bốn source workflows khác SUCCESS. Root review
actual log và framework cache lifecycle, duyệt test-only fresh direct-peer
query trong new Accessibility helper cùng own docs. Ghi cached counts trước
ResetChildrenCache, giữ substantive assertions; không SDK/CI/timing changes.
Cho một bộ năm exact-source gates ở commit mới, freeze/report sau outcome.
Automatic connected-client cache/events và actual screen-reader U2 vẫn OPEN;
không diễn giải bốn failures cũ thành PASS. Cumulative grant vẫn đúng bốn paths.

C01a0fa6b đúng bốn reviewed blobs: canonical34190050107 completed FAILURE,
10/11 jobs SUCCESS; Mac101946706676 thiếu result, constructor-only diagnostics,
precleanup exit/absence observed, exitCategory unknown/currentRunAssociated false.
Root đã đọc actual fixture101946083141 và Mac logs. Source frozen, không retry,
extra probe/CI hoặc import; chưa cause/clean-exit/runtime acceptance. B87 HOLD.
Root463 vẫn all-six-green. Clipboard/per-sheet queued work chưa audit/dispatch.

## Local grant sau463 all-six-green — A UX-008, C status diagnosis (lịch sử)

**C sau immutable review:** root đã đọc toàn bộ patch9868fe23/39025bytes,
kiểm4blobs/hash/unchanged boundaries và tự chạy157actual-extracted cases PASS
(fake kernel/wait API, synthetic formatter inputs). 33+23+13existing tests và
architecture/packaging/Bash/diff PASS. Grant commit/push đúng packet
9868fe23abc88a0f03fd667326a75b5d2d0951dffbb139d5da1eadcd4ccd3b23 và MỘT
automaticcanonicalcohort, không extra5compatibility/manual/retry hoặc imports.
Grant mới supersedes LOCAL-only bên dưới và trong frozen own docs; không sửa
thêm bytes đã hash. Native/C# availability/permission/category còn pending.
Giữ strict acceptance/90s/128/precleanup/privacy, không status0/causeclaim.

Root463 có full34188043102/iOS34188043132/Q34188043118/Windows34188040548/
MAUI34188040552/demo34188040545 SUCCESS; actual174desktop/46MAUI/9Picker PNG
và28split PNG sourcehashmatch đã root kiểm. Giữ final baseline này khi làm tiếp.

A dùng task/worktree hiện có, branch mới feature/ux-008-paged-filter-accessibility
từ exact463; không xóa/rewrite source8c hoặc artifacts. Write scope đúng4paths:
1. tests/NeraSpreadSheet.Windows.Rendering.Tests/Release009SplitAutoFilterSmokeTests.cs:
   chỉ thêm modifier partial vào existing test class để dùng lại private helpers;
   không thay tests/assertions/helpers/timing hiện có.
2. tests/NeraSpreadSheet.Windows.Rendering.Tests/Release009SplitAutoFilterSmokeTests.Accessibility.cs:
   tests mới trên actual loaded presenter/sample/session; không fixture model mới.
3. docs/ux-008-paged-filter-accessibility-contract.md.
4. docs/worklog/UX-008-PAGED-FILTER-ACCESSIBILITY.md.

U2 subset: Table/worksheet và4split panes, vi/en native peer roles/names/patterns/
checked/enabled/focus, header filter/sort states và bounded page/search/Apply
announcements; page250/100, stale hidden-page peers/focus và sheet/close lifecycle.
Native WPF peers trên owning UI thread, không tạo external UIA client/COM hook.
Nếu cần production fix hoặc sharedhelper edit ngoài modifier phải báo evidence/
exactpaths và chờ amendedgrant. Giữ tất cả174tests/capture assertions trước đó.
Không claim actual screen-reader/physical-DPI/touch acceptance. Không local
heavy build; existing source full/iOS/Q/Windows package/demo gates, không
duplicate dispatch hoặc rerun-until-green. Root giữ shared docs/CI/capture paths.

C chỉ LOCAL proposal: request NOTE_EXIT|NOTE_EXITSTATUS một lần, fixed wait-status
category từ exact validated event khi requested+echoed flags; bareNOTE/EV_ERROR
không đọc thành status. Public Darwin constant/version caveat phải rõ. Current
run association từ existing checked context/scoped finite nonce records, không
birthidentity. Invalid/missing/denied/unavailable/clipped => UNKNOWN; zero không
PASS/cause proof. Scope observer trong scripts/run-maui-maccatalyst-smoke.sh,
actual fixture eng/release-009-maui/emission-fixture/Program.cs, own contract/log.
Không app/SDK/launch/cleanup/timeouts/parser/logflags/security/reportscan change.
Giữ128observations/90seconds/precleanup immutable và<=2KiB. Actual-code fixtures,
patchSHA/4blobs phải root review trước commit/push/MỘT probe grant; hiện chưa có.
B remains whole87 HOLD, không import C hoặc sửa chồng hai lanes.

## Checkpoint tích hợp A — source được nhận, combined acceptance còn chờ

Root nhận đúng 10 commit/18 file nguồn A `8c9bdef9` lên nền `20654456`,
thành `f6ae7027..e97cdb3d`; không conflict, mọi blob nguồn khớp. Root giữ
parser diagnostic mới, hai test capture files và toàn bộ code B/C chưa release
ở ngoài nhánh tích hợp. Source A full đỏ riêng old shared Picker capture;
root đã kiểm native và chín ảnh đúng tại206 cùng năm workflow xanh. Ngoại lệ
source này được ghi rõ, không bỏ sáu exact-combined gates hoặc lấy source
green thay nghiệm thu. [Mapping/evidence](SPLIT_FILTER_INTEGRATION_20260908.md).

A giữ source frozen, không chạy lại lỗi chung. B frozen87: Windows diagnostic
PASS, Mac vẫn FAIL; chưa nhận whole lane. C frozen0a: package Windows/Android/
iOS PASS, Mac quan sát kernel exit trước cleanup nhưng chưa biết nguyên nhân.
C chỉ gửi một đề xuất read-only NOTE_EXITSTATUS, chưa grant code/probe.
Root tiếp tục sole writer shared docs/PR; B/C không tự import Picker/parser.
Chưa thêm task, hardware/whole B/final P3/100% vẫn OPEN.

## B — grant Windows commit evidence v2, không production correction

Root đọc fullproposal/sourceevent/host/helper và primaryMAUI10.0.20 native bridge;
hash/applycheck PASS. Grant exactpatch
`9bb778e40ad5418b40f9d909fa1c41978ef23251e904e47841ac01d9502dce64` cho
`tests/NeraSpreadSheet.Maui.Windows.Smoke/Table007EditorSmoke.cs`
(9ecc3be3→2a139c1e) và cùng project `SmokePage.cs` (54848577→b5fc515b), ownlog.
Apply/verify/commitpush rồi MỘT automatic paired TABLE007 native diagnostic.
Windows snapshots mới; Mac giữ code/control cũ, không coi là Mac fix. Không
manualextra/full/iOS/Q/retry, production/editor/Mac/workflow/launcher imports.

Tối đa4fixedphase snapshots: after-completion, before-native-enter ngay sau
existingfocus/80ms/foreground check, canonical-ended, after-native-enter.
Chỉ booleans/boundednumbers/fixedkind, no rawdraft/value/ID/path; explicit
commitArgumentObserved=false vì event không expose actualargument. Sameobject/
address, prefixcomplete/equality, storedformulakind/expected60/source10+20+30 và
historydelta dùng syntheticdata. Giữ33assertions/Delays/KeyEvents/cleanupfinally.
Native build/outputshape/privacy/cap checks còn bắt buộc. TextChanged asynchronous
là hypothesis; không đổi60 hoặc thêmcalcwait và không gọi instrumentedPASS là
sourcecausecorrected. Sauactualoutcome Bfreeze/report, wholeB vẫn HOLD.

## C — grant LOCAL watcher proposal; chưa push hoặc probe

**Checkpoint sau review:** root đã đọc full4path patch SHA256
`11ab73c40d9efc41a6edf884d326670483d66a9fe64f5676348532f24038a13e`,
kiểm4afterblobs, reverse-apply/Bash/diff và chạy actual-extracted43cases PASS.
Cho C commit/push đúng reviewed patch và chạy MỘT canonical MAUI cohort gồm
hosted compiled fixture/native API/consumer. Không manual5compatibility/full/
demo hoặc retry khi chưa MacPASS và rootPicker chưa released. Không import WIP,
không sửa thêm docs-only sau hash; grant mới này supersedes LOCAL-only ở dưới.
Actual native availability/permissions/exit observations còn chưa biết. Chưa
release/sourcegreen; lifetime diagnosis không đổi completed-result acceptance.

Sau sourcefa Mac chỉ có4stages và Baa directleaf-only, lifetime vẫn chưa rõ.
Root đọc actual package receiver/Swift launcher/cleanup và primary
[Apple kqueue](https://developer.apple.com/library/archive/documentation/System/Conceptual/ManPages_iPhoneOS/man2/kqueue.2.html)/
[Python select](https://docs.python.org/3/library/select.html#kqueue-objects).
Cho C chuẩn bị local diff trong đúng4existingownedpaths:
`scripts/run-maui-maccatalyst-smoke.sh` (package Python observer only),
`eng/release-009-maui/emission-fixture/Program.cs` (actual extracted implementation
fixtures), `docs/release-009-maui-package-consumer-contract.md`, own
`docs/worklog/RELEASE-009-MAUI.md`. Root phải review exact patch/hash/tests
trước commit/push hoặc một native run mới; source remote vẫnfa.

Đăng ký một kqueue process NOTE_EXIT cho numeric launchedPID, poll timeout0
tối đa1event tại existing loop boundaries và finally; kill0 chỉ observation.
Xử lý immediate event, EV_ERROR/OSError registration, ESRCH/denied/unavailable,
unexpected events UNKNOWN. Snapshot đóng trước Bash cleanup để không gán TERM
của harness thành spontaneous exit. Fixed booleans + capped pollCount, combined
summaries<=2KiB; không PID/timestamp/path/process name/errno text/kevent.data/
exitcode/rawlogs/env. Không đổi scheduling native app, result acceptance,
90s/query2MiB/10s bounds, launch flags/security/signing hay parser/classifier.

Fixtures phải gọi actual watcher/reducer: immediate/later exit/noevent, absent,
denied/unavailable, malformed/mismatched/error, before-cleanup snapshot,
privacy/caps và diagnostic-only vẫn FAIL. NOTE_EXIT hoặc absence chỉ cung cấp
observed termination/absence, không cause/clean exit/unobserved callback claims.
Pre-registration gap/PID reuse vẫn UNKNOWN; presentPID không live-original proof.
Root Picker96 chưa released vì dimension guard FAIL, không C tự import/sửa.

## Root-owned test correction — native Picker screen origin

Cfa actual failure artifact10039767660 đã root xem/verify: alleged open-Picker
PNG là nền customization. Root giữ sole writer đúng
`tests/NeraSpreadSheet.Maui.Windows.RibbonSmoke/NativePopupCapture.cs` và
`tests/NeraSpreadSheet.Maui.Windows.RibbonSmoke/SmokePage.Customization.cs`.
A chỉ read-only review, write grant vẫn18paths; C vẫn12authorized paths/frozenfa.
Test-only correction dùng actual item automation-peer screen bounds/verified DPI,
multi-reference origin agreement, actual window ownership/visibility và stability;
không owner-origin fallback hoặc giảm caption/palette assertions/9captures.
[Contract/evidence](../ux-007-keyboard-accessibility-contract.md) giữ giới hạn.
Chỉ chuyển immutable slice cho C sau root actual native proof/final images;
không C/B tự sửa hai files hoặc lấy unverified WIP. SDK/renderer/UI không thay.

## B — grant một queued witness sau classifier77, không thay true editor

Root đọc actual77 Mac101924799380 và Windows101924799555: Mac FAIL sau Dispatch
true/no callback,14categories0/unclassified4/unclipped, không currentIPS;
Windows baseline43/candidate60frames PASS. Một draw sau Dispatch không chứng minh
queued callback đã chạy: MainThread.BeginInvokeOnMainThread gọi inline khi ở
main thread theo [MAUI10.0.20 source](https://raw.githubusercontent.com/dotnet/maui/10.0.20/src/Essentials/src/MainThread/MainThread.shared.cs).
[Apple dispatcher source](https://raw.githubusercontent.com/dotnet/maui/10.0.20/src/Core/src/Dispatching/Dispatcher.iOS.cs)
queue rồi trả true, không xác nhận entry. Không suy signal/JIT/native cause.

Root đọc toàn bộ immutable witness-v1 proposal/13-line patch, hash và actual
apply-check. Grant đúng patch SHA256
`f1674a15800e7c8d2cce773b829163287229b7a5293a94b65faddccf29204c30`,
chỉ `tests/NeraSpreadSheet.Maui.MacCatalyst.AnalyticsSmoke/SmokePage.cs`:
before `a82776c5468ebeda2fa8de67e34c6b1c134c1f6d` →
after `3856665fc2a4a949b72607aa2ffbf9c23465a996`, cùng own TABLE-007 worklog.
Một private static NoInlining trace-only leaf, cùng Action gọi direct một lần
với brackets rồi queue đúng một lần trên cùng dispatcher trước unchanged true
callback. Không UI/native reads, field/Task/editor/analytics trong leaf; không
đổi assertions/readiness/scheduling của true callback, launcher/config/timeout.

Chạy đúng một paired baseline/true-candidate narrow sau apply/verify. Không dùng
witness thay editor/result gate hoặc full-gate dispatch trước Mac PASS. Direct
PASS nhưng queued absent vẫn không phân biệt pending/entry/native termination;
queued PASS nhưng true entry absent không chứng minh JIT. Extra queue thay timing:
nếu true editor PASS vẫn chưa chứng minh correction/cause. Sau kết quả báo root,
không tự sinh variant/retry tiếp. C vẫn sole launcher/classifier writer; immutable
B77 import giữ nguyên và root SKIP khi nhận whole B. B local04fb850c chỉ own docs,
native proof vẫn exact77; không gọi docs HEAD là sourcegreen.

## Điều phối 08/09 — f3 xanh; gỡ hai phạm vi bị chặn

Root exact `f3d65d03ef8a52f40d116b03d37876ae3c80069e` đã SUCCESS cả sáu:
full34180853269/iOS34180853216/Q34180853268/packages34180848586/
MAUI34180848576/demo34180950552. Không nhận A/C WIP hoặc whole B.

### B — đã duyệt immutable diagnostic import

Root đọc actual block và fixture C `cf4f9dac1eb8c7a45ecf366328523df834f0cd45`,
đối chiếu frozen implementation và chạy lại13tests PASS/0skip. Patch SHA256
`6f72d9d4fa514eaab43ffc633775331e53819f3d373a9c8a288f90993c9b0b2a`;
Mac `fee485b3a771b7fe58711d8112bc51ccbc187c3d` →
`13287ae318dea2cc318329a76373611aefb6fe6a`; fixture
`a3243dc6f6a1b89669e7d3586956b5d43b4b549c`. Actual B apply-check PASS.
B nhận đúng2paths IMPORT-ONLY thành77f4c447, giữ local docs commits; được chạy
một paired baseline/true-candidate narrow, giữ SmokePage529/workflow byte-equal.
Không nhận C opt-in, sửa classifier hoặc chạy full gates trước Mac PASS. C vẫn
sole writer; root SKIP B77 import khi nhận whole B, nhưng PHẢI nhận baseline5906
và classifier cùng own delta khi nhận final C. Counts chỉ là text indicators,
không phải crash cause; all-zero không chứng minh clean exit.

### A — thêm đúng một fixture path, tổng18paths

Root đọc native job101921208530/source609a54ae: build0/0, Core1515 PASS;
PERF008 thất bại khi đọc binding sau khi unfrozen header rời viewport. Fixture
same-binding mâu thuẫn lifecycle đóng popup offscreen mới. Cho A thêm đúng một
dòng `session.View.FreezeTopRows(1)` sau `CreateSession()` trong riêng WPF test
của `tests/NeraSpreadSheet.Windows.Rendering.Tests/PERF008NativeStressTests.cs`.
Giữ WinForms,12cycles,offsets,assertions,generation,no-rescan,page-cap,history,
subscriptions. Không auto-freeze production hoặc giữ popup offscreen để qua test.
Test mới vẫn phải kiểm đóng popup khi unfrozen header rời viewport. CLR test-host
abort sau đó là lỗi riêng chưa rõ nguyên nhân. A6a728b8d vẫn HOLD, cần native/
captures và năm final-source gates; một dòng fixture không đóng final P3.

### C — thêm đúng hai consumer files, tổng12paths

Cdd6eafda50f5e98bb51fe23a9e0312525be70868 báo năm compatibility gates SUCCESS:
full34182133420/iOS34182134752/Q34182135997/packages34182137232/demo34182138742.
MAUI34181795895 vẫn FAIL. C báo Windows9frames/child0,Android11,iOS8 PASS;
root đọc Mac101922679905: fixtures/build0/0, native app-file0/unified-bytes2.
Constructor/Loaded/dispatch, writable context và Emit chưa phân biệt được.

Grant thêm C sole writer `tests/NeraSpreadSheet.Packaged.Maui.Smoke/PackageProvenance.cs`
và `tests/NeraSpreadSheet.Packaged.Maui.Smoke/SmokePage.cs`, chỉ hosted Mac
bounded diagnostics và native compact envelope. Dùng public
`CoreFoundation.OSLog.Default.Log` dưới MACCATALYST, không package/PInvoke mới.
Finite stage allowlist + exact transport nonce trong private log; existing query
giữ exactPID/start,2MiB/deadline bounds; public summary fixed stage counts/booleans
<=2KiB, không raw message/exception/type/path/PID/nonce/environment hoặc raw-log
artifact. Diagnostic không là result. Compact marker chỉ sau actual CreateNew/
Write/Flush/close, console/native duplicates phải giống hệt. Giữ full payload,
hash/nonce, strict shared parser, minimum frames/cohort và mọi native assertion.
File-only/diagnostic-only vẫn FAIL. Android/iOS/Windows/default behavior giữ
nguyên. Không đổi scheduling/await, SDK/editor, signing/AOT/runtime config,
timeout/retries/cleanup hay thêm launcher. Existing linked fixture kiểm actual
Emit/protocol và bounded stage/privacy negatives; actual Mac compile/runtime
kiểm platform binding, không coi neutral fixtures là OSLog delivery proof.
Root đọc [primary binding source](https://raw.githubusercontent.com/dotnet/macios/main/src/CoreFoundation/OSLog.cs).
Classifier cf4f9dac frozen trong lúc B probe. Final C vẫn cần cả bốn actual native
consumers và năm compatibility gates đúng finalSHA; không lặp cohort trước khi
có implementation correction có lý do. Baseline5906 PHẢI nhận khi integrate C.

## Amended grant C — classifier stderr hữu hạn cho B529

Root `e228ddc678f829d471382b050699e1eacb922741` đã được REST xác minh đủ sáu
workflow SUCCESS (full34180125560/iOS34180125523/Q34180125531/packages34180122646/
MAUI34180122612/demo34180196414). A/C vẫn triển khai từ frozen green d73;
không đổi base hoặc model. C đã nhận hai B launcher blobs thành baseline commit
`5906ea9fe4d27baaa310997cfb9938ee4d194934`, commit PHẢI nhận khi tích hợp C.

B529 narrow34180074766: Mac baseline10 PASS, true candidate build0/0/native FAIL.
Actual Dispatch returned true, chưa callback/invoke/catch/editor; Windows baseline43/
candidate62 PASS/3 recreation. No matching current IPS/stack; stderr664bytes/5lines
không đủ exit/crash category. Không suy thành JIT failure từ thiếu callback.

Root đọc đầy đủ proposal của B; C là SOLE WRITER launcher và được thêm đúng
`eng/release-009-maui/test_native_stderr_classifier.py` vào chín-path grant.
Chỉ bổ sung classifier vào Python `native_stderr_diagnostics()` hiện hữu và
fixture/owned workflow/own log liên quan. Không B/root ghi chồng launcher C đang sửa.

- Reuse exact current-process/run header, freshness, UID, no-symlink guards và
  buffer <=64 KiB; không file/capture/env/flag/timeout/retry/cleanup mới.
- Bỏ header, <=128 lines, <=4096 chars/line; mỗi category count0..64, clipped
  boolean và unclassified count. Một versioned JSON <=2 KiB, fixed keys only.
- Allowed indicators: objcDuplicateClassText, objcClassMetadataText,
  objcUncaughtExceptionText, managedUnhandledText, aotJitRestrictionText,
  runtimeAssertionText, dynamicLoaderText, sigsegvText, sigabrtText, sigbusText,
  typeLoadExceptionText, invalidProgramExceptionText, missingMethodExceptionText,
  typeInitializationExceptionText. Dùng đúng conjunction/token literals trong
  proposal B, không generic error/failed/JIT matching hoặc free-form extraction.
- Chỉ output số đếm/fixed category/version, không raw line/message/type/class/
  symbol/path/address/PID/run/hash/env. Positive chỉ là text indicator, không
  fatal cause/effective mode/OS exit proof; all-zero không là clean-exit proof.
- Giữ toàn bộ native result/exit/assertions và default/package transport semantics.
  Fixture gọi chính classifier implementation, kiểm positives/near-token negatives,
  limits và identity/stale/foreign/symlink không emit. Không copy classifier vào test.

C bổ sung vào own branch đang làm, bảo toàn dirty opt-in edits. Để B chạy probe
độc lập, C cung cấp IMMUTABLE minimal patch chỉ classifier against frozen Mac blob
`fee485b3a771b7fe58711d8112bc51ccbc187c3d`, before/after blobs, patch SHA256 và
fixture hash/evidence. Nếu không tách được khỏi opt-in thì báo root, không stash/
reset/delete hoặc cấp ngầm B writer. Không đưa toàn launcher có opt-in chưa release
vào B. Root review exact patch/fixtures trước khi cho B nhận immutable slice.

Sau root xác minh slice, B chỉ apply chính immutable classifier slice + fixture
trong commit import-only riêng, ghi own log, không sửa implementation. Một paired
baseline/true-candidate narrow mới với SmokePage529 byte-equal và existing workflow;
không variant bỏ editor/full source gates trước Mac PASS. C giữ writer lâu dài.
Khi root nhận whole B, SKIP classifier import-only và không ghi đè hai launcher
do C sở hữu; khi nhận C vẫn phải nhận baseline5906 và own final delta đầy đủ.

## Grant triển khai tiếp — baseline kết hợp d73 đã xanh đủ sáu gates

Root REST xác minh `d73cd2e2bcb7e2832e9a8b3fd071373b62cb2a0f` SUCCESS:
full `34179085621`, iOS `34179085641`, Q `34179085671`, Windows packages
`34179082790`, demo `34179082695`, MAUI `34179082733` (11/11 jobs).
Dùng exact d73 làm base cho A/C, giữ branches đã release349/5d nguyên trạng.
Tái dùng đúng ba task GPT-6 Astra/xhigh; không tạo task/desktop lease mới.

### A — active split paged AutoFilter

Branch mới `feature/release-009-split-autofilter` từ exact d73. Grant 17 paths:

- `src/NeraSpreadSheet.Wpf/NeraAutoFilterPagedPopupPresenter.cs`
- `src/NeraSpreadSheet.Wpf/NeraAutoFilterPagedPopupPresenter.Host.cs`
- `src/NeraSpreadSheet.Wpf/NeraAutoFilterPagedPopupPresenter.Popup.cs`
- `src/NeraSpreadSheet.Wpf/NeraAutoFilterPagedPopupPresenter.Operations.cs`
- `src/NeraSpreadSheet.Wpf/NeraAutoFilterPagedPopupPresenter.SplitHost.cs`
- `src/NeraSpreadSheet.Wpf/NeraSpreadsheetSplitController.cs`
- `src/NeraSpreadSheet.Wpf/NeraSpreadsheetSplitAdorner.cs`
- `tests/NeraSpreadSheet.Windows.Rendering.Tests/Release009SplitAutoFilterSmokeTests.cs`
- `samples/NeraSpreadSheet.Wpf.Sample/RibbonPreviewWindow.cs`
- `samples/NeraSpreadSheet.Wpf.Sample/RibbonPreviewWindow.Commands.cs`
- `samples/NeraSpreadSheet.Wpf.Sample/RibbonPreviewWindow.Capture.cs`
- `samples/NeraSpreadSheet.Wpf.Sample/RibbonPreviewWindow.SplitFilterCapture.cs`
- `docs/release-009-split-autofilter-contract.md`
- `docs/worklog/RELEASE-009-SPLIT-AUTOFILTER.md`
- `docs/demo/COMMANDS-WIN11-VI.md`
- `src/NeraSpreadSheet.Commands/PresentationStrings.resx`
- `src/NeraSpreadSheet.Commands/PresentationStrings.en.resx`

Giới hạn trong các files: split controller chỉ internal surface/frame/activation
bridge; split adorner chỉ internal frame/surface lifecycle, không editor/history.
Sample main khởi tạo/dispose đúng một presenter; Commands chỉ Sample.Filter;
Capture chỉ một bounded call và manifest append; resource catalogs tối đa hai
matching keys cho lý do không mở được filter. Guide chỉ filter/split sections.
Additional pointer/header path cần evidence và amended grant trước khi sửa.
Existing binding/Core/Editing/Viewport/geometry model/shared CI/docs read-only.

Chốt behavior: khi native draft active, từ chối mở filter và giữ nguyên
State/text/range/focus; không âm thầm commit/cancel hoặc đổi selection.
Dùng cùng presenter/binding/session hiện hữu, không model/cancellation stack khác.
Pane identity phải đi cùng owner/header và rectangle từ existing presented
LastFrame/layout, offsets double và pane clip; cùng snapshot cho draw/hit/anchor.
Không Compose/RenderNow/WorksheetSnapshot/value scan theo từng raw event.
Giữ separator/scrollbar/resize precedence. Host/session/worksheet/pane/open-generation
guards chặn stale close/search/focus callbacks; chuyển surface/sheet đóng đồng bộ.
Popup cùng header còn visible được relocate, không còn visible thì đóng.
Tạo presenter từ đầu để first header click/Alt+Down hoạt động trước Ribbon command.

Regression: Table + worksheet, standalone + bốn panes/repeated headers; actual
header/Alt+Down SystemKey/command, offsets fractional/hidden axes/clip/zoom/resize,
negative separators/scrollbars/resize handles, active draft refusal; 250+ distinct
nhưng chỉ page100 controls, page/search/toggle/Apply/Clear/Undo/Redo đúng một lần.
Close/reopen/pending search/sheet/session/reattach/unload/dispose races không stale
mutation/focus; idle/layout storm/cache bounded. Giữ tests và giới hạn source cap,
truncated catalog, rich/date/custom/sort hiện hành, không nới assertions.
14 planned full-shell transitions và actual Popup images phải có pane/clip/anchor/
page/history evidence và được xem hết; có thể giảm ảnh chỉ khi trùng evidence,
không bỏ coverage. Source năm workflows/native tests và root sáu combined gates
riêng; P3 đo cuối không được thay bằng source benchmark hoặc lời hứa nhanh hơn.

### C — Windows/Mac package transport opt-in trong shared launchers hiện hữu

Branch mới `feature/release-009-maui-desktop-consumer` từ exact d73.
B đã RELEASE riêng hai launcher paths tại source `6509393a`, byte-equal48fe:
Windows `b50e60f9f90190594ae6c648e2d0354d83fd8c5c`,
Mac `fee485b3a771b7fe58711d8112bc51ccbc187c3d`.
Root đã đọc delta. C nhận đúng hai frozen blobs bằng apply_patch vào commit
riêng trước implementation; đây là received baseline delta PHẢI nhận khi root
tích hợp C, KHÔNG phải import-only để skip vì root d73 chưa có chúng. B không
ghi hai paths nữa; không import whole B/editor/corpus hoặc B workflow.

Grant đúng chín paths:

- `scripts/run-maui-windows-smoke.ps1`
- `scripts/run-maui-maccatalyst-smoke.sh`
- `scripts/run-release-009-maui-consumer.ps1`
- `.github/workflows/release-009-maui-packages.yml`
- `eng/release-009-maui/emission-fixture/EmissionFixture.csproj`
- `eng/release-009-maui/emission-fixture/Program.cs`
- `docs/adr/0008-maui-cross-host-package-assembly.md`
- `docs/release-009-maui-package-consumer-contract.md`
- `docs/worklog/RELEASE-009-MAUI.md`

Giữ default legacy behavior/timeout/assertions và diagnostics đã release; thêm
opt-in `app-file-v1` trong CHÍNH hai launcher, không launcher hoặc parser thứ hai.
Chỉ chạy isolated hosted CI với RUNNER_TEMP; không điều khiển desktop/workbook thật.
Windows proposed flags ResultPath/MarkerPrefix/ResultProtocol cùng existing
ExecutablePath, TimeoutSeconds75, MaximumAttempts1; mode mới bắt một attempt,
fresh output khác private app payload/context, actual child ExitCode0 và bounded
completion, capture stdout/stderr chính child. Timeout/nonzero dù marker success
vẫn FAIL; async pipe reads phải bounded, không deadlock/đợi vô hạn.

Mac proposed args giữ hai legacy args rồi expected bundle/prefix/mode. Verify
Info.plist/bundle identity, strict codesign và dùng existing NSWorkspace/LaunchServices
helper; không signing/security flags, direct-executable bypass hoặc native retry.
Fresh writable path phải được xác minh trong actual container của app hosted,
không lấy simctl iOS áp sang Mac. Compact envelope phải thật sự thu được qua
scoped native console/unified transport; không có marker thì không pass file-only.
Opt-in không chạy legacy broad deletion/container search/stale fallback/process
replacement search; chỉ owned fresh paths và launched process, cleanup bounded.
Giữ bounds launch30s/result90s. LaunchServices success KHÔNG là child ExitCode0:
Mac chỉ ghi `launchservices-started-and-explicit-completed-marker` tới khi có
cơ chế OS exit đo được. Windows ghi actual child exit0, Android giữ marker-only.

Dùng nguyên shared strict Python CLI/file-context protocol33 của root d73, không
sửa parser/tests hoặc iOS/Android helpers. Full payload/nonce/hash/strict JSON,
minimum3, exact cohort/SDK assemblies/public postconditions và prelaunch app hash
giữ nguyên. Full raw logs/path/context không upload/in ra; không overwrite.
Consumer Emit/page/csproj/SDK giữ nguyên; nếu actual Mac không cung cấp marker/
writable path qua interface này, báo exact blocker và xin amended scope trước.
Fixture hiện hữu được mở rộng với synthetic child modes/launcher-negative tests
và actual parser; giữ toàn bộ existing six behavior groups + CLI roundtrip.
No new packages. Runtime-only Windows/Mac fixture steps nằm trong owned workflow.

Source final sáu gates, whole fresh canonical cohort và native public consumer
bốn targets bắt buộc; thử nghiệm implementation có thể bắt đầu trước native
proof, không coi đó là acceptance. Rollback reverse-revert own slice, giữ d73
SDK và iOS/Android protocol. Whole B/native editor/P3/hardware vẫn OPEN.

### B — chẩn đoán đúng ranh giới Dispatch, không thay true editor gate

True650 narrow `34178968589`: Mac baseline10 PASS, candidate build0/0 nhưng
FAIL trước callback/editor; Windows baseline41/candidate64 PASS, 3 recreations.
Selected registrar entries match prior source, không khẳng định whole mm equal.
Không có current matching IPS/stack; fd2 shape664bytes/5lines không đủ suy crash type.

Grant đúng proposal patch SHA256
`04d34290728692101b4d0d6651c22d6658f8d358770ff81c8de57de6a05207f3`,
chỉ Mac SmokePage.cs before `a12981ecc93ab3521738e8685090c6cda053aee0`
→ after `a82776c5468ebeda2fa8de67e34c6b1c134c1f6d`, cùng own TABLE-007.md.
Thêm resolved/before-call/returned-true-false/callback/invoke markers, fixed
exception classifier + bare rethrow, NoInlining invocation boundary. Không đổi
scheduling/readiness/editor/assertion/timeout/runner, không editor-free variant.
Chạy một paired narrow mới; full source gates chỉ sau actual Mac PASS.
True-return/no-callback không đủ chứng minh JIT failure. Hai launcher đã chuyển
C writer, B không ghi lại hoặc nhận package changes vào lượt chẩn đoán này.

Root giữ shared status/worklog/CI/parser. Mọi handoff phải release paths và có
exact-final source gates/artifact/manifest, rồi root nhận tuần tự và test combined.
Không merge PR #1, publish public feed hoặc suy 100% từ partial green.

## Checkpoint 08/09 — nhận A/C, chuẩn bị grant tiếp theo

Root nhận C final `5d70be93` thành `389c883d..aabd359f` và A final `349cc0aa`
thành `48f141dc..107dab01`. Các paths đã RELEASED nay do root sở hữu. C đủ sáu
source gates, iOS native consumer 8 frames/Android 10 frames; A đủ năm gates,
Windows native 158/0 skip, 5 final PNG root đã xem. Shared transport `30e74bef`
giữ nguyên. Combined CI phải chạy riêng, không dùng source green thay thế.

Tái dùng ba task/model hiện hành, không tạo thêm task/desktop lease:

- A READ-ONLY: plan active split paged AutoFilter bằng presenter/binding/controller
  hiện hữu; chỉ ra hit-test/header events, anchor/focus/lifecycle và exact paths
  cần grant. Chưa được ghi SDK/sample/filter hoặc shared docs mới.
- C READ-ONLY: plan Windows/Mac PackageReference native consumer; tái dùng actual
  launcher của B, xác định args/env/result/exit/provenance và điều kiện chuyển
  ownership. Không launcher thứ hai, source mutation hoặc local native/build.
- B giữ grant actual Mac SmokePage orchestration bên dưới; dirty edits trong
  worktree B được bảo toàn. Không mở rộng SDK/renderer/handler hoặc nới assertions.
- Root giữ shared docs/CI và các source vừa nhận, chạy exact combined gates.

Ba sự cố quota ở các turn cũ đã được tiếp tục đúng một lần ngày 08/09; không
lặp vô hạn, đổi model hoặc consume reset. A đã phục hồi và hoàn tất handoff.
Checklist U1–R4 giữ OPEN khi chưa đủ toàn bộ bằng chứng của từng mục; partial
bar/iOS success không đóng whole B, P3, hardware hoặc R3 đa nền tảng.

## B chuyển từ diagnostic sang actual Mac smoke orchestration

Completed-await1ff narrow33991085641: baselinePASS10, originalcandidateFAIL,
noeditor variantPASS10, WindowscandidatePASS62/3recreation. Giữ failures cũ;
không coi async/Dispatcher/renderer là root cause đã chứng minh. Đóng one-off.
B được writer CHỈ MacCatalyst.AnalyticsSmoke/SmokePage.cs để bắt đầu true
editor/analytics phase từ page Loaded + actual frame/layout readiness ngoài
PaintSurface (trước đây child Loaded→InvalidateSurface→Paint khởi tạo phase
trước page Loaded). Giữ true Table007EditorSmoke và mọi native assertions/
disposal/recreation; không SDK/renderer/handler edit, fixed sleep, retry, flags
hoặc linking workaround. Chạy paired baseline/truecandidate narrow, sau đó
full source gates/corpus nếu đạt. Helper/path ngoài scope phải xin grant trước.

C review4660 tìm counterexample truncated compact duplicate accepted; root
hardening file-mode đọc từng complete compact marker, không legacy truncated
reconciliation. Shared source vẫn root-owned; C nhận immutable update riêng,
không nới own full-cohort/>=3/nativepostconditions hoặc tự sửa parser.

## Grant tiếp sau nhận A finaleea6 — sample bar / full result protocol

A released11paths đã nhận root8eb622b5..37a55b05, skip257611. A tái dùng worktree
và model hiện hữu, branch feature/release-009-formula-bar từ exact
37a55b05e3af25809d412b67debb0d9e3581f929. Có thể bắt đầu sample độc lập trên
SDK source đã native-verify; không bỏ final combined/source gates. Scope writer:

- Sample RibbonPreviewWindow.cs, mới RibbonPreviewWindow.FormulaBar.cs;
  Commands.cs CHỈ4Formula action registrations/ShowFormulaHelp; Capture.cs CHỈ
  bounded bar captures, không bỏ existing assertions.
- Mới Windows.Rendering.Tests/Release009FormulaBarSmokeTests.cs;
  Release009RibbonCommandSmokeTests.cs chỉ4formula/help behaviors và
  RibbonLoadedWorkbookSmokeTests.cs chỉ formula presentation assertions nếu cần.
- Commands/PresentationStrings.resx và .en.resx chỉ thêm localized bar labels;
  giữ toàn bộ existing keys.
- docs/release-009-formula-bar-contract.md, worklog/RELEASE-009-FORMULA-BAR.md;
  docs/demo/COMMANDS-WIN11-VI.md chỉ các mục bar/4formula/help.

Bar dùng CurrentEditorDraft/EditorDraftChanged/UpdateEditorDraft/FocusEditor;
Enter commitonce, AltEnter newline, Esc cancel, giữ anchor/validation/range/
focus, không queued refresh clobber/resurrectdraft. Text shortcuts không thành
workbook shortcut; giữ keytips. Bốn nút cập nhật existing draft thay restart;
help theo actual draft/caret. Không production reflection/replayed native keys,
new editor model, SDK/filter/navigation/shareddocs/CI writes. Tab/F2 có thể
chuyển focus về native editor; direct bar completion cần SDK hook thì xin grant
riêng, không giả đã có. Tests both standalone/split và final5gates/ảnh bắt buộc.

B a3 dispatch-only variant FAIL trước callback dù không editor; Windows lần
này PASS59frames/3recreation nhưng previous intermittent fail vẫn là risk.
Đóng a3; grant đúng một CompletedTask partition before89bc459f →
afterc8a2bfc0417cf1fdfc4ff1064c7a9575c6b15d56, patch SHA256
f184f03f6a70dd7e61e10598b982ad3910bbc3c4eb849e48b5ad61f6b6bb6b69.
Giữ async helper/caller, thay Dispatcher bằng awaitTask.CompletedTask/marker,
bỏ unused using mechanical. Source1ff4f358/narrow33991085641; original gates
giữ nguyên, không editor acceptance hoặc thêm retry/linking/private flags.

C686 iOS consumer FAIL malformed987/offset976; stream cụ thể UNKNOWN.
Root giữ launcher/parser/context/path-generation/32tests/sharedcontract;
C được writer PackageProvenance.cs opt-in app-file-v1, wrapper fifthflag và
linked actual-emitter console fixture dưới eng/release-009-maui, workflow
fixtures exactSDK10.0.302 hosted-only/khôngpackage mới, ADR/contract/worklog riêng.
Spec là native-result-file-v1/env3names/exact5envelopekeys trong shared contract.
Default long marker và fullcohortpostconditions không giảm. C có thể viết own
experiment trước native shared proof, chỉ import shared checkpoint nguyên
trạng khi root gửi; import-only phải SKIP lúc root nhận. File-only success,
overwrite, stale nonce hoặc partial JSON không được nhận. Actual iOS/native
opt-in và full final6gates vẫn OPEN, không có circular wait hoặc acceptance waiver.

## Grant bổ sung sau f344 actual native PASS / nhận C8b

Root f344 full33988991344 và iOS33988991332 SUCCESS. A/C được import đúng
ba immutable root blobs trong commit import-only riêng, không sửa implementation:

- scripts/run-maui-ios-smoke.sh: `41b8d02bad14c812fffcda562b63c17c02336682`.
- scripts/verify-native-smoke-result.py: `5fd92e2866fc7231c44c81feda3dd4dcc8c43788`.
- scripts/test-native-smoke-result.py: `cf540735e2a20faa72be3a842338b4a4638d49b7`.

Khi nhận source A/C, root SKIP import-only để không ghi đè shared transport.
A thêm đúng hai writer paths NeraSpreadsheetControl.EditorDraft.cs và
NeraSpreadsheetSplitAdorner.EditorDraft.cs: sau text update chỉ Select nếu
native SelectionStart/SelectionLength khác requested range. Không đổi public
API hoặc suy CaretIndex thành moving edge của nonempty WPF selection. Native
test unchanged echo giữ backward direction, changed range vẫn áp dụng một lần.
Các grant WPF trước giữ nguyên; sample bar/filter/resources vẫn chưa được giao.

C frozen8b đã release/nhận28paths; root thêm integration push trigger workflow
mới và giữ shared status. C tiếp tục branch feature/release-009-maui-ios-consumer
từ8b, nhận ba f344 files nguyên trạng và chỉ wire iOS trong owned wrapper/
workflow. verify-app ngay trước launcher, own verify-runtime cohort/nonce/feed/
target/version/source, >=3frames và public postconditions sau launch bắt buộc.
Không lấy legacy min2 thay consumer gate; final source sáu workflows phải xanh.

B baseline-page7fa run33988816930: baseline PASS10, original candidate FAIL,
exact baseline-page variant PASS10; generated CellTextView vẫn có, targeted
class metadata trùng nhưng không khẳng định toàn registrar.mm trùng. Windows
candidate vẫn FAIL ở first reinsert. Đóng variant cũ; grant DUY NHẤT one-page
baseline-dispatch partition mới, before89bc459fa5c86b523e96bcd22f19a496e2151df6
→ after0ff170b2483d5137b7e8518fdbb3e2d59d2f42c5, patch SHA256
`6996d3c42d01f96423698ece2b03c4f9ae123c188036eb41b190357698eb01e5`.
So passing baseline page chỉ thêm awaited Dispatcher Action marker và async
caller adaptation; không editor/fd2 hook/production delta. Original failures
giữ FAIL, không đổi timeout/retry/linking/flags. Variant không phải TABLE007
acceptance; beforeblob đổi phải xin lại phạm vi. B pusha3b7a4df để chạy narrow CI.

## A tiếp tục SDK split editor routing từ7a378ca1 — grant hiện hành

Root7a desktop job101365914847 đã SUCCESS, cùng Core/Q/packages33988335138
và demo33988360802. iOS transport còn pending, không phải all-platform accepted
baseline. Để desktop và native transport độc lập tiếp tục song song, thay điều
kiện chờ đủ năm gate trước khi BẮT ĐẦU ở các mục cũ; cổng HOÀN THÀNH final
source và combined vẫn phải đủ xanh, không có ngoại lệ acceptance.

A tái dùng task/worktree/model hiện hữu, nhánh mới
`feature/release-009-split-editor-routing` từ exact
`7a378ca133a517820a3e9425423e841513e8d07d`; giữ navigation branchbed515b7.
Writer duy nhất cho năm WPF files: NeraSpreadsheetControl.cs,
NeraSpreadsheetControl.FormulaEditing.cs, NeraSpreadsheetSplitController.cs,
NeraSpreadsheetSplitAdorner.KeyboardEditor.cs và
NeraSpreadsheetSplitAdorner.FormulaEditing.cs.
Tests: Table007WpfEditorDraftSmokeTests.cs, Table007SplitEditorSmokeTests.cs
và mới Release009SplitEditorRoutingSmokeTests.cs nếu cần. Không ghi đè root
full-cell measure/opt-out/queued-native-scroll regressions. Private docs:
docs/release-009-split-editor-routing-contract.md và
docs/worklog/RELEASE-009-SPLIT-EDITOR.md. Không sửa file khác khi chưa xin grant.

Scope: owner ScrollCellIntoView và native Enter/Tab dùng đúng active pane qua
existing controller/frame/metrics, giữ fractional offsets và panes khác,
hidden/merge/freeze/history. Route current formula metadata/highlights/help
đến actual split UI, render nested argument help bằng assistant hiện hữu;
giữ stable Table IDs, draft/caret, opt-out và cleanup. Không thêm model hoặc
quét workbook. Test hướng selection ngược trước đề xuất mở API mới.
Sample formula bar, Commands, resources và paged filter KHÔNG thuộc grant này.
Root giữ các file đó cùng shared docs/CI; B không lấy lại B21desktop đã nhận.

B được thêm một partition diagnostic sau khi class-exclusion không cô lập lỗi:
checkout riêng cùng candidate, thay DUY NHẤT Mac SmokePage.cs bằng exact base
2e848 blob28bd663814d338a7a9564b852e6f4c5eb31dd664, beforeblob phải đúng
89bc459fa5c86b523e96bcd22f19a496e2151df6. Patch SHA256
bf026f39611378559bb8b2a4d0dfb1096891a44e639d4a79c19e43f8fb28a83b.
Giữ candidate SDK/renderer/handler registration, record generated reachability;
baseline/candidate gốc chạy trước và giữ FAIL. Variant cố ý chỉ kiểm analytics
baseline, không chạy editor nên không thể nghiệm thu TABLE007. Không đổi
timeouts/attempts, flags/linking/signing hoặc thêm biến thể khác. B ghi evidence
trong owned worklog; đây không phải production fix.

## Checkpoint tích hợp desktop — 06/09

A đã release toàn bộ navigation tại `bed515b7`, source năm workflow xanh;
root ghép bốn commits thành `fba2de6a..8fda1486`, review đủ 8 ảnh final.
B release riêng 21 desktop/shared-assistant/test paths tại `9bf24af9`, root
nhận thành `ce1a00d2` và kiểm exact blobs, không lấy MAUI/corpus/Viewport/docs
hoặc lặp cancellation49/64. Root hiện giữ các paths đã nhận; A/B không viết
tiếp khi chưa được grant. Root hardening measure/clip và highlight opt-out có
native regressions mới; phải xanh đúng combined HEAD.

A tạm read-only chuẩn bị formula-bar/active split command routing qua chính
public draft bridge, không tạo model hoặc tự sửa SDK/Commands. C tiếp tục
canonical MAUI consumer ở branch riêng; shared Android/iOS launchers root
chưa release vì iOS framing FAIL. Whole B/P3/hardware vẫn OPEN.

Partial transport release theo sau: Android job101364379179 ở root22338c79
SUCCESS (và job101363322182 ở479 cũng SUCCESS). C được nhận đúng ba blobs
source223 của run-maui-android-smoke.sh, verify-native-smoke-result.py và
test-native-smoke-result.py; shared source vẫn root-owned, không sửa riêng.
C được wire Android runtime trong owned package wrapper/workflow: verify-app
file-set/size/hash trước install, explicit identity/tag/prefix/fresh result và
verify-runtime đầy đủ source/version/feed/nonce/target/public API/assemblies,
>=3 completed frames. Không lấy legacy minimum2 làm consumer acceptance.
Android source success không thay consumer success; Windows/iOS/Mac native
và full B/P3 vẫn OPEN. iOS launcher chưa release, Windows/Mac còn B giữ.

B được thêm một Mac diagnostic variant từ cùng exact candidate ở checkout
tạm riêng: bỏ handler mapping và exclude native Apple CellTextView class khỏi
compile bằng explicit patch đã freeze. Phải verify registrar thật không còn
CellTextView (và vẫn có shared MauiTextView) trước launch. Baseline/candidate
gốc chạy trước, failure giữ nguyên làm job FAIL; không thêm variants/retries,
đổi assertions/time bounds, production fallback hoặc publish variant. Existing
sanitized 32 KiB bound giữ nguyên. Đây không phải acceptance hoặc bằng chứng
để tự sửa Register/Preserve/renderer.

Variant c187fe80/run33987516524 đã build/registrar-absence guard PASS nhưng
native vẫn FAIL cùng stack; shared registrar.h trùng baseline. B đóng variant,
không suy ra subclass là root cause. B tiếp tục read-only first-failing SHA/
minimal startup delta analysis; chưa được bật thêm private Objective-C flags.

Chi tiết source, test, giới hạn và rollback trong
[integration record](RELEASE-009_DESKTOP_INTEGRATION_20260906.md).

## Chuyển quyền tiếp tục tại baseline tích hợp 50cb357a — 06/09

Mục này thay thế quyền sửa lịch sử của A/C bên dưới. Root đã xác minh đủ
năm workflow **success** tại `50cb357a00d6bb8a6b134cdeebce624a09bd1b21`:
full `33984177819`, iOS `33984177815`, Q003C `33984177818`, Windows packages
`33984174136`, published demo `33984234305`. Đây chưa phải whole B acceptance.

Tái sử dụng ba task/worktree hiện hữu, giữ GPT-6 Astra / xhigh. A và C tạo
nhánh mới từ đúng baseline trên sau khi kiểm tra clean tracked tree; giữ
nguyên nhánh đã bàn giao, không reset/rebase hoặc xóa artifacts. Không tạo
thêm task/worktree. Không local heavy build/native lease khi đĩa C gần đầy.

### A — RELEASE-009 navigation shell

- Nhánh mới `feature/release-009-navigation-shell`.
- Writer duy nhất trong WPF sample: `RibbonPreviewWindow.cs`,
  `RibbonPreviewWindow.WorksheetTabs.cs`, `RibbonPreviewWindow.Capture.cs`,
  các partial mới `RibbonPreviewWindow.Navigation.cs` và
  `RibbonPreviewWindow.SplitShell.cs`.
- Tests: `RibbonLoadedWorkbookSmokeTests.cs` và mới
  `RibbonWorksheetNavigationSmokeTests.cs` trong Windows.Rendering.Tests.
- Tài liệu riêng: `docs/release-009-navigation-shell-contract.md` và
  `docs/worklog/RELEASE-009-NAVIGATION.md`.
- Bổ sung grant localization: A giữ duy nhất
  `src/NeraSpreadSheet.Commands/PresentationStrings.resx` và
  `PresentationStrings.en.resx` để thêm đúng hai key automation name
  `Cuộn ngang trang tính` / `Cuộn dọc trang tính`, English tương ứng
  `Scroll worksheet horizontally` / `Scroll worksheet vertically`.
  Native names phải theo runtime localization change; test trong owned
  RibbonWorksheetNavigationSmokeTests, giữ resource parity gate hiện hữu.
  Không mở rộng sang command mutation hoặc thay các resource không liên quan.
- Gắn hai standalone worksheet scrollbars bằng API/extent hiện có, coalesce
  thumb input theo frame, giữ fractional offsets và adaptive extent floor.
  Loaded split dùng đúng session/stored topology, không force Both hoặc ghi
  far cells; chỉ một input host/scrollbar topology hoạt động tại một thời điểm.
- Không sửa SDK/Scrolling/Viewport/Editing hoặc existing CI/shared docs. Formula
  bar chờ public draft/caret bridge B; không dựng editor model trong sample.
  Lifecycle split cần B release trước khi gọi full editor acceptance hoàn tất.
- Existing regression + actual loaded shell native CI/captures bắt buộc; không
  lấy headless tests làm native proof hoặc chạy heavy builds ở local.

### C — RELEASE-009 MAUI package matrix

- Nhánh mới `feature/release-009-maui-packages`.
- Writer cho workflow mới `release-009-maui-packages.yml`; scripts mới
  `build-release-009-maui-shard.ps1`, `assemble-release-009-maui-packages.ps1`,
  `run-release-009-maui-consumer.ps1`, các fixture tests tên RELEASE-009 MAUI;
  `eng/release-009-maui/` và `tests/NeraSpreadSheet.Packaged.Maui.Smoke/`.
- Reserve ADR `docs/adr/0008-maui-cross-host-package-assembly.md`; contract riêng
  `docs/release-009-maui-package-consumer-contract.md`, worklog riêng
  `docs/worklog/RELEASE-009-MAUI.md`.
- Một exact source/version/toolchain cohort, neutral packages và bốn TFM
  producers, assemble canonical MAUI package qua NuGet pack; kiểm payload và
  dependency groups, không đưa bốn partial cùng ID/version vào consumer feed.
  Consumer public API, ngoài checkout, PackageReference-only/cache cô lập;
  mỗi platform phải chạy đúng package/app hash và fresh runtime marker.
- Không sửa production/csproj SDK, existing workflows hoặc runner scripts B
  đang giữ. Có thể chuẩn bị và kiểm assembler/consumer/build matrix song song;
  đề xuất exact launcher parameterization riêng để root chuyển quyền sau B,
  không copy runner rồi âm thầm chạy source smoke cũ hoặc bỏ runtime gates.
- Không public feed; Windows-only pack không phải multi-target proof. Source
  matrix và final combined whole-B matrix là hai checkpoint khác nhau. P3 vẫn
  chờ full B, không đổi perf protocol hoặc lấy package build làm performance.

### B và root

B tiếp tục native/editor/corpus. Sau current bounded Mac probe, B được nhận
lại WPF `NeraSpreadsheetControl.cs` (slice 49 đã tích hợp), cùng các owned
formula/split files để bổ sung public bridge draft/caret/notifications và
Begin/Commit/Cancel trên chính editor hiện hữu. API phải có tests/docs, giữ
validation, một Undo, focus/caret và cleanup khi canonical editor đã hủy.
Chưa thay scheduler từ giả thuyết; numeric stderr counts và callback activation
isolation chỉ là diagnostic, không phải production fix hoặc runtime proof.
MAUI external-cancel cleanup và split cleanup cần native regressions riêng.

Cho B một biến thể diagnostic riêng trong Mac paired job sau candidate gốc:
checkout cùng source ở thư mục tạm riêng của run, chỉ bỏ registration của
`NeraCellEditorHandler` để dùng default handler, không sửa tracked candidate.
Giữ toàn bộ editor/analytics assertions và runtime timeouts; ghi rõ variant
không phải acceptance. Baseline/candidate gốc vẫn chạy và failure vẫn làm job
fail dù variant tiến xa hơn hoặc success. Không upload/publish variant package
hoặc gọi đó là production fix. Báo exact source delta và entry stages để root
quyết định bước tiếp, không mở nhiều biến thể hoặc rerun-until-green.

Sau matched IPS của run `33985385938`, cho B thêm read-only diagnostics trong
paired Mac workflow đang sở hữu: đối chiếu generated registrar/linker metadata
của đúng baseline/candidate đã build trong job, đặc biệt native editor subclass.
Chỉ đọc outputs hiện có bằng công cụ sẵn trên runner, không build biến thể,
install tooling, debugger/OS changes hoặc sửa production. Xuất bounded summary
tối đa 32 KiB gồm logical type/selector/signature/flags, source/toolchain và
relative output hashes; nếu output không có thì báo unavailable, không suy đoán.
Không upload raw generated files/binlog/paths/UUID/native dumps. Native runtime
failure và existing acceptance gates giữ nguyên; metadata không thay runtime.

Root giữ sample Commands/command audit, tất cả shared docs/CI ngoài các quyền
mới ghi rõ, review và tích hợp tuần tự. A/C báo đường dẫn cần thêm trước sửa;
không ghi cùng CURRENT hoặc worklog của lane khác. CI đúng HEAD cuối và tất cả
acceptance OPEN dưới đây vẫn giữ nguyên, không tự giảm phạm vi để báo 100%.

## Phạm vi và mốc xuất phát

Người dùng yêu cầu chia worktree tiếp tục đến khi hoàn thành. Đợt này thay thế
quy định tạm dừng mở wave sau nghiệm thu ở các handoff lịch sử. Mục tiêu 100%
là hoàn tất acceptance của Table / Filter / Ribbon / UX trong delivery plan,
không phải tương thích 100% mọi tính năng của Microsoft Excel hoặc toàn SDK.
Không tự chuyển acceptance chưa kiểm chứng thành ngoại lệ để báo DONE.

- Integration branch: `feature/bootstrap-architecture-v0.1`; PR #1 Draft/open.
- Exact green baseline: `2e8482c25a44797a479b276ae26f472811a0a81e`.
- Full CI `33966917191`, iOS `33966917101`, Q003C `33966917091`: success,
  đủ bảy job đúng SHA. Core 1505/1505, Windows 105/105, MAUI 44/44.
- Handoff và artifacts: [PR #1 comment](https://github.com/HoangHung997/NeraSpreadSheet/pull/1#issuecomment-5551912649).
- Baseline đã có 226 Ribbon PNG / 128 layout records, 4 MAUI PNG và 18 nupkg.
  Chúng không thay thế nghiệm thu trên HEAD kết hợp mới.

## Ba lane độc lập

Tất cả task mới dùng `gpt-6-astra`, `xhigh`, worktree riêng từ baseline trên.
Không tự tạo thêm agent/task. Đã xác minh cả ba task chạy đúng base/model/effort.

| Lane | Công việc | Trạng thái |
| --- | --- | --- |
| A — UX-007 | Keyboard, focus, accessibility; hoàn thiện localization/chrome/customization còn thiếu của UX-006 | SOURCE RELEASED / INTEGRATED; hardware/combined OPEN |
| B — TABLE-007 | Shared structured-formula editor cho split/MAUI; corpus do LibreOffice thật tạo | ACTIVE |
| C — PERF-008-HARNESS | Harness, stress tests, paired baseline trên CI runner riêng | SOURCE RELEASED / INTEGRATED; P3 OPEN |

| Lane | Task ID | Branch |
| --- | --- | --- |
| A | `01a071e8-82ec-76c2-8c96-d97faa31e40c` | `feature/ux-007-keyboard-a11y` |
| B | `01a071e8-82eb-7722-8e51-adb2bcf7dd1c` | `feature/table-007-editor-corpus` |
| C | `01a071e8-82eb-7722-8e51-ada88da6d05b` | `feature/perf-008-harness` |

### Quyền sửa A

- Ribbon.Core, Bars.Core và các Ribbon/Bar/customization presenter/binding/chrome
  của WPF, WinForms, MAUI; Commands chỉ presentation/localization, không Table
  mutation handlers hoặc formula/calculation.
- Table/Filter popup, paging input, accessibility và MAUI AutoFilter/Table host
  chrome; không thay đổi filter/sort/workbook semantics.
- Ribbon preview/capture và test Ribbon/customization/filter-focus tương ứng.
- `tests/NeraSpreadSheet.Maui.Windows.RibbonSmoke/SmokePage.cs` và
  `tests/NeraSpreadSheet.Maui.Windows.TableFilterSmoke/SmokePage.cs` thuộc A;
  không sửa generic Windows.Smoke editor page dự kiến dành B.
- Ngoại lệ chuyển quyền **A là writer duy nhất `.github/workflows/ci.yml`**
  đến handoff/release, chỉ thêm upload `maui-windows-ribbon-ux007` cho
  `artifacts/maui-windows-ribbon-smoke/ux007-*.png` sau loaded Ribbon smoke,
  `if: always()` / `if-no-files-found: error`. Commit cùng producer 9 captures;
  không đổi triggers/SDK/jobs/gates khác. Root không sửa file này khi A giữ.
- Contract riêng `docs/ux-007-keyboard-accessibility-contract.md`, worklog
  `docs/worklog/UX-007.md`; có thể cập nhật contract UX-006/keyboard liên quan.
- **A giữ duy nhất desktop lease** cho synthetic native UI smoke/capture.
  Đọc đầy đủ Computer Use skill/guidance trước thao tác. Không sửa workbook thật
  của người dùng, không đổi thiết lập hệ thống để giả lập bằng chứng phần cứng.

### Quyền sửa B

- Editing formula assistant/editor contracts; editor/input của standalone WPF,
  WinForms và `NeraSpreadsheetSplitAdorner` / `NeraSpreadsheetSplitSurface`;
  MAUI spreadsheet view/editor overlay, không Table/AutoFilter host chrome của A.
- Corpus Table/OpenXml và converter fixes nếu có regression chứng minh; giữ
  native Table identity, preservation boundaries và session transaction hiện có.
- Test structured-editor, split editor, MAUI editor, producer corpus riêng.
- Contract riêng `docs/table-007-editor-corpus-contract.md`, worklog
  `docs/worklog/TABLE-007.md`; contract native Table/split editor liên quan.
- Không điều khiển desktop local khi A giữ lease. Dùng headless/CI; xin chuyển
  lease qua coordinator nếu thực sự cần native runtime trên máy này.
- Đã cấp B độc quyền tạo `.github/workflows/table-007-libreoffice.yml` và
  `scripts/table-007-libreoffice.py` để actual Calc headless tạo synthetic XLSX,
  version/hash/privacy provenance. Existing workflows vẫn thuộc root.
- Đã chuyển B độc quyền `tests/NeraSpreadSheet.Maui.Windows.Smoke/SmokePage.cs`
  và helper mới `Table007EditorSmoke.cs` trong cùng project: bọc view bằng host
  dùng canonical session.Editor, chạy editor regression trước runtime stress cũ.
  Không bỏ gates cũ/csproj; không dùng chung RibbonSmoke/TableFilterSmoke page A.
- Đã chuyển B độc quyền
  `src/NeraSpreadSheet.Maui/NeraSpreadSheetMauiAppBuilderExtensions.cs` chỉ để
  register internal reused cell-editor handler. Giữ UseSkiaSharp và existing
  Mac Catalyst SKGLView handler workaround; A không sửa registration file này.
  Native Apple key handling phải giữ IME/composition và delegate unhandled keys;
  Apple build không thay native editor keyboard smoke.
- Đã chuyển B ba native hooks: `SmokePage.cs` trong
  `tests/NeraSpreadSheet.Maui.Android.AnalyticsSmoke`,
  `tests/NeraSpreadSheet.Maui.iOS.AnalyticsSmoke`,
  `tests/NeraSpreadSheet.Maui.MacCatalyst.AnalyticsSmoke`; mỗi project thêm
  `Table007EditorSmoke.cs`. Chỉ thêm editor phase/result evidence, restore
  focus/layout/selection và giữ mọi analytics assertion/gate cũ. Không đổi
  workflow/csproj, không nới timeout để né lỗi. Editor fail phải fail smoke.
  Native InsertText/MarkedText không thay Apple hardware OS-keyboard evidence.
- B được chuyển `src/NeraSpreadSheet.Viewport/SpreadsheetViewportEngine.cs`
  chỉ extract layout computation dùng chung Compose/public ComputeLayout và
  test mới `tests/NeraSpreadSheet.Viewport.Tests/Table007EditorGeometryTests.cs`.
  Không đổi scroll/cache/composition hoặc duplicate metrics. Layout-only không
  tạo display list/recalculate, nhưng `EnsureMetrics` hiện có thể capture
  snapshot khi active Table filters; không hứa snapshot-free cho filtered sheet.
  Giữ visibility/freeze/merge/fractional geometry và snapshot reuse, test với
  Compose.Layout; MAUI fallback theo real host size/zoom không giả GPU thành công.
- Chuyển B writer duy nhất `scripts/run-maui-maccatalyst-smoke.sh` chỉ cho
  diagnostic sau failure: chờ tối đa 10 giây cho crash report của process/run
  vừa exit, in sanitized exception/termination/thread frames để khoanh lỗi.
  Giữ success criteria, launch/runtime timeout và exit status; không retry để
  được xanh. Không in/upload raw report có Machine ID, đường dẫn người dùng,
  UUID thiết bị hoặc environment. Không đổi cleanup/phạm vi filesystem hiện có.
- Chuyển B writer duy nhất `scripts/run-maui-windows-smoke.ps1` chỉ để đọc
  bounded stage sidecar của đúng resultPath/attempt khi process thất bại,
  whitelist stage labels, không workbook values/paths/environment. Giữ retry
  count/criteria, timeout, frame/success gates và cleanup cũ; trace không được
  thay result JSON hoặc biến failure thành success. Không retry đến khi xanh.
- Cho B tạo riêng `.github/workflows/table-007-native-diagnostics.yml` để
  rút ngắn vòng PROBE: chỉ Windows.Smoke và MacCatalyst.AnalyticsSmoke trên
  hosted runners, giữ setup/scripts/timeouts/attempts và tất cả assertions của
  hai project như full CI. Push chỉ nhánh B, không sửa ci.yml. Có thể chạy
  diagnostic trước trong vòng probe; final source vẫn bắt buộc ba existing
  workflows/bảy jobs và diagnostic xanh đúng HEAD. Không thay final gates.
- Chuyển B writer duy nhất
  `src/NeraSpreadSheet.Maui/NeraMacCatalystSKGLViewHandler.cs` cho diagnostic
  bounded render-depth/queued/drawing/disposed stage, không đổi scheduling,
  dispose hoặc success criteria trong probe. Trace chỉ stage/depth/flags,
  không dữ liệu workbook/đường dẫn/identity thiết bị. B báo evidence trước khi
  đề xuất production fix; root không sửa file này đến khi B release.
- Windows SmokePage được bắt native Application.UnhandledException để ghi
  failure stage và exception type/stack đã sanitize vào existing failure
  result; không set Handled, không nuốt lỗi, không success fallback. Chỉ đăng
  ký trong smoke, unsubscribe Dispose; giữ runner retries/timeouts/assertions.
  Loại workbook values/đường dẫn/UUID khỏi diagnostic, không upload raw dump.
- Cho B một paired baseline/candidate diagnostic trong cùng hai hosted jobs
  của `table-007-native-diagnostics.yml`: baseline cố định
  `2e8482c25a44797a479b276ae26f472811a0a81e` checkout ở subdirectory riêng,
  build bằng cùng SDK/workload và chạy tuần tự trước candidate bằng current
  sanitized runner scripts. Không sửa baseline, OS/signing/security hoặc dùng
  debugger bypass; labels/results tách biệt. Giữ runtime timeouts/attempts/
  assertions, không continue-on-error hoặc success fallback. Candidate vẫn chạy
  để thu evidence nếu baseline fail, nhưng job failure được giữ; baseline không
  thay candidate acceptance hoặc final full/iOS/Q003C gates.
- Cho B probe Mac stderr của chính synthetic smoke process bằng dup2(fd2)
  trong owned SmokePage trước native attachment. Chỉ file temp riêng current
  process/run trong app sandbox, không truncate file của lượt/instance khác.
  Runner chỉ đọc failure-only tối đa64KiB, xuất tối đa64 symbolized method
  frames qua whitelist và bỏ paths/UUID/addresses/registers/locals/values;
  không upload raw stderr, dump hoặc artifacts chứa dữ liệu native chưa lọc.
  Cleanup chỉ exact current-run file đã xác minh. Giữ Console/result criteria,
  timeout/attempts; không debugger attach, OS/signing/security hoặc scheduler
  changes. Redirect chỉ để thu evidence, không thay success gate hay kết luận fix.

### Quyền sửa C

- `benchmarks/`, harness/test mới có tên riêng PERF-008, script
  `scripts/run-perf-008*` và workflow mới `.github/workflows/perf-008.yml`.
- Contract riêng `docs/perf-008-acceptance-contract.md`, worklog
  `docs/worklog/PERF-008.md`; được cập nhật performance budget bằng số đo thật.
- Không sửa production code trong khi A/B active; không sửa existing CI workflow,
  project references hoặc test của A/B. Chuyển quyền qua coordinator nếu phát
  hiện bottleneck cần sửa production sau integration.
- Chuẩn bị paired baseline/candidate trên runner riêng; không lấy local timing
  lúc ba lane build đồng thời làm bằng chứng. Lưu raw output, input/output
  fingerprints, SDK/runner/config và độ dao động; threshold từ baseline đo lại.
- Hoàn tất harness chưa có nghĩa PERF-008 DONE: phải chạy lại trên HEAD kết hợp
  A+B và xử lý hồi quy trước nghiệm thu.
- Checkpoint C đã nhận: baseline A/A calibration trước candidate, paired AB/BA
  có cùng harness overlay hash, noisy run trả INCONCLUSIVE; Program.cs thuộc
  benchmarks và new PERF008 Windows smoke file thuộc C, không sửa csproj.
- Audit C ghi nhận cache hiện giữ requested pages, chưa eviction: phải đo và
  nêu source/default distinct cap, số trang và memory/disposal; không gọi cache
  constant-bounded khi chỉ native UI page đang bounded.
- C preflight run `33971846930` tại `413ab07a` dừng trước đo vì actual SDK
  resolve 10.0.400 thay vì exact 10.0.302 của harness. Không có statistical
  samples và không phải production regression. C isolate DOTNET_INSTALL_DIR
  trong workflow riêng; không đổi global.json hoặc existing CI.
- C complete run `33972169896` tại `2c8c4e2c`: source report native 2/2 PASS,
  paired 10/11 PASS, completion tiny-batch baseline noise INCONCLUSIVE. Artifact
  `9971302162` giữ đầy đủ; không gọi INCONCLUSIVE là no-regression acceptance.
- Root chấp thuận một protocol correction trước baseline mới: toggle4096 ops/
  warmup128, completion32768/1024, cachedPage262144/4096, target batch khoảng
  20ms trở lên để giảm timer/scheduling noise; Ribbon/open/search giữ nguyên.
  Giữ nguyên thresholds/statistics/datasets; freeze revision rồi chạy lại TOÀN
  A/A và AB/BA, không ghép/chọn samples. Run cũ không xóa; P1/P3 vẫn OPEN.
- Approval count cuối đến sau C đã push revision v2 `97f20261`, run
  `33973443725`. Root thấy phase measurement đã completed nên không cancel;
  v2 được lưu riêng, không trộn. C freeze v3 theo counts cuối đã chốt trước khi
  đọc candidate raw; giữ cả v1/v2 dù kết quả nào.
- C final candidate `3cefe685` đã dispatch full `33974253095`, iOS
  `33974254655`, Q003C `33974255889`, isolated `33974159142` đúng SHA.
  Source chưa release: root review thấy Measure hash precomputed output trước
  batch, chưa recapture kết quả thực sau batch. C được yêu cầu bổ sung before/
  after postcondition ngoài measured window + negative drift regression, giữ
  v3 counts/thresholds/datasets và archive các run cũ; new final SHA cần gate lại.
- Exact-final perf run `33974159142` tại `3cefe685`: C báo INCONCLUSIVE 4/11,
  native 2/2 PASS, 0 skip. Giữ artifact `9971867019`, không dùng implementation
  `726ace80` green thay final. Root không retest commit còn correctness gap;
  sau guard fix chạy full matrix ở new HEAD, nếu còn noise chỉ cho một bounded
  full exact-HEAD retest, không đổi policy/counts hoặc rerun-until-green. Nếu
  vẫn noisy giữ P1/P3 OPEN và yêu cầu controlled-runner decision.

## Checkpoint vận hành

- 06/09: R3 Windows source `4534e231` đã PASS run `33979253272`, artifact
  `9973277152` root verify. A/B chưa integrated, MAUI/combined R3 vẫn OPEN.
  A release desktop sau local native capture AccessDenied; final producer
  chuyển CI, không đổi quyền/né lỗi. B checkpoint `ee675078` đang exact gates.
- A/B có thể tự dispatch existing workflows trên nhánh mình bằng configured
  Git authentication, giữ secret trong memory, kiểm remote SHA và duplicate
  runs; báo IDs/exact-head evidence. Không cần chờ root cho mỗi checkpoint,
  không thay đổi workflow triggers hoặc tự tạo credential.

- C final `fe015864` đã được root xác minh bốn workflow đúng SHA xanh, review
  corrected output guards và ghép bảy commits sạch thành `4e42a584`.
  [Integration/evidence](PERF_008_INTEGRATION_20260905.md); P3 chưa chạy combined.
- A source `1c855249` đang CI; producer HCLight open-Picker cần sửa thêm. A đã
  release sample paths, nhưng tiếp tục giữ production/ci.yml và reacquire
  desktop độc quyền để kiểm chứng. B/C/root không dùng native local.
- B `35cedeaa` xanh desktop/Windows MAUI/Android, còn Apple failures. Source
  `3e9239ab` thêm layout-ready wait và stage diagnostics, cần gate đúng SHA mới.
- Root thêm RELEASE-009 package consumer qua workflow riêng; CI pending,
  không đụng ci.yml A đang giữ. Local chỉ plan/parser/architecture/metadata.
- Dung lượng C khoảng 130 MB; dừng local heavy builds, không cleanup workaround.
  Heartbeat trong task root kiểm tra mỗi 15 phút, không tạo task trùng/đổi model,
  im lặng khi chưa có thay đổi đáng chú ý. Các capacity interruptions được
  tiếp tục trên task/model cũ, không thay worktree hoặc ghi đè source.

- Coordination commit `847ff4beec70a05ab4f4f15be9e4d52e82ae7ac7` đã xanh ba
  workflows: full `33971257042`, iOS `33971257063`, Q003C `33971256987`.
  Đây là docs-only code-equivalent baseline, chưa tích hợp implementation mới.
- B producer run `33971871140` tại `acee9aba` success; actual LibreOffice 24.2.7
  theo source handoff. Corpus compatibility/privacy còn được B kiểm thử, không
  đóng T3 chỉ vì producer chạy được.
- B đã nhận actual producer artifact `9971160827` và verify hash. Calc bỏ
  calculated-column/totals/style metadata, cần ghi producer difference thật.
  Empty AutoFilter full Table range gồm totals row đang bị importer reject;
  B làm narrow normalization có regression/negative tests, không nới predicate/
  sort/opaque-content validation hoặc dựng metadata để giả parity.
- B source checkpoint `cf680688741addd7675ae1a4320c07125df0a5e4` đã push:
  báo Core 1522/1522, desktop builds 0/0; source chưa release, B tiếp tục
  negative/lifecycle/native runtime hooks. Root đã dispatch full/iOS/Q003C
  bằng configured Git authentication và REST, không cài gh/đổi triggers;
  checkpoint CI không thay final source hoặc combined acceptance.
- SDK version trong global.json là 10.0.302 với `rollForward: latestFeature`.
  Không suy ra actual SDK của existing CI từ requested version; các lane ghi
  actual version trong logs. Controlled performance dùng exact isolated SDK.
- Root đã audit sơ bộ command/demo: 49 session commands, 35/35 headless
  catalog/Table/Ribbon tests PASS; R1/R2 vẫn OPEN. Preview Open hiện mở một grid
  window không có full shell và worksheet selector là ComboBox, cần xử lý sau
  A release sample ownership. Xem [release audit](RELEASE-009_COMMAND_AUDIT.md).
- R3 audit: artifact hiện chỉ có 18 core packages, chưa có WPF/WinForms/MAUI/
  Direct2D nupkg. Packable metadata hoặc source project build không thay
  isolated PackageReference consumer/loaded host proof; R3 vẫn OPEN.

## Single writer và tích hợp

- A final24d130c6 đã release toàn bộ production/tests/docs/ci.yml; root nhận
  bảy commits sạch đến79fb8aa9 và verify mọi A-owned path byte-equivalent.
  Exact source full33983346342/iOS33983348485/Q003C33983350512 xanh7jobs.
  Root review final images và delta resource/keyboard/customization, không
  thay bằng CI parent. A không còn writer/desktop lease; root giữ ci.yml.
- B đã release thêm riêng WinForms delta64c04f5f: NeraSpreadsheetControl.cs
  và Table007WinFormsEditorLifecycleSmokeTests.cs, Windows job101352853547 /
  run33983522435 PASS111/111,0skip. Root kiểm parent blob khớp, nhận đúng hai
  source/test paths bằng apply_patch, không lấy B docs. Như WPF49, không nhập
  trùng source/test64 khi nhận whole B; docs delta64 chưa tích hợp. Native B
  source còn HOLD, không coi partial releases là whole source-green.

- B đã release riêng delta hai file WPF tại
  `49e1debeaa6187c91546d23c6ac63f96d9c10c60`: NeraSpreadsheetControl.cs và
  Table007EditorLifecycleSmokeTests.cs. Exact Windows job `101350852141` /
  full run `33982772337` success, 109/109 native tests, 0 skip; root review
  source/test độc lập base và nhận bằng path-limited apply_patch, không lấy B
  docs hoặc code MAUI đang fail. Đây là ngoại lệ slice có bounded evidence,
  không công nhận toàn B source-green; root exact combined gates vẫn bắt buộc.
  Khi ghép whole B sau này không nhập trùng hai source/test hunks của 49; docs
  delta trong commit 49 chưa được nhận. B vẫn giữ các file/lane chưa release.

- Root coordinator sở hữu CURRENT, current-status, AI_COORDINATION, delivery
  plan, file wave này; existing CI workflows, solution/shared props/project files
  và `TableRibbonIntegrationTests.cs`. Ngoại lệ ci.yml đã chuyển riêng cho A ở
  trên; các lane không ghi file root sở hữu hoặc file lane khác đang giữ.
- Mỗi file chỉ một writer. Không tự cherry-pick/merge/rebase lane bên cạnh; chỉ
  commit/push nhánh của mình. Không force-push, không merge PR #1/main/develop.
- Cần file ngoài phạm vi: báo exact paths + lý do, tiếp tục phần không vướng;
  đợi coordinator ghi chuyển quyền trước khi sửa. Không coi việc đọc trạng thái
  cũ là lock hoặc dùng cùng file MD cho nhiều tác nhân đồng thời ghi.
- Mỗi lane ghi worklog riêng: base/HEAD, commits, tests, exact-head CI, gaps,
  rollback và xác nhận release files/desktop. Báo handoff cho coordinator.
- Coordinator review diff và tests, ghép từng lane đã release, chạy combined
  build/regression/architecture/pack/runtime và ba workflows đúng SHA cuối
  (kể cả commit docs). Source-green không thay combined-green.
- Đĩa local hạn chế: reuse caches, không cài workload mới hoặc tạo nhiều bản
  self-contained không cần thiết. Không xóa artifact/worktree của tác nhân khác.

## Checklist nghiệm thu cố định

Trạng thái khởi tạo là OPEN; từng mục phải có commit/test/artifact để chuyển PASS.

| ID | Bằng chứng cần đạt | Owner | Trạng thái |
| --- | --- | --- | --- |
| U1 | Keyboard-only tab/group/QAT/menu/popup; shortcut không chạy hai lần; focus restore, Esc/Enter đúng phạm vi | A | OPEN |
| U2 | Native accessibility names/roles/states/focus cho Ribbon/Table/Filter; screen-reader smoke thực tế trên nền tảng hỗ trợ | A | OPEN |
| U3 | Vietnamese/English resources hoàn chỉnh trong surface được hỗ trợ, host isolation, light/dark/high-contrast và Picker mở | A | OPEN |
| U4 | MAUI customization shell thực, responsive full/narrow window, persisted add/remove/reorder và cancel/undo đúng contract | A | OPEN |
| U5 | Physical DPI/multi-monitor/touch acceptance có bằng chứng thật; phần cứng chưa có phải báo chưa nghiệm thu | A/root | OPEN |
| T1 | Structured reference assistance chạy thật trong WPF/WinForms split editor và MAUI reused overlay | B | OPEN |
| T2 | Enter commit, Alt+Enter newline, Esc cancel, full-cell layout/clipping; Table rename/stale selection/caret safety; bounded metadata-only suggestions | B | OPEN |
| T3 | LibreOffice-produced synthetic workbook, producer version/hash/privacy, repeated load/edit/save/reopen so với Excel/Nera corpus | B | OPEN |
| P1 | Reproducible baseline/candidate runner, raw latency/allocation/bounds evidence cho Ribbon/filter/table | C | OPEN |
| P2 | Resize/theme/customization/popup/dispose stress, subscriptions/memory, large filter bounded paging; không rebuild/recalc khi scroll | C | OPEN |
| P3 | Đo lại toàn bộ trên combined A+B; không hồi quy ngoài budget được chứng minh, không thay thế bằng source benchmark | root/C | OPEN |
| R1 | Audit toàn command surface: enabled commands có handler thật, disabled nêu lý do; không stub giả capability | root | OPEN |
| R2 | Win11 x64 demo tích hợp surface đã hỗ trợ, synthetic workbook/checklist, screenshots và known limitations | root | OPEN |
| R3 | NuGet pack + isolated consumer smoke trên cùng source; cung cấp artifact thử nghiệm, không tự publish feed công khai | root | OPEN |
| R4 | Architecture/privacy/regressions/native runtimes và exact-final-HEAD GitHub Actions xanh, docs/handoff đúng trạng thái | root | OPEN |

Giới hạn sản phẩm đã khai báo (Power Query/VBA/add-ins/OLAP, unsupported color/icon
sort execution, mixed opaque conditional-format preservation, `dataDxfId`
preserve-only) vẫn phải được nêu rõ trong demo/SDK; không hứa parity toàn Excel.
Các giới hạn đó không cho phép bỏ qua acceptance U1–R4 nêu trên. Nếu cần thiết bị,
quyền mới hoặc quyết định sản phẩm để đóng một gate, coordinator báo đúng blocker
và xin người dùng thay vì tự xác nhận PASS.

## Chuỗi tiếp tục sau wave

1. A/B/C thực hiện song song trong phạm vi ownership.
2. Root review và integrate từng lane ready; chạy exact-head combined gates.
3. C chạy final performance trên combined code; root đóng command/demo/consumer
   acceptance. Chỉ giao follow-up bounded mới nếu còn mục OPEN.
4. Cung cấp demo và NuGet artifacts để test, báo checklist PASS/OPEN. Chỉ báo
   hoàn thành 100% phần Table/Filter/Ribbon/UX khi không còn gate OPEN/FAIL.
