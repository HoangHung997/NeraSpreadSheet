# NeraSpreadSheet AI Coordination Protocol

> Nguồn điều phối duy nhất cho ChatGPT và Codex. File này sống trên branch
> `coordination/ai-work-queue`; không dùng bản sao local cũ để quyết định công việc.

## 1. Trạng thái điều phối hiện tại

```yaml
schema_version: 1
coordination_branch: coordination/ai-work-queue
integration_branch: feature/bootstrap-architecture-v0.1
integration_pr: 1

write_lock:
  state: HELD
  owner: CODEX
  lease_id: CODEX-RIBBON-001-BLOCK-20260828T055813Z
  acquired_utc: 2026-08-28T05:58:13Z
  expires_utc: 2026-08-28T06:08:13Z
  purpose: Record external Mac Catalyst CI blocker for RIBBON-001.

last_update:
  utc: 2026-08-28T05:46:28Z
  writer: CODEX
  summary: RIBBON-001 exact-head CI run 33145751287 started for d2d009cc909a3f16b242a282444ae8011727fa31.
```

`write_lock` chỉ khóa việc sửa **file điều phối này**. Không giữ khóa trong lúc viết
code, build, test hoặc chờ CI. Thời hạn khóa tối đa là 10 phút.

## 2. Quy tắc không được vi phạm

1. ChatGPT và Codex không làm trực tiếp trong cùng worktree, branch hoặc task.
2. Không ai được bắt đầu task nếu chưa có một dòng `CLAIMED` mang đúng tên mình.
3. Hai task chạy song song không được có phạm vi file/module giao nhau.
4. Không sửa file điều phối từ một checkout cũ rồi `git push` thông thường.
5. Mọi lần sửa file điều phối phải dùng GitHub Contents API với blob SHA vừa đọc.
6. Nếu GitHub trả `409 Conflict`, dừng ngay, đọc lại file mới nhất và chờ lượt.
7. Cấm `push --force`, `--force-with-lease` và sửa lịch sử branch điều phối.
8. Chỉ `INTEGRATOR` được merge/cherry-pick vào branch tích hợp.
9. Một task chỉ là `DONE` khi commit đã có trên GitHub và CI xanh đúng SHA yêu cầu.
10. Không được tự đổi phạm vi task. Phát hiện việc mới thì thêm vào `Backlog`, không
    tiện tay sửa.
11. `docs/worklog/CURRENT.md`, `docs/current-status.md`, `docs/project-progress.md`,
    `ROADMAP.md`, `.github/workflows/**`, `Directory.Build.*` và solution files là
    vùng dùng chung; chỉ `INTEGRATOR` sửa trong lượt tích hợp đã khóa.
12. Trước task mới phải tuân thủ `AGENTS.md`, đọc tài liệu bắt buộc và test/benchmark
    của module bị tác động.

## 3. Vai trò

| Vai trò | Người giữ hiện tại | Trách nhiệm |
|---|---|---|
| `INTEGRATOR` | ChatGPT | Giữ branch tích hợp, merge tuần tự, cập nhật tài liệu chung và chạy exact-head CI. |
| `WORKER_CHATGPT` | ChatGPT | Q003B/Mac Catalyst và các file đã được cấp riêng. |
| `WORKER_CODEX` | Codex | Ribbon/Bars customization trên branch và worktree riêng. |
| `OWNER` | Người dùng | Đổi ưu tiên, giải quyết xung đột phạm vi và cho phép merge/release. |

`INTEGRATOR` là vai trò, không phải quyền sở hữu vĩnh viễn. Muốn chuyển vai trò phải
cập nhật file này khi đang giữ `write_lock`.

## 4. Bảng công việc đang hoạt động

| ID | Trạng thái | Người làm | Branch | Phạm vi độc quyền | Phụ thuộc | Bằng chứng hoàn thành |
|---|---|---|---|---|---|---|
| `COORD-001` | `DONE` | Codex | `coordination/ai-work-queue` | `docs/worklog/AI_COORDINATION.md` | Không | File có trên GitHub và đọc được bằng raw/API. |
| `Q003B-MAC` | `IN_PROGRESS` | ChatGPT | `feature/bootstrap-architecture-v0.1` | `src/NeraSpreadSheet.Maui/**Apple**`, `src/NeraSpreadSheet.Maui/**MacCatalyst**`, `tests/NeraSpreadSheet.Maui.MacCatalyst.AnalyticsSmoke/**`, `scripts/run-maui-maccatalyst-smoke.sh` | Không | Apple build và Mac Catalyst runtime smoke xanh đúng HEAD. |
| `RIBBON-001` | `CI_RUNNING` | Codex | `ai/codex/ribbon-bars-customization` | `src/NeraSpreadSheet.Ribbon.Core/**`, `src/NeraSpreadSheet.Bars.Core/**`, test riêng của hai module, contract riêng | Base `fea278057465a983d85c0b472532a2d13c644c37`; không giao Q003B-MAC | Build/test module, architecture verification và branch CI xanh. |
| `INTEGRATE-001` | `BLOCKED` | ChatGPT (`INTEGRATOR`) | `feature/bootstrap-architecture-v0.1` | Chỉ merge `Q003B-MAC` và `RIBBON-001`; cập nhật tài liệu chung | Hai task phải `READY_FOR_INTEGRATION` | Exact-head CI của branch tích hợp xanh toàn bộ. |

### Trạng thái hợp lệ

```text
BACKLOG -> READY -> CLAIMED -> IN_PROGRESS -> LOCAL_GREEN
        -> CI_RUNNING -> READY_FOR_INTEGRATION -> INTEGRATING -> DONE

Bất kỳ trạng thái nào có thể chuyển sang BLOCKED hoặc CANCELLED kèm lý do.
```

Mỗi worker chỉ có tối đa một task `CLAIMED`, `IN_PROGRESS`, `LOCAL_GREEN` hoặc
`CI_RUNNING`. Sau khi task thành `READY_FOR_INTEGRATION`, worker mới được claim task
`READY` tiếp theo không giao phạm vi.

## 5. Backlog đã phân luồng

| Thứ tự | ID | Người dự kiến | Phạm vi | Chỉ được bắt đầu khi |
|---:|---|---|---|---|
| 1 | `Q003B-MAC` | ChatGPT | Kết thúc Mac Catalyst accessibility/build/runtime smoke | Đang chạy |
| 2 | `RIBBON-001` | Codex | Model customization Ribbon/Bars và tests, chưa làm platform presenter | Protocol được chấp nhận |
| 3 | `Q003B-CLOSE` | ChatGPT | Chốt Q003B, evidence và handoff; không mở rộng sang Ribbon/Bars | `Q003B-MAC` xanh |
| 4 | `RIBBON-002` | Codex | Persistence/versioning cho customization contract | `RIBBON-001` đã tích hợp |
| 5 | `INTEGRATE-001` | ChatGPT | Merge tuần tự và chạy exact-head CI tổng hợp | Các task đầu vào sẵn sàng |

Task mới phải được OWNER hoặc INTEGRATOR thêm vào bảng trong một lượt ghi hợp lệ.

## 6. Cơ chế khóa ghi chống ghi đè

### 6.1. Nguyên tắc

GitHub Contents API nhận trường `sha` của blob hiện tại. Đây là compare-and-swap:
nếu file đã đổi sau lúc đọc, lần ghi dùng SHA cũ sẽ thất bại. Không được xử lý `409`
bằng cách ghi lại mù quáng.

Khóa có hai bước:

1. `ACQUIRE`: đọc file mới nhất, đổi `write_lock.state` thành `HELD`, đặt owner,
   `lease_id`, `acquired_utc`, `expires_utc`, rồi PUT bằng blob SHA vừa đọc.
2. `UPDATE_AND_RELEASE`: đọc lại phiên bản đang giữ khóa, cập nhật bảng công việc và
   đổi khóa về `FREE`, rồi PUT bằng blob SHA mới nhất.

Nếu hai AI cùng `ACQUIRE`, chỉ một PUT thành công. AI nhận `409` phải chờ ít nhất 60
giây, đọc lại và chỉ thử tiếp khi khóa `FREE` hoặc đã hết hạn.

### 6.2. Đọc bản mới nhất bằng GitHub CLI

```powershell
$NeraRepo = 'HoangHung997/NeraSpreadSheet'
$CoordBranch = 'coordination/ai-work-queue'
$CoordPath = 'docs/worklog/AI_COORDINATION.md'

$RemoteFile = gh api "repos/$NeraRepo/contents/$CoordPath`?ref=$CoordBranch" |
    ConvertFrom-Json
$ExpectedBlobSha = $RemoteFile.sha
$NormalizedBase64 = $RemoteFile.content -replace '\s', ''
$CurrentText = [Text.Encoding]::UTF8.GetString(
    [Convert]::FromBase64String($NormalizedBase64))
$CurrentText
```

Luôn kiểm tra `write_lock`, bảng task và phạm vi độc quyền trong `$CurrentText` trước
khi ghi.

### 6.3. Ghi có điều kiện

Sau khi tạo nội dung mới trong một file tạm riêng của task:

```powershell
$CoordTempFile = Join-Path $env:TEMP 'nera-ai-coordination-update.md'
$NewText = Get-Content -LiteralPath $CoordTempFile -Raw
$NewBase64 = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($NewText))

gh api --method PUT "repos/$NeraRepo/contents/$CoordPath" `
    -f message='chore(coordination): update AI work queue' `
    -f branch="$CoordBranch" `
    -f sha="$ExpectedBlobSha" `
    -f content="$NewBase64"
```

Sau PUT phải đọc lại từ GitHub và xác nhận commit SHA cùng nội dung. Nếu lệnh thất
bại, không được giả định file đã cập nhật.

### 6.4. Khóa hết hạn

- Lease tối đa: 10 phút.
- Không gia hạn chỉ vì đang code hoặc đang chờ CI.
- Chỉ được thu hồi khóa hết hạn sau khi đọc lại GitHub và ghi rõ sự kiện
  `RECOVER_EXPIRED_LOCK` trong nhật ký.
- Không thu hồi khóa chưa hết hạn. Hãy chờ hoặc báo OWNER.

## 7. Quy trình nhận và hoàn thành task

### Nhận task

1. `git fetch --prune origin` và đọc `AGENTS.md` cùng tài liệu bắt buộc.
2. Đọc file điều phối trực tiếp từ branch điều phối.
3. Acquire khóa bằng SHA hiện tại.
4. Kiểm tra task là `READY`, không có path giao với task đang hoạt động.
5. Đổi task thành `CLAIMED`, ghi owner, branch, base SHA, thời điểm UTC và phạm vi.
6. Release khóa ngay.
7. Tạo branch/worktree riêng từ SHA nền đã ghi; không dùng worktree của AI kia.
8. Đổi thành `IN_PROGRESS` trong một lượt khóa ngắn.

### Trong khi làm

- Không mở rộng path ngoài phạm vi đã claim.
- Nếu cần file dùng chung, chuyển task sang `BLOCKED` và yêu cầu INTEGRATOR.
- Commit nhỏ, rõ ràng; không push vào branch của AI kia.
- Test local theo `AGENTS.md` trước khi chạy GitHub Actions.

### Hoàn thành

1. Push branch task lên GitHub.
2. Chạy CI cho đúng branch/SHA và theo dõi đến kết luận.
3. Ghi commit SHA, CI URL/kết quả, test, file trọng tâm, rủi ro và rollback.
4. Chuyển task thành `READY_FOR_INTEGRATION`; release khóa.
5. Worker có thể nhận task `READY` kế tiếp, nhưng không tự merge task vừa xong.

## 8. Quy trình tích hợp không đè nhau

1. INTEGRATOR acquire khóa và đổi đúng một task sang `INTEGRATING`.
2. Ghi lại HEAD hiện tại của branch tích hợp.
3. Merge/cherry-pick đúng commit đã được CI xác minh; không lấy thêm commit ngoài
   task.
4. Nếu branch tích hợp đã đổi ngoài dự kiến, dừng và đọc lại file điều phối.
5. Build/test phần giao nhau; cập nhật tài liệu dùng chung.
6. Push không force và chạy exact-head GitHub Actions.
7. Chỉ khi toàn bộ job xanh đúng HEAD mới chuyển task thành `DONE`.
8. Nếu CI đỏ, task về `BLOCKED`; ghi job/step lỗi và không tích hợp task tiếp theo.

Chỉ tích hợp một task tại một thời điểm. Task thứ hai phải chờ task thứ nhất có trạng
thái `DONE` hoặc được rollback hoàn toàn.

## 9. Mẫu handoff bắt buộc

```yaml
task_id: RIBBON-001
owner: CODEX
branch: ai/codex/ribbon-bars-customization
base_sha: <sha>
implementation_sha: <sha>
status: READY_FOR_INTEGRATION
exclusive_paths:
  - src/NeraSpreadSheet.Ribbon.Core/**
  - src/NeraSpreadSheet.Bars.Core/**
tests:
  - command: <command>
    result: PASS
ci:
  run_url: <url>
  head_sha: <sha>
  conclusion: success
files_changed:
  - <path>
remaining_limits:
  - <limit>
rollback: Revert <implementation_sha>.
next_single_step: <one concrete action>
```

Không dùng mô tả “đã xong” nếu thiếu SHA hoặc CI/evidence tương ứng.

## 10. Nhật ký điều phối

Chỉ thêm dòng mới ở cuối bảng trong lúc đang giữ khóa. Không sửa hoặc xóa lịch sử.

| UTC | Sự kiện | Người | Task | Nội dung |
|---|---|---|---|---|
| 2026-08-28T03:15:00Z | `PROTOCOL_CREATED` | Codex | `COORD-001` | Tạo protocol single-writer, phân vùng ChatGPT/Codex và quy trình tích hợp tuần tự. |
| 2026-08-28T03:50:05Z | `CLAIMED` | ChatGPT | `Q003B-MAC` | OWNER xác nhận ChatGPT phụ trách; branch `feature/bootstrap-architecture-v0.1`; base SHA `e5559ca1633be8b941651e5dedc9d4b5cc3d1a92`; exclusive paths giữ nguyên theo bảng task. |
| 2026-08-28T03:52:16Z | `IN_PROGRESS` | ChatGPT | `Q003B-MAC` | Tiếp tục Mac Catalyst Apple accessibility/runtime smoke trên branch tích hợp hiện hữu; không mở rộng sang Ribbon/Bars hoặc OpenXML. |

| 2026-08-28T05:28:42Z | `LOCK_ACQUIRED` | Codex | `RIBBON-001` | Lease `CODEX-RIBBON-001-20260828T052842Z` acquired for task claim. |

| 2026-08-28T05:29:11Z | `CLAIMED` | Codex | `RIBBON-001` | Branch `ai/codex/ribbon-bars-customization`; base `fea278057465a983d85c0b472532a2d13c644c37`; coordination lease released. |

| 2026-08-28T05:31:30Z | `IN_PROGRESS` | Codex | `RIBBON-001` | Isolated worktree created at base `fea278057465a983d85c0b472532a2d13c644c37`; scope unchanged; lease released. |

| 2026-08-28T05:45:33Z | `LOCAL_GREEN` | Codex | `RIBBON-001` | Commit `d2d009c` pushed; Core 1162/1162, focused 16/16, architecture verification passed; lease released. |

| 2026-08-28T05:46:28Z | `CI_RUNNING` | Codex | `RIBBON-001` | Exact-head run `33145751287` started for `d2d009cc909a3f16b242a282444ae8011727fa31`; lease released. |

## 11. Prompt ngắn gửi cho mỗi AI

```text
Trước khi làm việc, đọc AGENTS.md và file
docs/worklog/AI_COORDINATION.md trên branch coordination/ai-work-queue trực tiếp
từ GitHub. Tuân thủ single-writer lease và GitHub Contents API compare-and-swap.
Không bắt đầu task nếu chưa CLAIMED, không sửa ngoài exclusive_paths, không sửa
branch/worktree của AI kia, không force-push. Khi xong phải push branch, chạy CI đúng
SHA, cập nhật handoff rồi chuyển READY_FOR_INTEGRATION. Nếu gặp lock HELD hoặc lỗi
409, chờ và đọc lại; tuyệt đối không ghi đè.
```
