# Contract Clipboard UX — C1-A, C1-B và C1-C

## Trạng thái

Đợt `CHATGPT-CLIPBOARD-VIEW-20260908`; baseline
`2e80250b1b1c51ad56a415d1bbeb70d7b2e85f25`. C1-A implementation là
`5cf3e7decc640f917e75d17d892a091c3588fddd`; C1-B nối tiếp commit này trong PR #5.

C1-A chặn Cut nhiều vùng. C1-B thêm đường ghi clipboard có acknowledgement
ngay trên controller dùng chung, cùng regression cho failure/cancellation và
completion muộn. **Mới có implementation và test code, chưa build/chạy test**.
C1-C nối tiếp `e782ac1b51ddcd5d2e4ddada0caaa9aa396bf203`, sửa reentrancy và
preflight của Cut đồng bộ; giữ nguyên API và đường history hiện có.
Chưa nối các host, chưa nghiệm thu toàn bộ C1, Mục 2 hoặc Mục 3.
Receipt C1-A và audit trước đó được giữ trong lịch sử PR/progress; không dùng
các phát hiện đọc source thay kết quả runtime.

## 1. API trong session và API ghi clipboard host

`CopyPrimarySelection()` vẫn copy vùng đầu vào clipboard nội bộ.
`CutPrimarySelection()` vẫn là thao tác đồng bộ, không tự ghi OS clipboard.
Cut từ chối selection có nhiều vùng **trước** Copy/Clear; không gộp ngầm vùng
kề/chồng nhau và không sửa `ClearSelection()` thành chỉ xóa vùng đầu. C1-B
bổ sung từ chối Cut giao một phần merged range; người gọi phải chọn trọn vùng.
C1-C từ chối Cut khi editor còn mở hoặc source worksheet đã bị xóa khỏi workbook,
trước khi thay clipboard cũ. Không tự Cancel editor hoặc chọn sheet khác để Cut.

API mới trên chính `SpreadsheetClipboardController`:

```csharp
ValueTask<bool> CopyToClipboardAsync(
    Func<SpreadsheetClipboardPackage, CancellationToken, ValueTask> writeAsync,
    bool cut = false,
    CancellationToken cancellationToken = default);
bool IsClipboardWritePending { get; }
bool CancelPendingClipboardWrite();
```

Cùng một private package builder phục vụ Copy nội bộ và API mới; không tạo
controller/session/workbook hoặc history pipeline thứ hai. Bước xóa được
thực hiện qua `SpreadsheetSession.ClearSelection()` và history hiện có.

Host cung cấp `writeAsync` thật. Callback chỉ thành công sau khi OS đã nhận
dữ liệu và hoàn tất flush bắt buộc của nền tảng; phải truyền lỗi/cancellation
ra ngoài. Callback không được gọi Copy/Cut/Import/Paste trên controller hoặc
tự ClearSelection. Callback giả trong test không phải bằng chứng OS runtime.

## 2. Chuẩn bị, acknowledgement và commit

API mới chỉ nhận một vùng chữ nhật và không chạy trong lúc cell editor đang
hoạt động. Validate worksheet còn trong workbook, complete spill, giới hạn
materialization và (khi Cut) complete merged ranges trước khi gọi transport.

Package được tạo riêng nhưng **chưa publish** vào thuộc tính Clipboard.
Trong lúc transport chờ, dữ liệu, selection và clipboard package cũ không
bị thao tác này thay đổi; history chưa nhận thao tác xóa.

Sau callback thành công, kiểm cancellation và lease nguồn: đúng Worksheet
instance, worksheet còn trong Workbook, tên sheet, Worksheet.Version,
Workbook.Version, Dimensions.Version, Selection.Version/active/anchor/range,
View.Version và snapshot merged ranges. Đổi sheet rồi quay lại hoặc mở editor
rồi Cancel cũng làm lease cũ mất hiệu lực nhờ event invalidation tạm thời.
Đặc biệt, không dùng riêng Workbook.Version để suy rằng nội dung ô chưa đổi.

Lease hết hiệu lực: trả false, không publish package và không xóa ô; dữ liệu
mới do session khác sửa được giữ nguyên. Transport có thể đã ghi OS clipboard,
vì vậy false không đồng nghĩa OS clipboard chưa đổi. Host phải gắn stamp/token
với đúng package đã được chấp nhận, không tái dùng private stamp cũ.

Lease hợp lệ: publish package đã được acknowledgement, rồi nếu Cut thì gọi
ClearSelection. Copy trả true; Cut trả kết quả ClearSelection. Cut vùng rỗng
vẫn có thể đã ghi/publish package nhưng trả false và không tạo history, giữ
hành vi API đồng bộ cũ. Không coi false luôn là lỗi transport.

Transport exception/cancellation trước commit không làm mất package cũ.
Exception của subscriber/session sau khi xóa đã bắt đầu vẫn được truyền ra;
package đã được acknowledgement không bị bỏ đi để còn nguồn phục hồi. Đây
không phải bảo đảm rollback nguyên tử cho mọi exception trong history/model;
phạm vi đó vẫn cần review/grant riêng, không được tự gọi DONE.

## 3. Busy, hủy và vòng đời

Một writer mỗi controller. Khi đang chờ hoặc đang commit Cut, Copy/Cut/Import
đồng bộ và writer thứ hai bị từ chối; CanPaste=false và Paste trả false.
Không đổi clipboard cũ chỉ để thể hiện busy. Sau finally, busy được giải phóng,
event handlers tạm được gỡ và lượt sau không kế thừa invalidation.

Quy tắc này áp dụng cả `CutPrimarySelection()` đồng bộ, không chỉ API async.
C1-B chưa giữ busy quanh ClearSelection của đường đồng bộ: callback CellsChanged
có thể gọi Copy và thay package nguồn bằng ô vừa bị xóa. C1-C giữ busy từ package
creation đến khi ClearSelection và các callback kết thúc, rồi giải phóng bằng
finally. Gọi private package builder để không tự vi phạm guard của public Copy.
`CancelPendingClipboardWrite()` không hủy ngược một synchronous Cut đã bắt đầu.
Failure preflight không thay package cũ; failure downstream vẫn giữ package nguồn
đã publish, nhưng không được dùng điều này để nhận generic transaction rollback.

`CancelPendingClipboardWrite()` gửi cancellation đúng một lần; không clear
clipboard nội bộ/hệ điều hành. Nếu transport không hỗ trợ hủy, vẫn phải chờ
transport thật sự kết thúc trước khi mở writer mới; acknowledgement đến muộn
sau yêu cầu hủy không được xóa nguồn. Không mở writer mới chỉ vì timeout để
rồi transport cũ ghi đè OS clipboard của lượt mới.

Mọi entry và continuation dùng context sở hữu session; controller không phải
thread-safe. Await cố ý giữ SynchronizationContext. Host phải marshal đúng UI
context, arbitrate Esc sau editor/IME/popup, và gọi Cancel khi detach/dispose.
Host không được coi Cancel là bằng chứng native write đã kết thúc.

## 4. Merge, spill, protection và phạm vi chưa xong

Cut một phần merge bị từ chối trước khi đổi clipboard. Cut đầy đủ merge xóa
nội dung qua đường hiện có, giữ topology merge ở source; Undo khôi phục nội
dung. Chưa thêm khả năng vận chuyển/tạo merge ở đích Paste.

Complete-spill preflight và package chứa owner formula/child style vẫn dùng
code hiện có; partial spill bị từ chối. Chưa thay formula translation, TSV
parser, validation hoặc format transport. Paste Special, transpose/skip blanks,
viền nguồn copy và trạng thái command catalog thuộc bước sau, chưa triển khai.

Protection policy không bị suy diễn từ style Locked (không tương đương sheet
đang được bảo vệ). Host phải thực thi quyền sửa thực tế trước transport/commit;
shared protection acceptance đầy đủ vẫn OPEN. C1-B không cấp quyền sửa host
và không tự thay model/serializer ngoài danh sách file giao việc.

WPF/WinForms/MAUI/Avalonia vẫn cần grant path/API/baseline của coordinator.
Chưa thay luồng clipboard hiện tại của chúng, chưa giải quyết toàn bộ native
exception routing, external clipboard replacement, rich/TSV ownership, Esc/IME,
đa cửa sổ hoặc runtime UI. Không lấy shared code làm bằng chứng host đã an toàn.
V1/V2/H1 và integration evidence vẫn OPEN; C2 chỉ bắt đầu sau C1 regression xanh.

## 5. Regression và lệnh kiểm tra

File `ClipboardSafetyTests.cs` giữ nguyên 14 test C1-A, thêm class
`ClipboardAcknowledgementTests` với **35 test methods**; tổng file là 49.
Không giảm assertion, xóa hoặc skip test cũ.

C1-C giữ file safety 49 methods không đổi và nối thêm 12 methods trong class
`ClipboardSynchronousCutTests` của `ClipboardTests.cs`, giữ nguyên cả 5 test cũ
trong file đó. Tổng test bổ sung của C1-A/B/C là 61 methods, **chưa chạy**.
12 methods mới kiểm reentrant Copy/Cut/Import/Paste, async writer lồng nhau,
editor/detached-sheet preflight, cleanup sau materialization/spill rejection,
recovery package khi observer ném lỗi, empty Cut, đổi sheet trong observer,
canonical Cut command và giữ redo khi từ chối editor-active Cut.
Hai test observer failure chỉ kiểm busy cleanup/package retention, không thay
cổng rollback/history atomicity hoặc native runtime.

Các nhóm C1-B kiểm pending-write không xóa/publish sớm; lỗi sync/async transport;
clipboard ban đầu rỗng; cancellation trước/trong write; transport không chịu hủy;
Cancel lặp; busy/chống ghi chồng; sửa trực tiếp hoặc từ session khác; edit rồi
Undo; đổi selection/range/sheet rồi quay lại; rename/remove/workbook changes;
dimensions/view/editor lifecycle; stale Copy; partial/full merge và spill;
empty Cut; formula/style/paste translation; null writer; callback reentrancy
và cleanup để lượt sau hoạt động bình thường. Fake transport dùng
TaskCompletionSource để điều khiển acknowledgement, không dùng sleep/retry.

```powershell
dotnet restore NeraSpreadSheet.Core.slnx
dotnet build NeraSpreadSheet.Core.slnx -c Release --no-restore
dotnet test tests/NeraSpreadSheet.Editing.Tests/NeraSpreadSheet.Editing.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~Clipboard"
dotnet test NeraSpreadSheet.Core.slnx -c Release --no-build
./scripts/verify-architecture.ps1
```

**Chưa chạy những lệnh này**, chưa có 61 PASS cho các test bổ sung, chưa có red-before-fix execution,
build/analyzers hoặc native acceptance. Container không có dotnet/gh; đường tải
SDK trực tiếp không hoạt động. Đã tìm workflow offline có sẵn nhưng run
34167427165 FAILED và artifact list rỗng; không có toolchain đã dùng thành công.

Kiểm SHA, số test, whitespace và kiểm từ vựng chỉ là kiểm tĩnh, không thay C#
compiler/test runner. Cần dispatch workflow `ci.yml` hiện có trên source SHA cuối,
đọc actual build/analyzer/Editing/native/architecture outcomes; không nhận CI
của commit cha hoặc tạo run thành công là test PASS. Không đổi base PR, workflow
hay acceptance để ép xanh.

## 6. Hiệu năng, bàn giao và rollback

Không đổi render/scroll hoặc thêm dependency/package. Package creation vẫn
quét sparse used cells như trước; Cut thêm kiểm merge, pending lease lưu snapshot
merge và so sánh sau acknowledgement. Chưa có benchmark, không nhận cải thiện
hiệu năng hoặc mức overhead cụ thể khi worksheet có nhiều merged ranges.

Cộng dồn PR #5 có bốn paths: SpreadsheetClipboard.cs, ClipboardTests.cs,
ClipboardSafetyTests.cs và contract này. Riêng C1-C chỉ thay source, ClipboardTests
và contract; safety file giữ blob `126fc3372a9cf5ac003009ac7696144d0b21b6bb`.
Progress riêng dùng expected-SHA lock;
không sửa root/plan/shared status, PR #1/#4, host hoặc workflow. PR giữ Draft.

Rollback C1-B bằng revert commit tương ứng về checkpoint C1-A; không migration
workbook. Revert tiếp C1-A sẽ đưa lỗi Cut nhiều vùng trở lại. Cần kiểm lại đúng
HEAD sau rollback, không gọi việc revert là đã nghiệm thu an toàn.

Rollback C1-C bằng revert commit C1-C được nêu ở PR/progress để về e782ac1b;
không migration workbook. Sẽ mất guard synchronous reentrancy/editor/detached sheet,
không được coi rollback là đã an toàn. C1-B và C1-A vẫn giữ trong lịch sử.
