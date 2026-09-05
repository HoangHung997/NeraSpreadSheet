# Phạm vi lệnh của bản demo Ribbon

Đối chiếu nguồn tại baseline `50cb357a`: **49 lệnh session SDK** và **36 lệnh
host của app mẫu**, tổng cộng 85 định danh duy nhất. Nhiều tab/QAT có thể dùng
lại cùng lệnh. Số này không phải số tính năng Excel đã tương thích, cũng không
chứng minh mọi public API của SDK đã có nút trên Ribbon.

## Lệnh SDK dùng được từ ứng dụng khác

Các định danh dưới đây được đăng ký bởi session và được đặt trên Ribbon mẫu.
SDK dùng cùng command handler, workbook và lịch sử chỉnh sửa; app tích hợp có
thể bố trí lại qua definition/customization, không cần sao chép workbook model.

| Nhóm | Định danh lệnh SDK |
| --- | --- |
| Chỉnh sửa | `Edit.Undo`, `Edit.Redo`, `Edit.Copy`, `Edit.Cut`, `Edit.Paste`, `Cell.ClearContents` |
| Định dạng và gộp ô | `Cell.Format.Bold`, `Cell.Format.Italic`, `Cell.Merge`, `Cell.Unmerge` |
| Tính và sắp xếp | `Formula.RecalculateWorkbook`, `Data.SortAscending`, `Data.SortDescending` |
| Hàng/cột | `Structure.Row.Insert/Delete/Hide/Unhide`, `Structure.Column.Insert/Delete/Hide/Unhide` (mỗi hậu tố là một lệnh riêng) |
| Khung nhìn | `View.FreezePanes`, `View.UnfreezePanes`, `View.Split.Undo`, `View.Split.Redo` |
| Biểu đồ và Pivot | `Insert.Chart.Column/Bar/Line/Pie`, `Insert.Pivot.Sum` |
| Tạo và định nghĩa Table | `Table.Create`, `Table.Rename`, `Table.Resize`, `Table.ConvertToRange` |
| Kiểu Table | `Table.HeaderRow`, `Table.TotalsRow`, `Table.FirstColumn`, `Table.LastColumn`, `Table.BandedRows`, `Table.BandedColumns`, `Table.FilterButtons`, `Table.Style` |
| Dữ liệu Table | `Table.CalculatedColumn`, `Table.TotalsFunction`, `Table.Row.Insert/Delete`, `Table.Column.Insert/Delete`, `Table.RemoveDuplicates` |

## Lệnh host của app mẫu

Các lệnh `Sample.*` có thực thi API thật nhưng **không phải stable built-in
session command IDs**. NuGet consumer muốn có chúng phải tự nối action/dialog
của host như app mẫu. Chúng không tự được thêm vào app chỉ vì reference SDK.

| Nhóm | Lệnh trong demo và giới hạn |
| --- | --- |
| Font/màu/căn chỉnh (10) | `Sample.Font`, `FontSize`, `Underline`, `Fill`, `FontColor`, `Align.Left/Center/Right`, `Wrap`, `Borders`: thay style của selection; Borders hiện chỉ viền mảnh bốn cạnh, không phải hộp Format Cells đầy đủ |
| Số (3) | `Sample.Number`, `Percent`, `Decimal`: Number có General, số nguyên, hai số thập phân, phần trăm, `dd/mm/yyyy`; Decimal đặt hai số thập phân, không tăng từng bậc |
| Bộ lọc (3) | `Sample.Filter`, `FilterClear`, `FilterReapply`: mở presenter SDK hoặc gọi Table/worksheet filter thật; cần target hợp lệ |
| Công thức (5) | `Sample.FormulaHelp`, `FormulaSum`, `FormulaAverage`, `FormulaIf`, `FormulaLookup`: Help hiện mở hướng dẫn SUM; bốn nút còn lại bắt đầu draft trong editor, không tự suy vùng dữ liệu như AutoSum Excel |
| In (6) | `Sample.Orientation`, `Paper`, `Margins`, `PrintGrid`, `PrintHeadings`, `PrintPreview`: demo có A4/A3, lề thường/hẹp; preview dùng print area nếu có, nếu chưa đặt thì fallback **A1:E33**, chưa phải tự chọn toàn used range hoặc hộp thoại in máy in |
| Kiểm tra (2) | `Sample.Statistics`, `Errors`: thống kê sheet đang active và tối đa 100 ô lỗi; không phải kiểm định toàn workbook hoặc formula auditing Excel |
| Hiển thị (4) | `Sample.Gridlines`, `Headers`, `Zoom`, `ZoomReset`: thay presentation của host, không thêm workbook Undo |
| Tệp (3) | `Sample.New`, `Open`, `Save`: New mở dữ liệu mẫu trong cửa sổ mới; Open giữ full shell với session đã import; Save là **lưu bản sao** bằng file tạm rồi thay đích đã chọn, không phải tự lưu vào file nguồn |

Tên viết rút gọn trong bảng vẫn có tiền tố `Sample.`. Ví dụ `FontSize` là
`Sample.FontSize`; `Align.Left/Center/Right` là ba định danh riêng.

## Vì sao có lệnh bị ẩn hoặc không bấm được?

- Thiết kế Bảng chỉ xuất hiện khi selection nằm trong Table. Đổi tên/kiểu/
  công thức Table yêu cầu Table/cột tương ứng; xóa hàng không áp dụng cho hàng
  tiêu đề hoặc tổng, xóa cột không được làm Table mất cột cuối cùng.
- Totals Function cần Table đã bật hàng tổng và cột hợp lệ. Các lệnh cần tham
  số phải qua dialog/validation, Cancel không sửa workbook.
- Undo/Redo cần lịch sử tương ứng; Paste cần dữ liệu clipboard hợp lệ; Clear
  Contents cần nội dung trong selection; Unfreeze cần trạng thái đã freeze;
  Split Undo/Redo cần lịch sử split. Lệnh hiện có không đồng nghĩa luôn enabled.
- Filter Clear/Reapply cần target lọc. Nút Mở bộ lọc hiện vẫn có thể bấm ngoài
  target và báo hướng dẫn trên status bar; không tạo bảng/lọc giả để báo thành công.
- Chưa có thông báo lý do disabled riêng, đầy đủ cho từng nút. Bảng giải thích
  này không thay nghiệm thu tooltip/accessibility/focus của UI.

## Cổng kiểm tra và phần còn mở

`Release009RibbonCommandSmokeTests` bổ sung audit registry/placement cho toàn
85 định danh, kiểm context disabled và loaded standalone shell thực thi 15
lệnh style, 5 thiết lập in, 4 draft công thức, 4 lệnh hiển thị. Các mutation
được kiểm giá trị thực và đúng một Undo; view/draft Cancel không đổi dữ liệu.
Chỉ được gọi các test này là PASS khi CI tại đúng source của artifact xanh.
Đây là loaded runtime dispatch, không phải thao tác bàn phím hệ điều hành trên
mọi nút; Table dialogs/gallery/filter có các native regression riêng.

R1 vẫn mở cho walkthrough toàn surface, lý do disabled và các kết nối tới
active split editor/filter. R2 còn editable formula bar, worksheet scrollbars,
loaded split và hoàn thiện đóng gói tại source kết hợp. Bản thân preview chưa
có nút thêm/đổi tên/xóa sheet, toàn bộ Format Cells hoặc mọi public API SDK.
Không coi icon/caption, registered handler, source build hoặc ảnh capture là
bằng chứng tương thích đầy đủ Excel. Xem README và CURRENT tại đúng SHA để
biết phần nào đã được tích hợp sau checkpoint này.
