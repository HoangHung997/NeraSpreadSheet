# RELEASE-009 — Tích hợp canonical MAUI package

## Nhận native iOS consumer — 08/09

Final source `5d70be936db42dc016fe9fde051453f784727266`, branch
`feature/release-009-maui-ios-consumer`, remote bằng frozen local, tracked clean.
Root nhận tuần tự trên baseline `30e74bef`:

| Source C | Commit root |
| --- | --- |
| 6864459e | 389c883d |
| f5c9cce4 | 7bcc4c5a |
| 5d70be93 | aabd359f |

SKIP import-only `9fe954b1`, `332a79a8`, `e17242d0`. Bốn shared launchers/
parser/tests giữ exact blobs của root30e. Tám owned paths: workflow MAUI,
consumer wrapper, PackageProvenance.cs, hai emission-fixture files và ba docs
ADR/consumer contract/private worklog. Manifest source SHA256 đã xác minh:
`246f1df59ebe3619a5987be047308ea4b7fece584274f4545a1d246f63feaecc`.
Schema của manifest này là records `path/blob/sha256/bytes`, paths và JSON keys
được sort, separators compact; khác schema `blob/mode/path` của checkpoint8b.
Lần kiểm đầu root dùng nhầm schema đã dừng trước cherry-pick; tính lại đúng
schema xác nhận hash khớp. Không có source mismatch hoặc thay evidence.

Năm non-reconciled final blobs trùng source. Ba khác biệt có chủ đích: workflow
giữ thêm `feature/bootstrap-architecture-v0.1` (xung đột đúng một dòng trigger),
ADR giữ trạng thái Accepted và contract giữ lịch sử root. Sau nhận, root cập
nhật acceptance/docs chung; không sửa emitter, fixture hoặc SDK production.

Root independently xác minh sáu exact-source workflows SUCCESS:
MAUI `33992029278` (11/11 jobs), full `33992098584`, legacy iOS `33992100189`,
Q `33992101588`, Windows packages `33992102661`, demo `33992103684`.
Log iOS `101376255364`: build 0 warnings/errors, actual compact/full-file
transport 8 frames rồi own cohort/public postconditions PASS. Log Android
`101376255301`: build 0/0, 10 frames và own validator PASS. Fixture job
`101375853395`: compiled linked actual Emit + actual shared Python CLI
roundtrip, 23 package và 33 transport tests, architecture/packaging đều PASS.
Windows/Mac consumer chỉ build và app-hash PASS; không native runtime proof.

Artifact iOS `9977059422` và Android `9977014815`; source báo ZIP SHA256 lần
lượt `ddf640fd050dc7a1cd5f5cd63b08a87c5eac80f576961640c6f940514d9d724c` và
`900d91d9e93958b40e670e76b9c95be9fb6174cd6a59b26f96068078ed7da1fd`.
Root xác minh source/log, chưa tải lại hai archive này. Không upload private
context hoặc raw app logs; source686 malformed987/offset976 vẫn là failure
lịch sử, stream cụ thể UNKNOWN, không suy thành console truncation.

Local combined 33 transport + 23 package fixtures PASS/0 skip, architecture,
packaging, iOS/Android PlanOnly và Bash syntax PASS. Không local heavy/native
build. Combined HEAD mới phải chạy lại đủ sáu gates; source green không thay
acceptance. C chỉ READ-ONLY chuẩn bị Windows/Mac theo ownership wave. Rollback
riêng ba received commits và acceptance docs, không revert legacy transport,
SDK, workbook migration hoặc publish feed.

## Nguồn và phạm vi

Source C `8b7781ca44b4f9f3647c5434d02970be873d9624` đã release 28 paths:
workflow mới, ADR/contract/worklog riêng, `eng/release-009-maui`, ba package
scripts và public consumer `tests/NeraSpreadSheet.Packaged.Maui.Smoke`.
Không đổi SDK production hoặc nhận whole B. JSON manifest ordered paths, mỗi
record có sorted keys blob/mode/path, SHA256:
`083ca567a1fe2849784dd60954425f9733d3e24f796ea89fdb7b617aff99437b`.

| Source C | Commit root |
| --- | --- |
| feb12d5f | e3c0e1c7 |
| 4a7cf6fb | 13446cae |
| e64367ad | bab383b8 |
| 4415b890 | d603b9cd |
| 823e913e | 69e589d6 |
| f8007838 | 40fd4773 |
| bcebe527 | efa365fc |
| 1bbe334a | 1ef5e84a |
| 8b7781ca | f62ea0dd |

Root kiểm manifest trước khi nhận, toàn bộ 28 final Git blobs bằng source sau
cherry-pick. Hai tên AppDelegate cũ chỉ tồn tại tạm trong lịch sử rename sang
PlatformApplication; không còn trong final source. SKIP import-only
`dfd31be98dc64150bd34fcb13c1aa336058eb4b5` hoàn toàn. Bốn shared Android/iOS
launcher/parser/test blobs của root `f344b5ec` giữ nguyên sau tích hợp.
Root chỉ thêm integration branch vào push trigger workflow mới, ghi status;
C giữ writer cho next iOS consumer trong worktree riêng.

## Kiểm chứng

Exact C source sáu workflow SUCCESS: full `33989004197`, iOS `33989005769`,
Q003C `33989007405`, Windows packages `33989009301`, demo `33989010831`,
MAUI matrix `33988945365` (11 jobs). Root independently kiểm REST full/matrix;
Android job `101368019613` thật đạt 10 frames, required SDK source/version,
nonce/feedHash, controller edit/Undo/Cancel, 20 native filter values, resize và
idle GPU cùng pre-install app inventory. Không phải native editor acceptance.
Feed artifact `9976061844`, source-reported ZIP SHA256
`aad51d463f79bc3e16338364526750b23deae25a9e6c94e817517eca4157b285`;
root review source/guards, chưa tải lại archive này.

Root review producer closure/cohort, NuGet metadata merge, fresh consumer cache,
source mapping, exact PackageReference/TFM và app inventory; actual symlink
identity/cycle fixtures được đọc. Local combined: 23 package fixtures và 20
shared parser fixtures PASS, không skip; ba PlanOnly, architecture và packaging
metadata verification PASS. Không heavy local build/native do thiếu dung lượng.
Build, native runtime và published demo ở combined HEAD phải chạy lại trên CI.

Root `f344b5ec8060a127e3ce030a717013ce4f2bb637` trước nhận C đã SUCCESS full
`33988991344`, iOS `33988991332`, Q `33988991326`, Windows packages
`33988988456`. Không dispatch demo riêng f344; published demo sẽ chạy trên HEAD
kết hợp mới, không lấy demo7a thay bằng chứng mới.

## OPEN và rollback

C đang nối iOS public package consumer trên branch riêng bằng immutable f344
transport; Windows/Mac package runtime còn chờ shared launchers của B.
Whole B/editor/corpus, final combined P3, hardware và release acceptance OPEN.
Không publish feed công khai hoặc merge PR #1. Rollback bằng revert các commit
package/consumer và trigger mới; không có workbook migration hoặc SDK API đổi.

Bước tiếp theo duy nhất: chạy và xác minh sáu workflow đúng HEAD kết hợp,
giữ các task A/B/C tiếp tục theo ownership wave hiện hành.
