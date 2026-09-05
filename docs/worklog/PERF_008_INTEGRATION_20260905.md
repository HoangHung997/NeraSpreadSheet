# PERF-008 — Tích hợp source harness đã nghiệm thu

Root xác minh bốn workflows tại đúng source final
`fe01586468d455c2ec26cc084e523c32c4c31baa`: full `33977445862` (5 jobs),
iOS `33977447539`, Q003C `33977449065`, isolated `33975174271`, tất cả success
attempt 1. C đã release toàn bộ owned files; không giữ desktop lease.

Ghép sạch bảy commits theo thứ tự, không production/source model changes:

| Source | Integration |
| --- | --- |
| 413ab07a | d94e3b1b |
| a08eeffb | b5990d83 |
| 2c8c4e2c | 5d07d313 |
| 97f20261 | 535f3c7c |
| 726ace80 | 958292a1 |
| 3cefe685 | 976528f6 |
| fe015864 | 4e42a584 |

Root diff source/integration bằng rỗng trên toàn C-owned paths. Review gồm
capture actual pre-warmup/post-batch output ngoài timing/allocation/GC windows,
hai negative drift guards, live fixture/result thay saved/replacement output.
Giữ v3 batch counts, datasets/statistics và mọi historical raw, kể cả v1 và
3cefe685 INCONCLUSIVE. Không retest-until-green hoặc lấy green parent thay final.

Source full: Core 1505/1505, Windows 107/107, MAUI 44/44, zero skips; capture
226 PNG/128 layouts. Existing workflow actual SDK 10.0.400. Isolated runner SDK
10.0.302/runtime 10.0.11, Intel Xeon Platinum 8573C, 4 logical processors,
Windows image 20260824.214.3. Không suy ra SDK thực từ rollForward global.json.

Corrected isolated result: 11/11 CPU/allocation workloads PASS, native 2/2 PASS,
zero skips. Sáu A/A pairs rồi freeze budget trước 12 AB/BA pairs; 36 timing
workers/396 measurements, 38 execution evidence hashes và 418 pre/post pairs
được lane audit. Root đọc audit và kiểm metadata CI; raw gốc ở artifact
`9972149619` (8,952,953 bytes), ZIP SHA-256
`9fde603f6ed812e5678c32d74a191301d8218727ed6dea2254976a0f535ac75f`.
Budget SHA-256
`AAF147F67BEA1C9DFE1626DADFC0AB6CF76AB001C5F20F4E01BFC1ACFD2D80DB`.
Frozen per-workload tolerance 5–10.30695%, largest upper one-sided 95% ratio
1.02678796, allocation delta intervals [0,0]. Không gọi đây là uniform 5% gate.

Native 12 lifecycle cycles mỗi host: direct subscriptions quay về zero;
WPF managed 6,417,288→6,694,344 bytes/handles 667→669; WinForms managed
6,435,072→6,418,112/handles 682→682. Chỉ là bounded observations, không chứng
nhận whole-process leak-free, input-to-display/GPU latency hoặc physical DPI.
Requested-page cache vẫn không có constant LRU bound.

P3 vẫn OPEN tới khi chạy mới trên exact combined UX-007 + TABLE-007 source,
cùng baseline/calibration protocol. Whole wave chưa DONE; integration commit
gồm release consumer/docs cũng phải có exact-final-HEAD CI riêng. Rollback:
revert bảy integration commits theo thứ tự ngược, không data migration/reset.
