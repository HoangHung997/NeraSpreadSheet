# Kế hoạch hoàn thiện Table, Filter, Ribbon và UX

Tiến độ thực tế 05/09/2026: hai lane TABLE-RIBBON-012 và TABLE-006 đã bàn giao,
TABLE-005/Table Design cùng headless compatibility đã ghép vào implementation
`e29acb44`, xanh đủ ba workflow/mọi job. Lịch ngày bên dưới vẫn là mốc dự kiến,
không buộc chờ đến ngày đó. TABLE-006 còn native structured-reference editor
wiring và corpus Excel/LibreOffice có provenance, chưa đóng toàn checkpoint.
Xem [integration worklog](worklog/TABLE_RIBBON_INTEGRATION_20260905.md).

## 1. Mục tiêu phát hành

Hoàn thiện một command surface có thể dùng như SDK cho WPF, WinForms và .NET
MAUI, đồng thời tích hợp vào bản demo Windows 11 x64 để kiểm thử thực tế.

Đích của chương trình này là:

- Ribbon tự thích ứng theo chiều rộng, DPI và theme;
- ứng dụng nhúng SDK có thể thêm, bớt, đổi thứ tự tab, group và command mà
  không sửa workbook/calculation code;
- Table có đầy đủ vòng đời và giao diện Table Design cho model hiện có;
- AutoFilter có checklist phân trang, tìm kiếm, lọc text/number/date,
  top/bottom, màu, sort, clear và reapply;
- toàn bộ hành vi bàn phím, focus, accessibility, localization và visual state
  được thống nhất giữa WPF, WinForms và MAUI;
- NuGet và demo Win11 x64 được tạo từ đúng commit đã qua CI.

Đây là phạm vi hoàn thiện của bộ SDK NeraSpreadSheet, không phải tuyên bố sao
chép toàn bộ Microsoft Excel. Power Query, VBA, Office add-in, OLAP, slicer,
timeline và cộng tác thời gian thực không thuộc đợt này.

## 2. Điểm xuất phát đã xác minh

Baseline của lịch là commit
`34a81c5d45c2b28397c1688ae665cce0d0e8dfe7`, đã qua full CI #1302, iOS gate
#123 và Q003C/OpenXML gate #120.

Không làm lại các capability sau:

- 272 semantic icon keys, 242 SVG masters và 4.840 PNG variants ở 5 kích thước,
  4 theme;
- Ribbon/Bars runtime, snapshot, command dispatcher và persistence JSON;
- WPF, WinForms và MAUI Ribbon/Bar presenters cơ bản;
- visibility/order/large-small customization hiện có;
- Table model, stable identity, calculated column, totals và structured
  reference;
- Table và worksheet AutoFilter, compressed row visibility, Undo/Redo;
- checklist/search phân trang, generation/cancellation và native presenters;
- text/date comparison cơ bản trong Core;
- Table/OpenXML round-trip cho phạm vi đã hỗ trợ.

Khoảng trống chính hiện tại là Ribbon responsive và các item phức hợp, tùy
biến sâu/QAT/contextual tabs, Table style rendering và Table Design surface,
filter semantics/OpenXML nâng cao, sau đó là chất lượng UX và release gate.

## 3. Định nghĩa “hoàn thành”

Một checkpoint chỉ chuyển sang `DONE` khi có đủ:

1. contract/ADR được cập nhật trước hoặc cùng implementation;
2. API dùng chung nằm ở tầng host-neutral, không tạo model riêng ở từng host;
3. build Release và analyzers không có warning/error;
4. focused unit/integration tests cho mọi public behavior mới;
5. loaded runtime smoke trên host bị tác động;
6. architecture và packaging verification xanh;
7. ảnh kiểm tra light/dark/high-contrast khi thay đổi giao diện;
8. exact-head GitHub Actions xanh trên commit tích hợp cuối;
9. cập nhật worklog với commit, CI, giới hạn và rollback.

Không dùng `ScrollViewer` ngang như cách duy nhất để xử lý Ribbon hẹp. Không
tạo control cho từng ô hay từng giá trị filter; danh sách filter phải phân
trang/virtualize. Scroll worksheet không được rebuild Ribbon hoặc tính lại
filter catalog.

## 4. Sơ đồ phụ thuộc

```text
ICON/RIBBON nền tảng đã hoàn thành
          |
          +--> RIBBON-007 --> RIBBON-008 --> RIBBON-009 --> RIBBON-010
          |
          +--> FILTER-005 --> FILTER-006 --> FILTER-007
          |
          +--> TABLE-004  --> TABLE-005  --> TABLE-006
                                             |
                    +------------------------+
                    v
                UX-006 --> UX-007 --> PERF-008 --> RELEASE-009
```

`FILTER-006` chỉ tích hợp item phức hợp vào Ribbon sau `RIBBON-008`.
`TABLE-005` chỉ tích hợp contextual Table Design sau `RIBBON-009`. Phần model,
OpenXML và tests của hai lane vẫn có thể làm song song trước các mốc đó.

## 5. Lịch chuẩn hai lane

Lịch tính theo ngày làm việc từ 07/09/2026, không tính thứ Bảy và Chủ nhật.
Ngày kết thúc là mục tiêu kỹ thuật, có thể dịch chuyển nếu phát hiện sai khác
OpenXML hoặc runtime blocker mới.

Để rút ngắn thời gian, hai checkpoint đầu được khởi động sớm từ 04/09/2026.
Các mốc trong bảng là trần kế hoạch; checkpoint kế tiếp được mở ngay khi
dependency và exact-head gate của nó xanh, không chờ đến ngày ghi trong lịch.

### Lane A — Ribbon

| Mã | Thời gian | Số ngày | Kết quả bắt buộc |
| --- | --- | ---: | --- |
| `RIBBON-007` | 07–09/09 | 3 | Responsive measurement, group collapse/overflow và DPI/theme refresh |
| `RIBBON-008` | 10–16/09 | 5 | Item model cho button, toggle, split/dropdown, menu, combo, gallery và color picker |
| `RIBBON-009` | 17–22/09 | 4 | Contextual tabs, minimized state, backstage/File surface, QAT và key tips |
| `RIBBON-010` | 23–29/09 | 5 | Tùy biến sâu: tạo/đổi tên tab-group, drag/drop, command catalog, reset/import/export |

### Lane B — Filter và Table

| Mã | Thời gian | Số ngày | Kết quả bắt buộc |
| --- | --- | ---: | --- |
| `FILTER-005` | 07–11/09 | 5 | Host-neutral semantics và OpenXML cho rich filter/sort |
| `FILTER-006` | 14–18/09 | 5 | Popup UX phân trang/virtualized cho text, number, date, color và custom filter |
| `FILTER-007` | 21–23/09 | 3 | Sort, clear/reapply, indicator, bàn phím và focus hoàn chỉnh |
| `TABLE-004` | 24–29/09 | 4 | Table style engine và render thống nhất trên bốn backend |
| `TABLE-005` | 30/09–06/10 | 5 | Contextual Table Design và toàn bộ command vòng đời Table |
| `TABLE-006` | 07–09/10 | 3 | XLSX compatibility corpus, structured-reference UX và regression hardening |

### Lane tích hợp — UX, hiệu năng và phát hành

| Mã | Thời gian | Số ngày | Kết quả bắt buộc |
| --- | --- | ---: | --- |
| `UX-006` | 12–15/10 | 4 | Theme, DPI, visual states và localization thống nhất |
| `UX-007` | 16–21/10 | 4 | Keyboard-only, key tips, focus, screen reader và high-contrast |
| `PERF-008` | 22–26/10 | 3 | Benchmark, resize/filter stress và memory/leak gate |
| `RELEASE-009` | 27–28/10 | 2 | Demo Win11 x64, NuGet, screenshots, exact-head CI và rollback note |

Nếu chỉ có một tác nhân làm tuần tự, cùng phạm vi cần khoảng 55 ngày làm việc,
dự kiến 07/09–20/11/2026. Hai lane độc lập đưa mục tiêu về khoảng 38 ngày làm
việc, dự kiến 07/09–28/10/2026. Đây là ước lượng không gồm thời gian chờ người
dùng nghiệm thu giao diện hoặc scope mới.

## 6. Nội dung và cổng nghiệm thu từng checkpoint

### RIBBON-007 — responsive layout

- tạo measurement/layout snapshot host-neutral theo available width, scale,
  item size và collapse priority;
- hỗ trợ large/small/compact, group overflow và deterministic collapse;
- giữ selected tab và focus identity khi resize/rebuild;
- WPF, WinForms và MAUI chỉ trình bày snapshot, không tự tính chiến lược khác;
- test các ngưỡng 100%, 125%, 150%, 200% DPI và resize liên tục.

Thoát checkpoint khi không còn command bị cắt, không cần cuộn ngang để dùng
command chính, và cùng input tạo cùng collapse result trên ba host.

### RIBBON-008 — item model đầy đủ

- mở rộng item kind: button, toggle, split button, dropdown/menu, combo box,
  gallery, color picker và separator;
- command state có selected value, checked state, enabled state và items source
  bất biến;
- native presenters có cùng activation/error semantics;
- shortcut, automation name, tooltip và icon fallback áp dụng cho mọi item.

Thoát checkpoint khi mỗi item kind có contract test và loaded smoke trên host
đích, không có platform type lọt vào Ribbon.Core.

### RIBBON-009 — contextual surface, QAT và key tips

- contextual tab xuất hiện theo selection/table state và biến mất có kiểm soát;
- hỗ trợ minimized/expanded Ribbon và ghi nhớ trạng thái;
- File/backstage surface dùng command registry hiện có;
- Quick Access Toolbar dùng cùng stable command identity;
- audit và đưa mọi capability production hiện có vào command catalog; không
  dừng ở 30 command đang được đăng ký;
- key tips có scope, collision validation, Escape/back navigation và focus
  restoration.

Thoát checkpoint khi có thể thao tác toàn bộ command chính bằng bàn phím và
Table Design chỉ xuất hiện khi active selection thuộc Table.

### RIBBON-010 — customization như một SDK

- command catalog phân nhóm để app nhúng chọn capability;
- thêm, xóa, đổi tên và đổi thứ tự custom tab/group;
- kéo command giữa group, đổi large/small và thêm vào QAT;
- preview/apply/cancel/reset, import/export JSON versioned;
- preserve unknown optional-module ids và migrate profile cũ;
- application policy có thể khóa command/tab không cho người dùng sửa.

Thoát checkpoint khi customization round-trip ổn định, không duplicate stable
ID, rollback được và ba host dùng cùng một profile.

### FILTER-005 — semantics và OpenXML nâng cao

- chuẩn hóa text, number và date predicates đang có;
- thêm date grouping/tree, Top/Bottom N và %, dynamic date, fill/font color,
  icon và sort state;
- Table và worksheet AutoFilter dùng cùng predicate model;
- OpenXML import/export phải giữ unknown producer markup khi preservation bật;
- recalculation chỉ đánh dấu dependents bị ảnh hưởng, không chạy toàn workbook
  trên UI thread.

Thoát checkpoint khi round-trip schema-valid và các kết quả lọc/sort có test
đối chiếu trên mixed values, blank, error, date system 1900/1904 và locale.

### FILTER-006 — popup/dropdown/sheet hoàn chỉnh

- search, checklist, select all/clear visible và paging/virtualization;
- phân loại menu theo text, number, date, color và custom conditions;
- date tree theo year/month/day nhưng không materialize toàn nguồn;
- Apply/Cancel/Clear có generation/cancellation và Undo/Redo đúng một lần;
- WPF Popup, WinForms DropDown và MAUI overlay/bottom sheet dùng cùng presenter.

Thoát checkpoint khi 100.000 dòng/10.000 distinct values không tạo một native
control cho mỗi value, stale async result không thể ghi đè phiên mới.

### FILTER-007 — sort, reapply và accessibility

- sort ascending/descending, custom order trong phạm vi đã chốt và clear sort;
- reapply giữ identity của filter target sau structural edit hợp lệ;
- header indicator phân biệt filtered/sorted/both;
- hoàn thiện Alt+Down, arrows, Home/End, Page Up/Down, Space, Enter, Escape và
  focus restoration;
- screen reader công bố cột, trạng thái và số lượng kết quả.

### TABLE-004 — Table style engine

- model hóa style elements cho whole table, header, totals, first/last column,
  row/column stripes và filter button;
- resolve theme colors/tints thành render style dùng chung;
- built-in gallery có stable style identity và preview nhẹ;
- WPF, WinForms, Direct2D và Skia nhận cùng resolved style;
- OpenXML `tableStyleInfo` và custom table style được preserve đúng phạm vi.

Thoát checkpoint khi cùng workbook cho kết quả hình học/màu/font/border tương
đương trên bốn renderer, không sinh per-cell control.

### TABLE-005 — contextual Table Design

- Create Table, rename, resize, header row, total row, first/last column,
  banded rows/columns và filter buttons;
- style gallery, calculated-column/totals-function UI;
- insert/delete table row/column, remove duplicates và convert to range;
- mọi mutation dùng `SpreadsheetSession.Tables`, cùng transaction Undo/Redo;
- contextual tab và command state phản ánh selection hiện tại.

Thoát checkpoint khi command không làm mất stable Table/column identity, formula
reference hoặc OpenXML relationship.

### TABLE-006 — compatibility và hardening

- corpus do Excel, LibreOffice và Nera tạo;
- Save -> Load -> Save cho style, filter, totals, calculated columns và rename;
- point-mode/selection UX cho structured references;
- malformed input bị từ chối nguyên tử;
- tài liệu hóa rõ phần preserve-only và phần Nera sở hữu semantic.

### UX-006 — visual system và localization

- typography, spacing, corner, focus ring, hover/pressed/checked/disabled;
- light, dark, high-contrast dark/light và icon theme switching;
- kiểm tra 100%, 125%, 150%, 200% DPI, narrow/wide window và touch target;
- toàn bộ chuỗi mặc định tiếng Việt có dấu và đi qua resource localization;
- trạng thái không chỉ được truyền đạt bằng màu.

### UX-007 — keyboard và accessibility

- tab order, key tips, shortcut collision, focus trap/restore;
- AutomationPeer/AccessibleObject/Semantics có stable IDs và state;
- keyboard-only matrix cho Ribbon, Table Design và Filter;
- screen-reader smoke cho Windows, đồng thời giữ Android/iOS/Mac build gates;
- animation ngắn, hủy được và tắt khi reduced-motion được yêu cầu.

### PERF-008 — performance và độ bền

- benchmark Ribbon layout/collapse với command catalog lớn;
- stress resize/theme/customization và popup open/search/cancel lặp lại;
- filter large-data giữ bounded page/cache, không block UI lâu;
- kiểm tra subscription/dispose và memory growth sau nhiều open/close;
- scroll worksheet không kích hoạt Ribbon rebuild, full filter scan hoặc full
  recalculation.

Ngưỡng số cụ thể phải được chốt từ baseline đo được ở commit đầu checkpoint;
không đặt con số giả trước khi có benchmark ổn định trên runner.

### RELEASE-009 — demo và NuGet

- tích hợp toàn bộ command surface vào demo Win11 x64;
- tạo bộ workbook kiểm thử Table/Filter/Ribbon và checklist nghiệm thu;
- command coverage audit không còn production capability bị bỏ quên khỏi
  Ribbon hoặc contextual surface tương ứng;
- pack toàn bộ SDK/NuGet, kiểm tra package contents và consumer sample;
- chụp light/dark/high-contrast, ghi giới hạn còn lại và rollback SHA;
- chỉ phát hành artifact từ exact HEAD khi mọi GitHub gate xanh.

## 7. Quy tắc hai tác nhân không đè nhau

Điều chỉnh được coordinator chốt ngày 05/09/2026 theo yêu cầu mở hai worktree
`gpt-6-astra / xhigh`: TABLE-RIBBON-012 nhập source TABLE-005 vào Ribbon mới;
TABLE-006 được làm phần headless từ source TABLE-005 có exact-head CI xanh,
không phải đợi ghép UI. Lane A giữ Ribbon/host và được ủy quyền riêng WPF
sample/capture; lane B giữ Table/Core/Editing/OpenXml/Formulas. Ngoại lệ này
chỉ cho phép bắt đầu công việc độc lập, không bỏ cổng integration CI trước
checkpoint phụ thuộc tiếp theo. Chi tiết khóa file và chỉ lấy delta B sau
base source tại [wave contract](worklog/TABLE_RIBBON_WAVE_20260905.md).

1. Mỗi checkpoint có đúng một owner và một branch
   `feature/<checkpoint>-<slug>`.
2. Owner phải ghi base SHA trước khi sửa. Không dùng một working tree chung cho
   hai checkpoint đang chạy.
3. Lane Ribbon sở hữu `Ribbon.Core` và các file Ribbon presenter. Lane
   Table/Filter sở hữu Core Table/Filter, Editing presenter, OpenXML và filter
   overlay. File project, sample shell, resources dùng chung và
   `docs/worklog/CURRENT.md` chỉ integration owner được sửa.
4. Mỗi worker cập nhật worklog riêng `docs/worklog/<CHECKPOINT>.md`; không cùng
   ghi `CURRENT.md`.
5. Trước tích hợp, integration owner fetch remote và xác minh integration HEAD
   vẫn bằng SHA đã ghi. Nếu đã thay đổi thì rebase/cherry-pick trên head mới và
   chạy lại focused tests.
6. Chỉ cherry-pick commit implementation đã tự đủ test. Không merge toàn branch
   bằng force push và không sửa lịch sử của lane còn đang chạy.
7. Sau mỗi checkpoint, integration owner cập nhật board, `CURRENT.md`,
   `current-status.md`, đẩy integration branch và chờ exact-head CI xanh rồi mới
   mở checkpoint phụ thuộc.
8. Nếu hai task cần cùng file, task đến sau ở trạng thái `WAITING`; không sửa
   song song và “giải quyết conflict sau”.

Trạng thái hợp lệ: `BACKLOG`, `READY`, `ACTIVE`, `WAITING`, `INTEGRATING`,
`CI`, `DONE`, `BLOCKED`.

## 8. Ma trận CI tối thiểu

| Vùng thay đổi | Gate bắt buộc |
| --- | --- |
| Ribbon.Core/Commands | Core solution, Commands tests, architecture |
| WPF/WinForms Ribbon | Windows.Rendering tests và loaded desktop Ribbon smoke |
| MAUI Ribbon | MAUI tests, Windows loaded Ribbon smoke, Android/iOS/Mac builds |
| Table/Filter Core | Core, Editing, Viewport và formula dependency tests |
| OpenXML Table/Filter | OpenXML round-trip, schema validation, preservation corpus |
| Rendering/Table style | Rendering.Spreadsheet, Skia, Direct2D/WPF/WinForms focused tests |
| UX/a11y | keyboard/focus tests, loaded smokes, theme/DPI screenshot matrix |
| Release | full CI, iOS gate, Q003C/OpenXML gate, packaging verifier và demo smoke |

## 9. Mốc nghiệm thu người dùng

- **29/09/2026:** xem Ribbon responsive và customization hoàn chỉnh.
- **09/10/2026:** xem Table Design và Filter hoàn chỉnh trên demo tích hợp.
- **21/10/2026:** nghiệm thu giao diện, bàn phím, theme và accessibility.
- **28/10/2026:** nhận demo Win11 x64 và NuGet release candidate.

Phản hồi giao diện tại mỗi mốc được sửa trong checkpoint kế tiếp. Thay đổi lớn
về scope sẽ được thêm mã mới và ước lượng lại, không âm thầm kéo dài checkpoint
đang chạy.
