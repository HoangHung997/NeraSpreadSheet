# RELEASE-009 — Kiểm tra command surface trước bản demo

## 06/09 — Audit toàn registry và bổ sung runtime regression

Baseline `50cb357a` đã xanh full `33984177819`, iOS `33984177815`, Q003C
`33984177818`, packages `33984174136` và demo `33984234305`. Đọc lại actual
sample Commands/Definition và production catalog cho thấy 49 SDK IDs + 36
Sample IDs, không phải mọi public API đã có UI. Bảng đầy đủ và giới hạn được
đóng kèm demo trong [COMMANDS](../demo/COMMANDS-WIN11-VI.md), có SHA-256 riêng.

Root thêm `Release009RibbonCommandSmokeTests`: exact session catalog và actual
host reachability, contextual disabled/no-op, 15 style mutations/one Undo,
5 print setting mutations/one Undo, 4 formula drafts/native editor/Cancel và
4 view commands/no workbook history. Đây là test mới, CI chưa chạy tại lúc
ghi source; không lấy baseline green làm proof. Không sửa sample/SDK A/B giữ.

Static audit còn phát hiện giới hạn cần nêu rõ: Print Preview fallback A1:E33
khi không có print area, Help chỉ SUM, formula buttons không tự suy AutoSum
range, Decimal đặt cố định hai chữ số, New tạo fixture, Save chỉ bản sao.
R1 vẫn OPEN cho toàn surface native walkthrough/lý do disabled và active split
command routing sau B bridge. Source tests không gọi đây là full Excel parity.

## Checkpoint sơ bộ 05/09/2026

Coordinator audit code tại `847ff4beec70a05ab4f4f15be9e4d52e82ae7ac7`,
production code-equivalent với integrated green `2e8482c2`. A/B/C đang active;
không sửa source hay native desktop thuộc các lane. Đây chưa phải R1/R2 PASS.

### Bằng chứng đã có

- `RibbonProductionCommandCatalog.CommandIds` hiện có 49 command session,
  gồm 19 command Table. `RibbonCommandCatalogAudit.ValidateExact` kiểm tra
  registration và placement, đồng thời từ chối registered ID bị thiếu trong
  manifest. Không thay manifest này bằng một danh mục UI song song.
- WPF `RibbonPreviewWindow` dùng session handlers và kiểm tra manifest production
  khi dựng cửa sổ. Command `Sample.*` bổ sung font/màu/căn chỉnh/number format,
  filter popup, công thức, thiết lập in, view và file shell; đây là host actions
  dùng API SDK, không phải tất cả đã có stable built-in session command IDs.
- Table parameter callback/typed selected value, cancel, disabled state và
  Undo/Redo có regressions hiện hữu. Contextual Table tab chỉ hiện khi selection
  thuộc Table; sự vắng mặt theo context không phải thiếu registration.
- Chạy lại 35 headless tests từ các lớp `RibbonContextualSurfaceTests`,
  `RibbonDenseLayoutTests`, `TableDesignCommandTests`, `TableRibbonIntegrationTests`:
  **35/35 PASS, 0 skip**, Release `--no-build`, dùng binary baseline đã build.
  Code chưa đổi ở root; source CI exact `847ff4be` cũng đã xanh. Không coi đây là
  35 native UI tests hoặc kiểm chứng từng tính năng trong toàn SDK.

### Các điểm phải đóng trước nghiệm thu demo

1. Manifest audit chứng minh 49 registered session commands có placement, không
   chứng minh toàn bộ public SDK APIs đã có command/dialog. Cần danh sách rõ
   built-in / host-only / chưa hỗ trợ trong release checklist và consumer sample.
2. `RibbonPreviewWindow.Commands.cs::OpenWorkbookAsync` hiện tạo `Window` mới
   chỉ chứa `NeraSpreadsheetControl` với loaded session, không dựng lại Ribbon/
   formula bar/sheet selector. Bản demo để người dùng test file thật phải giữ
   full shell khi mở workbook; đây là công việc release, chưa sửa vì A đang giữ
   sample/Ribbon files. Không đóng R2 bằng ảnh synthetic preview hiện có.
3. Preview dùng ComboBox chọn worksheet, chưa phải sheet tabs ngang của bản demo
   trước đó. Phải xác định đúng app đóng gói và giữ UX/sheet navigation đã hứa;
   không đưa nhầm basic preview làm bản demo đầy đủ.
4. Cần native walkthrough sau khi ghép UX-007/TABLE-007: parameter dialogs,
   enabled/disabled state có điều kiện rõ, thực thi/Undo và window resize/open/
   save roundtrip. Không dựa vào việc có icon/caption hoặc `CanExecute=true`.
5. Giữ preservation/unsupported semantics đã công bố; không mô tả sample actions
   là Excel parity, không public NuGet publish khi mới pack thử nghiệm.

### Kiểm tra NuGet / consumer — R3 còn OPEN

Artifact `sdk-packages` của baseline integrated `2e8482c2` có 18 nupkg từ
`NeraSpreadSheet.Core.slnx`. Đã đối chiếu file artifact: **không có**
`NeraSpreadSheet.Wpf`, `NeraSpreadSheet.WinForms`, `NeraSpreadSheet.Maui` hoặc
`NeraSpreadSheet.Rendering.Direct2D`. Các project này có PackageId và vẫn
packable, nhưng metadata pass hoặc build desktop không phải pack/consumer proof.

Trước khi báo SDK control sẵn sàng qua NuGet cho app khác cần:

- pack các desktop host/backend và toàn bộ dependency packages từ cùng final
  source; MAUI phải ghi chính xác target frameworks thực sự có trong package,
  không gọi một Windows-only pack là đủ đa nền tảng;
- restore/build một consumer ở thư mục cô lập, chỉ dùng PackageReference và
  local artifact feed cho Nera packages, không ProjectReference về source repo;
- dùng version/cache isolation và kiểm tra assets/source revision để không vô
  tình test lại gói 0.1.0 cũ trong cache;
- loaded WPF/WinForms consumer smoke trên runner riêng hoặc sau desktop lease
  transfer; kiểm tra workbook/editor/Ribbon thực từ các assembly đã pack;
- giữ việc tạo artifact thử nghiệm tách biệt public NuGet feed publishing.

Root đã thêm workflow riêng `release-009-packages.yml`, script
`run-release-009-packages.ps1` và consumer `NeraSpreadSheet.Packaged.Windows.Smoke`;
không sửa ci.yml A đang giữ. Closure thực tế có 18 packages trong tập desktop/
OpenXml này (không phải cùng tập 18 core packages cũ). Local read-only plan,
parser, architecture và package metadata pass; CI build/pack/loaded runtime
chưa nghiệm thu. [Contract](../release-009-package-consumer-contract.md).
R3 không được đóng bằng số packages hoặc source ProjectReference tests; MAUI
package matrix và final combined-source consumer còn OPEN.

Source gate đầu `94869dce` / run `33978469048`: SDK/consumer build 0 errors,
synthetic roundtrip và loaded WPF/WinForms smoke chạy xong; fail ở assembly
provenance vì prefix filter nhận cả consumer assembly. C review tìm đúng lỗi;
artifact `9973045882` đã root verify ZIP SHA và thấy mọi SDK DLL đúng version/
source. Sửa loại trừ duy nhất assembly consumer theo identity, giữ SDK/satellite
provenance và thêm diagnostic từng assembly. Cần CI trên source sửa, chưa PASS.

Follow-up `4534e231`: run `33979253272` **SUCCESS**, artifact `9973277152`
root verify ZIP digest, manifest 18 packages và 18 resolved SDK dependencies,
17 loaded SDK assemblies đúng version/SHA. Actual popup tải đủ 50 values,
native changed drafts/commit/Undo/Cancel và width-matched resize đã PASS.
Source review C không còn actionable blocker trong gate. R3 Windows checkpoint
đạt; MAUI và final A+B combined vẫn OPEN. Workflow chạy mọi push integration
branch để không bỏ final docs/source revision sau ghép.

### Bước tiếp theo duy nhất

Demo source `44f5a125` đã PASS run `33982749805`, artifact `9974246790`,
98,872,278 bytes, ZIP SHA-256
`c3d85d45f1a92c611236cbe0f9807aef08b312c9727ccd3a44974c25fa96bc0f`.
Root verify source/version và toàn bộ 447 app-file hashes, 19 SDK/sample DLLs,
226 PNG/128 layouts; đã xem representative Home/Table Design/light/dark ảnh
published app. R3 Windows consumer cùng SHA run `33982749737` SUCCESS.
Standalone worksheet thumb scrollbars chưa gắn trong Ribbon shell; bổ sung
vào known limitations/R2, không nhầm với sheet-tab scrollbar mới.

Root nhận slice WPF lifecycle đã release từ `49e1debe` (không whole B): two-path
delta cùng two-case loaded regression. Tab-switch regression đổi từ headless
Editor.BeginEdit sang actual native changed draft `=SU` + popup trước activation,
kiểm editor/popup hidden và dữ liệu/history nguyên vẹn. New source cần CI/demo/
consumer lần nữa; artifact 44f5 chưa có fix này, không phải final demo.

Root bổ sung workflow `release-009-demo.yml` (bounded push + dispatch) và
`run-release-009-demo.ps1`: self-contained win-x64 apphost mặc định vào Ribbon,
exact source/version/PE/runtime checks, chạy existing complete capture matrix
bằng published exe. Experimental artifact chỉ upload khi success, không public
release; [contract](../release-009-demo-contract.md) và README demo ghi rõ gaps.
Local parser/plan/architecture/packaging PASS; native demo gate còn pending.

Root exact `c6c25d17` xanh đủ full/iOS/Q003C/Windows packages, gồm loaded full-
shell regression. Tiếp tục thay worksheet ComboBox bằng horizontal virtualized
ListBox trên chính Workbook.Worksheets, giữ native selection/focus và session
activation/cancel/history; không thêm model hoặc add/remove/rename transactions.
Loaded tests bổ sung 40 sheets, narrow resize, active-tab visibility và external
activation. Table-dialog capture chuyển selector type, không bỏ context gates.
CI/capture ở source mới vẫn pending. Editable formula bar, final combined demo
packaging/walkthrough vẫn OPEN; không đóng R2 chỉ bằng tab row.

Root static review phát hiện lifecycle gap trong SDK WPF đang B sở hữu:
ActivateWorksheet hủy controller trước event, nhưng CancelEditor trả false sớm
nếu state đã null, trước HideEditor/ResetFormulaEditingUi. Native draft overlay
có thể chưa được dọn khi đổi sheet. Đã giao B xử lý cùng regression; không
workaround model trong sample hoặc gọi headless cancel assertion là native proof.

06/09, sau khi A release sample paths: root bổ sung constructor full shell nhận
existing session và chuyển OpenWorkbookAsync sang cửa sổ đó, giữ theme/tên file.
Regression loaded Windows mới kiểm cùng session, active sheet/cell/history,
Ribbon/formula display và selector thật. Không thay fixture của capture cũ,
chưa sửa sheet tabs/formula-bar editor hoặc đóng gói app. Build/native CI trên
checkpoint này còn pending; không đóng R2 bằng thay đổi constructor đơn lẻ.

Sau khi A release source/sample ownership, coordinator tích hợp và bổ sung
full-shell workbook loading + acceptance checklist/isolated package consumer trên cùng
combined source. Sau đó chạy lại catalog/native/roundtrip gates và mới đóng R1/R2.
