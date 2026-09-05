# PERF-008 — Lane C harness

## Current — output-guard correctness correction, giữ timing v3

Root review chặn release `3cefe685`: `Measure` trước đây hash object đã tạo trước
warmup nên có thể bỏ lọt drift sau batch. Đã thay actual result factories trước
warmup/sau batch, kiểm/hash ngoài elapsed/allocation/GC windows. Ribbon/completion/
cache giữ last result của operation thật; Table và search đọc live fixture.
Filter-open giữ last initialized measured view đến khi kiểm hash, mỗi iteration
dispose view trước rồi mở view tiếp; không tạo replacement fixture trong capture.
Raw ghi actual `OutputHash` và `OutputBeforeHash`. Hai deterministic negative
self-tests gây drift sau initial sample và sau warmup phải reject; analyzer
kiểm guard marker và pre/post hashes. Dataset/counts/thresholds/bootstrap/order
v3, native test source và production giữ nguyên.

Local corrected benchmark .302 build **0 warnings / 0 errors**, worker verify
pass hai negative guards và **11/11 actual pre/post hashes bằng nhau**;
P2 correctness pass. Statistical gate self-test pass cả post-hash mismatch.
Commit chứa correction/archive/docs này là HEAD lane mới; exact SHA và bốn
workflow final được gửi root sau push. **Không release hoặc dùng source green
lịch sử thay final CI.** Root giữ shared CURRENT/status/plan/wave.

Run [33974159142](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/33974159142)
tại `3cefe685f2bb278c8a0b1375d003ef175d2ecd52`: **INCONCLUSIVE 4/11**, gồm
toggle/Undo 0 unrelated, completion 0/100.000 unrelated và filter search cycle;
không có classification REGRESSION. Native **2/2**, 0 skip, 24 giây; build0/0,
architecture pass. Artifact [9971867019](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/33974159142/artifacts/9971867019),
8.921.942 bytes, SHA-256 đã verify
`944e3f6b199c58aa2ecb9ac88e10fb3cd65a304bd6c976d66e65ba740463ef52`.
[Raw riêng](../../benchmarks/PERF008/results/33974159142.json) giữ
36 workers/396 measurements cùng stress. Không dùng green `726ace80` thay run
này. Full/iOS/Q003C historical runs ở `3cefe685` lần lượt `33974253095` /
`33974254655` / `33974255889`, không thay corrected gates.

Root không cho rerun `3cefe685` để chọn PASS; phải sửa guard và chạy full v3
A/A→budget freeze→AB/BA→native trên HEAD mới. Nếu HEAD mới INCONCLUSIVE khi
correctness đúng, tối đa một full retest, giữ nguyên code/counts/policy và raw
attempts riêng; nếu vẫn noisy, P1/P3 acceptance OPEN, cần controlled runner.
Không nới gate hoặc lặp đến khi xanh. **Whole PERF-008 chưa DONE.**

Files/desktop: không đổi production/shared/other-lane files; desktop local chưa
acquire và luôn release. Lane giữ owned files đến corrected final CI/handoff;
rollback revert correction rồi chuỗi lịch sử ngược, không data migration.
Bước tiếp theo duy nhất: nghiệm thu corrected exact-final-HEAD cùng bốn workflows
và raw, bàn giao root để giao combined A+B SHA cho P3.

## Lịch sử checkpoint P1/P2 — trước output-guard correction; P3 OPEN

- Branch `feature/perf-008-harness`, base
  `2e8482c25a44797a479b276ae26f472811a0a81e`; không nhập lane khác.
- Implementation final **`726ace806896ca1f3e5f7db85d9a5a1cb8deb062`**.
  Isolated run [33973552493](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/33973552493)
  **success**, attempt 1: benchmark builds **0 warnings / 0 errors**, CPU
  **11/11 PASS**, headless stress pass, native **2/2**, 0 skip, 26 giây,
  architecture pass. Input/output/operation fingerprints bằng nhau.
- Actual SDK **10.0.302**, runtime **10.0.11**; Release/tiered off/workstation GC,
  Windows Server 2025 image `20260824.214.3`, AMD EPYC 7763, 4 logical processors.
- Frozen v3 budget SHA-256:
  `C52B42A523E1A02282D583C6BC70ADBAF823FA349F791E8F003B6A4855B2301C`.
  Tolerance variance-derived 5–47,28%, giữ nguyên rule từ trước candidate;
  observed upper 95% ratio từng workload ≤1,0429. Không gọi threshold là 5% chung.
  [Bảng số thật](../performance-budget.md), [hợp đồng](../perf-008-acceptance-contract.md).
- Artifact [9971704564](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/33973552493/artifacts/9971704564),
  **8.922.034 bytes**, đã tải/verify digest
  `37119a7dcb1ab619a03184b1c28daef2c42ee8fa48eb8f6adb9f03b87142ae4a`.
  [Raw v3](../../benchmarks/PERF008/results/33973552493.json) giữ 36 workers /
  396 workload measurements cùng manifest/budget/comparison/native/headless data.
- V2 `97f20261` / run `33973443725` **success**, artifact `9971644470`, digest
  `58e3fa38515657720ba481025da5bf3fe68b242615a0bceba392117085dbe384` đã verify;
  [raw v2](../../benchmarks/PERF008/results/33973443725.json) giữ riêng. Root
  không cancel vì phase đo đã xong; không dùng kết quả v2 để chọn counts v3.
  V1 vẫn INCONCLUSIVE như hồ sơ dưới, không bỏ hoặc ghép samples.
- Local .302: benchmark build **0/0**, paging regression **8/8**, statistical
  gate self-test pass (noise/regression/fingerprint/runtime/tamper), architecture
  và diff check pass. Worker local chỉ correctness, không lấy timing nghiệm thu.
- P2 native WPF managed bytes cycles 3→12 **6.278→6.594 MB**, WinForms
  **6.292→6.404 MB**; raw có private bytes/working set/handles. Direct
  runtime/grid/Table binding subscriptions trở baseline; không chứng nhận toàn
  framework/GPU không leak. Native desktop local chưa acquire và luôn release.
- Commit tài liệu/raw cuối là descendant của implementation; exact SHA lấy từ
  `git rev-parse HEAD` sau commit và được gửi root cùng final run URLs. Cần
  isolated perf + **full/iOS/Q003C success tại chính HEAD gồm docs**, không dùng
  implementation run này thay gate final. Root giữ CURRENT/status/plan/wave.

Commit map theo thứ tự, rollback bằng revert ngược:

| Commit | Nội dung |
| --- | --- |
| `413ab07a` | Harness/workflow/native stress và predeclared v1 policy |
| `a08eeffb` | SDK isolation, native compile/analyzer/probes, output evidence paths |
| `2c8c4e2c` | Runner-context setup correction; v1 complete run |
| `97f20261` | Protocol v2, environment guards, archive v1 INCONCLUSIVE |
| `726ace80` | Counts v3 được coordinator chốt trước candidate; implementation final |
| Commit chứa mục handoff này | Archive v2/v3, actual performance budget và documentation final |

Phạm vi nghiệm thu checkpoint: reproducible paired CPU harness và các P2 stress
đã liệt kê. **Whole PERF-008 chưa DONE.** P3 cần combined A+B; native MAUI/
physical input-to-display, DPI/touch/4K/60–120 Hz và memory dài hạn còn thiếu.
Cache chỉ bounded theo source cap/requested pages, chưa có eviction constant cap.
Không đổi production, shared props/project refs/workflow hiện có hoặc PR #1.
PR #1 giữ Draft/open/unmerged; không publish demo/NuGet. Khi final CI xanh, lane
release các file đã liệt kê cho root; không giữ desktop lease.

Bước tiếp theo duy nhất sau final CI/handoff: coordinator giao exact combined
UX-007 + TABLE-007 SHA để dispatch `perf-008.yml` với baseline SHA ở trên và
`candidate_sha` là combined SHA, rồi xử lý P3 dưới ownership được chuyển riêng.

## Checkpoint thiết kế trước chạy dài

- Branch `feature/perf-008-harness`; base sạch đã xác minh
  `2e8482c25a44797a479b276ae26f472811a0a81e`.
- Base CI được giao: full `33966917191`, iOS `33966917101`, Q003C `33966917091`.
- PR #1 Draft/open/unmerged; lane không đổi PR head hoặc main/develop.
- Wave authority read-only ở coordinator; docs commit `847ff4be` không nhập vào
  lane. Root nhận và chấp thuận design/Program.cs/new native test ownership trước
  khi chạy dài. Không tạo task/subagent, không cài workload hoặc package.
- P1/P2 implementation ở commit chứa checkpoint này; lấy exact SHA bằng
  `git rev-parse HEAD`. Final CI/runs được ghi trong checkpoint nghiệm thu tiếp.

## Đã thực hiện

Harness reuse hai benchmark fixture đã tồn tại, thêm 11 workload CPU measurements,
baseline-only A/A calibration, budget bất biến và AB/BA paired measurements.
Input/output JSON fingerprints, raw ticks/allocation/GC, SHA/runner/SDK/config,
timestamp và hash evidence được lưu trong artifact. Workflow Windows riêng giữ
raw kể cả failure/inconclusive, build tuần tự và native stress sau phép đo.

Headless stress dùng thật production paged view, source session và viewport;
native test mới kiểm tra WPF/WinForms resize/theme/customization/popup/scroll/
dispose, direct subscriptions và memory observations. Không sửa production,
existing workflow, project refs hoặc test lane A/B.

## Validation hiện tại

- Benchmark project Release SDK **10.0.302**: build **0 warnings / 0 errors**.
- Architecture verification, `git diff --check`: pass.
- Python statistical self-test: pass; kiểm tra stable pass, 20% slowdown fail,
  baseline noise inconclusive, mismatch fingerprint và calibration tampering fail.
- Một worker verify local chỉ kiểm correctness: 40 disposed views sống **0**,
  20 pages/2.000 items, search cache ≤100; 200 fractional compositions giữ
  snapshot/Ribbon/filter refresh deltas **0**, formula sentinel không đổi.
  Không dùng local timing làm acceptance vì A/B đang làm việc đồng thời.
- Native tests chỉ source tại checkpoint này, chưa compile/run trên CI.
- Exact-final-HEAD full/iOS/Q003C và isolated perf workflow còn PENDING.

## Files trọng tâm

### Lượt đo đầy đủ v1 — INCONCLUSIVE, không bỏ raw

Run [33972169896](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/33972169896)
tại `2c8c4e2c221d1a1502badbdba3ec55c260e315f9`, baseline `2e8482c2`, harness
và candidate cùng SHA source; production của hai bên không đổi. SDK **10.0.302**,
runtime **10.0.11**, Windows Server 2025 image `20260824.214.3`, AMD EPYC 7763,
4 logical processors. Hai build benchmark 0/0; native **2/2 passed**, 0 skip,
25 giây, architecture pass. Mỗi host có 12 lifecycle cycles; scroll đến 106 px,
Ribbon/filter/formula tripwires pass; dispose trả direct delegate counts về
baseline, gồm TableDesign binding. Native managed bytes ở cycles 3→12:
WPF **6.277→6.590 MB**, WinForms **6.290→6.403 MB** (đơn vị decimal), không coi
đây là chứng nhận process/GPU không leak. Headless 40 view weak references đều
được giải phóng, 200 compositions giữ các refresh deltas bằng 0 ở cả hai SHA.

Paired **10/11 workload PASS**; `table.completion.100000` INCONCLUSIVE do baseline
noise >10%, dù paired medians chỉ **0.654/0.657 µs/op**. Batch 256 calls chỉ
khoảng 0,17 ms nên chưa đủ ổn định. Không coi đây là regression production hoặc
tuyên bố overall latency PASS. Frozen budget SHA-256:
`289BBA16F05ECE1AC29A682374A2520D16E1A61E817841D89EC1D0F5285AD385`.

Artifact [9971302162](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/33972169896/artifacts/9971302162),
8.918.232 bytes, SHA-256
`60a74af8aa0f6411b83b2c4aade761f85046cefbb5bbf35ee56dbf6dfd4e2fcc`, đã tải và
verify digest. Bản raw bền trong repo:
[`benchmarks/PERF008/results/33972169896.json`](../../benchmarks/PERF008/results/33972169896.json),
giữ **36 workers / 396 workload measurements**, metadata, budget, comparison và
native/headless stress. Full input/output JSON snapshots và TRX nằm ở artifact,
retention 30 ngày. Không trộn samples với run preflight fail hoặc phương pháp mới.

Coordinator đã chấp thuận correction **v2**: chỉ completion và cached-page tăng
thành **32.768 operations / 1.024 warmup**. Toggle, Ribbon, filter-open/search,
datasets và AB/BA ordering giữ nguyên. Tất cả ratio/bootstrap/allocation/noise
thresholds giữ nguyên; commit revision trước baseline A/A mới, khóa budget mới
rồi chạy lại toàn ma trận. Bản v1 vẫn INCONCLUSIVE, không trộn hoặc chọn samples.
Review bổ sung guard environment/runtime/config giữa workers và hai phases,
test phát hiện runtime mismatch và bảo toàn raw kể cả artifact hết retention.

Coordinator supersede approval v2 bằng counts mục tiêu batch khoảng 20 ms trở
lên: **toggle 4096/128, completion 32768/1024, cached-page 262144/4096**. Message
đến sau push v2 `97f20261a6ee50a0246e03b78a60b3c3dd30aaff`; run `33973443725`
vừa bắt đầu. C nhận quyết định và freeze **v3** trước khi xem candidate v2;
đã yêu cầu root cancel run superseded nếu chưa đo để tiết kiệm. Nếu có samples,
giữ riêng run v2, không ghép hoặc dùng nó để chọn counts/thresholds v3.

Run đầu [33971846930](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/33971846930)
tại `413ab07a0feec7bc258f39b825ceba97e497f458` dừng trước calibration: runner
setup theo global.json `latestFeature` chọn SDK **10.0.400**; canonical guard từ
chối đúng. Chưa sinh baseline/candidate samples. Native test mới cũng dừng compile
ở thiếu `System.IO` và CA1861 arrays; không có runtime evidence tại run này.
Harness fix pin install **10.0.302** vào runtime directory riêng của runner,
giữ global.json nguyên trạng; sửa test/analyzer và OutputRoot theo PowerShell
working directory, giữ manifest kể cả preflight failure. Không đổi threshold.

Run `33972065761` tại `a08eeffb` bị workflow validation từ chối trước khi tạo
job vì `runner.temp` nằm ở job-level env. Đưa install-dir env xuống setup step
(context runner hợp lệ); không có samples hoặc runtime evidence ở run này.

- `benchmarks/NeraSpreadSheet.Benchmarks/PERF008Harness.cs`, `PERF008Stress.cs`,
  `Program.cs`; không sửa workbook/editor/calculation model.
- `scripts/run-perf-008.ps1`, `scripts/run-perf-008-analysis.py`.
- `.github/workflows/perf-008.yml`.
- `tests/NeraSpreadSheet.Windows.Rendering.Tests/PERF008NativeStressTests.cs`.
- [Acceptance contract](../perf-008-acceptance-contract.md).

## Giới hạn và handoff

P1/P2 chưa nghiệm thu trước raw isolated run. Cache không có eviction; chỉ có
source cap và current-page UI bound, không gọi constant bounded cache. Native
memory chỉ là observations với direct delegate subscription gate, không phải
chứng nhận toàn process/GPU không leak. Physical/native MAUI performance và P3
combined A+B còn OPEN. Không tuyên bố whole PERF-008 DONE.

Desktop local không acquire, luôn release. File ownership lane còn active đến
khi gửi handoff final cho root; shared CURRENT/status/plan/wave luôn do root ghi.
Rollback: revert commits C mới nhất trước; không đổi data/profile/production.

Bước tiếp theo duy nhất: push checkpoint để chạy workflow isolated, inspect toàn
bộ raw/native outputs, sửa harness nếu cần rồi gửi exact-final-HEAD handoff/runs
cho coordinator trước P3.
