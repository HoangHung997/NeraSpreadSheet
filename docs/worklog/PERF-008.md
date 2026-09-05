# PERF-008 — Lane C harness

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
