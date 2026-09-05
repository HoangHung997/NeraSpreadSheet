# Tích hợp RIBBON-VISUAL-011 — 05/09/2026

## Phạm vi và trạng thái

- Owner: tác nhân tích hợp; yêu cầu người dùng: tích hợp lane Ribbon đã hoàn thành.
- Nhánh đích: `feature/bootstrap-architecture-v0.1`; PR #1 Draft, open,
  unmerged, base `develop`. Không merge PR hoặc đổi trạng thái Draft.
- Mốc trước tích hợp: `284ccb76e5f69e170356ffc66915dd6e290b68fb`.
- Lane nguồn: `feature/ribbon-visual-011`, SHA cuối
  `7cfcfdfc6f337da1b37ca05b9254b903a33f32d2`.
- Đã fast-forward đúng chín commit; không conflict, không cherry-pick lại
  cùng thay đổi và không thêm đường production code mới.
- `TABLE-005` chưa được nhập. Không sửa worktree của lane đó, không giao task
  mới và không thay đổi file workbook hoặc app demo bên ngoài repository.

## Review

- Layout tiếp tục là model host-neutral duy nhất; gallery preview bất biến,
  bị chặn kích thước/enumeration và không chạy callback trong layout.
- WPF/WinForms/MAUI dùng bounds chung; command identity, dispatcher và
  customization persistence được giữ nguyên. Không thay workbook calculation,
  worksheet rendering/scrolling, Table mutation hoặc OpenXML.
- MAUI bỏ height-only/stale resize rebuild; regression giữ focus, popup và
  command execution count. API constructors cũ và caption/icon fallback có tests.
- CI chỉ thêm capture từ workbook mẫu sinh trong bộ nhớ. Không upload ảnh
  Excel thật, dữ liệu cá nhân hoặc đường dẫn máy của người dùng.
- File trọng tâm: `RibbonResponsiveLayout.cs`, `RibbonGalleryPreview.cs`,
  ba Ribbon presenters/chrome, customization dialogs, WPF `RibbonPreviewWindow`
  và `scripts/capture-ribbon-visual.ps1`.

## Kiểm tra tại checkout tích hợp

| Cổng | Kết quả |
| --- | --- |
| Full solution + WPF preview Release build | 0 warning / 0 error |
| Core solution regression | 1386/1386 |
| MAUI presenter tests | 41/41 |
| Windows desktop/runtime regression | 74/75 local |
| Architecture verifier | pass |
| SDK packaging verifier | pass |
| Core SDK pack | 18 nupkg |
| SDK runtime visual capture | 176 ảnh, 128 native layout snapshots, success |

Windows test duy nhất đỏ là
`PublicWpfScrollBarDragMovesOnlyBottomRightPaneAndPersistsState`, dừng ở
`Assert.IsTrue(window.Activate())` trước SDK behavior. Đây là hạn chế
foreground đã có ở baseline và lane nguồn; không xóa test hoặc nới assertion.
Full CI ở checkpoint dưới đây chạy đủ Windows 75/75 thành công.

Build/Core/Windows/capture dùng SDK 10.0.302; local MAUI dùng SDK 10.0.201
cùng workload đã cài, không đổi `global.json`. GitHub dùng SDK của repository.
Artifacts local được Git ignore, dưới `artifacts/ribbon-visual-integration/`.
Đã xem ảnh đại diện Trang đầu và Thiết kế Bảng; benchmark trước/sau của chính
implementation được giữ tại [responsive contract](../ribbon-responsive-layout-contract.md).
Không chạy lại benchmark cho thay đổi tích hợp chỉ fast-forward và tài liệu.

## Evidence GitHub đã xác minh

Tại đúng SHA `7cfcfdfc6f337da1b37ca05b9254b903a33f32d2`:

- [Full CI #1324](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/33945704736): success;
- [iOS #145](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/33945705984): success;
- [Q003C/OpenXML #142](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/33945707221): success.

Đây là implementation SHA được đưa nguyên vẹn vào nhánh tích hợp. Commit
đồng bộ tài liệu sau đó cũng phải có đủ ba workflow xanh với `head_sha` bằng
`git rev-parse HEAD` trước bàn giao; kết quả cuối ghi trên PR #1. Không coi
checkpoint cha là evidence cho commit mới.

## Giới hạn và rollback

- Table Style gallery trong preview chỉ chọn mẫu xem trước. Khi tích hợp
  TABLE-005, gắn thumbnail vào cùng command/style handler của lane đó.
- Raster export scale không tương đương physical multi-monitor DPI testing.
- SDK có preview mới; app demo đóng gói ngoài repository chưa được cập nhật.
- Rollback bằng commit revert, theo thứ tự ngược từ docs, MAUI lifecycle fix,
  preview/customization/native presenters đến layout Core. Không reset nhánh
  tích hợp, không revert thay đổi TABLE-005 và phải chạy lại ba cổng CI.

## Bước tiếp theo duy nhất

Sau khi HEAD tích hợp cuối xanh, đọc và review handoff TABLE-005 để chuẩn bị
tích hợp Table Design thực, giữ command identities và chỉ một mutation path.
