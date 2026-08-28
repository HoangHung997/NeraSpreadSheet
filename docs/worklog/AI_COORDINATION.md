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
  lease_id: CODEX-RIBBON-KEYBOARD-LOCAL-20260828T085911Z
  acquired_utc: 2026-08-28T08:59:11.5669171Z
  expires_utc: 2026-08-28T09:09:11.5669171Z
  purpose: Record RIBBON-KEYBOARD local green

last_update:
  utc: 2026-08-28T08:50:37Z
  writer: CODEX
  summary: OWNER instructed continuation; RIBBON-KEYBOARD claimed without Apple overlap.
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
| `RIBBON-001` | `BLOCKED` | Codex | `ai/codex/ribbon-bars-customization` | `src/NeraSpreadSheet.Ribbon.Core/**`, `src/NeraSpreadSheet.Bars.Core/**`, test riêng của hai module, contract riêng | Base `fea278057465a983d85c0b472532a2d13c644c37`; không giao Q003B-MAC | Commit `d2d009cc909a3f16b242a282444ae8011727fa31`; local Core 1162/1162 và architecture xanh; run `33145751287` xanh Core/Windows/Android/MAUI Windows nhưng Mac Catalyst runtime smoke đỏ 2 lần, thuộc `Q003B-MAC`. |
| `RIBBON-002` | `BLOCKED` | Codex | `ai/codex/ribbon-bars-persistence` | `src/NeraSpreadSheet.Ribbon.Core/**`, `src/NeraSpreadSheet.Bars.Core/**`, persistence tests của hai module, `docs/ribbon-bars-persistence-contract.md` | Stacked trên `RIBBON-001` commit `d2d009cc909a3f16b242a282444ae8011727fa31`; OWNER cho phép tiếp tục trước tích hợp | Commit `f31223e0a82b24b4da0dd0426741d1bcff99c67d`; local 1178/1178; run `33148789081` xanh Core/Windows/Android/MAUI Windows, chỉ Mac Catalyst runtime đỏ sau window activation thuộc `Q003B-MAC`; tích hợp tuần tự `d2d009c` rồi `f31223e`. |
| `RIBBON-003` | `BLOCKED` | Codex | `ai/codex/ribbon-bars-presentation` | `src/NeraSpreadSheet.Commands/CommandPresentation.cs`, `src/NeraSpreadSheet.Ribbon.Core/**`, `src/NeraSpreadSheet.Bars.Core/**`, presentation tests trong `NeraSpreadSheet.Commands.Tests`, `docs/ribbon-bars-presentation-contract.md` | Stacked trên `RIBBON-002` commit `f31223e0a82b24b4da0dd0426741d1bcff99c67d`; OWNER yêu cầu tiếp tục | Commit `fb2284907ad3659e7b7aae6d8ecacaccc29415e4`; local 1184/1184; run `33150555391` xanh Core/Windows/Android/MAUI Windows, chỉ Mac Catalyst runtime đỏ thuộc `Q003B-MAC`; tích hợp tuần tự `d2d009c`, `f31223e`, `fb22849`. |
| `RIBBON-004` | `BLOCKED` | Codex | `ai/codex/ribbon-bars-runtime` | `src/NeraSpreadSheet.Ribbon.Core/**`, `src/NeraSpreadSheet.Bars.Core/**`, runtime tests trong `NeraSpreadSheet.Commands.Tests`, `docs/ribbon-bars-runtime-contract.md` | Stacked trên `RIBBON-003` commit `fb2284907ad3659e7b7aae6d8ecacaccc29415e4`; OWNER yêu cầu tiếp tục đến khi hoàn thành Ribbon | Commit `8e95f7a3000bee1a515101369c61cb09e613d286` pushed; focused 44/44, Core 1190/1190, build 0 warnings/errors, architecture và sensitive scan xanh; run `33153003700` xanh Core/Windows/Android/MAUI Windows, chỉ Mac Catalyst runtime đỏ thuộc `Q003B-MAC`. |
| `RIBBON-DESKTOP` | `BLOCKED` | Codex | `ai/codex/ribbon-desktop-presenters` | `src/NeraSpreadSheet.Wpf/**Ribbon**`, `src/NeraSpreadSheet.Wpf/**Bar**`, `src/NeraSpreadSheet.WinForms/**Ribbon**`, `src/NeraSpreadSheet.WinForms/**Bar**`, hai host csproj, desktop presenter tests riêng, `docs/ribbon-desktop-presenter-contract.md` | Stacked trên `RIBBON-004` commit `8e95f7a3000bee1a515101369c61cb09e613d286`; không giao path Apple/Mac Catalyst | Commit `3a6f7e54e17ca5d996a469653672fc9375a72010` pushed; exact-head run `33154620901`: Core, Windows desktop (gồm presenter smoke), Android và MAUI Windows xanh; chỉ Mac Catalyst runtime smoke cũ đỏ thuộc `Q003B-MAC`. Local: Ribbon loaded smoke 2/2, Core 1190/1190, architecture xanh. |
| `RIBBON-CUSTOMIZE-UI` | `BLOCKED` | Codex | `ai/codex/ribbon-customization-ui` | New customization-session files in Ribbon.Core/Bars.Core; new WPF/WinForms customization dialog files; focused session/UI tests; `docs/ribbon-customization-ui-contract.md` | Stack trên `3a6f7e5`; OWNER yêu cầu tiếp tục Ribbon; không chạm Apple/Mac Catalyst | Commit `7f8471e15ef0189bd192e6ac3fbc3515e2b41894` pushed; focused session/UI 8/8, Core 1196/1196, builds 0 warnings/errors, architecture và sensitive scan xanh. Full local Windows 47/49; chỉ hai lỗi môi trường WPF cũ DPI 125% và foreground activation. |
| INTEGRATE-001 | `BLOCKED` | ChatGPT (`INTEGRATOR`) | `feature/bootstrap-architecture-v0.1` | Chỉ merge `Q003B-MAC` và `RIBBON-001`; cập nhật tài liệu chung | Hai task phải `READY_FOR_INTEGRATION` | Exact-head CI của branch tích hợp xanh toàn bộ. |

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
| 5 | `RIBBON-004` | Codex | Runtime controller cho customization, snapshot refresh và command activation | OWNER đã yêu cầu tiếp tục; stack trên `RIBBON-003` |
| 6 | `RIBBON-DESKTOP` | Codex | Presenter và runtime smoke WPF/WinForms | `RIBBON-004` có evidence sẵn sàng |
| 7 | `RIBBON-CUSTOMIZE-UI` | Codex | Phiên tùy biến và dialog native WPF/WinForms cho ẩn/hiện, đổi thứ tự và kích thước item | OWNER yêu cầu tiếp tục; stack trên desktop presenter |
| 8 | RIBBON-MAUI | Codex | Presenter và runtime smoke MAUI, không chạm code Apple đang khóa | `Q003B-MAC` kết thúc và desktop presenter ổn định |
| 10 | `RIBBON-CLOSE` | ChatGPT (`INTEGRATOR`) | Tích hợp stack, cập nhật tài liệu dùng chung và exact-head CI | Tất cả task Ribbon đầu vào sẵn sàng |
| 11 | `INTEGRATE-001` | ChatGPT | Merge tuần tự và chạy exact-head CI tổng hợp | Các task đầu vào sẵn sàng |

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

| 2026-08-28T05:58:16Z | `BLOCKED` | Codex | `RIBBON-001` | Run `33145751287` attempt 1 and failed-job retry attempt 2 both fail only at Mac Catalyst analytics accessibility runtime smoke after window activation; task code/Core/other hosts green; wait for owner of `Q003B-MAC`; lease released. |

| 2026-08-28T06:27:49Z | `CLAIMED` | Codex | `RIBBON-002` | OWNER explicitly instructed Codex to continue while ChatGPT handles integration; branch `ai/codex/ribbon-bars-persistence` stacks on `d2d009cc909a3f16b242a282444ae8011727fa31`; no Mac/shared paths; lease released. |

| 2026-08-28T06:30:41Z | `IN_PROGRESS` | Codex | `RIBBON-002` | Required docs, customization contract, source and tests read; implementing deterministic JSON v1 plus explicit legacy v0 migration; lease released. |

| 2026-08-28T06:41:21Z | `LOCAL_GREEN` | Codex | `RIBBON-002` | Commit `f31223e` pushed; focused 32/32, Core 1178/1178 and architecture verification passed; lease released. |

| 2026-08-28T06:42:06Z | `CI_RUNNING` | Codex | `RIBBON-002` | Exact-head run `33148789081` started for `f31223e0a82b24b4da0dd0426741d1bcff99c67d`; lease released. |

| 2026-08-28T06:50:09Z | `BLOCKED` | Codex | `RIBBON-002` | Run `33148789081` at `f31223e0a82b24b4da0dd0426741d1bcff99c67d`: Core, Windows desktop, Android and MAUI Windows green; only Mac Catalyst runtime exits after `smoke-window-activated` without result. Handoff order: `d2d009c` then `f31223e`; lease released. |

| 2026-08-28T07:04:05Z | `CLAIMED` | Codex | `RIBBON-003` | OWNER instructed continuation; task stacks on `f31223e` and only projects Ribbon/Bars definitions with Commands state; no Mac/shared paths; lease released. |

| 2026-08-28T07:04:53Z | `IN_PROGRESS` | Codex | `RIBBON-003` | Branch created at `f31223e`; Commands state/descriptor contracts and Ribbon/Bars tests reviewed; lease released. |

| 2026-08-28T07:06:12Z | `SCOPE_REFINED` | Codex | `RIBBON-003` | Added only `src/NeraSpreadSheet.Commands/CommandPresentation.cs` so Ribbon/Bars share descriptor/state projection; no active-task overlap; lease released. |

| 2026-08-28T07:11:23Z | `LOCAL_GREEN` | Codex | `RIBBON-003` | Commit `fb22849` pushed; focused 38/38, Core 1184/1184 and architecture verification passed; lease released. |

| 2026-08-28T07:12:05Z | `CI_RUNNING` | Codex | `RIBBON-003` | Exact-head run `33150555391` started for `fb2284907ad3659e7b7aae6d8ecacaccc29415e4`; lease released. |

| 2026-08-28T07:19:22Z | `BLOCKED` | Codex | `RIBBON-003` | Run `33150555391` at `fb2284907ad3659e7b7aae6d8ecacaccc29415e4`: Core, Windows desktop, Android and MAUI Windows green; only existing Mac Catalyst runtime smoke failed. Handoff order `d2d009c` -> `f31223e` -> `fb22849`; lease released. |

| 2026-08-28T07:43:23Z | `CLAIMED` | Codex | `RIBBON-004` | OWNER yêu cầu tiếp tục đến khi hoàn thành Ribbon; branch `ai/codex/ribbon-bars-runtime` stack trên `fb22849`; phạm vi runtime headless không giao vùng Mac Catalyst; lease released. |

| 2026-08-28T07:44:32Z | `COORDINATION_REPAIRED` | Codex | `RIBBON-004` | Sửa dòng backlog bị chèn sai và phần purpose bị nối dư trong lượt claim; không thay đổi phạm vi hay trạng thái task. |

| 2026-08-28T07:45:13Z | `IN_PROGRESS` | Codex | `RIBBON-004` | Branch `ai/codex/ribbon-bars-runtime` created at `fb22849`; required docs, contracts, command dispatcher and focused tests reviewed; lease released. |

| 2026-08-28T07:50:37Z | `LOCAL_GREEN` | Codex | `RIBBON-004` | Commit `8e95f7a` pushed; focused 44/44, Core 1190/1190, build/analyzers 0 warnings/errors, architecture and sensitive scan passed; lease released. |

| 2026-08-28T07:52:12Z | `CI_RUNNING` | Codex | `RIBBON-004` | Exact-head run `33153003700` started for `8e95f7a3000bee1a515101369c61cb09e613d286`; lease released. |

| 2026-08-28T07:59:13Z | `BLOCKED` | Codex | `RIBBON-004` | Run `33153003700` at `8e95f7a`: Core, Windows, Android and MAUI Windows green; only existing Mac Catalyst runtime smoke failed under `Q003B-MAC`. |
| 2026-08-28T07:59:13Z | `CLAIMED` | Codex | `RIBBON-DESKTOP` | OWNER requested continuation through Ribbon completion; desktop native presenters stack on `8e95f7a` with no Apple/Mac path overlap; lease released. |

| 2026-08-28T08:00:20Z | `COORDINATION_REPAIRED` | Codex | `RIBBON-DESKTOP` | Removed the duplicate active-task row from backlog; active claim and scope unchanged. |

| 2026-08-28T08:02:03Z | `IN_PROGRESS` | Codex | `RIBBON-DESKTOP` | Branch created at `8e95f7a`; mandatory docs, desktop project boundaries and existing native presenter/smoke patterns reviewed; lease released. |

| 2026-08-28T08:16:00Z | `LOCAL_GREEN` | Codex | `RIBBON-DESKTOP` | Commit `3a6f7e5` pushed; host build clean, new loaded WPF/WinForms smoke 2/2, Commands 44/44, Core 1190/1190, architecture passed. Existing local suite 45/47 due WPF DPI transform and foreground activation checks outside changed files. |
| 2026-08-28T08:16:00Z | `CI_RUNNING` | Codex | `RIBBON-DESKTOP` | Exact-head run `33154620901` started for `3a6f7e54e17ca5d996a469653672fc9375a72010`; lease released. |


| 2026-08-28T08:24:54Z | `BLOCKED` | Codex | `RIBBON-DESKTOP` | Run `33154620901` at `3a6f7e5`: Core, Windows desktop, Android and MAUI Windows green; only existing Mac Catalyst runtime smoke failed under `Q003B-MAC`. |


| 2026-08-28T08:26:30Z | `CLAIMED` | Codex | `RIBBON-CUSTOMIZE-UI` | OWNER yêu cầu tiếp tục đến khi hoàn thành Ribbon; task thêm phiên tùy biến và dialog native WPF/WinForms, stack trên `3a6f7e5`, không chạm Apple/Mac Catalyst. |


| 2026-08-28T08:27:29Z | `IN_PROGRESS` | Codex | `RIBBON-CUSTOMIZE-UI` | Branch created at `3a6f7e5`; mandatory contracts, runtime APIs and focused desktop smoke reviewed; scope unchanged. |


| 2026-08-28T08:40:34Z | `LOCAL_GREEN` | Codex | `RIBBON-CUSTOMIZE-UI` | Commit `7f8471e` pushed; focused 8/8, Core 1196/1196, builds/analyzers and architecture green; full local Windows 47/49 with only two repeated environment-specific WPF failures. |


| 2026-08-28T08:41:19Z | `CI_RUNNING` | Codex | `RIBBON-CUSTOMIZE-UI` | Exact-head run `33156296542` started for `7f8471e15ef0189bd192e6ac3fbc3515e2b41894`. |


| 2026-08-28T08:49:08Z | `BLOCKED` | Codex | `RIBBON-CUSTOMIZE-UI` | Run `33156296542` at `7f8471e`: Core, Windows desktop, Android and MAUI Windows green; only existing Mac Catalyst runtime smoke failed under `Q003B-MAC`. |


| 2026-08-28T08:50:37Z | `CLAIMED` | Codex | `RIBBON-KEYBOARD` | OWNER yêu cầu tiếp tục Ribbon; shortcut activation and desktop keyboard binding stack on `7f8471e` with no Apple/Mac path overlap. |


| 2026-08-28T08:51:21Z | `IN_PROGRESS` | Codex | `RIBBON-KEYBOARD` | Branch created at `7f8471e`; mandatory command/runtime/presenter contracts and keyboard tests reviewed; scope unchanged. |

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
