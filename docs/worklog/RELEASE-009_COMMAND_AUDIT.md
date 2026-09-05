# RELEASE-009 — Kiểm tra command surface trước bản demo

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

### Bước tiếp theo duy nhất

Sau khi A release source/sample ownership, coordinator tích hợp và bổ sung
full-shell workbook loading + acceptance checklist/consumer demo trên cùng
combined source. Sau đó chạy lại catalog/native/roundtrip gates và mới đóng R1/R2.
