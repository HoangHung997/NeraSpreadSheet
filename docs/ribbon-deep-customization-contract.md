# Contract SDK tùy biến sâu Ribbon

## Phạm vi RIBBON-010

`RibbonCustomizationSession` là editor host-neutral duy nhất cho WPF, WinForms và
MAUI. Session tiếp tục dùng `RibbonDefinition`, `RibbonCustomization` và
`RibbonRuntimeController` hiện hữu; không tạo workbook model, command dispatcher
hoặc presenter model song song.

SDK hỗ trợ command catalog phân nhóm; tạo, đổi tên, xóa và sắp xếp tab/group tùy
biến; chuyển command giữa group; đổi large/small; và thêm, xóa, sắp xếp Quick
Access Toolbar (QAT). ID tab, group và command so khớp không phân biệt hoa thường.
Custom ID không được va chạm definition, command placement và QAT không được trùng.
Catalog gom command đã nằm trên Ribbon theo tab nguồn và đưa command ứng dụng đã
đăng ký nhưng chưa được đặt vào nhóm **Lệnh khác**; cùng command xuất hiện trên nhiều
surface vẫn chỉ có một catalog entry.

## Transaction và policy

Mutation chỉ đổi working profile. `Preview` tạo definition bất biến để host xem
trước; `Commit` nâng working profile thành rollback point; `Cancel` khôi phục đúng
rollback point; `Reset` xóa override có chủ ý. Dialog WPF/WinForms và binding MAUI
dùng cùng semantics. Preview có thể dựng lại cây Ribbon nhỏ nhưng không nằm trên
worksheet scroll/render frame và không tạo control theo ô.

`RibbonCustomizationPolicy` do ứng dụng sở hữu khóa tab, group, command, QAT,
import/reset hoặc việc tạo tab/group. Policy được kiểm tra trước mutation. Import
được kiểm tra toàn profile trước khi thay working state, nên lỗi policy không để
session ở trạng thái nửa cũ nửa mới.

## Persistence và module tùy chọn

Schema hiện hành là `neraspreadsheet.ribbon-customization`, version `2`. Version 2
bổ sung caption/custom identity, command placement và QAT. Codec vẫn đọc legacy-v0
không header và version 1, sau đó `MigrateToCurrent` xuất canonical version 2.
Version tương lai bị từ chối.

Unknown tab/group/command/QAT IDs được giữ nguyên trong profile để module tùy chọn
có thể biến mất rồi xuất hiện lại. Unknown command không được materialize thành nút
disabled giả trong custom group khi module chưa nạp. `Reset` là thao tác duy nhất
cố ý xóa unknown overrides.

Payload giữ giới hạn 1 MiB, JSON depth 64 và tối đa 10.000 node kể cả QAT. Apply,
catalog projection và serialization chỉ duyệt cây Ribbon/profile hữu hạn. QAT key
tip mới dùng allocator deterministic tối đa bốn ký tự và kiểm tra prefix collision.

## Presenter, bàn phím và accessibility

WPF/WinForms dialog có nút **Áp dụng** và **Hủy** với automation ID ổn định; danh
sách, tùy chọn visibility/size, nút sắp xếp và reset tiếp tục dùng keyboard native.
MAUI cung cấp cùng public operations qua
`NeraMauiRibbonCustomizationBinding`; ứng dụng có thể ánh xạ chúng vào visual shell
phù hợp từng form factor. Ribbon sau preview/apply tiếp tục dùng stable key tips,
focus restoration và automation identity của RIBBON-009.

Rollback tích hợp: revert commit RIBBON-010. Profile v1 vẫn đọc bằng stack cũ;
profile v2 phải được giữ làm backup hoặc migrate ngược ở tầng ứng dụng nếu rollback
sau khi người dùng đã lưu deep customization.
