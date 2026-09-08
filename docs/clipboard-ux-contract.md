# Contract Clipboard UX — checkpoint C1-A

## Trạng thái và phạm vi

Đợt `CHATGPT-CLIPBOARD-VIEW-20260908`, baseline
`2e80250b1b1c51ad56a415d1bbeb70d7b2e85f25`.

Checkpoint này chỉ chặn lỗi mất dữ liệu khi gọi
`SpreadsheetClipboardController.CutPrimarySelection()` với nhiều vùng chọn.
Đã có source và regression trong nhánh worker; build, test và exact-head CI
chưa được xác nhận tại thời điểm viết contract. Đây không phải toàn bộ C1,
Mục 2 hoặc Mục 3 đã DONE.

## Lỗi và quyết định

Ở baseline, `CopyPrimarySelection()` tạo package từ `Selection.Ranges[0]`,
nhưng `CutPrimarySelection()` gọi tiếp `SpreadsheetSession.ClearSelection()`,
vốn xóa các ô trong mọi vùng chọn. Với A1 và C1 rời nhau, C1 có thể bị xóa dù
không có trong package đã copy. Đây là kết luận đọc source, chưa có lượt chạy
regression red-before-fix trong môi trường worker.

Chọn từ chối Cut khi selection không có đúng một vùng, trước khi gọi Copy
hoặc Clear. Không hợp nhất ngầm các vùng kề nhau/chồng nhau, không copy một
vùng rồi xóa nhiều vùng, không sửa ClearSelection thành chỉ xóa vùng đầu.

Khi bị từ chối vì nhiều vùng:

- Phát `InvalidOperationException` theo kiểu lỗi validation hiện có.
- Không đổi bất kỳ ô, công thức, style, spill hoặc worksheet version nào.
- Giữ nguyên clipboard package cũ và CanPaste, kể cả clipboard đang rỗng.
- Không thêm/xóa lịch sử Undo/Redo; không đổi selection, anchor hoặc active cell.

`CopyPrimarySelection()` vẫn là API copy vùng đầu; checkpoint này không đổi
hành vi Copy hoặc Paste. Với đúng một vùng, Cut vẫn dùng đường Copy/Clear và
history hiện có; các kiểm tra spill và giới hạn materialization vẫn áp dụng.
Không thêm model, pipeline, dependency hoặc package mới.

## Ranh giới clipboard hệ điều hành

`CutPrimarySelection()` hiện là API đồng bộ cho clipboard trong session,
không tự ghi clipboard hệ điều hành. Guard nhiều vùng không chứng minh rằng
Cut của từng host đã an toàn trước OS clipboard failure hoặc completion muộn.

Các hạng mục sau vẫn OPEN, không được dùng checkpoint này để đóng:

- Ghi OS clipboard thành công trước khi xóa source; thất bại không làm mất dữ liệu.
- Guard workbook/sheet/selection/source version khi completion trả về muộn,
  đổi sheet hoặc cửa sổ, clipboard thay đổi từ ứng dụng ngoài và hủy/Esc.
- Preflight và tính nguyên tử đầy đủ với protection/merge; phạm vi spill có
  regression hiện có nhưng vẫn cần chạy lại trên source SHA mới.
- CanExecute/CanPaste và phản hồi lỗi trên host thực; Paste Special/copy border.

Không ghi WPF/WinForms/MAUI/Avalonia trong checkpoint này. Những thay đổi host
cần coordinator cấp path/API/baseline cụ thể theo file giao việc. Không lấy
việc shared API có source làm bằng chứng nghiệm thu UI/OS clipboard.

## Regression

Thêm 14 test methods trong `ClipboardSafetyTests.cs`:

1. Hai vùng rời nhau A1/C1 và history rỗng.
2. Đảo thứ tự chọn C1/A1.
3. Vùng đầu rỗng, vùng thêm có dữ liệu.
4. Giữ package nội bộ đã copy.
5. Giữ clipboard TSV đã import và kiểm khả năng Paste/Undo sau khi bị từ chối.
6. Giữ redo entry có sẵn.
7. Giữ undo entry có sẵn.
8. Từ chối các vùng chồng nhau.
9. Từ chối các vùng kề nhau.
10. Giữ dynamic-array spill thuộc vùng thêm.
11. Giữ công thức và style trong các vùng.
12. Cut một vùng vẫn copy đúng phạm vi, giữ ô ngoài vùng, Undo/Redo đúng một bước.
13. Cut một vùng rỗng vẫn trả false và không thêm history như baseline.
14. Partial-spill Cut vẫn từ chối và giữ clipboard cũ.

Helper của các ca từ chối so sánh toàn bộ used cells, worksheet identity/version,
selection version/active/anchor/ranges và clipboard identity/CanPaste trước-sau.
Không sửa, xóa, skip hoặc giảm assertion của các test cũ.

Lệnh kiểm tra khi có đúng SDK của `global.json`:

```powershell
dotnet restore NeraSpreadSheet.Core.slnx
dotnet build NeraSpreadSheet.Core.slnx -c Release --no-restore
dotnet test tests/NeraSpreadSheet.Editing.Tests/NeraSpreadSheet.Editing.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~Clipboard"
dotnet test NeraSpreadSheet.Core.slnx -c Release --no-build
./scripts/verify-architecture.ps1
```

Worker chưa chạy các lệnh này: container không có dotnet, không phân giải được
github.com để lấy môi trường. Kiểm SHA/diff hoặc cú pháp tĩnh không thay kết quả
C# build/test. Bằng chứng chạy thật phải ghi trên PR/progress với đúng final SHA.

`ci.yml` hiện có workflow_dispatch và tự trigger push/PR vào main/develop.
Không đổi base PR chỉ để ép CI, không sửa workflow/acceptance, không rerun CI
của baseline để nhận thay cho source mới. Khi nhánh worker không tự trigger,
cần coordinator dispatch workflow hiện có trên đúng ref và kiểm head_sha.

## Hiệu năng, bàn giao và rollback

Guard chỉ đọc số vùng chọn trước khi vào đường Copy/Clear cũ; không đổi
scroll/render, không thêm scan worksheet. Không có số đo benchmark mới và
không tuyên bố cải thiện hiệu năng. Các cổng runtime/benchmark khác vẫn áp dụng
khi bước tiếp theo thay đổi đúng phần đó.

Chỉ ba path của checkpoint: `SpreadsheetClipboard.cs`,
`ClipboardSafetyTests.cs` và contract này. Shared root status/worklog, nhánh plan,
PR #1/#4 và source host không bị thay đổi. PR giữ Draft, không merge/Ready/publish.

Rollback: coordinator revert commit implementation C1-A trong nhánh tích hợp;
không có migration workbook. Khi revert guard, lỗi Cut multi-range baseline
sẽ quay lại, nên không gọi rollback là trạng thái đã an toàn.
