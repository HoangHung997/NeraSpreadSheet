# RELEASE-009 Split AutoFilter — Handoff lane A

## Branch và grant

- Root đã APPROVE amended scope tổng18paths: chỉ thêm
  `session.View.FreezeTopRows(1)` ngay sau CreateSession trong WPF PERF008 test.
  Tất cả assertions/cycles/offsets và WinForms giữ nguyên. Unfrozen-header close
  được kiểm riêng trong owned regression; CLR abort vẫn là vấn đề chưa rõ nguyên nhân.

- Branch `feature/release-009-split-autofilter` từ exact root base
  `d73cd2e2bcb7e2832e9a8b3fd071373b62cb2a0f` (root đã xác minh sáu gates).
- Grant 17 paths đọc tại root `e228ddc678f829d471382b050699e1eacb922741`, đầu
  `docs/worklog/UX_TABLE_COMPLETION_WAVE_20260905.md`. Giữ branch formula-bar
  `349cc0aa051578b5e0b2e29790ea3dd556a7acd0` và artifacts cũ nguyên trạng.
- Source đang implementation/HOLD; chưa release. PR #1 Draft, chưa merge/publish.
  Không sửa binding/Core/Editing/Viewport/geometry/editor/history/shared docs/CI.
  Không local native/heavy-build, cleanup hoặc model/task/subagent mới.

## Implementation checkpoint, chờ native CI

- `80d98b0754fc8116879eb9a38a9956309fdc7212`, full `34184469464`, Windows
  `101929924108`: native step PASS và capture tạo artifact `10040111964`,
  270 PNG/28 mới, ZIP SHA256 `6e34a9c7df00a487a346c2adb1de25c26916efde7db888f0bf58c7d2bc342b23`.
  Visual QA 14 shell + popup Table/worksheet phát hiện footer Xóa lọc/Hủy/Áp dụng
  bị cắt khỏi popup540px. Chưa release source này. Follow-up reserve footer và
  paging trước last-fill list trong existing DockPanel; loaded regressions và
  capture guard kiểm đủ5 action bounds, native hit targets. Page100/caps/binding
  không đổi; cần chạy lại native/28ảnh tại source cuối.

- `835eb855dfeed8dd6f37652ed5ba1ebd81e3f650`, full `34184114570`, Windows
  `101928908214`: 172 PASS/2 FAIL/0 skip trong đủ174 native. First OS click,
  lost-capture, AltDown/command ở từng pane và toàn bộ geometry/zoom đều qua;
  CLR abort không tái hiện. Hai failures ở chuyển split sheet sang empty Other:
  fixture chọn old split trước khi sample thực thi host transition ở ContextIdle,
  rồi chờ standalone layout chưa được pump. Follow-up drain ContextIdle trước
  khi chọn actual surface để render/wait. Production shell không đổi.

- `88c39e7bcdd2379cad4cb06f3edcd6c3d6b5c370`, full `34183597175`, Windows
  `101927431766`: 166 PASS/8 FAIL/0 skip trong đủ174 native; CLR abort không
  tái hiện. Log chứng minh hit đúng surface, popup mở trước mouse-up rồi đóng
  ngay sau mouse-up. Follow-up mở ở matching mouse-up sau khi nhả capture;
  hủy pending gesture khi mất capture/host/generation. Regression vẫn dùng OS
  down/up và thêm lost-capture cancellation. WPF Popup xử lý cả outside up:
  https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Primitives/Popup.cs
  Standalone fixture chờ layout vẫn timeout sau resize/zoom và disable split;
  follow-up invalidate actual native visual trước layout pump, không compose
  geometry thay thế. Chưa chứng minh final native/capture PASS.

- `a2d80b34c841fb09fc601e5d86be775d20eb158a`, full `34183087697`, Windows
  `101925956030`: native chạy hết 174 cases, 168 PASS/6 FAIL/0 skip; PERF008 qua,
  CLR abort không tái hiện (chưa chứng minh nguyên nhân). Bốn first-header native
  clicks chưa giữ popup mở; hai standalone geometry cases đọc null layout ngay
  sau zoom. Follow-up chờ actual layout và quan sát mouse-down trước mouse-up;
  giữ actual OS input và assertions. Capture metadata sửa theo scrollbar-reserved
  clip/anchor ở native-surface DIPs theo review của root.

- `6e8de0203029c151a10cc011eaa19c143d765e4c` đã thêm đúng amended PERF1line.
  Full `34182884926`, Windows `101925352662` FAIL test compile CS0136 vì biến
  clipped trùng scope trong test mới. Follow-up đổi tên local và chọn lại Header
  trong fixture sau ActivateWorksheet (API reset selection về A1). Chưa native.

- `609a54aeaa51344e9f47e080a1b4c035ae038307`, full `34181447860`:
  Windows `101921208530` build 0 warning/error, Core 1.515 PASS. Native FAIL
  existing PERF008 WPF line80: fixture unfrozen A1 scroll tới106 nhưng đòi
  binding giữ mở; contract mới đóng khi header offscreen. Đã xin root amended
  grant đúng một dòng FreezeTopRows(1) trong WPF fixture, giữ toàn bộ assertions;
  chưa sửa file ngoài grant. Sau77 PASS, testhost còn CLR thread-state abort,
  nguyên nhân chưa được xác định; không gọi renderer/SDK hoặc test mới là PASS.
- Owned test follow-up bổ sung partial-header/scrollbar exclusion, bar draft
  refusal, actual unload/reattach và dispatcher drain sau Close trước shutdown.
  Chờ quyết định fixture trước dispatch tiếp, không lặp CI biết chắc còn conflict.

- Checkpoint `ee8fbea5b1f0c8c40da1190e8173668912036b53` full CI `34181243092`:
  Core SUCCESS; Windows job `101920628361` FAIL compile test vì ButtonBase trùng
  WPF/WinForms global using. Chưa chạy native/capture; follow-up thêm WPF alias.
- Follow-up cũng chặn events từ controls thuộc popup/page cũ, gồm search/Apply/
  checkbox/date drill, và test old-control race cùng exact fractional offsets.
  Không làm yếu assertion hay sửa test cũ để nhận xanh.

- Một existing paged presenter từ đầu sample; input đi qua actual split adorner.
  Internal controller/frame hooks và private UI context, không presenter/model mới.
- Geometry từ presented native frame của từng pane; shared Table/worksheet hits
  được clip ngoài scrollbars, dùng cùng snapshot cho draw/hit/anchor. Raw hit
  không compose/render/scan. Refresh gộp dispatcher, unchanged overlay không
  invalidate layout liên tục. Standalone vẫn dùng viewport helper hiện có ngoài
  raw input; chưa tuyên bố cải thiện benchmark/performance.
- Active draft refusal giữ canonical State/text/range/focus/selection/history.
  Sheet/surface/open-generation guards và current-popup check bảo vệ binding,
  queued focus và async completion. Same visible header được relocate.
- 16 loaded native cases mới, mỗi case Table/worksheet + standalone/split,
  first pointer/AltDown/command chạy đủ bốn panes; paging250/page100, mutation/
  history, hidden/fractional/resize/idle/raw hit và stale lifecycle/draft refusal.
- 14 shell capture scenarios + actual popup files, metadata pane/owner/header/
  clip/offset/page/history. Hai matching resource keys cho lý do không mở filter.
- File trọng tâm: presenter `.SplitHost.cs` và bốn partial hiện có; internal
  hooks trong SplitController/Adorner; sample main/Commands/Capture cùng mới
  SplitFilterCapture; mới Release009SplitAutoFilterSmokeTests và contract này.

## Kiểm chứng, giới hạn và rollback

- Local architecture verification và diff check đã PASS ở checkpoint đang viết;
  final source phải kiểm lại sau khi hoàn tất code. Chưa build/native/capture
  success tại commit chứa checkpoint này. Không dùng baseline/source cha thay CI.
- Native pointer ordering phải được loaded test chứng minh; nếu cần sửa thêm
  pointer/header path ngoài grant thì báo evidence và xin amended grant trước.
- P3, hardware, whole MAUI và final combined acceptance vẫn OPEN. Source caps,
  rich/date/custom/sort và existing native regressions tiếp tục được giữ.
- Rollback: revert riêng các owned commits của split-filter slice theo thứ tự
  ngược; giữ d73/formula-bar/SDK base. Không migration hoặc dependency mới.
- Không documentation-only churn sau final CI: exact-final SHA/run IDs/17-blob
  manifest/capture hashes và giới hạn sẽ gửi root trong final handoff; root ghi
  kết quả cuối ở shared docs khi tích hợp.
- Một bước tiếp theo: chạy source CI sau amended fixture, xử lý native
  abort/capture, rồi xác minh đủ năm workflows tại SHA cuối trước RELEASED.
