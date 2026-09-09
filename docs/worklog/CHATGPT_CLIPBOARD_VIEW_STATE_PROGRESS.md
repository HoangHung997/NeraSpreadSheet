# Tiến độ ChatGPT — Clipboard và worksheet view state

- queueId: `CHATGPT-CLIPBOARD-VIEW-20260908`
- status: `RUNNING`
- ownerTask: `ChatGPT-current-chat-clipboard-view-state`
- runId: `BATCH-20260909-006`
- runScope: `C1_C2_V1_V2_SHARED_IMPLEMENTATION_BEFORE_COMBINED_CI`
- sourceFrozen: `false`
- sourceHead: `709f69f9c819d45ceb7a12646929faf1052d5a8b`
- baseline: `2e80250b1b1c51ad56a415d1bbeb70d7b2e85f25`
- assignmentCommit: `c03bcb29788294734af4cca199c8fdb4a8e05afb`
- assignmentBlob: `04b129d1035b11411900ce1bce32a0c9fe252e56`
- sourceBranch: `feature/chatgpt-clipboard-view-state`
- progressBranch: `feature/chatgpt-clipboard-view-state-progress`
- handoffPR: `5`, Draft/open/unmerged
- acceptanceStatus: `NOT_READY_FOR_ACCEPTANCE`

## Chỉ đạo trực tiếp mới ngày 09/09/2026

Người dùng: “vậy tạm thời đánh dấu lại và làm đầy đủ C1,C2,V1,V2 làm xong rồi mới chạy thử nghiệm Ci cả thể.”

Chỉ đạo này thay điều kiện phải chờ C1 CI/regression xanh trước khi viết C2/V1/V2 trong lượt Chat hiện tại. Thực hiện source và regression code theo thứ tự C1 → C2 → V1 → V2, giữ các điểm chưa được kiểm thử ở trạng thái pending, rồi mới kiểm thử CI trên HEAD kết hợp cuối. Không tự coi trì hoãn CI là giảm assertion hoặc bỏ cổng nghiệm thu. Không dispatch/rerun CI từng checkpoint. C1 OS/native acceptance và H1 chưa được nhận DONE.

Phạm vi được giữ: các path C1/C2/V1/V2 đã cấp trong mục 5 assignment. H1, WPF/WinForms/MAUI/Avalonia, Core/structural transaction paths ngoài grant, workflow, nhánh plan/root và shared status vẫn không được sửa. PR #4 HEAD thực đọc hiện f890ae2610e0e08175e45ea1b0d8bb7650a24754, khác SHA cũ trong mô tả PR; không nhập hay chạm nhánh đó.

Pro đã được người dùng xác nhận trong Chat: `USER_CONFIRMED_CURRENT_CHAT`; không phải scheduler evidence. Không hỏi lại, không tạo/kích hoạt task/lịch/PR Watch. Owner/run là nhãn ownership, không phải ID scheduler.

## Khóa

Nhận IDLE → RUNNING bằng expected blob SHA 6b412441bc827add9ca20da5927bc91dde123961. Phải đọc lại đúng owner/run trước ghi source. Mỗi source commit fast-forward từ HEAD đã đọc; không force-push. Giải phóng khóa chỉ sau khi ngừng ghi.

## Receipt được bảo toàn

[Receipt đầy đủ trước lượt006](https://github.com/HoangHung997/NeraSpreadSheet/blob/9ef916c9c3f365f00829b431e22b72147f4ea25e/docs/worklog/CHATGPT_CLIPBOARD_VIEW_STATE_PROGRESS.md) giữ C1-A/B/C/authorization, 78 test bổ sung chưa chạy, SHA và giới hạn host/protection/history. Chưa có source commit mới ở lúc nhận khóa.

## Kế hoạch hiện hành

C2: một pipeline Paste All/Values/Formulas/Formats, preflight, command state và clipboard mode/version/cancel; không tạo menu giả hay clipboard/session thứ hai.
V1: lưu/khôi phục selection, double offsets, zoom theo Worksheet identity và session bằng ViewController hiện có; giữ split/freeze và chặn write-back trong activation.
V2: đọc/lưu session views không xóa toàn bộ SheetViews; bảo toàn metadata view khác/unknown và standard/native precision; thêm regression load/save/load.

Build/analyzers/tests/native/CI: chưa chạy trong lượt này, CI hoãn đến HEAD cuối theo chỉ đạo. Không nhận source code hoặc kiểm tĩnh là PASS.
