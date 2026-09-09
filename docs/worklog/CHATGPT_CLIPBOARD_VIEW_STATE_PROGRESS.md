# Tiến độ ChatGPT — Clipboard và worksheet view state

- queueId: `CHATGPT-CLIPBOARD-VIEW-20260908`
- status: `IDLE`
- ownerTask: `NONE`
- runId: `NONE`
- lastOwnerTask: `ChatGPT-current-chat-clipboard-view-state`
- lastRunId: `BATCH-20260909-007`
- lastRunScope: `SHARED_C1_C2_V1_V2_REVIEW_FIXES_AND_COMBINED_VALIDATION`
- checkpointStatus: `SHARED_HARDENING_IMPLEMENTED_EXECUTION_AND_SCOPE_BLOCKED`
- acceptanceStatus: `NOT_READY_FOR_ACCEPTANCE`
- sourceFrozen: `true`
- sourceHead: `197d666022a8c05991f70202c7e9defc0c36e513`
- sourceTree: `ab11afa6c40dcd7fe153b4996ca1102861d5b08b`
- implementationCommit: `197d666022a8c05991f70202c7e9defc0c36e513`
- previousImplementationCommit: `f75308e51e16bc473670ba7bde8c88cbbf98f9ee`
- baseline: `2e80250b1b1c51ad56a415d1bbeb70d7b2e85f25`
- assignmentCommit: `c03bcb29788294734af4cca199c8fdb4a8e05afb`
- assignmentBlob: `04b129d1035b11411900ce1bce32a0c9fe252e56`
- sourceBranch: `feature/chatgpt-clipboard-view-state`
- progressBranch: `feature/chatgpt-clipboard-view-state-progress`
- handoffPR: [#5](https://github.com/HoangHung997/NeraSpreadSheet/pull/5), Draft/open/unmerged
- handoffComment: [5583713687](https://github.com/HoangHung997/NeraSpreadSheet/pull/5#issuecomment-5583713687)

## Chỉ đạo và kết thúc lượt

Người dùng yêu cầu làm để hoàn thành toàn bộ; chỉ đạo source C1/C2/V1/V2 trước, CI chung sau vẫn áp dụng. Không quay lại gate CI riêng C1 trước C2/V1/V2. **Toàn bộ chưa hoàn thành; không đề nghị nghiệm thu.** Pro là USER_CONFIRMED_CURRENT_CHAT, không phải scheduler configuration evidence; không hỏi lại. Không tạo/kích hoạt lịch/Work/PRWatch hoặc cam kết chạy nền.

Nhận khóa IDLE→RUNNING bằng expected blob SHA8073c0327723c710b99d1df179e00fbe7bdee3fa, readback owner/run007 ở blob913955a3549c8bec647cd9f52023f908ea58be81. Trước source ref update đã đọc lại đúng khóa/sourcef753; candidate compare trướcpush xác nhận10paths/1commitahead/0behind. Ref cập nhật force=false thành197d6660. Đã đọc source mới và PR metadata saupush; PR hiện6commits/18cumulativepaths, Draft/open/unmerged.

Source đã ngừng ghi ở197d6660; PR/comment/2contracts đã cập nhật. Đọc lại đúng owner/run và blob913955 trước release RUNNING→IDLE bằng expectedSHA. Không còn path giữ để ghi. Source freeze là checkpoint, không phải test hoặc acceptance evidence.

## Những lỗi đã sửa trong batch007

### C1/C2 — clipboard ownership

Generation quyền sở hữu OS riêng dùng chung canonical session gate. Ngay trước transportwrite, retire stamp cũ vì flushfailure/cancel/stale có thể đã đổi OS. Controller phụ ghi cũng làm stamp cũ mất hiệu lực nhưng không xóa recoverypackage.

Actualread external/token/textkhác hoặc không cótext vôhiệu hóa privatelease trướcparse/Values/authorization. DeniedPaste hoặc lỗi parse sau đó không bật fallback cũ. ExternalPaste thành công không nhận HasOsOwnership=true; đọc không phải ghiOS. Readfailure trướcobservation không là bằng chứng clipboardđổi. Tất cả vẫn per-session, owning-context, không OSglobal/threadsafe.

### V1 — viewport và activation

SetWorksheetViewport chỉ cậpnhật zoom/TopLeftScroll, không normalize/restore hidden/merged selection hoặc publish freeze khôngđổi. Scroll phátPaneScroll; zoom-only tăngView.Version/phátWorksheetViewChanged; no-op khôngevents. Giữsourcetag/feedbackguard và finallycleanup. Fullstate so sánh sau normalize; snapshot từchối ActiveCell nằm ngoài tấtcảselectedranges trước cache.

Activationlỗi giải phóngguard, giữlỗigốc; không publish completiongiả trongfinally để maskingerror. Không nhận rollback mọi sideeffect của callback hoặc mọi structuralchange.

### V2 — binding cùng workbook window

Chọn WorkbookViewId khởiđầu một lần rồi matchingID trên toàn bộsheets, kểcả thứtựSheetViewkhác nhau. SheetthiếuviewcùngID không mượnstatecửasổkhác; khi cầnlưu thì thêm đúngID và giữsiblingviews. ValidateID/duplicateSheetViews. Mapping đếmWorksheetPartthực và giữSheetIndexgốc choActiveTab; không claim fullchart-sheet/topologypreservation. Nativeprecision/envelope/analytics/pivot/chart/recoverypipelinegiữ nguyên.

## Manifest nguồn và tests

| File | Final blob SHA |
|---|---|
| docs/clipboard-ux-contract.md | 1f5a5e7777efc68fb872747ca1a5b0d81b0f6434 |
| docs/worksheet-view-state-contract.md | 8734a40dca72e91edb9bd1597eb73564f2bc481f |
| src/NeraSpreadSheet.Editing/SpreadsheetClipboard.cs | 0fbda71ec6a01e92764c5e3bb73ca1e3ba9b3f18 |
| src/NeraSpreadSheet.Editing/SpreadsheetClipboardState.cs | f0dbd61092fd932ce5babea90aef55c3f5816e94 |
| src/NeraSpreadSheet.Editing/SpreadsheetSession.cs | e7600547c890a280ba2a46861ff8bd824a0215d2 |
| src/NeraSpreadSheet.Editing/SpreadsheetWorksheetViewState.cs | 4e169504a6b7dc592364f8085db42243dd3814b6 |
| src/NeraSpreadSheet.OpenXml/NeraOpenXmlSpreadsheetSessionSerializer.cs | 082b59016a22a9ec9b5797511e48f39d0cfc46e1 |
| tests/NeraSpreadSheet.Editing.Tests/ClipboardOwnershipRegressionTests.cs | 46085b85e35e6d1cce2b914d48f6d178e2bd894c |
| tests/NeraSpreadSheet.Editing.Tests/SpreadsheetWorksheetViewStateRegressionTests.cs | 78f134052851c010432e75090a25a615a21b100e |
| tests/NeraSpreadSheet.OpenXml.Tests/WorksheetViewStateWindowBindingTests.cs | ff1255917cf956a6657460e9f42d48c805b69be4 |

26methods mới=10clipboard+8viewport/lifecycle+8XLSX. Giữtests cũ; tổng đợt139methodsđãviết, **CHƯA CHẠY**, không phải139PASS hoặc tổngtestrepo. Fakecallbacks/schemaassertions không là OS/OpenXmlValidator đã chạy. Riêngbatch10files=7modified/3added,+871/-42.

## Kiểm tra thực tế và môi trường

Đã đối chiếu exact Gitblob của input/output,10uploadedblobs với localmanifest và remotetree/diff. git diff --check exit0; git apply --reverse --check exit0; UTF-8/LF/newline, tên/sốtests, lexical checks. Tất cả chỉ là staticchecks, không C#compiler/analyzers/testexecution. Có bảnvá và10filezip được tạo trong conversation; không phải ứngdụngbuild hoặc toànrepository.

Actionsquery đúng197d666022a8c05991f70202c7e9defc0c36e513 saupush trả total_count0/workflow_runs=[]. Chưa córunID/CIURL; không phảiCI FAIL/PASS. .NETbuild/analyzers, unit/regression, OpenXmlValidator, fullEditing/OpenXML/Core, architecturescript, nativesmoke vàbenchmark NOT_RUN.

Container không códotnet/gh; DNS tảiSDK/GitHub thấtbại. Connector hiện cóworkflowread/rerun, khôngdispatch; pluginsearch không tìm được runnerphùhợp. Không sửaworkflow/base/filter/assertions, không rerunparentlấyPASS. Chỉ giữtruyvấnCIreadonly; không gọi lệnh dispatch đãghi trongcomment là đãchạy.

## Phần chưa hoàn thành cần được mở chặn thật

| Mục | Phần có source | Phần còn thiếu |
|---|---|---|
| C1/C2 | Guards/modes/state/commands và ownership fixes | H1 actualtransport/policy/native; tests chưa chạy; genericdownstreamatomicity chưa đảm bảo |
| V1 | Per-sheet/sessionstate, viewport/activation fixes | Typedcross-session inactive structural identity remapping; fulltests/native |
| V2 | Standard/nativepreservation và consistentwindowbinding | Build/schema/roundtrip/runtime/integrationproof chưa chạy |
| H1 | Chỉ audit/pathplan | Chưa cógrant host/test/transport và chưa nối host |

Cross-session structural/reorder không thể suy đúng từ genericCellsChanged. Đã đọc SpreadsheetStructureController dùng private structuraloperation quaHistory.Execute, Commands/UndoRedo.cs không cótypednotifications. Cầncoordinator chốtscope typedtransaction/event vàtests, tối thiểuđốichiếu SpreadsheetStructureController.cs, SpreadsheetAxisReorderController.cs vàCore/Commandsliênquan. Không reflection/đoán/resetcache để giảremap, không tựsửa ngoài grant.

H1 grants theo comment5582936737 vẫn chưa được cấp; root2e80250b/PR4f890ae26 không nhập/cherry-pick. Nullauthorization không là SheetProtection; Transpose/Skipblanks/axisstyles chưa hỗtrợ; bulkPaste không thayeditordialogs. Arbitraryobserver/history/recalcrollback không bảođảm, V2siblings/unknowncầnPreserveUnknownParts=true. Những giới hạn này không được đóng bằng sốtests đãviết.

## Một bước tiếp theo

Coordinator mở lượt typedtransaction/H1 theo exactpaths không chồngB/C/PR4 và đườngbuild/CI thực sự, rồi hoànthiện phần cònthiếu trên HEADkếtiếp trướcnghiệmthu chung. Chi tiếtvà lệnhci.yml đã cậpnhật CHÍNHcomment5583713687; khi testcheckpoint197 cầnactualrun.head_sha đúng197, khôngdùngCIparent hoặcsourceđãđổi. Việcghi bàn giao không chứngminhcoordinatorđãnhậnviệc.

Không sửaplan/main/develop/root/sharedstatus/host/structuralcontrollers/Core/history/workflows/PR1/PR4 hoặc scheduler; không merge/Ready/publish.

## Lịch sử và rollback

[Receipt batch006 đầy đủ](https://github.com/HoangHung997/NeraSpreadSheet/blob/8adeb53c4c137f0140b54b4120ed0d6a69ff06d1/docs/worklog/CHATGPT_CLIPBOARD_VIEW_STATE_PROGRESS.md) giữ113methods vàscope cũ. Rollbackbatch007 bằngrevert197d6660 vềf75308e5, khôngworkbookmigration nhưng bỏownership/viewport/bindingfixes. KiểmđúngHEADsaurollback, khôngcoirevertlàtựđộngantoàn.
