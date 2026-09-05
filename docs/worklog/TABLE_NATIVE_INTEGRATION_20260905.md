# Tích hợp TABLE-006-NATIVE — 05/09/2026

## Phạm vi và nguồn đã nghiệm thu

Coordinator nhận handoff độc quyền từ task A
`01a070ae-bcfe-7562-acad-9c53287433d3`, nhánh
`feature/table-006-native-compat`, final source
`4ae7731fbc35362809fd9fb4027b53fdbfb6ebdc` sau base `2bc00eb6`.
Đã kiểm tra trực tiếp ba workflow completed/success, đủ bảy job đúng SHA:

- Full CI #1339 / `33958874774`.
- iOS #160 / `33958876307`.
- Q003C #157 / `33958877741`.

Windows source CI xác nhận Core **1497/1497**, OpenXml **153/153**, Editing
**283/283**, Windows runtime **102/102**, không skip, gồm activation smoke.
Local Windows **95/96** của A là trước review fixes; rerun activation **0/1**
không xác định nguyên nhân tại máy local. Không thay kết quả local bằng CI.

## Commit map và cách ghép

Integration branch `feature/bootstrap-architecture-v0.1`, PR #1
Draft/open/unmerged. Base điều phối `554fbf7e`; chỉ cherry-pick đúng năm
commits sau source base `2bc00eb6`, không conflict hoặc nhập lại baseline:

| Source | Integration | Nội dung |
| --- | --- | --- |
| `7ff66bfd` | `bbc5efe9` | Editor/corpus/codec và preservation |
| `82c21e17` | `40a7d988` | Contract và source worklog |
| `30c2564d` | `e59322a2` | Guard caret/provisional span |
| `2056c438` | `3e7431b6` | Evidence multiline regression |
| `4ae7731f` | `1aaba747` | Pointer guard/regression và tài liệu cuối |

Implementation integration: `1aaba747f379877d0b1813ebebbf3705763a6186`.
So sánh source final với integration ở src/tests/samples/scripts/.github không
có khác biệt. Coordinator không triển khai lại code hoặc sửa assertions.
Commit đồng bộ tài liệu là descendant; phải kiểm tra cả ba workflow/bảy job
ở đúng HEAD cuối trước bàn giao, không dùng source CI thay cho integration.
Final SHA/run URLs được ghi trong handoff PR #1 sau khi xanh để không tạo
vòng lặp commit tài liệu đổi HEAD sau mỗi lần CI.

## Hành vi đã ghép và review

- Standalone WPF/WinForms nối shared assistant/analyzer cho completion Table/
  function, nhận bằng stable IDs, point-mode replacement và draft highlights.
- Guard con trỏ/selection tránh ghi đè reference cũ sau Up/Down/PageUp/PageDown
  hoặc programmatic Select. Pointer precheck không rơi xuống commit khi caret
  chuyển vào quoted literal. A tái hiện red 5/5 và red 1/1 trước fix; final
  WinForms editor 15/15 xanh. Coordinator đã đọc cả hai fixes/regressions.
- OpenXML decoder giữ explicit General dxf; Table dxfs được giữ và generated
  CF được remap bằng engine hiện hữu, không thay Core differential catalog.
- Corpus Excel 16.0.20326.20132 và Nera có provenance/hash/recipe/schema/privacy,
  repeated Save-Load-Save và Convert/Undo/Redo. Chỉ metadata/core và workbook
  path/revision được sanitize; 11/13 native payloads giữ nguyên byte.
- Không có package/model/history/calculation engine mới. File trọng tâm và
  giới hạn chi tiết: [source worklog](TABLE-006-NATIVE.md),
  [contract](../table-native-compatibility-contract.md),
  [corpus](../../tests/NeraSpreadSheet.OpenXml.Tests/Fixtures/TableNative/README.md).

## Kiểm chứng tại checkout tích hợp

- Full desktop solution Release build: **0 warnings / 0 errors**.
- Full Core regression: **1497/1497**, 14 test assemblies, không fail/skip.
  `table-native-integration.trx` chỉ nằm trong TestResults được ignore của tests.
- Architecture, packaging SDK và diff whitespace: pass.
- Không chạy desktop local vì B đang sở hữu desktop để debug UX. Native smoke
  và capture được kiểm chứng lại trong exact-HEAD GitHub CI của integration.
- Completion benchmark source 0/100k unrelated cells: 149,0/137,9 ns, 648 B/op,
  ShortRun in-process. Chỉ là sparse sanity; không phải SLO hoặc before/after
  native latency. Coordinator không đổi render/scroll algorithm của A.

## Quyền ghi và phần còn lại

A đã release toàn bộ owned files và desktop, không tiếp tục ghi sau handoff.
B `01a070af-9093-7303-a811-821cde640467` vẫn ACTIVE ở
`feature/ux-006-visual-localization`, giữ `gpt-6-astra / xhigh`, base `2bc00eb6`.
Desktop hiện **độc quyền B**; coordinator chỉ build/test headless và dùng CI.
Không rebase hoặc sửa source B trong lúc task còn chạy.

B chưa tích hợp: candidate `f9fc3724` có Windows tests xanh nhưng WPF capture
timeout và MAUI TableFilter theme failure. B đang sửa Filter invalidation,
loaded Label background và contrast, cùng native regressions/captures. Không
dùng candidate thất bại làm bằng chứng UX hoàn tất. Benchmark Ribbon allocation
bằng nhau nhưng latency nhiễu; không tuyên bố speedup/no-regression.

TABLE-006 chưa đóng toàn bộ: MAUI chưa có editor binding nền; WPF/WinForms split
editor chưa nối assistance; thiếu LibreOffice-produced corpus và physical DPI
acceptance. Excel dataDxfId là preserve-only; strict reject. Có opaque CF thì
giữ full-set preservation, không hỗ trợ mixed CF editing trong save cycle đó.
Không repack demo ngoài repo, phát hành NuGet/Avalonia hoặc merge PR #1.

Rollback bằng revert riêng năm integration commits theo thứ tự ngược và commit
điều phối nếu cần; không reset, xóa worktree hoặc đổi lịch sử source B.

## Bước tiếp theo duy nhất

Xác minh ba workflow/bảy job trên HEAD tích hợp cuối gồm tài liệu, ghi SHA/run
URLs lên PR #1; sau đó chỉ nhận delta UX-006 sau `2bc00eb6` khi B có handoff
final, đã review và exact-source CI xanh.
