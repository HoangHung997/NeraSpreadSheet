# RIBBON-VISUAL-011 — Ribbon dense layout và visual chrome

- Trạng thái: `IMPLEMENTATION VALIDATED` tại checkpoint exact-head dưới đây.
- Owner: Codex, GPT-6 Astra / max theo yêu cầu mới nhất.
- Branch: `feature/ribbon-visual-011`.
- Base SHA: `284ccb76e5f69e170356ffc66915dd6e290b68fb`.
- Checkpoint implementation trước: `57a8c0c0fe8eb452bcb054432d2d37b9e9807e73`;
  full CI #1318 / 33936291893, iOS #139 / 33936291863 và Q003C #136 /
  33936291978 đã được worklog tích hợp xác minh.
- PR #1 vẫn Draft và không thuộc thao tác merge của lane này.

## Phạm vi sở hữu

Ribbon.Core layout/item contract; Ribbon presenter và customization chrome của
WPF, WinForms, MAUI; Ribbon tests/benchmark; preview và script chụp ảnh trong
repository. Table calculation, style resolver, identity, mutation và OpenXML
không thuộc lane này.

Các phần chạy song song có file sở hữu riêng: dense-layout core/tests; WPF
presenter; WinForms/MAUI presenters. Primary agent sở hữu preview, catalog,
aggregate validation và tài liệu handoff.

## Đã triển khai

- Một layout snapshot chung chứa tọa độ item, row/span và caption nhóm ở đáy;
  ba hàng logical, icon large 32 px / small 16 px, caption tối đa hai dòng.
- Collapse ba phase có priority xác định; không tăng width khi đổi large sang
  small, giữ caption fallback và phần arrow 18 px. Không cuộn ngang main Ribbon.
- Gallery preview bất biến, tối đa 16 × 16 cell, dùng đúng callback host; ba
  presenter có visual thumbnail, selection và More. Popup selection vẫn đi qua
  runtime/dispatcher cũ, không có mutation path thứ hai.
- Bốn palette, icon QAT, Backstage rail/content tách chọn pane và thực thi;
  customization hai cột/search, hỗ trợ command chung nhiều tab và đóng an toàn
  khi constructor chưa xong. Resize/theme rebuild không giành worksheet focus.
- Preview runnable dùng workbook bán hàng sinh trong bộ nhớ, 30 command session
  có sẵn cùng host commands gọi API thật. Save thay file sau serialize thành
  công; rename hiển thị lỗi validation; totals phản ánh formula thật/Undo.
- Gallery Table Style chỉ xem trước, không áp dụng style và không chiếm việc
  TABLE-005. Không thay calculation, structural identity, OpenXML hay worksheet
  scrolling/rendering.

## Commit và file trọng tâm

- `1d0ad498`: dense layout, immutable preview, production sizing, Core tests,
  benchmark và contracts.
- `88ef9c03`: WPF/WinForms/MAUI presenters, chrome, focus và loaded regressions.
- `a24cbcd0`: themed customization dùng shared command identity, rollback/close
  safety và regression.
- `621fb1ec`: dark checkbox foreground và regression effective brush color.
- `ee7df1ebeba207c3b0488604abf8d578199bbdd5`: runnable SDK preview, capture
  matrix/CI artifact, README và visual contract. Checkpoint tài liệu đứng ngay
  sau commit này là `8a3175510ed1281545335a5fd59f172d5df4edec`.
- `29bceded702ff31822b1e40f14d4f59a267436bb`: MAUI chỉ rebuild do resize
  khi width/scale khác snapshot; loaded smoke dùng visible stage/native-frame
  readiness và kiểm tra height-only identity, bounds và popup persistence.
- `47ccc882be420de1464888b3400606d040cd9194`: checkpoint tài liệu sau fix,
  đã xác minh đủ ba workflow. Thứ tự cherry-pick đầy đủ (gồm cả docs cuối):
  `git log --reverse --format="%H %s" 284ccb76..feature/ribbon-visual-011`.
- Core: `RibbonResponsiveLayout.cs`, `RibbonGalleryPreview.cs`,
  `RibbonProductionCommandCatalog.cs`.
- Native: `NeraRibbonControl.cs` ở WPF/WinForms, `NeraMauiRibbonView.cs`,
  các helper chrome/thumbnail, `RibbonChrome.xaml`, customization dialogs.
- Preview: `samples/NeraSpreadSheet.Wpf.Sample/RibbonPreviewWindow*.cs`,
  `scripts/capture-ribbon-visual.ps1`, `.github/workflows/ci.yml`.
- Contract: [responsive layout](../ribbon-responsive-layout-contract.md),
  [item model](../ribbon-item-model-contract.md),
  [visual preview](../ribbon-visual-contract.md).

## Validation hiện tại

- Full solution, WPF/WinForms/sample và MAUI Windows build: **0 warning/error**.
  SDK repository 10.0.302; MAUI local dùng workload đã cài với SDK 10.0.201,
  không đổi `global.json`; GitHub dùng SDK của repository cho tất cả targets.
- Core solution **1386/1386**, focused Ribbon **75/75**.
- Focused loaded desktop Ribbon **16/16**, MAUI tests **41/41**, loaded MAUI
  Windows Ribbon smoke **success** (bốn widths/scales/themes, item kinds,
  Backstage và focus). Customization kiểm tra shared commands trên hai tab.
- Full Windows **74/75**: duy nhất
  `PublicWpfScrollBarDragMovesOnlyBottomRightPaneAndPersistsState` thất bại tại
  `Assert.IsTrue(window.Activate())`, đúng hạn chế foreground đã có ở baseline,
  trước khi hành vi SDK được chạy. Không bỏ test hoặc nới assertion.
- Architecture verifier, SDK packaging verifier pass; Core pack tạo **18 nupkg**.
- Capture **176 ảnh / 128 native layout snapshots**: tám tab + File,
  1536/1280/1024/820 logical px, bốn palettes, customization và gallery More;
  Home/Table Design thêm raster scale 1.25/1.5/2. Manifest ghi native DPI riêng,
  không coi export scale là đổi DPI màn hình. Geometry/bounds/no-overlap,
  scale invariant, selection, Bold/Undo, totals/Average/Undo và gallery
  không-mutation được kiểm tra trong process thật.
- Representative visual QA: Home, Table Design, gallery More, File và
  customization ở sáng/tối/HC; sửa caption `Không màu`, nền raster và checkbox
  foreground của dark customization. Ảnh tham chiếu Excel/audit local không
  được commit hoặc upload; chỉ ảnh từ SDK sample đi vào CI artifact.
- Benchmark cùng 720 commands: sau **122,2/126,2/121,6/129,5 µs** so với
  trước **231,4/187,7/184,9/189,4 µs** tại widths trên; allocation sau
  **400,18–415,72 KiB** so với trước **439,40–454,30 KiB**. Short run ba
  iterations có nhiễu, không suy diễn thành worksheet performance hay release
  threshold. Số liệu đầy đủ và lệnh chạy nằm trong responsive contract.
- CI checkpoint `8a3175510ed1281545335a5fd59f172d5df4edec`:
  full CI #1322 / `33944196848` đỏ duy nhất ở loaded MAUI Windows Ribbon
  geometry (`A MAUI group caption overlaps its packed commands`). Core,
  Windows desktop **75/75**, capture **176/128**, Android và Apple jobs xanh.
  iOS #143 / `33944198191` và Q003C #140 / `33944199269` success.
  Run đỏ cũ này không phải evidence DONE. Local regression đã tái hiện việc height-only
  resize thay snapshot/control thừa trên MAUI; khi arrange hoàn tất, caption
  Y=80 và command bottom=80 đúng contract. Fixture cũ cũng đo control nằm
  ngoài viewport sau delay 50 ms; fix guard resize và visible native stage/frame
  readiness đã triển khai, giữ và tăng kiểm tra actual bounds/overlap/focus.
  Nguyên nhân chính xác của caption CI cũ thiếu metrics nên không khẳng định
  tuyệt đối; redundant height-only rebuild đã được tái hiện bằng regression.
- Sau fix `29bceded`: MAUI build **0 warning/error**, tests **41/41** và loaded
  Ribbon smoke **3 lần liên tiếp success**, bao gồm dropdown giữ mở và choice
  thực thi đúng một lần sau height layout. Architecture/packaging verifier pass.
  Bộ ba CI của checkpoint sau fix đã xanh như ghi bên dưới.

## Checkpoint exact-head đã xác minh

SHA `47ccc882be420de1464888b3400606d040cd9194`:

| Cổng | Run | Kết quả |
| --- | --- | --- |
| Full CI #1323 | [33945177202](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/33945177202) | success |
| iOS #144 | [33945179079](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/33945179079) | success |
| Q003C/OpenXML #141 | [33945180352](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/33945180352) | success |

Windows CI **75/75** và capture **176 ảnh / 128 snapshots**; các job Core,
Windows, MAUI Windows/Android/Apple và toàn bộ native smokes đều success.
Artifact `ribbon-visual-matrix`: `9963130150`, digest
`sha256:ed93aa7379b08bc61f42f8be311f3c4ab507fb10046290a7564d6d332a4bc52f`.
Artifact `sdk-packages`: `9963106595`, digest
`sha256:51a628cbf1ca2aae272e7d5942d04a2577c848dbb0a5fec6487a59763534b84c`.

Bản ghi này chỉ thay tài liệu sau checkpoint xanh. Khi bàn giao, lấy SHA bằng
`git rev-parse HEAD` và yêu cầu ba run có `head_sha` trùng chính SHA đó;
checkpoint cha không thay thế cổng cuối. PR #1 vẫn Draft/unmerged.

## Giới hạn và rollback

- TABLE-005 phải gắn callback visual vào command/style mutation của chính lane
  đó sau tích hợp; sample hiện chỉ chọn preview. Không công bố full Excel parity.
- Visual matrix pixel sampling không thay thế kiểm tra physical multi-monitor
  DPI; native regression chạy tại DPI host, layout contract kiểm tra đủ scales.
- Rollback bằng revert các commit riêng của lane theo thứ tự ngược, bắt đầu
  docs và MAUI lifecycle fix rồi preview, customization, native presenters và
  core; không reset nhánh
  tích hợp hoặc hoàn tác thay đổi TABLE-005. Sau rollback chạy lại ba cổng CI.

## File có khả năng conflict với TABLE-005

`RibbonProductionCommandCatalog.cs`, các `NeraRibbonControl.cs`,
`NeraMauiRibbonView.cs`, tests Ribbon/Table Design và tài liệu status/worklog.
Tích hợp cần giữ nguyên Table command identities/handlers của TABLE-005 rồi áp
dụng item sizing/preview vào cùng definition. Không tạo Table mutation thứ hai.

## Bước tiếp theo duy nhất

Sau khi xác minh cả HEAD tài liệu cuối, bàn giao chuỗi cherry-pick theo thứ tự
ở trên cho nhánh tích hợp được duyệt, giữ TABLE-005 command identities/handlers
và yêu cầu full CI/iOS/Q003C tại SHA tích hợp trước bước tiếp theo của roadmap.
