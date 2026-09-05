# PERF-008 — Hợp đồng đo và kiểm thử độ bền

P1/P2 là checkpoint harness; P3 chỉ được nghiệm thu sau khi chạy lại trên SHA
kết hợp UX-007 + TABLE-007 do coordinator giao. Không suy diễn source-green
thành combined-green hoặc headless latency thành input-to-display latency.

## Mốc và quyền sửa

- Baseline production: `2e8482c25a44797a479b276ae26f472811a0a81e`.
- C chỉ sửa benchmarks, file test PERF008 mới, scripts `run-perf-008*`, workflow
  mới `perf-008.yml`, hợp đồng/worklog lane và số đo thật trong performance budget.
- Không đổi production, project references, shared props hoặc workflow hiện có.
- CURRENT/status/plan/wave do coordinator sở hữu. Native local desktop thuộc A;
  C chỉ chạy native stress trên Windows GitHub runner.

## Phương pháp tái lập

Một job Windows hosted riêng checkout ba cây: harness, baseline, candidate.
Hai production trees bắt đầu sạch, ghi exact SHA. Chỉ overlay Program, các file
PERF008 và hai fixture benchmark gốc từ cùng harness revision vào cả hai cây;
manifest lưu từng path/SHA-256. Đây là production SHA cộng harness overlay đã
khai báo, không tuyên bố cây đo là Git tree nguyên trạng. Không sửa csproj.

Build Release net10.0 tuần tự bằng SDK **10.0.302**, tắt build servers trước đo.
Worker chạy process riêng, `DOTNET_TieredCompilation=0`, workstation GC, culture
invariant/vi-VN. Lưu SDK, runtime thực, OS/architecture/CPU model, số processors,
runner image/version, run/attempt, cấu hình, Stopwatch frequency, timestamps và
assembly/raw hashes. Không lưu Machine ID, tên máy/user hoặc credential.

Thứ tự: sáu cặp A/A baseline calibration; ghi budget bất biến; mười hai cặp
baseline/candidate, cặp chẵn A/B và cặp lẻ B/A. Không build/native stress trong
khoảng đo. Không xóa outlier, chọn sample đẹp hoặc ghép khác cấu hình. Tất cả
raw ticks, số operations, bytes process-wide, GC counts và input/output snapshot
JSON + SHA-256 được upload kể cả run FAIL/INCONCLUSIVE. Process allocations bao
gồm Task.Run của filter; không gắn nhãn thread allocation cho async work.

| Workload | Kích thước; operations/warmup mỗi process |
| --- | --- |
| Ribbon packing/collapse | Fixture gốc 9 tabs × 8 groups × 10 commands; widths 1536/1280/1024/820, scale 1; 128/32 |
| Table filter-button toggle/Undo | Table 10 rows/2 columns, unrelated occupancy 0 hoặc 100.000 cells; 64/8 |
| Table structured completion | Cùng fixture, `=Sales[Am`; 256/32 |
| Filter mở catalog | 100.000 rows/unique values, source cap 10.000, page 100; 2/2 |
| Filter cache hit | Trang đầu đã load; 512/64 |
| Filter search cycle | Search `0001` rồi clear; 4/2 |

Packing không bao gồm Setup/projection/localization. Filter open bao gồm snapshot
catalog, còn cached-page không bao gồm full scan. Search cycle là hai request.
Đây là batch CPU averages. P95/P99 trong report là percentile giữa batch averages,
không phải percentile từng input event, frame time, render throughput hoặc GPU
present. Không dùng kết quả local khi A/B build để chấp nhận latency.

## Quy tắc budget khóa trước candidate

Rule version `perf008-v1` được commit trước lần đo acceptance đầu tiên. Các tỷ lệ
dưới đây là chính sách quyết định, không phải số đo hiệu năng giả:

1. Mỗi workload lấy baseline A/A log-ratios `log(second/first)`. Robust sigma là
   `1.4826 × MAD`; tolerance latency = `max(5%, exp(3 × robust sigma) − 1)`.
   Số microseconds budget thực chỉ sinh từ baseline trong `budget.json`.
2. Baseline quality không đạt nếu relative MAD của 12 baseline batch hoặc median
   absolute A/A log-ratio lớn hơn 10%. Không nới gate để chấp nhận baseline nhiễu.
3. Tolerance allocation = `max(1 B/op, 1% median baseline B/op)`.
4. Candidate dùng paired log-ratios và allocation deltas, deterministic bootstrap
   10.000 resamples, seed 8008. Upper one-sided 95% bound phải nằm trong budget;
   lower bound vượt budget là REGRESSION; còn lại INCONCLUSIVE. Baseline samples
   ở candidate phase cũng phải có relative MAD ≤10% để kết luận PASS.
5. Input/output fingerprints, operation/warmup counts phải bằng calibration.
   Fingerprint khác là workload/correctness mismatch, không tự chấp nhận vì nhanh.
6. Không ghi đè frozen budget. Lưu nguyên run nếu nhiễu; rerun đầy đủ, nêu tất cả
   kết quả. Đổi phương pháp cần decision và baseline mới trước đo candidate mới.

Workflow thất bại nếu regression, inconclusive hoặc correctness gate đỏ. Pass
chỉ chứng minh giới hạn workload/môi trường của report. CI full/iOS/Q003C vẫn
là gate riêng tại exact final HEAD gồm docs.

## P2 — stress và giới hạn cache

Headless verify dùng production session/view/viewport: load 20 requested pages
(2.000 cached items), search trả cache về ≤100, cancellation không publish state,
200 fractional scroll compositions giữ snapshot/generation/first-page identity,
không Ribbon/filter refresh và không tính lại formula sentinel. 40 lần
open/search/dispose phải giải phóng tất cả weak references tới paged view; ghi
managed/private/working-set trend mỗi tám lần. Managed samples có cả chi phí
giữ telemetry; không gọi các số đó là pure retained production bytes.

P2 native CI-only: WPF và WinForms mỗi host 12 lifecycle cycles, bốn widths/themes,
customization/restore, shortcuts/Table binding, popup open/search/close trong lúc
search debounce đang chờ. Scroll offset thực phải đến 106 px; Ribbon layout và
runtime snapshots không đổi, filter generation/catalog refresh counter không
đổi, stale formula cache không được recalc. Dispose trả direct delegate field
subscription counts về mức trước attach. Native memory/private/handles được ghi
mỗi ba cycles; đây không phải chứng nhận không có leak ở toàn bộ framework/GPU.
Private-field probes fail closed nếu cấu trúc production thay đổi; không thay
host production bằng mock rồi suy diễn native behavior.

Cache hiện giữ tất cả requested pages cho generation hiện tại, chưa có LRU
eviction. Bounds có thật: native current page 100, source defaults 100.000 rows /
10.000 distinct; 20 page requests giữ 2.000 items. Không tuyên bố cache có một
hằng số cap độc lập số trang khi host nâng source cap. Việc đo/mở source trên
background worker không chứng minh UI latency nhỏ trên mọi dữ liệu.

## Gate còn lại

- P3 rerun cùng harness trên combined A+B SHA; root chuyển ownership trước khi
  sửa bottleneck production. Các thay đổi fingerprint cần review riêng.
- Native MAUI stress/memory dài hạn, physical input-to-display/frame/dropped-frame,
  DPI 100/125/150/200%, 4K/60/120 Hz, touchpad/touch và thiết bị thực cần platform/
  hardware runner thích hợp. Không dùng hosted Windows synthetic tests thay chúng.
- [Performance budget](performance-budget.md) tiếp tục phân biệt mục tiêu frame
  của SDK với số CPU measurements đã có bằng chứng.
- Rollback: revert commit lane C theo thứ tự ngược; không có migration workbook,
  không force-push/reset hoặc thay PR #1 Draft/open/unmerged.
