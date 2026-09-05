# Performance Budget

Đây là mục tiêu nghiệm thu, chưa phải số liệu đã đạt ở M0.

## Frame budget

| Màn hình | Tổng ngân sách/frame |
|---|---:|
| 60 Hz | 16,67 ms |
| 120 Hz | 8,33 ms |

Mục tiêu 60 Hz cho viewport 4K:

- xử lý input: dưới 1 ms;
- tra row/column và layout visible region: dưới 2 ms;
- tạo/cập nhật display list: dưới 3 ms;
- backend render + present: dưới 8 ms;
- phần dự phòng: tối thiểu 2 ms.

## Bộ nhớ

- Không có object UI cho từng ô.
- Cell storage là sparse.
- Style dùng interning/ID.
- Text layout, glyph, tile và GPU resource có giới hạn cache và cơ chế eviction.

## Dataset bắt buộc

- 1.048.576 hàng × 16.384 cột logical, dữ liệu sparse.
- 100.000 hàng có dữ liệu.
- chiều cao hàng và độ rộng cột không đều;
- hidden rows/columns;
- merged cells;
- freeze panes;
- conditional formatting;
- zoom và DPI 100/125/150/200%;
- 60 Hz và 120 Hz;
- regular mouse, precision touchpad và touch.

## Metric cần báo cáo

- median, P95 và P99 frame time;
- dropped frames;
- allocated bytes/frame;
- working set;
- số tile cache hit/miss;
- số cell layout/render thực tế mỗi frame.

## PERF-008 — CPU paired checkpoint v3, 05/09/2026

**Bảng dưới là lịch sử trước output-guard correction, chưa thay nghiệm thu HEAD
mới.** Final docs run `33974159142` tại `3cefe685` INCONCLUSIVE 4/11, native 2/2
pass. Review phát hiện OutputHash cũ dùng object chụp trước warmup. Harness đã
đổi sang actual pre-warmup/post-batch factories trên chính result/fixture đã đo
và negative drift tests, giữ timing v3. Raw cũ không chứng minh post-batch output
stability. Exact-HEAD results sau correction được gửi coordinator ở handoff;
P3 combined và yêu cầu controlled runner nếu còn noisy vẫn là gate riêng.

Số dưới đây là **batch CPU averages**, không phải input-to-display, displayed
frame time hay GPU framerate. Production baseline `2e8482c2` so với harness
checkpoint `726ace80`; lane không đổi production, nên đây là kiểm chứng harness
và baseline trước P3. **P3 phải chạy lại trên combined UX-007 + TABLE-007.**

Run [33973552493](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/33973552493):
SDK **10.0.302**, runtime **10.0.11**, Release/net10.0/workstation GC/tiered off,
Windows Server 2025 image `20260824.214.3`, AMD EPYC 7763, 4 logical processors.
Sáu A/A pairs khóa budget trước 12 A/B–B/A pairs. Cả **11/11 workloads PASS**
theo [protocol v3](perf-008-acceptance-contract.md); không bỏ outlier.

| Workload | Baseline µs/op | Candidate µs/op | B/op baseline | Tolerance đã khóa | Upper paired ratio 95% |
| --- | ---: | ---: | ---: | ---: | ---: |
| Ribbon 1536 | 364,109 | 368,691 | 412.511,875 | 5,00% | 1,0246 |
| Ribbon 1280 | 358,881 | 359,502 | 428.429,125 | 11,69% | 0,9987 |
| Ribbon 1024 | 349,259 | 345,501 | 428.429,125 | 14,21% | 1,0015 |
| Ribbon 820 | 346,778 | 348,971 | 427.133,125 | 20,93% | 1,0355 |
| Table toggle/Undo, 0 unrelated | 8,257 | 8,264 | 9.080,574 | 5,00% | 1,0337 |
| Table completion, 0 unrelated | 0,560 | 0,565 | 648,072 | 41,74% | 1,0414 |
| Table toggle/Undo, 100.000 unrelated | 8,235 | 8,329 | 9.080,574 | 7,54% | 1,0428 |
| Table completion, 100.000 unrelated | 0,568 | 0,586 | 648,072 | 20,50% | 1,0367 |
| Filter open, 100.000 rows | 13.638,950 | 13.426,900 | 4.989.344,000 | 47,28% | 1,0035 |
| Filter cached page | 0,078 | 0,079 | 72,011 | 7,34% | 1,0335 |
| Filter search + clear cycle | 2.774,000 | 2.764,250 | 655.400,000 | 5,00% | 1,0091 |

Tolerance sinh từ baseline variance, không được nới sau candidate; có workload
ngưỡng rộng tới **47,28%**, nên không mô tả gate này như ngưỡng cố định 5% cho
mọi workload. Trong lượt đo cụ thể này, upper one-sided 95% paired ratio từng
workload đều ≤**1,0429**; đây là observation riêng, không thay đổi gate hoặc là
family-wise 95% claim. Allocation median tương đương; upper allocation delta
lớn nhất là 18 B/op ở filter-open, nằm trong tolerance đã khóa. Raw giữ cả
P95/P99 batch averages, không coi đó là tail latency từng input event.

P2 headless: 40 dispose cycles mỗi SHA, **0** surviving paged-view weak references;
20 requested pages/2.000 cached values, search cache ≤100. 200 fractional scroll
compositions giữ snapshot/Ribbon/filter refresh deltas **0** và formula sentinel
không đổi. Native WPF/WinForms **2/2 pass**, mỗi host 12 resize/theme/customization/
popup/dispose cycles, direct subscriptions trở baseline. Memory observations chưa
chứng minh toàn process/GPU không leak; cache chưa có eviction độc lập source cap.

Full artifact [9971704564](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/33973552493/artifacts/9971704564)
có digest `37119a7dcb1ab619a03184b1c28daef2c42ee8fa48eb8f6adb9f03b87142ae4a`.
Raw bền trong repo: [v3](../benchmarks/PERF008/results/33973552493.json),
[v2](../benchmarks/PERF008/results/33973443725.json),
[v1 INCONCLUSIVE](../benchmarks/PERF008/results/33972169896.json).
Không ghép samples giữa các revision; physical DPI/touch/60–120 Hz và MAUI
performance vẫn cần runner/thiết bị phù hợp. Frame budgets ở trên vẫn là mục tiêu.
