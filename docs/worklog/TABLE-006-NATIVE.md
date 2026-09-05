# TABLE-006-NATIVE — editor và corpus native producer

- Trạng thái: implementation và local verification xong; chờ exact-HEAD CI.
  Chưa đóng toàn bộ TABLE-006.
- Branch: `feature/table-006-native-compat`.
- Base sạch: `2bc00eb667da2f2c5afda1024ab753ac638d85d4`.
- Implementation: `7ff66bfd2b983d05ca595e9e07c8de2a239c17d1`.
- Review fix: `30c2564dd2870e0ca521a24f04f99091998b5dcc`.
- PR #1 giữ Draft/open/unmerged; chỉ bàn giao delta sau base.
- Baseline CI: full `33954450148`, iOS `33954450152`, Q003C `33954450150`.
  Không dùng baseline làm bằng chứng CI cho delta mới.

## Ownership và quyết định trước implementation

Đã đọc AGENTS, README, ARCHITECTURE, status, CURRENT, integration/TABLE-006
worklogs, compatibility/editing-help contracts, tests/benchmark và toàn bộ
TABLE_UX_WAVE_20260905 từ checkout coordinator ở chế độ read-only.

Lane A claim Table Core/Editing/Formulas/OpenXml, native editor/input/surface/
split, tests tương ứng và fixture directory riêng. Chỉ ghi worklog này và
`docs/table-native-compatibility-contract.md`. Không ghi CURRENT/status/board,
Ribbon/Bar/Filter chrome, Ribbon tests/capture hoặc CI.

Corpus Excel phát hiện lỗi ngoài claim ban đầu. Coordinator đã APPROVE từng
bounded transfer, ghi nhận trong working worklog/contract trước edit:

1. `OpenXmlConditionalFormattingCodec.cs` và targeted decoder tests: chấp nhận
   explicit default-valued dxf như General, giữ nguyên Core catalog.
2. `OpenXmlPackagePreserver.cs`: giữ native Table dxfs độc lập với opaque CF,
   remap generated cfRule bằng remapper hiện hữu. Không sửa common remapper,
   CF Core hoặc tạo priority policy mới.

Coordinator từ chối mở rộng mixed supported/opaque CF merge: contract basic
Excel cố ý giữ nguyên toàn bộ CF set khi có opaque rule. Tests ghi đúng giới
hạn này; supported-only CF vẫn được chỉnh/lưu, không khóa để né collision.

Hai MAUI TableHost files thuộc lane B; A không sửa. Desktop đã **RELEASE** cho
coordinator sau mọi local smoke. Computer Use xác nhận không còn cửa sổ
Nera/smoke/Book2; không chạy lại desktop khi chưa được transfer.

## Implementation và file trọng tâm

- WPF `NeraSpreadsheetControl.FormulaEditing.cs` và WinForms partial cùng tên
  nối assistant/analyzer hiện hữu: bounded Table/function popup, Tab/click,
  stable-ID validation khi rename/delete, Enter/Alt+Enter/Escape, workbook-aware
  point mode, provisional replacement và draft precedent outlines.
- `SpreadsheetFormulaEditingAssistant.Tables.cs` thêm guard metadata-only cho
  literal/structured fragment và provisional span overflow. Completion/point
  mode không tính lại hoặc tạo history; commit dùng editor/session cũ.
- WinForms giữ raw editor bounds, clip bằng native Region; wrapping không đổi
  theo viewport. Outline dùng nested display-list composer hiện hữu.
- Table codec validate native dxf references; decoder giữ explicit default
  override; preserver giữ native indices và remap generated CF đúng một lần.
  Không package, workbook model hoặc calculation/history engine mới.
- `TableNativeProducerCorpusTests.cs`, decoder tests, hai native editor test
  files và `Fixtures/TableNative` chứa regression/provenance có thể tái tạo.

## Corpus và privacy

Excel Windows **16.0.20326.20132** tạo workbook trắng mới bằng UI với Table,
calculated column, totals và summary rồi lưu XLSX. Chỉ workbook mẫu được đóng;
workbook cá nhân không đọc/sửa/lưu/đóng. Sanitizer chỉ đổi `docProps/core.xml`
và `xl/workbook.xml`; audit ZIP xác nhận **11/13 payloads khác giữ nguyên byte**,
gồm Table/styles/worksheet/relationships. Native original có metadata/path chỉ
ở ignored artifacts, không commit.

Nera fixture do `SpreadsheetSession.Tables` và production session serializer
tạo trực tiếp, không sửa XML. Hai fixture tổng khoảng 13 KB; recipe, version,
SHA-256, licensing/provenance, expected formulas/values/graph trong fixture
directory. Privacy/schema/hash tests xanh; staged text không chứa đường dẫn
máy cá nhân hoặc token pattern. Không nhập tài liệu bên thứ ba.

Excel `dataDxfId` là preserve-only: strict import từ chối, không xóa native
markup để giả hỗ trợ. Repeated preservation kiểm tra schema Microsoft365,
stable Table/column IDs, native part URI/relationship/numeric IDs, calculated/
totals/summary values, recalculate và Convert/Undo/Redo. Collision matrix gồm
native/generated dxf 0, supported CF edit, opaque full-set preservation, color
filter/sort, custom Table style và ba cycles không tăng dxfs vô hạn. Malformed
negative/out-of-range/missing dxf bị reject, source bytes không đổi.

## Kiểm chứng local

SDK 10.0.302, Release, analyzers/warnings-as-errors. Logs/TRX/native original ở
ignored `artifacts/table-006-native`; không commit raw logs.

| Kiểm chứng | Kết quả |
| --- | --- |
| Core và full desktop solution build | 0 warnings, 0 errors |
| Full Core | **1497/1497** |
| OpenXml / Editing trong full Core | **153/153**, **283/283** |
| Tests mới sau review fix | 7 Editing, 20 OpenXml, 24 Windows editor cases |
| Targeted loaded WPF/WinForms editor trước review | **19/19** |
| Full Windows trước review fix | **95/96**, không skip |
| Rerun riêng WPF activation smoke | **0/1**, cùng assertion |
| Multiline caret regression trước fix | **0/5**, tái hiện đủ 5 cases |
| Toàn bộ WinForms editor sau review fix | **14/14** |
| Architecture / packaging SDK / diff whitespace | Passed |

Windows failure duy nhất:
`WpfSplitScrollBarWindowMessageSmokeTests.PublicWpfScrollBarDragMovesOnlyBottomRightPaneAndPersistsState`
dừng tại `Assert.IsTrue(window.Activate())`, trước scrollbar behavior. Không có
quan sát focus đúng thời điểm lỗi nên **nguyên nhân chưa xác định**. Không
sửa/skip/nới assertion hoặc timeout. Exact-HEAD Windows CI phải pass bài này.

Các lệnh chính, với SDK 10.0.302 đã được resolve khi chạy:

```powershell
dotnet build NeraSpreadSheet.Core.slnx -c Release
dotnet test NeraSpreadSheet.Core.slnx -c Release --no-build
dotnet build NeraSpreadSheet.slnx -c Release
dotnet test tests/NeraSpreadSheet.Windows.Rendering.Tests/NeraSpreadSheet.Windows.Rendering.Tests.csproj -c Release --no-build
dotnet test tests/NeraSpreadSheet.Windows.Rendering.Tests/NeraSpreadSheet.Windows.Rendering.Tests.csproj -c Release --no-build --filter FullyQualifiedName~StructuredReferenceEditorTests
dotnet test tests/NeraSpreadSheet.Windows.Rendering.Tests/NeraSpreadSheet.Windows.Rendering.Tests.csproj -c Release --no-build --filter FullyQualifiedName~PublicWpfScrollBarDragMovesOnlyBottomRightPaneAndPersistsState
./scripts/verify-architecture.ps1
./scripts/verify-packaging-sdk.ps1
git diff --check
dotnet run --project benchmarks/NeraSpreadSheet.Benchmarks/NeraSpreadSheet.Benchmarks.csproj -c Release --no-build -- --filter '*TableCompatibilityBenchmarks.ColumnCompletion*' --job short --inProcess
```

BenchmarkDotNet 0.15.8, runtime .NET 10.0.10, Windows x64/i5-13500H, ShortRun
in-process, ba warmups/ba measured iterations: completion với 0/100.000
unrelated cells **149,0/137,9 ns**, **648 B/op** cả hai. Đây là sparse occupancy
sanity, không so sánh trước/sau hai phiên bản, không phải SLO hoặc native
input/render latency evidence. Không thay render/scroll algorithm.

## Giới hạn và CI handoff

- MAUI SKGLView chưa có cell editor overlay/binding nền; không dựng framework
  mới. WPF split adorner/WinForms split surface có editor riêng, chưa nối
  assistance. Chỉ standalone WPF/WinForms được nghiệm thu trong lane này.
- Không có LibreOffice trong native inventory/standard install locations.
  Bốn candidate upstream đều ghi Excel/Excel Online producer; không commit,
  không giả LibreOffice evidence hoặc cài producer mới. Corpus LibreOffice,
  physical multi-monitor/DPI và full visual parity vẫn là gap.
- Unsupported nested partial/multi-area completion và mixed supported/opaque
  CF editing giữ contract trước; toàn TABLE-006 còn mở.
- Tài liệu này chốt trước final-HEAD CI. Worker phải push cả commit tài liệu,
  dispatch `ci.yml`, `chatgpt-ios-analytics-smoke.yml`,
  `q003c-openxml-analytics-gate.yml`, xác minh **3 workflow/7 jobs xanh đúng
  final SHA**. Không dùng implementation-only CI thay thế. Final SHA/run URLs
  được gửi bằng handoff đến coordinator sau khi xanh.
- Coordinator nhập integration và cập nhật CURRENT/status/board. Sau handoff,
  A ngừng ghi owned files đến khi có yêu cầu mới.

Rollback: revert bốn commit implementation/documentation/review fix của delta
sau `2bc00eb6`; không migration hoặc package dependency mới.

## Bước tiếp theo duy nhất

Push source branch chứa cả tài liệu, dispatch và xác minh đủ ba workflow/bảy
jobs tại final HEAD, rồi gửi coordinator base/delta/SHA/URLs và giới hạn trên.

## Review follow-up: WinForms caret và provisional span

Coordinator phát hiện Up/Down/PageUp/PageDown và programmatic Select có thể
đổi caret mà giữ span cũ trong multiline editor. Regression mới kiểm tra chèn
ở caret mới, giữ reference trước, repeated drag vẫn replace, không history/
recalculate. Candidate `82c21e17` không được dùng để đóng finding này.

Trước local test, coordinator **APPROVED DESKTOP TRANSFER B → A**: B xác nhận
đã dừng toàn bộ native UI. A chỉ chạy targeted synthetic WinForms regression
red-before-fix/green-after-fix; không chạy full suite hoặc sửa activation smoke,
không thao tác workbook cá nhân. Phải báo RELEASE sau khi mọi test/UI dừng.

Đã tái hiện **5/5 failures** với Up/Down/PageUp/PageDown/programmatic Select:
expected `=SUM(A5\r\nB2:B3`, actual `=SUM(\r\nA5`. Fix kiểm tra caret/selection
ở KeyUp và insertion boundary, clear span khi caret không còn span.End hoặc
selection không rỗng. Repeated drag ở cuối span vẫn replace đúng reference.
Test giữ history=0 và cached sentinel=999 để phát hiện tính lại không mong muốn.

Sau fix build Windows test project 0 warnings/errors; targeted toàn bộ
WinForms editor **14/14 passed**. Architecture/packaging/diff-check xanh lại.
Không chạy lại full desktop suite hoặc baseline activation test. Commands:

```powershell
dotnet test tests/NeraSpreadSheet.Windows.Rendering.Tests/NeraSpreadSheet.Windows.Rendering.Tests.csproj -c Release --no-build --filter FullyQualifiedName~PointModeShouldInsertAtMovedMultilineCaretAndKeepPreviousReference
dotnet test tests/NeraSpreadSheet.Windows.Rendering.Tests/NeraSpreadSheet.Windows.Rendering.Tests.csproj -c Release --no-build --filter FullyQualifiedName~WinFormsStructuredReferenceEditorTests
```

Mọi targeted process đã exit; fresh Computer Use inventory không còn cửa sổ
Nera/smoke/Book2. A đã báo **RELEASE DESKTOP** lại cho coordinator; không dùng
desktop đến khi được transfer mới. Candidate cũ `82c21e17` có runs full
`33958088560`, iOS `33958089562`, Q003C `33958090512`; chúng chỉ là checkpoint,
không thay final-HEAD verification sau review fix và tài liệu này.
