# ChatGPT Chat / Pro — giao việc Mục 2 và Mục 3

## 1. Chỉ đạo hiện hành

- Ngày giao: 08/09/2026. Mã đợt: `CHATGPT-CLIPBOARD-VIEW-20260908`.
- Người dùng yêu cầu một **Trò chuyện (Chat) ChatGPT MỚI trên cloud, KHÔNG phải Công việc (Work)**, riêng với các task giám sát PR và Avalonia, dùng chế độ **Pro**, đọc file này mỗi **15 phút** để thực hiện công việc, không chỉ báo tiến độ. Người dùng đã đính chính rõ Chat thay vì Work sau lượt thiết lập đầu.
- File giao việc: `docs/worklog/CHATGPT_CLIPBOARD_VIEW_STATE_ASSIGNMENT.md` trên nhánh `feature/chatgpt-clipboard-view-state-plan` của `HoangHung997/NeraSpreadSheet`. Codex coordinator là người duy nhất sửa file này.
- Chỉ đạo này thay điều kiện trì hoãn Mục 2/3 trong [hàng chờ cũ](NEXT_CLIPBOARD_WORKSHEET_VIEW_STATE.md) và [comment 5579073325](https://github.com/HoangHung997/NeraSpreadSheet/pull/1#issuecomment-5579073325): được bắt đầu các bước đã cấp phạm vi dưới đây, không cần chờ toàn bộ Mac hoàn tất. Không mở rộng hoặc ngắt công việc A/B/C hay PR #4.
- Người dùng đã tự tạo và chỉ định đúng Trò chuyện **“Thiết lập tác vụ lịch vertex”**. Đây là Chat nhận đợt giao việc này; coordinator đã gửi yêu cầu thiết lập vào chính cuộc trò chuyện đó, không tạo Work khác.
- Trạng thái hiện hành: **SCHEDULE CREATED / PAUSED; Pro chưa xác minh**. Coordinator đã tạo lịch `neraspreadsheet-m-c-2-v-3-trong-chat-pro` bằng công cụ automation của app và đọc lại cấu hình: đúng Chat người dùng chỉ định, nhịp 15 phút, trạng thái PAUSED. Chưa có lượt chạy hoặc bằng chứng Pro; cấu hình không có tham số model/mode Pro. Việc app lưu lịch gắn tới Chat cloud không chứng minh scheduler chạy độc lập khi máy/app tắt. Chờ người dùng xác nhận lựa chọn Pro trước khi kích hoạt, không tạo lịch thứ hai.
- Chat tự báo không có công cụ scheduler cloud gọi được; không có lịch nào được tạo từ phía Chat. Lịch PAUSED ở trên là kết quả coordinator tạo sau đó. Task Work tạo nhầm trước đó vẫn dừng và không có quyền worker. Không lấy prompt yêu cầu Pro hoặc tên lịch thay bằng chứng cấu hình.
- Nếu không xác minh được Pro trong cấu hình thực thi của task/lịch, chỉ được thiết lập, đọc và lập báo cáo; chưa sửa source. Không coi chữ “Pro” trong prompt, tên task, gói thuê bao hoặc Astra `high/xhigh/max/ultra` là bằng chứng Pro đã bật. Không tự thay bằng Codex local hoặc model khác.

## 2. Nguồn, nhánh và các phần phải bảo toàn

Baseline đã xác minh của PR #1: `2e80250b1b1c51ad56a415d1bbeb70d7b2e85f25`, nhánh `feature/bootstrap-architecture-v0.1`, Draft/open/unmerged. Sáu workflow tại baseline đã SUCCESS: full `34191742907`, iOS `34191742919`, Q003C `34191742899`, Windows packages `34191738775`, canonical MAUI `34191738776`, demo Win11 `34191973894`. Đây là CI cũ, KHÔNG thay CI của commit mới.

Trước mỗi bước, đọc lại remote refs, PR #1, PR #4, `AGENTS.md`, `README.md`, `ARCHITECTURE.md`, `docs/current-status.md`, `docs/worklog/CURRENT.md`, contract, test và benchmark liên quan. Phân biệt checkpoint lịch sử với chỉ đạo mới; nếu có mâu thuẫn quyền sửa thì dừng phần xung đột để hỏi coordinator, không suy quyền từ một comment bất kỳ.

Các nhánh dành riêng cho đợt này (không dùng nhánh Avalonia làm nhánh source):

| Nhánh | Người ghi | Công dụng |
|---|---|---|
| `feature/chatgpt-clipboard-view-state-plan` | Codex coordinator | File giao việc này; không nhận source của worker |
| `feature/chatgpt-clipboard-view-state` | Một task ChatGPT cloud được chỉ định | Source, tests và contract thuộc phạm vi được cấp; tạo từ baseline trên |
| `feature/chatgpt-clipboard-view-state-progress` | Cùng task ChatGPT cloud | Chỉ file `docs/worklog/CHATGPT_CLIPBOARD_VIEW_STATE_PROGRESS.md`: khóa lượt, tiến độ và bàn giao; không mở PR để tránh chạy build vì cập nhật trạng thái |

Nếu nhánh đã tồn tại, phải đọc đúng owner/base/HEAD trước khi dùng; không reset, force-push hoặc tự chiếm nhánh. Không ghi vào main/develop/nhánh root, không merge, không Ready, không publish NuGet công khai, không sửa workbook thật hoặc đưa dữ liệu cá nhân lên GitHub.

Các phần chưa được giao quyền sửa:

- Lane C `feature/release-009-maui-desktop-consumer`: Mac launcher, emission fixture, release contract/worklog; hiện có candidate local chờ coordinator review. Không sửa launcher, parser, workflow, timeout, diagnostic hoặc điều kiện nghiệm thu để xử lý Mục 2/3.
- Lane B `feature/table-007-editor-corpus`: phần MAUI editor/host và thay đổi Viewport còn HOLD; không nhận cả nhánh hoặc sửa chồng các file đó.
- PR #4 `feature/avalonia-001-host`, HEAD đã đọc `64fc1643c099c56a1017b93d5ab8b0e40db32282`: Formula UX/host/sample/tests do task ChatGPT khác giữ. Mục 2/3 mới không phải quyền ghi PR #4 hoặc tự cherry-pick nó.
- Shared status/worklog (`CURRENT.md`, `docs/current-status.md`, wave plans) và comment bàn giao của coordinator vẫn chỉ coordinator ghi. Worker báo qua file progress riêng và PR riêng; không sửa comment của tác nhân khác.
- A UX-008 đã tích hợp; giữ giới hạn fresh direct-peer. U2/U5, whole B/C, final P3/hardware còn OPEN. Không nâng tiến độ toàn dự án thành 100% từ việc hoàn thành đợt này.

## 3. Mỗi lượt 15 phút làm gì

1. Đọc bản mới nhất của file giao việc qua GitHub, ghi nhận commit SHA; đọc progress ở đúng nhánh riêng. Nếu không đọc được GitHub, không dùng bản nhớ cũ để sửa.
2. Xác minh đúng task được giao và Pro đã được xác nhận từ cấu hình thực thi. Không tự tạo task/model/lịch khác. Một lịch quay lại cùng task; không tạo task mới mỗi 15 phút.
3. Nếu lượt trước còn chạy hoặc khóa chưa được giải phóng, kết thúc lượt kiểm tra, không khởi động worker thứ hai. 15 phút là nhịp tiếp tục, không phải giới hạn để hủy lượt đang làm.
4. Khi đủ điều kiện, nhận khóa nguyên tử theo mục 4; chọn đúng một bước còn làm được theo thứ tự mục 5. Đọc source/test mới, tái hiện, sửa tối thiểu, thêm regression, chạy kiểm tra liên quan và lưu checkpoint thực tế lên nhánh riêng.
5. Giữ lịch sử commit nhỏ theo thay đổi có nghĩa. Không đẩy commit rỗng, chỉ đổi timestamp trong source hoặc dispatch/rerun CI mỗi 15 phút. CI đang chạy trên cùng HEAD thì chờ kết quả; CI đỏ thì đọc nguyên nhân trước khi sửa, không retry mù.
6. Cập nhật progress bằng expected SHA; nêu việc đã làm, kết quả thật, giới hạn và một bước tiếp theo. Chỉ giải phóng khóa sau khi lượt thực sự ngừng ghi. Nếu lỗi giữa chừng, giữ khóa và báo coordinator; không tự đoán là an toàn để chiếm lại.
7. Nếu chỉ chờ quyền, CI hoặc không có thay đổi có thể hành động, giữ im lặng. Báo khi có checkpoint hoàn tất, thất bại mới, xung đột hoặc cần người dùng quyết định. Khi cả hai mục đã được nghiệm thu, tắt/xóa CHÍNH lịch này; không thay lịch giám sát hay mở việc ngoài phạm vi.

## 4. Chống ghi đè và chống hai lượt chạy chồng

Markdown tự nó KHÔNG phải khóa. Áp dụng đồng thời một writer mỗi nhánh/file và kiểm tra phiên bản của GitHub:

- File progress ban đầu do worker tạo với trạng thái `IDLE`. Chỉ tạo khi file chưa tồn tại; lỗi đã tồn tại phải đọc lại, không thay nội dung.
- Để nhận khóa, đọc nội dung và blob SHA hiện hành; chỉ khi `IDLE`, cập nhật thành `RUNNING` kèm `ownerTask`, `runId` duy nhất và `sourceHead` bằng thao tác GitHub cập nhật file có **expected blob SHA**. Chỉ chạy khi ghi thành công và đọc lại đúng owner/runId. Nếu API không cung cấp kiểm tra phiên bản, không triển khai một khóa dựa riêng vào comment hoặc thời gian.
- Lượt tranh khóa gặp conflict/409/422 phải dừng nhận việc và đọc lại; không tự ghi đè bằng SHA mới. Khóa không tự hết hạn sau 15 phút. Chỉ coordinator được xử lý khóa bị bỏ lại sau khi đã xác minh lượt cũ ngừng ghi; người dùng có thể yêu cầu dừng.
- Mỗi commit source phải nối tiếp HEAD remote đã đọc, chỉ chứa đúng file thuộc quyền của worker. Push bị non-fast-forward phải dừng và đối chiếu; không force-push, không tự chọn “ours/theirs”.
- Nếu một phiên còn thao tác sau khi mất khóa/đổi runId thì phải dừng trước lần ghi tiếp theo. Cập nhật report/giải phóng khóa cũng phải dùng SHA đã kiểm tra và đúng runId.
- Không nhập/xóa nhánh của B/C/Avalonia. Mọi path ngoài danh sách cấp dưới đây cần coordinator ghi quyền bổ sung trước; không suy rằng worktree riêng tránh được mọi xung đột.

Progress tối thiểu: queue ID, task và lịch thực tế, Pro evidence hoặc `UNVERIFIED`, lock/status/owner/runId, assignment commit, baseline/current remote SHA, task hiện hành, file đang giữ quyền, kết quả test/CI kèm SHA/run/link, blocker, PR bàn giao và một bước tiếp theo. Không ghi token, đường dẫn máy cá nhân, nonce/runtime ID nhạy cảm hoặc dữ liệu workbook thật.

## 5. Thứ tự triển khai và phạm vi được giao

### C1 — Cut an toàn, làm trước tiên

Tái hiện selection rời nhau A1 và C1: clipboard chỉ chứa vùng nào thì không được xóa ngoài vùng đó. Kiểm `CopyPrimarySelection` và `CutPrimarySelection` gọi `ClearSelection`; đây là điểm nghi ngờ từ source, chưa có regression mới chạy trong lần giao việc này.

Chọn contract rõ ràng: từ chối multi-range nguyên tử nếu chưa hỗ trợ, hoặc copy/cut đúng toàn bộ package theo contract được test. Không âm thầm copy một vùng rồi xóa nhiều vùng. Tôn trọng protection, merge, spill, undo/redo và lỗi clipboard. Việc xóa không được xảy ra trước khi kết quả ghi clipboard thành công; completion cũ không được xóa sheet/selection mới.

**Được sửa ở bước C1:** `src/NeraSpreadSheet.Editing/SpreadsheetClipboard.cs`, `tests/NeraSpreadSheet.Editing.Tests/ClipboardTests.cs`, `tests/NeraSpreadSheet.Editing.Tests/DynamicArrayClipboardTests.cs`; được thêm `tests/NeraSpreadSheet.Editing.Tests/ClipboardSafetyTests.cs` và `docs/clipboard-ux-contract.md`. Các host chỉ đọc trong bước này. Nếu fix an toàn cần file ngoài danh sách, trình patch plan cho coordinator trước.

### C2 — Paste Special và clipboard state dùng chung

Sau C1 regression xanh, triển khai All/Values/Formulas/Formats bằng cùng clipboard/session/history pipeline. Chốt rõ công thức tương đối/tuyệt đối, giá trị tính sẵn, blanks, styles, merges, spills, validation, protected/bounds và transaction. Transpose/Skip blanks phải ghi rõ có hay chưa; không dựng menu giả để gọi là đã hỗ trợ.

Copy/cut source state tách khỏi selection và formula references; quản lý workbook/sheet identity, phạm vi thật, payload/version, operation, busy/error/cancel, OS ownership và stale completion. Esc ưu tiên đúng editor/IME/popup, không mặc định xóa OS clipboard. CanPaste và trạng thái nút phải theo state thật.

**Được sửa bổ sung ở C2:** `src/NeraSpreadSheet.Editing/SpreadsheetClipboardCommandCatalog.cs`; được thêm các file mới có tiền tố `SpreadsheetClipboard` trong Editing và `Clipboard` trong Editing.Tests nếu gắn vào đường xử lý đang có. Không tạo workbook/session/clipboard pipeline song song. Sửa host/renderer vẫn chờ grant.

### V1 — Selection, scroll và zoom theo sheet/session

Sau phần shared C2 đã có checkpoint, làm Mục 3 ở lớp dùng chung. Mỗi sheet giữ active cell, anchor/directed/multiple ranges, zoom và double offsets trong đúng session/window. Không dùng tên sheet làm identity hoặc static/global state để các cửa sổ đè nhau. Tái sử dụng `SpreadsheetViewController` và split state; không tạo model thay thế.

Chuyển A → B → A → B phải giữ các giá trị khác nhau, có offset thập phân, standalone/split/freeze/active pane. Kiểm thứ tự event và suppress write-back trong quá trình restore, tránh Reset/Refresh/ViewportChanged ghi trạng thái sheet cũ vào sheet mới. Rename/reorder/remove, hidden/deleted/merged axes, clamp và lifecycle phải có test.

**Được sửa bổ sung ở V1:** `src/NeraSpreadSheet.Editing/SpreadsheetSession.cs`, `src/NeraSpreadSheet.Editing/SpreadsheetViewController.cs`, `src/NeraSpreadSheet.Editing/SpreadsheetSplitViewState.cs`; tests `SpreadsheetSessionTests.cs`, `SpreadsheetViewControllerTests.cs`, `SpreadsheetSplitViewStateTests.cs` trong Editing.Tests; được thêm `SpreadsheetWorksheetViewState*.cs` trong Editing và Editing.Tests, và `docs/worksheet-view-state-contract.md`. Nếu cần structural transaction/API khác, xin grant cụ thể trước, không bỏ qua invariants để giữ phạm vi nhỏ.

### V2 — Bảo toàn SheetViews khi lưu/nạp XLSX

Phân biệt trạng thái session với dạng lưu file. Bảo toàn selection/zoom/view IDs và các thuộc tính/child chưa hiểu trong `SheetViews`, cả workbook không split. Kiểm serializer hiện hành thay vì kết luận từ report cũ. Không xóa toàn bộ markup khi chỉ cập nhật một view; tuân thủ XML schema và preserve workbook view mapping. Custom metadata chỉ bổ sung độ chính xác cần thiết, không thay toàn bộ chuẩn SpreadsheetML.

**Được sửa bổ sung ở V2:** `src/NeraSpreadSheet.OpenXml/NeraOpenXmlSpreadsheetSessionSerializer.cs`, `tests/NeraSpreadSheet.OpenXml.Tests/SpreadsheetSessionViewRoundTripTests.cs`; được thêm `WorksheetViewState*.cs` trong OpenXml.Tests. Không sửa `OpenXmlTableCodec.cs`, workbook model hoặc serializer khác nếu chưa có grant.

### H1 — Nối WPF/WinForms/MAUI/Avalonia và nghiệm thu

Đây là phần phải hoàn tất của Mục 2/3, **chưa được quyền ghi host lúc khởi tạo**. Sau checkpoint shared, gửi danh sách path/API/baseline cần sửa cho coordinator để cấp lượt, đối chiếu B/C và PR #4. Có thể tiếp tục bước shared kế tiếp khi UI đang chờ; không gọi Mục 2 đã hoàn tất chỉ vì API xanh.

Nối cùng state vào Copy/Cut/Paste/Paste Special, viền nguồn đúng clip/visible+overscan/freeze/split/DPI, Esc, clipboard OS và sheet restore. Không per-cell controls, không full repaint theo raw input/timer, không recalculate toàn workbook khi cuộn. Kiểm từng host thật, nhiều cửa sổ, external clipboard replacement/unavailable/rich↔TSV và trả kết quả chậm. Mỗi host còn thiếu hoặc chưa smoke phải ghi OPEN, không che bằng test host khác.

## 6. Cổng kiểm thử, bàn giao và tích hợp

- Đọc trước: `docs/split-pane-contract.md`, `docs/split-scrollbar-contract.md`, `docs/header-reordering-contract.md`; các test Clipboard/DynamicArrayClipboard/Session/ViewController/SplitViewState và `SpreadsheetSessionViewRoundTripTests`. Kiểm benchmark hiện hành khi chạm hiệu năng; thiếu benchmark sát nghiệp vụ phải ghi rõ, không tự nhận đã đo.
- Bắt buộc regression Cut multi-range, clipboard OS failure/late completion/Esc; Paste Special có undo/redo và formula translation; A → B → A → B, independent windows/sessions, standalone/split/freeze, rename/remove/reorder; load/save/load XLSX giữ SheetViews đã có.
- Build/analyzers không warning/error; architecture verification; runtime smoke thật cho host bị tác động; benchmark trước/sau nếu đổi scroll/render performance. Không giảm assertion, skip test, sửa workflow hay nới CI acceptance để làm xanh.
- Dùng workflows hiện có, chạy/check đúng SHA source đã push. PR riêng giữ Draft, base nhánh tích hợp theo coordinator; nếu workflow không tự trigger vì base/path filter thì báo rõ và xin/chạy workflow hiện có trong phạm vi test được giao, không lấy CI commit cha. Không cần build lặp vì file progress thay đổi.
- Trước khi bàn giao: freeze source, nêu baseline → implementation → final source SHA, file list, test/CI URLs và kết quả thật, rủi ro/giới hạn, rollback và ảnh khi đổi UI. Có PR không đồng nghĩa đã tích hợp.
- Codex coordinator review/tích hợp tuần tự, giải quyết ownership và chạy lại CI trên HEAD kết hợp. Chỉ đánh dấu DONE sau khi source lẫn integration evidence đạt cổng tương ứng; không tự merge PR #1/#4 hoặc phát hành SDK.

## 7. Nội dung lịch 15 phút

Quay lại đúng Trò chuyện ChatGPT (Chat, KHÔNG phải Work) được tạo riêng cho đợt này mỗi 15 phút. Dùng Pro đã xác minh trong cấu hình thực thi; nếu chưa xác minh thì báo điều kiện còn thiếu, không sửa source. Đọc bản mới nhất của file giao việc này trên nhánh plan và progress của worker qua GitHub. Tuân thủ một lượt chạy/một writer, khóa expected-SHA, phạm vi file và thứ tự C1 → C2 → V1 → V2 → H1. Thực hiện bước kế tiếp có thể làm ngay: đọc source, tái hiện, sửa, test và checkpoint trong nhánh riêng; không chỉ viết báo cáo. Không ngắt lượt đang chạy, không tạo task mới mỗi lượt, không sửa các nhánh đang được người khác giữ. Khi chỉ chờ hoặc không có thay đổi thì giữ im lặng; báo checkpoint, lỗi mới hoặc cần quyết định. Dừng chính lịch này khi toàn bộ Mục 2/3 đã được nghiệm thu hoặc người dùng yêu cầu dừng. Không tự chuyển sang công việc khác.
