# Contract shortcut bàn phím cho Ribbon và Bars

## Phạm vi RIBBON-KEYBOARD

- `CommandShortcut` chuẩn hóa chord độc lập nền tảng;
- `CommandShortcutMap` lập chỉ mục command đang hiện trong từng Ribbon/Bar và từ chối
  chord trùng giữa hai command khác nhau;
- runtime Ribbon/Bars resolve và activate shortcut qua cùng `CommandDispatcher`;
- WPF và WinForms presenter cho phép bind shortcut vào input root của ứng dụng;
- WinForms menu gán `ToolStripMenuItem.ShortcutKeys` thật, không chỉ display string.

## Chuẩn hóa

Parser không phân biệt hoa thường, chấp nhận `Ctrl`/`Control` và
`Cmd`/`Command`/`Meta`/`Win` làm alias. Dạng canonical luôn theo thứ tự
`Ctrl+Alt+Shift+Meta+Key`. Chord rỗng, thiếu key, lặp modifier hoặc có nhiều key bị
từ chối. Descriptor có shortcut sai làm surface fail-fast khi tạo snapshot.

Một command xuất hiện nhiều nơi trên cùng surface được phép dùng lại chord. Hai
command khác nhau có cùng chord bị từ chối thay vì chọn command theo thứ tự tình cờ.

## Visibility, state và execution

Map chỉ chứa command đã đăng ký và đang có trong effective definition. Khi
customization ẩn command, snapshot kế tiếp loại shortcut đó. Activation vẫn gọi
`TryActivateAsync`, nên dispatcher kiểm tra `CanExecute` tại đúng thời điểm và command
disabled không chạy. Execution thành công refresh snapshot như click chuột.

## Host binding

`BindShortcuts` nhận input root do ứng dụng sở hữu (thường là `Window` WPF hoặc
`Form` WinForms) và trả về `IDisposable`. Dispose presenter/control cũng tháo mọi
binding đã tạo. WinForms tạm bật `Form.KeyPreview`; các binding cùng Form chia sẻ
reference count và chỉ khôi phục giá trị ban đầu khi binding cuối được dispose.
Lỗi activation đi qua cùng event boundary với click.

WPF/WinForms/MAUI bỏ qua event đã Handled; binding nhận chord phải claim trước
await activation để multicast không chạy command hai lần. Disabled command đã
resolve vẫn được claim. Bar binding không hỗ trợ KeyTips phải nhường Alt cho
Ribbon; Ctrl+Alt không được dùng làm tín hiệu mở KeyTips. Escape theo từng scope
và focus restoration của presenter hiện hữu vẫn được giữ.

Không có global OS hotkey, không hook bàn phím ngoài process và không chiếm chord
không thuộc surface. MAUI dùng `INeraMauiShortcutSource`/`NeraMauiShortcutBinding`
hiện hữu. Native role/focus tests và synthetic key input được phân biệt với
screen-reader hoặc hardware acceptance trong [UX-007](ux-007-keyboard-accessibility-contract.md).
