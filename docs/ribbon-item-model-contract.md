# Contract full Ribbon item model

## Phạm vi RIBBON-008

`RibbonItemDefinition` là model host-neutral duy nhất cho button, toggle, split
button, dropdown, menu, combo box, gallery, color picker và separator. WPF,
WinForms và MAUI chỉ ánh xạ cùng `RibbonPresentationSnapshot` sang native chrome;
không host nào tự tạo item model hoặc state song song.

Constructor ba tham số cũ tiếp tục tương thích: command có checked state được
hiển thị như toggle. Definition mới dùng `RibbonItemKind` tường minh; button tường
minh không tự đổi kind chỉ vì command có checked state. Separator có stable identity
nhưng không dispatch command.

## State bất biến

`CommandState` chứa enabled, checked, display text, selected value và items source.
Constructor materialize items source, và từng `CommandItem` materialize cây con,
để snapshot cũ không thay đổi khi collection nguồn bị sửa. Item hỗ trợ caption,
value, enabled/checked, tooltip, icon key và cây menu lồng nhau.

Mỗi projection vẫn query một command đúng một lần và chia sẻ cùng
`CommandPresentation` khi command xuất hiện nhiều nơi. Presenter không sửa state;
selection thành công làm runtime refresh snapshot như button activation.

## Activation và lỗi

Button/toggle và phần chính của split button gọi `TryActivateAsync`. Lựa chọn từ
split dropdown, menu, combo, gallery hoặc color picker gọi `TryActivateItemAsync`.
Handler nhận `RibbonItemActivation`, gồm selected value và parameter gốc do
`CommandContextFactory` cung cấp. Cả hai đường đều đi qua `CommandDispatcher`, giữ
nguyên CanExecute, cancellation, refresh và exception semantics.

Presenter chuyển exception qua `CommandActivationFailed`; nếu không có boundary
handler thì exception không bị nuốt. Shortcut của item gốc dùng cùng map runtime.

## Measurement, accessibility và overflow

Item có thể cung cấp `RibbonItemMeasurementCallback`. Callback nhận kind, size,
command snapshot và default logical width; kết quả phải hữu hạn, không âm và chỉ
được nhân scale một lần. Callback chạy trong layout snapshot, không nằm trên đường
scroll worksheet.

Tooltip ghép shortcut và fallback về caption. Automation name dùng override của
definition rồi fallback về caption. Icon không resolve được luôn giữ caption.
Overflow giữ action chính hoặc toàn bộ selectable values; separator giữ vai trò
phân cách. Focus identity tiếp tục dùng stable command ID qua rebuild/resize trên
ba host.

## Ranh giới

- Ribbon.Core không reference type WPF, WinForms hoặc MAUI.
- Không thêm package và không tạo control theo ô worksheet.
- RIBBON-008 không thay đổi Table/Filter model, OpenXml hoặc Filter popup.
- Contextual tabs, QAT, key tips và deep customization thuộc RIBBON-009/010.
