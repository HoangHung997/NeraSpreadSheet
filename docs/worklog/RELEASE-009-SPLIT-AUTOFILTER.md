# RELEASE-009 Split AutoFilter — Handoff lane A

## Branch và grant

- Branch `feature/release-009-split-autofilter` từ exact root base
  `d73cd2e2bcb7e2832e9a8b3fd071373b62cb2a0f` (root đã xác minh sáu gates).
- Grant 17 paths đọc tại root `e228ddc678f829d471382b050699e1eacb922741`, đầu
  `docs/worklog/UX_TABLE_COMPLETION_WAVE_20260905.md`. Giữ branch formula-bar
  `349cc0aa051578b5e0b2e29790ea3dd556a7acd0` và artifacts cũ nguyên trạng.
- Source đang implementation/HOLD; chưa release. PR #1 Draft, chưa merge/publish.
  Không sửa binding/Core/Editing/Viewport/geometry/editor/history/shared docs/CI.
  Không local native/heavy-build, cleanup hoặc model/task/subagent mới.

## Implementation checkpoint, chờ native CI

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
- Một bước tiếp theo: push checkpoint, chạy source CI để xử lý native/build/
  capture failures, xác minh đủ năm workflows tại SHA cuối rồi bàn giao RELEASED.
