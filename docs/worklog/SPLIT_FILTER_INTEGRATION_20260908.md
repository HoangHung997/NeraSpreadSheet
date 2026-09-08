# Tích hợp split AutoFilter và kiểm chứng Picker — 08/09/2026

## Phạm vi và trạng thái

Branch `feature/bootstrap-architecture-v0.1`, PR #1 Draft/open/unmerged.
Implementation cuối `e97cdb3d69c7de5d10d58f5793fa5416c8832b36`; commit chứa
tài liệu này sẽ là HEAD dùng cho sáu gate kết hợp. Chưa tuyên bố combined PASS.
Không sửa dữ liệu người dùng, merge main/develop hoặc publish NuGet công khai.

Nhận 18 file của A từ base `d73cd2e2bcb7e2832e9a8b3fd071373b62cb2a0f` tới
`8c9bdef9a1355646955a6785a5a2a7dd66e506c3`, trên root206 đã sửa shared capture.
Cherry-pick không conflict; root đối chiếu mọi blob của 18 file nguồn và thấy
khớp hoàn toàn. Không nhận whole B hoặc source C0a đang đỏ Mac.

## Mapping commit

| Source A | Root |
| --- | --- |
| ee8fbea5 | f6ae7027 |
| 609a54ae | 00a6035e |
| 6a728b8d | dbeadac2 |
| 6e8de020 | 14855175 |
| a2d80b34 | d0e43903 |
| 88c39e7b | dec08e8d |
| 835eb855 | 6a262bf6 |
| 80d98b07 | 5747af62 |
| 6e4e218f | cfe664a5 |
| 8c9bdef9 | e97cdb3d |

## Behavior nhận vào SDK/sample

- Một paged presenter/binding/session hiện có phục vụ Table và worksheet
  AutoFilter trong standalone và từng split pane; không thêm model/filter engine.
- Geometry lấy từ native frame đã trình bày, clip loại scrollbars. Raw pointer
  không compose, scan nguồn hoặc recalculate; refresh được coalesce.
- Pointer mở ở matching mouse-up sau release capture; lost capture/host generation
  hủy gesture. Active draft từ chối open, không implicit commit/cancel/history.
- Popup context giữ session/sheet/surface/pane/header identity; callback cũ không
  giành focus hoặc mutate popup mới. Scroll/resize relocate hoặc đóng khi offscreen.
- Footer và paging có chiều cao riêng; value list lấp phần còn lại của max540px.
  Page100, retained catalog caps, canonical Apply/Clear/Sort và Undo/Redo giữ nguyên.
- PERF008 chỉ thêm FreezeTopRows(1) trong WPF fixture để giữ header của stress
  scenario visible; 12 cycles/offset106/assertions và WinForms không thay đổi.

## Source A review và ngoại lệ shared gate

Root đọc production/tests/contracts và delta cuối mouse-up/footer/lifecycle;
không có finding hoặc scope violation chưa xử lý. Actual source Windows job
`101931775113`, run `34185113478`: build0 warnings/errors, 1515 nonnative và
174 native PASS/0skip. Full run FAIL riêng MAUI Windows `101931774955` old
shared Picker capture; không phải nguồn A all-five-green, không che failure.
Source iOS `34185115408`, Q `34185116903`, Windows packages `34185118300`,
demo `34185119908` đều SUCCESS. Root không yêu cầu rerun nguồn chưa chứa fix.

Artifact `10040312535` chứa270PNG/28ảnh mới; ZIP SHA256
`f1a49cf7893baf9303471c215f6adb369963bc3d774675f0e340bbde76d41737`.
Root trực tiếp xem14shell +2unique popup và kiểm14popup hashes (7 mỗi owner):
Table `9ed5871e2020bf78adc4e618f88e668ebdd03a589adecc075d03c0662428eb5e`,
worksheet `9183add798b8b6a747e50028e94a7b23420395155c52f5a1fa2460388e019cbe`.
Manifest SHA256 `20cfb3ff38142061b8fe28f0781414335f4dc7ba3ca5fdc9c4fe718671cc3fd7`;
14cases B3/page100/source250/history0. Footer/paging hiện đủ ở full/narrow.
Offscreen shell/popup captures riêng không chứng minh physical anchor trên màn
hình; visible native OS-input tests kiểm target/pane/offset riêng.

## Root Picker prerequisite đã được kiểm độc lập

Exact `2065445609352ebcbb4eafffb30c3f05a5493dd5`: full `34186850320`,
iOS `34186850295`, Q `34186850293`, Windows packages `34186847117`,
MAUI canonical `34186847115` đều SUCCESS. Demo chưa dispatch tại diagnostic
checkpoint này; không gọi206 all-six-green.

Windows job `101936814137` có46MAUI tests PASS và Ribbon native success,
ba frames, structural customization/full/narrow/nine captures. Root đọc actual
v2 references, download/checksum/view toàn bộ artifact `10040906335`, ZIP SHA256
`2633dcc44e2f89d995016321f1fcfb798bcd2b790c603308c3187fbe1a06a16c`.
Native Picker đúng popup và bốn palette, caption/pixel assertions giữ nguyên.
Hai root-owned test blobs giữ nguyên sau A integration:
NativePopupCapture `85babe2fefaefe2651fae764b13443cf8789cca8`,
SmokePage.Customization `dc3832b05b84bbba593058b0488b56b03f66553d`.
Đây là test-only correction, không sửa SDK UI/renderer/launcher để né test.

## Kiểm chứng kết hợp và giới hạn

Local nhẹ: architecture/packaging verification, 36transport tests/0skip,
resource parity và git diff check PASS; 18/18 source blobs khớp. Không dùng
local checks thay build, desktop runtime/captures hoặc exact-head Actions.
Sáu gate sẽ gồm full, iOS, Q003C, Windows SDK consumer, canonical MAUI packages,
published self-contained Win11 demo. Không dispatch demo trùng automatic run.

Whole B/editor corpus chưa nhận: source87 Windows diagnostic có4records hợp lệ,
formula60/history1 PASS, nhưng không chứng minh đã sửa intermittent cause;
Mac paired `34186605968` vẫn FAIL trước queued callback. C0a canonical
`34186366468` 10/11jobs SUCCESS, Mac `101935885806` đã quan sát kernel EXIT
và numeric PID absence trước cleanup, chưa biết cause hoặc clean exit.
C chỉ có một read-only exit-status proposal, chưa được code/probe.
Historical CLR abort của A không tái hiện ở174PASS, nguyên nhân vẫn UNKNOWN.
Final combined P3/hardware, actual screen reader/multi-monitor DPI/touch và
100% frozen scope vẫn OPEN. Không tuyên bố Excel parity hoặc toàn SDK hoàn thiện.

Rollback: revert riêng 10 mapped A commits theo thứ tự ngược và tài liệu liên
quan, giữ root206 capture/parser và source lanes khác. Không migration.

Một bước tiếp theo: xác minh đủ sáu workflows tại commit chứa tài liệu này;
nếu đỏ, đọc actual failing job và giữ PR Draft, không retry mù hoặc bỏ gate.
