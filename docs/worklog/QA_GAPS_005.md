# QA-GAPS-005 — đối chứng và sửa các khoảng trống còn lại của QA cũ

Nguồn bắt đầu: `main` `54067a37f7d91ab8c0ae42671e39bfef9c9ea273`. Báo cáo QA người dùng cung cấp được dùng để đối chứng, nhưng file/workbook riêng không đưa lên repository hoặc artifact. Avalonia tiếp tục là UI chính; WPF/WinForms/MAUI chỉ giữ maintenance gates, không được nhận là đã sửa cùng phạm vi nếu không có bằng chứng riêng.

## Finding cũ và hướng sửa

- **Alt+Enter / Ribbon KeyTips:** key-tip binding không còn vào mode ngay ở Alt-down. Bare Alt bật/tắt ở KeyUp; Alt+Enter để editor/formula TextBox xử lý newline. Alt+letter/digit vẫn đi vào keytips. Đây là native routed-event coverage, chưa phải physical keyboard/IME proof.
- **View state từng sheet:** thêm `NeraWorksheetViewStateBinding` dùng `SpreadsheetWorksheetViewState`/split state hiện hữu. Avalonia FullShell nối binding; selection vẫn do session restore, split/freeze vẫn do shared controller; host reapply zoom + pane scroll sau worksheet activation và không tạo workbook/history edit. Không tạo một cache view mới theo tên sheet.
- **Table/Filter:** sample đăng ký command Table an toàn từ session registry; contextual Table Design dùng `RibbonProductionCommandCatalog` hiện hữu. Native Filter window dùng `SpreadsheetAutoFilterPagedPresenter`, page 100, search/paging/select/apply/clear/sort; không scan toàn cột thành control và không tạo filter engine thứ hai.
- **Zoom 110% bị trống:** ComboBox chèn item tạm cho `SelectedValue` hợp lệ ngoài preset; `Ui.Zoom` hiển thị `%`. Host/dialog Avalonia dùng cùng miền zoom 10%–400% với worksheet view model.
- **Hide/Unhide:** Home/Ô có dropdown `Ui.CellsFormat` với Ẩn/Hiện hàng và cột, gọi lại `Structure.*` hiện hữu và giữ command state/Undo. Không nhân đôi transaction.
- **Responsive Ribbon:** native QA smoke chạy 768/820/1024/1366/1920 DIP ngoài ma trận cũ; tiếp tục dùng compact/overflow và pixel-budget. Không nhận pixel parity với Excel.

## Acceptance

Bắt buộc:

1. Build/analyzers/architecture không warning/error mới.
2. `QaGapRegressionTests`: Alt+Enter, bare Alt, dynamic zoom, A→B→A selection/scroll/zoom và Undo count.
3. Native `--qa-gaps-smoke`: năm width, 110%, Table Create→contextual tab, Filter window/paging/criteria/reapply/clear, Hide/Unhide, save/reload.
4. Mọi ảnh QA vào `artifacts/.../qa-gaps`; Check out packaged app phải chứa log, manifest và ảnh này. `Check out/Images`/evidence gallery tiếp tục gom same-run artifacts; không dùng ảnh commit cha.
5. Exact-head GitHub Actions trên branch; sau review ảnh mới được merge. Sau merge phải chạy fresh `main` Check out trước khi cập nhật `check-out-latest`.

## Không nhận trong checkpoint này

- Không sửa hoặc tuyên bố WPF/WinForms/MAUI per-sheet binding hoàn tất.
- Không hoàn tất locale no-op editor/TSV, full Excel CF rendering, inner-border rendering, XLS/XLSB/CSV readers hoặc macro execution.
- Filter UI là basic paged value/search/sort surface; không nhận full Excel Date/Color/Top10/custom-filter dialog parity.
- Table parameterized workflows Rename/Resize/CalculatedColumn vẫn cần dedicated input UX trước khi đưa ra Ribbon; không đăng ký command yêu cầu tham số bằng nút mù.
- Native routed tests không thay physical keyboard/mouse, IME, multi-monitor DPI hoặc screen reader testing.

Rollback bằng reviewed revert; không force-push main. Tạm dùng branch `fix/qa-gaps-005`/PR #10, và xóa workflow task tạm trước integration.