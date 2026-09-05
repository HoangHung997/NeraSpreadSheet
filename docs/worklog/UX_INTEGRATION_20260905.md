# Tích hợp UX-006 với native Table — 05/09/2026

## Baseline, source và quyền ghi

Integration branch `feature/bootstrap-architecture-v0.1`, PR #1 Draft/open/unmerged.
Baseline A đã tích hợp **`ca69da45a04301bc63286c168f887488d681a00e`**, xanh
full #1340 / `33964496297`, iOS #161 / `33964496300`, Q003C #158 /
`33964496314`, đủ bảy job. Core 1497/1497, Windows 102/102, capture 177/128.

B task `01a070af-9093-7303-a811-821cde640467` đã final handoff và
**RELEASE ALL OWNED FILES**, gồm legacy MAUI TableHost và CI upload step.
Source branch `feature/ux-006-visual-localization`, final
**`292a48d19435e76589c1f0eaa12302e94ace5a48`**, base `2bc00eb6`; không nhập A/root.
Coordinator xác minh trực tiếp run head SHA và đủ bảy job completed/success:

| Source workflow | Run | Jobs |
| --- | --- | ---: |
| Full CI #1343 | `33965918117` | 5/5 |
| iOS #164 | `33965919539` | 1/1 |
| Q003C #161 | `33965921194` | 1/1 |

Source logs: Core 1478/1478, Commands 130/130, Windows 80/80, MAUI 44/44,
0 skip; loaded Ribbon 3 frames, TableFilter 12 frames, runtime/analytics/scale
smokes success. Source không bao gồm 27 Core tests và 25 Windows tests của A.
Không dùng các số source riêng thay cho kết quả combined.

A và B đều release desktop và source, không còn writer chạy song song.
Coordinator sở hữu integration; không khởi chạy Excel hoặc sửa workbook cá nhân.
Không tạo thêm task, mở Avalonia, repack demo hoặc publish NuGet trong wave này.

## Delta và commit map

Chỉ cherry-pick đúng bốn commits sau base của B lên `ca69da45`:

| Source | Integration | Nội dung |
| --- | --- | --- |
| `f9fc3724` | `c6837868` | Resources và presentation chrome |
| `e493fc32` | `769bbef3` | Adorner idle, native contrast, regression và raw benchmark |
| `d55e33db` | `1029104a` | Native Picker theme và ItemsSource stability |
| `292a48d1` | `0e770e52` | Source contract/worklog cuối |

Combined implementation checkpoint: **`0e770e5293c4f7e60e53fdc9ffc1ad5a36616fc7`**.
Không conflict, không có path giao nhau giữa hai source deltas. Sau ghép,
từng path A bằng source `4ae7731f`, từng path B bằng source `292a48d1`.
Không sửa lại production, workbook/history/calculation engine hoặc test assertions.

## Review hành vi và bằng chứng

- Localization dùng resources neutral tiếng Việt, instance theo host, fallback
  và callback override. Stable IDs/key tips, custom caption và profile JSON giữ
  nguyên; không đổi culture/resources toàn application hoặc dịch dữ liệu ô.
- Ribbon/Bars/Table/Filter dùng palette theo control. WPF adorner chỉ invalidate
  khi visible-hit records hoặc immutable theme đổi, tránh vòng arrange làm
  dispatcher không idle. Loaded regression bao gồm geometry/state/lifecycle,
  empty visible hits, theme, worksheet/workbook và paint/hit-test agreement.
- MAUI tránh thêm background container cho loaded Label; phục hồi base colors
  sau thay visual-state groups; checkbox có glyph tương phản. Picker native nhận
  theme lúc HandlerChanged và Light/HC switch; giữ controls/selection/query/caret/
  focus/current page/history, giữ ItemsSource khi chỉ theme đổi.
- CI chỉ thêm một upload-artifact step cho native Filter PNG; timeout 180 giây,
  runners, native frame/focus/history assertions và gate cũ không bị nới/bỏ.
- Source final Ribbon artifact `9969462676`: 226 PNG / 128 layouts; MAUI artifact
  `9969499372`: bốn PNG. Coordinator tự hash-compare toàn bộ final files với
  source d55 đã inspect: **226/226 và 4/4 giống nhau**.
- Coordinator trực tiếp xem bốn ảnh MAUI, cùng WPF dark Home 1024, HC Table Design
  nhãn dài 1024, HC Filter scale 2 và en-GB Table Design. Text/checkbox/footer
  đọc được; chevron Picker HC hiện rõ; query `O` giữ nguyên. Gallery ellipsis
  có tooltip theo contract. Không coi logical export là physical DPI acceptance.
- Benchmarks tái sử dụng fixture 9 tabs/720 commands, bốn widths; T0/T1/T2 summaries
  và T3 raw/config/fingerprints/wrapper giữ nguyên ở source worklog. Layout input/
  output snapshots bằng nhau, allocation cùng mức; **chưa kết luận latency không
  hồi quy hoặc tăng tốc**. Local WPF timeout 180 giây/185 ảnh trước fix và 226 ảnh
  trong khoảng 44–53 giây sau fix chỉ là smoke completion evidence.

File trọng tâm: [source worklog](UX-006.md),
[contract](../ux-006-visual-localization-contract.md),
`PresentationLocalization.cs`, `NeraMauiRibbonChrome.cs`,
`NeraAutoFilterPagedPopupPresenter.Host.cs`, presentation/native regressions và
loaded MAUI TableFilterSmoke. Kiểm tra lại benchmark và tests trước tích hợp.

## Kiểm chứng trên checkout kết hợp

- Full desktop solution Release build: **0 warnings / 0 errors**, SDK 10.0.302.
- Full Core regression: **1505/1505**, 14 assemblies, 0 fail/skip; Commands
  130/130, Editing 283/283, OpenXml 153/153. TRX `ux-table-combined.trx` chỉ nằm
  trong TestResults được ignore.
- MAUI headless regression: **44/44**, 0 fail/skip, SDK 10.0.201 đã có workload
  trên máy. SDK 10.0.302 local báo NETSDK1147 thiếu workload trước compile;
  không cài workload hoặc đổi global.json để né lỗi. Exact-HEAD GitHub CI vẫn
  phải build/test MAUI bằng SDK 10.0.302 của repository.
- Architecture và SDK packaging verification: pass.
- Native runtime/capture trên combined HEAD dùng GitHub CI; không tái sử dụng
  source CI làm kết quả integration. Không cần thao tác desktop local để ghép
  các source paths đã review, được giữ nguyên và có native CI gates riêng.
- Commit đồng bộ docs là descendant của checkpoint trên. Phải xanh cả ba workflow,
  đủ bảy job ở **đúng HEAD cuối gồm docs**. Final SHA/run/artifact URLs ghi trong
  handoff PR #1 sau xác minh; không tạo vòng docs commit đổi HEAD sau mỗi CI.

## Giới hạn và rollback

Chưa đóng whole UX-006 hoặc TABLE-006. Còn physical DPI/multi-monitor/touch và
screen-reader acceptance của presentation; en-GB là partial override/fallback;
Picker dropdown mở chưa có capture riêng; MAUI customization chỉ là binding để
host xây shell. Table còn MAUI/split editor assistance, LibreOffice corpus;
dataDxfId preserve-only và mixed opaque CF editing vẫn theo contract cũ.
Không tuyên bố full Excel parity hoặc giải quyết supervisory holds ngoài scope.

Rollback riêng UX bằng revert bốn integration commits theo thứ tự ngược và
commit điều phối, giữ nguyên A tại baseline `ca69da45`. Không reset/force-push,
xóa worktree hoặc migration workbook/profile.

## Bước tiếp theo duy nhất

Chốt build/Core và tài liệu, push combined HEAD, xác minh cả ba workflow/bảy job
và runtime/capture artifacts đúng HEAD cuối rồi ghi bàn giao PR #1. Không merge
PR hoặc tự mở wave tiếp theo khi combined checkpoint chưa được nghiệm thu.
