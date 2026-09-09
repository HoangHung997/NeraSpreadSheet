# Clipboard dùng chung — C1/C2, batch 09/09/2026

## Trạng thái và thứ tự kiểm thử

Queue `CHATGPT-CLIPBOARD-VIEW-20260908`, source trước batch
`709f69f9c819d45ceb7a12646929faf1052d5a8b`.

Chỉ đạo trực tiếp ngày 09/09/2026: giữ các mục chưa nghiệm thu ở trạng thái
pending, triển khai C1/C2/V1/V2 trước, sau đó chạy CI trên HEAD kết hợp.
Không chờ C1 CI xanh mới viết C2; không dispatch CI từng bước. Đây không phải
miễn kiểm thử hoặc cho phép thay assertion/workflow. Source/test có trong PR
không đồng nghĩa đã build, chạy test, tích hợp hoặc nghiệm thu UI.

Receipt C1 trước batch được bảo toàn tại progress commit
`9ef916c9c3f365f00829b431e22b72147f4ea25e`. 78 test C1 đã bổ sung trước batch
được giữ nguyên; không gọi số test đã viết là số PASS. H1 và các host vẫn ngoài
quyền ghi của worker trong batch này.

## C1 — Các bảo vệ được giữ

Cut chỉ nhận một vùng, từ chối trước mutation khi editor đang mở, worksheet đã
bị xóa, phạm vi quá lớn, chọn một phần merge/spill hoặc policy không cho phép.
Không sửa ClearSelection thành chỉ xóa vùng đầu để che lỗi multi-range.

`CopyToClipboardAsync` dùng package builder và session/history hiện có. Host
cung cấp callback ghi/flush clipboard thật. Trước acknowledgement không publish
package mới và không xóa nguồn. Sau acknowledgement phải còn đúng worksheet
instance/name/membership, workbook/worksheet/dimensions/selection/view versions,
merge snapshot và selection. Đổi sheet/editor rồi quay lại vẫn invalidates.

`CutAuthorization` được hỏi trước transport và trước commit; callback phải thuần.
Quyền, busy, invalidation và cancellation được dùng chung giữa các controller
thuộc cùng session. Controller phụ không mở writer slot riêng; session khác độc
lập. Khi yêu cầu hủy, busy chỉ hết lúc transport thực sự kết thúc, kể cả transport
bỏ qua cancellation. Hủy không xóa clipboard OS hoặc recovery package.

API đồng bộ vẫn là clipboard trong session, không tự ghi OS. C1 chưa có nghiệm
thu native/OS/protection đầu-cuối; giữ recovery package khi observer ném lỗi
không phải bảo đảm rollback mọi lỗi của history, recalculation hoặc subscriber.

## C2 — Một pipeline Paste và bốn chế độ thật

`Paste(destination)` và lệnh `Edit.Paste` gọi chế độ All. Các overload với
`SpreadsheetClipboardPasteMode` và các lệnh PasteValues/PasteFormulas/PasteFormats
cùng gọi một đường preflight → edit operation → Session.Execute/history.
Không có workbook, clipboard engine hay history song song.

| Chế độ | Giá trị/công thức | Style trực tiếp của ô | Merge và validation |
|---|---|---|---|
| All | Chép value và formula; dịch A1 tương đối, giữ phần tuyệt đối/string literal | Chép từ nguồn | Thay merge được phủ trọn; chuyển validation bị giao với vùng nguồn, giữ phần rule ngoài vùng đích |
| Values | Chỉ value cached tại lúc Copy, không giữ formula | Giữ style đích | Giữ metadata đích; không ghi sai vào merge không tương thích |
| Formulas | Chép formula hoặc literal của nguồn; cùng translator với All | Giữ style đích | Giữ metadata đích; spill tái sinh từ owner |
| Formats | Giữ nguyên value/formula đích; không chạy recalculation | Chép style nguồn, kể cả reset về default | Giữ metadata đích |

Blanks được ghi thật, không tự Skip blanks. Values dùng snapshot cached riêng
cho cả spill children trong cùng package; không tính lại workbook lúc Copy.
Formula text nhập từ bên ngoài không có cached value: Values bị vô hiệu hóa và
API từ chối thay vì tự biến thành ô trống. TSV formula giữ nguyên địa chỉ khi
paste, không dịch như package nội bộ.

Chỉ sao chép style trực tiếp của ô; không chuyển kích thước hàng/cột, axis-style
spans hoặc metadata toàn worksheet. Bulk Paste chuyển/bảo toàn validation rules
như bảng trên, không gọi cơ chế cảnh báo của cell editor cho từng giá trị dán.
Transpose và Skip blanks **chưa hỗ trợ**, không có command/menu giả cho chúng.

## Preflight, merge, history và quyền ghi

Đích luôn là một rectangle theo tọa độ destination và kích thước package; các
vùng chọn bổ sung không làm tăng phạm vi được ghi. CanPaste cũ giữ nghĩa có
payload nội bộ sử dụng được; command-state dùng CanPasteSpecial để xét thêm
editor, membership, bounds và chế độ. CanCopy/CanCut chặn nhiều vùng tại command
boundary. Query trạng thái không tự thực thi callback quyền do ứng dụng cấp.

Preflight kiểm bounds/materialization, editor, worksheet identity, spill, merge
và `PasteAuthorization(worksheet, range, mode)`. Quyền callback không phải model
SheetProtection và null không chứng minh workbook không bảo vệ. Recheck lease
sau callback; callback tự đổi dữ liệu không được khiến paste ghi vào nguồn mới.

Partial merge bị từ chối trước mutation. All tạm bỏ các merge được phủ trọn để
SetCellsOperation không redirect interior writes lên anchor, sau đó phục hồi
metadata trong cùng history entry. Merge đích mới không được giao table/filter.
Values/Formulas/Formats không ghi vào các merged ranges đích không khớp nguồn.

Validation rules được clip/translate với anchor mới; phần rule ngoài đích được
trừ bằng các rectangle sparse, không materialize toàn sheet. Rule nguồn được
cấp ID mới; Undo/Redo giữ đúng snapshot trước/sau. Lỗi preflight không thêm
history hoặc đổi ô. Metadata transaction có recovery; lỗi recovery tiếp theo
được báo cùng lỗi gốc. Không tuyên bố atomicity cho callback tùy ý hoặc lỗi
recalculation xảy ra sau khi history đã nhận operation.

## State, clipboard OS và kết quả trả về muộn

`SpreadsheetClipboardState` tách source workbook/worksheet/range và PayloadId
khỏi selection hiện hành; có version, operation, busy, copy mode, status và lỗi.
StateChanged cho phép adapter cập nhật chrome; host vẫn phải theo dõi selection,
editor và lifecycle để query command availability đúng lúc.

`CancelCopyMode()` dừng đánh dấu nguồn, giữ payload và không clear OS clipboard.
Editor đang mở được ưu tiên; không đăng ký Esc toàn cục trong command catalog.
IME/popup và native input arbitration thuộc H1.

`PasteFromClipboardAsync` luôn nhận kết quả đọc thật từ callback host, không tự
thay clipboard OS không đọc được bằng package cũ. Chỉ dùng lại private package
khi đã có acknowledgement, PayloadId/text khớp và generation chưa bị mất quyền
sở hữu. `NotifyExternalClipboardChanged` phải được host gọi khi OS ownership
thực sự đổi; invalidates private leases của session nhưng không bỏ recovery data.

Read thất bại/hủy hoặc source context thay đổi trong khi await không ghi ô.
Pending writer/reader cùng dùng session gate. Callback phải giữ owning context;
controller không thread-safe và gate không phải khóa OS toàn ứng dụng. Trạng
thái ownership là lease do host xác nhận, không phải polling clipboard tự động.

Stale/empty Cut trả false không có nghĩa OS clipboard chưa đổi. Command catalog
hiện gọi pipeline nội bộ; nối native transport và phản hồi lỗi từng UI vẫn H1.

## Kiểm thử và bàn giao

Thêm `ClipboardPasteSpecialTests`: các mode, cache/spill, blanks/styles,
merge anchor, validation Undo/Redo, quyền/bounds, nguồn copy độc lập selection,
đọc chậm/hủy/ownership replacement và native-token/text checks. Các ca dùng
transport điều khiển bằng TaskCompletionSource, không là bằng chứng OS thật.

Chưa có .NET build/analyzers, regression execution, native smoke hay benchmark
của batch trong môi trường worker. Kiểm hash/diff/lexer không thay compiler.
Không sửa renderer/scroll loop; overhead snapshot/merge/rule cần đo ở gate phù hợp.

Sau khi source C1/C2/V1/V2 ổn định, chạy build/Editing/OpenXML/Core/architecture
và CI hiện có đúng SHA kết hợp; giữ full suite và mọi assertion. CI cũ không thay
CI mới. H1, actual protection, downstream failure atomicity và nghiệm thu native
vẫn được ghi pending riêng, không đóng bằng test shared.

Rollback batch bằng revert commit batch trong PR #5; không migration workbook.
Metadata native mới chỉ thêm fields tùy chọn vào format version 1. Revert các
bản C1 cũ riêng biệt có thể đưa nguy cơ mất dữ liệu multi-range trở lại.

## Rà soát batch007 — quyền sở hữu không đồng nghĩa dữ liệu phục hồi

Một lần thử ghi OS có thể đã thay clipboard trước khi flush thất bại. Vì vậy,
ngay trước khi gọi transport, controller tăng generation quyền sở hữu OS dùng
chung trong session. Gói cũ vẫn giữ để phục hồi, nhưng stamp cũ không còn được
nhận là một lần ghi OS còn hiệu lực. Ghi thành công từ controller phụ cũng làm
stamp của controller cũ mất hiệu lực mà không xóa package trong bộ nhớ.

Khi callback đọc thành công trả về dữ liệu khác payload/token/text hiện hành,
hoặc không có dữ liệu văn bản, các lease private cũ bị vô hiệu hóa ngay. Việc này
xảy ra trước parse, Values hoặc PasteAuthorization: lỗi/deny sau đó không được
vô tình bật lại fallback private cũ. Một lần đọc lỗi trước khi có observation
không bị nhận là bằng chứng clipboard đã đổi. Dữ liệu external được dán thành
công có thể làm payload nội bộ mới, nhưng không được nhận HasOsOwnership=true;
đọc clipboard không phải ghi clipboard.

Thêm 10 tests ClipboardOwnershipRegressionTests cho các trường hợp trên,
bao gồm controller phụ, transport thất bại, stale acknowledgement, formula
translation và history. Đây là test code, CHƯA CHẠY. Không suy nghiệm thu OS
thật hoặc khóa liên-session từ các fake transport này. Các giới hạn H1, actual
protection và downstream transaction nêu trên vẫn OPEN.
