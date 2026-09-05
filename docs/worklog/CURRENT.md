# Current Work Handoff

## Current integration — native Table + UX-006

- Branch `feature/bootstrap-architecture-v0.1`, PR #1 Draft/open/unmerged.
  A baseline final **`ca69da45a04301bc63286c168f887488d681a00e`** đã xanh full
  `33964496297`, iOS `33964496300`, Q003C `33964496314`, đủ bảy job đúng SHA.
- B final **`292a48d19435e76589c1f0eaa12302e94ace5a48`** đã handoff và release
  toàn bộ files/desktop; source full `33965918117`, iOS `33965919539`, Q003C
  `33965921194` đều success, đủ bảy job đúng SHA. Cả A/B không còn writer active.
- Ghép đúng bốn commits B sau `2bc00eb6` lên `ca69da45`, không conflict/path overlap.
  Combined checkpoint **`0e770e5293c4f7e60e53fdc9ffc1ad5a36616fc7`**; từng source
  path A/B giữ byte-equivalent với respective finals. Không nhập baseline hai lần.
- Đã ghép host-scoped resources/chrome, WPF adorner idle, MAUI Label/visual states/
  checkbox/Picker contrast và regressions giữ identity/focus/query/history.
  Coordinator inspect ảnh; final source hashes bằng 226 Ribbon + 4 MAUI ảnh d55.
- Source B Core 1478/1478, Windows 80/80, MAUI 44/44. Combined desktop build
  0 warnings/errors, Core **1505/1505**, 0 skip; architecture/packaging pass.
  MAUI headless **44/44** bằng SDK 10.0.201 có sẵn; SDK 10.0.302 local thiếu
  workload (NETSDK1147), không đổi global.json. Native combined gates
  dùng GitHub CI tại HEAD cuối gồm docs; source CI không thay integration CI.
- Whole TABLE-006/UX-006 còn mở: MAUI/split editor, LibreOffice; physical DPI/touch/
  screen reader, partial English, open Picker dropdown capture, MAUI custom shell.
  Layout allocation tương đương nhưng chưa chấp nhận no-latency-regression.
- Commit map, review, tests, artifacts, file trọng tâm và rollback:
  [UX_INTEGRATION_20260905.md](UX_INTEGRATION_20260905.md),
  [UX-006.md](UX-006.md), [contract](../ux-006-visual-localization-contract.md).
- Bước tiếp theo duy nhất: chốt build/Core/docs, push và kiểm chứng ba workflow/
  bảy job/runtime artifacts ở HEAD kết hợp cuối rồi ghi handoff PR #1. Không
  merge, publish NuGet, repack demo/Avalonia hoặc mở wave mới trước nghiệm thu.

## Historical integration — TABLE-006-NATIVE; UX-006 khi còn chạy

- Branch `feature/bootstrap-architecture-v0.1`, PR #1 Draft/open/unmerged.
  A đã bàn giao final `4ae7731fbc35362809fd9fb4027b53fdbfb6ebdc`, được kiểm tra
  trực tiếp full #1339 / `33958874774`, iOS #160 / `33958876307`, Q003C #157 /
  `33958877741`: đủ ba workflow/bảy job success đúng source SHA.
- Đã cherry-pick đúng năm commits A sau `2bc00eb6` lên `554fbf7e`, không conflict.
  Implementation integration **`1aaba747f379877d0b1813ebebbf3705763a6186`**;
  production/tests/scripts/CI giống source final. Không nhập source B.
- Standalone WPF/WinForms completion/point-mode/highlights, hai caret/pointer
  regressions, General dxf decoder và Table differential-style preservation đã
  ghép. Corpus Excel/Nera có provenance/privacy/schema/repeated roundtrip.
- Source CI: Core 1497/1497, Windows 102/102, 0 skip. Integration desktop build
  0 warnings/0 errors; Core 1497/1497, 0 skip; architecture/packaging/diff pass.
  Integration exact-final-HEAD CI sau commit tài liệu vẫn bắt buộc;
  kết quả final ghi trên PR #1, không thay bằng green source/commit cha.
- A đã release quyền ghi và desktop. B task
  `01a070af-9093-7303-a811-821cde640467` còn ACTIVE, desktop **độc quyền B**.
  B đang sửa WPF Filter capture starvation/MAUI theme và kiểm chứng UX; candidate
  `f9fc3724` chưa đạt gates. Không ghép sớm, đổi base hoặc sửa owned files của B.
- Whole TABLE-006 còn mở: MAUI/split editor assistance, LibreOffice corpus,
  physical DPI; dataDxfId preserve-only và mixed opaque CF editing chưa hỗ trợ.
  Không đóng gói demo/NuGet/Avalonia hoặc tự merge PR.
- File trọng tâm, commit map, review, tests và rollback:
  [TABLE_NATIVE_INTEGRATION_20260905.md](TABLE_NATIVE_INTEGRATION_20260905.md),
  [TABLE-006-NATIVE.md](TABLE-006-NATIVE.md),
  [contract](../table-native-compatibility-contract.md).
- Bước tiếp theo duy nhất: xác minh ba workflow/bảy job trên HEAD tích hợp cuối
  gồm tài liệu rồi ghi handoff PR; chỉ nhận delta B sau `2bc00eb6` khi final
  handoff/review/exact-source CI của B đạt.

## Historical dispatch — TABLE-006-NATIVE / UX-006

- Integration branch `feature/bootstrap-architecture-v0.1`, PR #1
  Draft/open/unmerged. Final integration baseline
  **`2bc00eb667da2f2c5afda1024ab753ac638d85d4`** đã xanh full #1334 /
  `33954450148`, iOS #155 / `33954450152`, Q003C #152 / `33954450150`,
  đủ bảy job; Windows CI 77/77, MAUI 43/43, capture 177/128.
- Theo yêu cầu tiếp tục, mở đúng hai worktree từ baseline trên, giữ
  **`gpt-6-astra / xhigh`**, đã xác nhận hai task active/inProgress và claim:
  A `01a070ae-bcfe-7562-acad-9c53287433d3` / `feature/table-006-native-compat`;
  B `01a070af-9093-7303-a811-821cde640467` / `feature/ux-006-visual-localization`.
- A tiếp tục TABLE-006 native editor/structured references và producer corpus;
  B làm UX-006 Ribbon/Bar/Table Design/Filter chrome và resource localization.
  Chỉ phần chrome độc lập của UX-006 được bắt đầu sớm; chưa đóng TABLE-006.
- Khóa file, desktop, project/CI và cổng handoff ở
  [`TABLE_UX_WAVE_20260905.md`](TABLE_UX_WAVE_20260905.md). Desktop local chỉ A;
  B chạy native smokes/capture bằng GitHub runner riêng, không giành focus.
- Coordinator chỉ thay tài liệu điều phối; hai source task cũ đã completed.
  Không sửa production, không đóng gói demo hoặc publish NuGet trong dispatch.
  Markdown không đổi runtime nên không cần local regression/runtime mới;
  diff/links/architecture/packaging và exact-head GitHub gates vẫn bắt buộc.
- File handoff của worker: `docs/worklog/TABLE-006-NATIVE.md` và
  `docs/worklog/UX-006.md` ở nhánh riêng. Shared CURRENT/status/board chỉ coordinator ghi.
- Bước tiếp theo duy nhất: đọc handoff/source delta sau `2bc00eb6` của A rồi B,
  review phạm vi/giới hạn và kiểm chứng combined exact HEAD trước checkpoint sau.

## Previous integration — TABLE-005 / TABLE-RIBBON-012 / TABLE-006

- Branch `feature/bootstrap-architecture-v0.1`, PR #1 Draft/open/unmerged.
- Đã nhận A đến `d283a55b` (sáu commit gồm TABLE-005 một lần), sau đó chỉ
  delta B `cf923db2..7f73a97d` (ba commit). Không conflict hoặc ghi đè source
  worktree. Base `6d1beca7`, implementation kết hợp
  `e29acb44bc058e91a27c9dcc35a6979909d4dd5b` đã push.
- Regression mới qua Ribbon giữ giá trị Convert-to-range/Undo/Redo và tính lại
  dependent sau sửa ô nguồn. Native WPF dialog kiểm tra giá trị/formula, không
  còn chỉ kiểm tra metadata/history.
- Local: full solution/WPF build 0/0, Core **1470/1470**, MAUI **43/43**,
  loaded MAUI Windows Ribbon smoke success/3 frames, capture **177 ảnh/128
  layouts**, architecture/packaging pass, **18 nupkg**.
- Windows local **76/77**: một test dừng tại `window.Activate()` như checkpoint
  trước; không bỏ/nới assertion. Combined Windows CI vẫn là cổng bắt buộc.
- Exact-head implementation gates **success**, attempt 1, đủ bảy job: full
  #1333 / `33953936497`, iOS #154 / `33953936520`, Q003C #151 / `33953936475`.
  Windows CI **77/77**, capture **177/128**; không còn blocker tích hợp.
- TABLE-005 và TABLE-RIBBON-012 **DONE for defined scope** tại implementation
  `e29acb44`. Commit tài liệu này là descendant; SHA cuối lấy bằng
  `git rev-parse HEAD`, phải xanh cả ba workflow trước bàn giao. Final SHA/run
  URLs được ghi ở [PR handoff](https://github.com/HoangHung997/NeraSpreadSheet/pull/1#issuecomment-5550451701).
- Hai task nguồn đã bàn giao, không còn giữ quyền ghi vào integration.
  TABLE-006 mới xong headless scope; native structured-reference UX và corpus
  Excel/LibreOffice có provenance chưa làm. Không đóng gói demo ngoài repo,
  không thêm Avalonia hoặc tự mở task kế tiếp.
- Chi tiết file, commits, test và rollback:
  [`TABLE_RIBBON_INTEGRATION_20260905.md`](TABLE_RIBBON_INTEGRATION_20260905.md).
- Bước tiếp theo duy nhất: kiểm tra đủ ba workflow/mọi job trên HEAD cuối
  (kể cả commit tài liệu), rồi ghi SHA/run URLs bàn giao trên PR #1.

## Historical dispatch — TABLE-RIBBON-012 / TABLE-006

- Integration branch vẫn `feature/bootstrap-architecture-v0.1`; PR #1
  Draft/open/unmerged. Checkpoint tích hợp cuối đã xanh tại
  `488c61ea75ea6c8f7a6ceb480035a341f24c6c19`: full CI `33949294692`,
  iOS `33949294721`, Q003C `33949294687` đều success.
- TABLE-005 source hoàn thành tại `cf923db2bf88d9f67f980b4f78a3364bcfddbe47`,
  ba workflow `33940937260` / `33940938443` / `33940939892` đều success;
  source này chưa tích hợp vào branch chính của công việc.
- Đã gửi tạo đúng hai task worktree dùng `gpt-6-astra` / `xhigh`:
  TABLE-RIBBON-012 ghép Table Design vào Ribbon mới từ `488c61ea`;
  TABLE-006 làm Table/XLSX headless hardening từ source xanh `cf923db2`.
  Hai task đã ACTIVE: A `01a0704c-1f36-7232-a54f-bc67c965e89c`,
  B `01a0704c-92f9-7990-b266-066ba4580db6`.
- Quyền ghi độc quyền, ngoại lệ khởi động sớm, cổng và thứ tự lấy commit:
  [`TABLE_RIBBON_WAVE_20260905.md`](TABLE_RIBBON_WAVE_20260905.md).
- Coordinator chỉ thay tài liệu điều phối trong đợt dispatch; không đổi
  production code, không đóng gói lại demo ngoài repository. Không cần
  regression/runtime local mới cho Markdown; kiểm tra diff, architecture,
  packaging và exact-head GitHub gates cho commit tài liệu.
- Bước tiếp theo duy nhất: kiểm tra handoff lane A trước và chỉ delta sau
  `cf923db2` của lane B sau; xác minh combined HEAD trước checkpoint kế tiếp.

## Current integration — RIBBON-VISUAL-011

- Branch hiện tại: `feature/bootstrap-architecture-v0.1`; PR #1 vẫn
  **Draft, open, unmerged**, base `develop`.
- Đã tích hợp fast-forward đúng chín commit từ `feature/ribbon-visual-011`,
  từ `284ccb76e5f69e170356ffc66915dd6e290b68fb` đến
  `7cfcfdfc6f337da1b37ca05b9254b903a33f32d2`; không conflict, không đổi SHA
  hoặc viết lại production code. Nhánh `TABLE-005` chưa được tích hợp.
- Exact-head implementation checkpoint đã kiểm tra trực tiếp trên GitHub:
  **full CI #1324 / 33945704736, iOS #145 / 33945705984,
  Q003C #142 / 33945707221 đều success** tại `7cfcfdfc`.
- Kiểm tra lại trên checkout tích hợp: full solution và WPF preview build
  **0 warning / 0 error**; Core **1386/1386**, MAUI **41/41**,
  Windows **74/75**. Test duy nhất đỏ dừng tại `window.Activate()` trước khi
  chạy SDK behavior, khớp giới hạn foreground đã có; không bỏ hoặc nới test.
- Architecture và SDK packaging verifier **pass**; pack tạo **18 nupkg**;
  runtime capture **176 ảnh / 128 native layout snapshots**, status success.
- Giới hạn: Table Style trong preview chỉ xem trước; nối callback vào handler
  của TABLE-005 khi tích hợp lane đó. Ma trận raster chưa thay thế kiểm tra
  physical multi-monitor DPI. Không cập nhật app demo đóng gói ngoài repository.
- Hồ sơ tích hợp, file trọng tâm và rollback:
  [`RIBBON_VISUAL_INTEGRATION_20260905.md`](RIBBON_VISUAL_INTEGRATION_20260905.md).
- Commit đồng bộ tài liệu sau checkpoint trên vẫn phải qua đủ ba workflow tại
  chính HEAD cuối; kiểm tra bằng `git rev-parse HEAD` và `head_sha` của run.
  Kết quả bàn giao cuối được ghi trên PR #1, không dùng run của commit cha.
- Bước tiếp theo duy nhất sau khi HEAD cuối xanh: đọc và review handoff của
  `TABLE-005 — Contextual Table Design` trước khi tích hợp, giữ nguyên command
  identities/handlers và gắn gallery preview vào cùng đường dispatch.

## Historical isolated implementation — RIBBON-VISUAL-011

- Branch hiện tại: `feature/ribbon-visual-011`; base SHA:
  `284ccb76e5f69e170356ffc66915dd6e290b68fb`.
- Implementation cuối: `29bceded702ff31822b1e40f14d4f59a267436bb`; preview
  commit `ee7df1ebeba207c3b0488604abf8d578199bbdd5`; chuỗi trước đó:
  `1d0ad498`, `88ef9c03`, `a24cbcd0`, `621fb1ec`.
- Exact-head checkpoint đã push và xác minh:
  `47ccc882be420de1464888b3400606d040cd9194` — full CI #1323 /
  `33945177202`, iOS #144 / `33945179079`, Q003C #141 / `33945180352`
  đều **success**. Full CI có Core, Windows **75/75** + capture **176/128**,
  MAUI Windows/Android/Apple và toàn bộ loaded smokes xanh.
- Bản ghi này là documentation-only descendant của checkpoint trên. HEAD bàn
  giao lấy bằng `git rev-parse HEAD`; ba workflow phải được xác minh lại với
  chính SHA đó trước khi kết thúc, không suy diễn từ CI của commit cha.
- Fix `29bceded` chặn MAUI
  height-only/stale resize rebuild; smoke dùng visible stage và chờ native
  arrange, thêm popup persistence/native bounds diagnostics. Build 0/0,
  MAUI **41/41**, loaded smoke **3 lần liên tiếp success**. Regression mới
  đã tái hiện lỗi thay snapshot trên mã cũ trước khi bật guard.
- PR #1 vẫn Draft/unmerged; lane này không merge hoặc thay trạng thái PR.
- Shared dense layout ba hàng, bốn palette, caption đáy, QAT, gallery và
  Backstage/customization đã triển khai trên ba presenter. Preview dùng command
  thật; Table Style chỉ xem trước, không có mutation thứ hai của TABLE-005.
- Core **1386/1386**, focused Ribbon **75/75**, desktop Ribbon **16/16**,
  MAUI **41/41**, loaded MAUI Windows Ribbon smoke success. Build/analyzers
  **0 warning/error**, architecture/SDK packaging pass, **18 nupkg**.
- Full Windows **74/75** do test foreground đã có dừng ở `window.Activate()`
  trước SDK behavior; không bỏ hoặc nới test. Dark checkbox contrast regression
  đã được thêm; focused customization chạy lại **3/3**.
- Preview/capture: **176 ảnh / 128 native layout snapshots**, bốn widths và
  palettes, gallery More/customization, raster exports 125/150/200%; command
  smoke Bold/Undo, totals Average/Undo và gallery không đổi style đều pass.
- CI artifact của checkpoint: `ribbon-visual-matrix` / `9963130150`,
  `sdk-packages` / `9963106595`. CI #1322 cũ đỏ và nguyên nhân/fix được giữ
  trong lane worklog, không bỏ hoặc nới assertions.
- File trọng tâm, benchmark trước/sau và giới hạn: xem
  [`RIBBON-VISUAL-011.md`](RIBBON-VISUAL-011.md) và
  [`ribbon-visual-contract.md`](../ribbon-visual-contract.md).
- Bước tiếp theo duy nhất sau exact-head handoff: tích hợp chuỗi commit của
  `feature/ribbon-visual-011` vào nhánh tích hợp được duyệt, giữ command
  identities/handlers của TABLE-005 rồi chạy lại ba cổng ở SHA tích hợp.
  Thứ tự lấy bằng `git log --reverse 284ccb76..feature/ribbon-visual-011`.
  Không merge PR #1 trong lane này.

## Integration history (checkpoint before this isolated lane)

- Repository: `HoangHung997/NeraSpreadSheet`.
- Local branch: `feature/ribbon-table-filter-ux-plan`; integration branch:
  `feature/bootstrap-architecture-v0.1`.
- Pull request: #1 Draft, open, unmerged; base `develop`.
- Latest implementation exact-head CI checkpoint:
  `57a8c0c0fe8eb452bcb054432d2d37b9e9807e73`; full CI run `33936291893` /
  #1318, iOS run `33936291863` / #139 and Q003C/OpenXML run `33936291978` /
  #136 passed.
- Iconography implementation commit:
  `2b933fd8b04042c0c52854dd73651161e9ae9322`; implementation/docs head
  `661504994f2b411c5f7d5a7c88fe836176335ba5` passed all exact-head gates.
- Verified implementation CI through `EXCEL-BASIC-VISIBILITY-004`: full run
  `33852379077` / #1298 — success; iOS gate `33852379268` / #119 — success;
  Q003C/OpenXML gate `33852379096` / #116 — success.
- Formula implementation: **DONE**, **546/546** locked catalog names; current formula suite **524/524**.
- Q001, Q002, Q003A, Q003B: **DONE**.
- Q003C: **DONE for managed analytics/chart OpenXML persistence scope**.
- Q003D: **DONE for standard Excel PivotTable/PivotCache package preservation scope**.
- Core solution at the verified Q003D checkpoint: **1212/1212 passed**, build/analyzers **0 warnings / 0 errors**, OpenXML **65/65**.
- Ribbon/Bars desktop stack through `RIBBON-KEYBOARD`: **integrated and green**.
- `RIBBON-ICONOGRAPHY-006`: **DONE FOR DEFINED SCOPE**. A packable
  host-neutral catalog supplies 272 semantic keys, 242 SVG masters and 4,840
  PNG variants across five sizes and four themes. WPF, WinForms and MAUI
  Ribbon/Bar presenters resolve cached default icons while preserving host
  override precedence; 30 production commands now carry semantic icon keys.
- `RIBBON-MAUI`: **DONE**. MAUI presenters, shortcut/customization binding and
  Windows Ribbon smoke are integrated and exact-head CI passed at
  `b806cc7ed2317b456a6171672e577ee816e4692d`.
- `PIVOT-OPENXML-STANDARD`: **DONE for defined scope**. Standard PivotTable,
  PivotCache and PivotCacheRecords export/import are integrated, local OpenXML
  and Core tests pass, and Q003D preservation-only behavior remains intact.
- `DRAWING-MEDIA-COMPAT`: **DONE for defined scope**. First preservation gate covers
  worksheet drawing image anchors, sheet background pictures and legacy VML
  drawing parts through repeated preserved session saves. OpenXML 69/69, Core
  1216/1216, solution build 0 warnings/errors and architecture verification are
  green locally.
- `PACKAGING-SDK`: **DONE for package-readiness gate scope**. Package metadata,
  README packaging, explicit SDK package IDs, CI pack artifact upload and a
  packaging metadata verifier are exact-head validated.
- `SECURITY-RECOVERY`: **ACTIVE**. External provider exception containment,
  document/workbook/session save failure recovery, preserve-unknown worksheet
  topology rejection and OpenXML graph malformed escaped URI rejection,
  including direct validators plus archive-level package relationship,
  duplicate-entry and content-type override scans, are exact-head validated;
  additional bounded trust/recovery coverage remains before
  localization/a11y completion and the final Windows 11 demo.
- `EXCEL-BASIC-COMPAT-001`: **DONE for defined scope as a bounded compatibility
  hotfix**. Excel `bgColor`-only differential fills load, unsupported preserved
  conditional-format rules remain opaque, formula completion/point mode/static
  precedent analysis are host-neutral, and WPF binds suggestions, mouse range
  insertion and colored precedent outlines. The supplied six-sheet workbook
  passes Load -> Save -> Load without modifying the source. Full CI #1290,
  iOS gate #111 and Q003C/OpenXML gate #108 passed at the implementation
  checkpoint.
- `EXCEL-BASIC-NAV-002`: **DONE FOR DEFINED SCOPE**. The viewport now
  offers an opt-in adaptive extent derived from used content and the active
  navigation cell. WPF/WinForms arrow, Enter and Tab navigation keep the active
  cell visible; empty navigation tails contract after returning. Viewport
  58/58, loaded desktop smoke 2/2, Core 1243/1243, build/analyzers and
  architecture verification pass locally. Full CI #1292, iOS gate #113 and
  Q003C/OpenXML gate #110 passed at the implementation checkpoint.
- `EXCEL-BASIC-NAV-003`: **DONE FOR DEFINED SCOPE**. The
  reusable adaptive extent now keeps at least one viewport plus a configurable
  default tail of 100 rows and 20 columns. Manual scrollbar movement preserves
  selection and the current viewport without compounded extent growth;
  keyboard navigation still moves selection and scrolls it into view. Viewport
  59/59, focused loaded desktop smoke 2/2, Core 1244/1244, build/analyzers and
  architecture verification pass locally. The external Win11 demo build and
  packaged smoke pass with the lighter demo-only chrome. Full CI #1294, iOS
  gate #115 and Q003C/OpenXML gate #112 passed at the implementation checkpoint.
- `EXCEL-BASIC-COMPAT-002`: **DONE FOR DEFINED SCOPE**. Recalculation of
  the supplied six-sheet workbook now preserves its 28 existing Excel error
  cells and creates zero new errors, compared with 981 total/953 new errors
  before the fix. VLOOKUP/HLOOKUP no longer propagate unrelated table-array
  errors, whole-column VLOOKUP uses sparse used-row evaluation with full-range
  dependencies, WPF supports Ctrl+wheel zoom from 25%-400%, and the reusable
  editor overlay matches cell typography/alignment/wrapping with Alt+Enter line
  breaks. Full CI #1296, iOS gate #117 and Q003C/OpenXML gate #114 passed at
  implementation checkpoint `d86376403e15011304974c5476fe11683347fd19`.
- `EXCEL-BASIC-VISIBILITY-004`: **DONE FOR DEFINED SCOPE**. Excel
  desktop observation confirmed arrow navigation skips hidden rows/columns.
  The SDK now stores manual visibility as normalized sparse intervals, retains
  custom sizes, maps visibility through structural edits/reorder, round-trips
  standard XLSX hidden flags, provides undoable commands and makes WPF/WinForms
  normal and split keyboard paths skip hidden axes. Implementation checkpoint
  `944fadd9864bfeca41abf9ff8e155305fc8cd06c` passed full CI #1298, iOS gate
  #119 and Q003C/OpenXML gate #116; SDK package artifact 9928856103 was
  produced.
- `EXCEL-FORMAT-EDIT-HELP-005`: **IMPLEMENTED LOCALLY; CI PENDING**. The SDK
  now owns shared Excel-compatible value formatting and expanded Format Cells
  metadata/import/export, cross-backend text style intent, full-cell editor
  measurement with viewport clipping, Enter/Alt+Enter semantics, indexed
  incremental calculation and formula signature/argument help for the callable
  function surface. The WPF invalid text-key shortcut crash is fixed, and the
  external Win11 demo consumes the new APIs without forcing a full recalculate
  after each edit.
- Weighted implementation-roadmap score: **83.98% ≈ 84%**.
- `RTFUX-2026`: **ACTIVE**. The complete Table/Filter/Ribbon/UX delivery
  sequence and two-lane ownership protocol are recorded in
  `docs/ribbon-table-filter-ux-delivery-plan.md` and
  `docs/worklog/RIBBON_TABLE_FILTER_UX.md`. With two independent lanes the
  target release-candidate date is 28/10/2026; the one-lane fallback target is
  20/11/2026. `RIBBON-007` and `FILTER-005` completed in isolated worktrees and
  were cherry-picked without conflicts, then integration review hardened Ribbon
  focus/selection/overflow and rich-filter wildcard/month/dxf/sort semantics.
  Local gates are Core solution **1296/1296**, OpenXML **93/93**, MAUI
  **34/34**, focused desktop Ribbon **3/3**, and loaded MAUI Windows Ribbon
  smoke **success**. Integrated exact head
  `05c6974fa907f5022f28c85f13f06dbb35288556` passed full CI #1307, iOS #128
  and Q003C/OpenXML #125. `RIBBON-008 Full Item Model` and `FILTER-006 Native
  Rich Filter UX` completed in two isolated worktrees from that exact SHA. The
  initial handoffs failed independent review, were hardened in their own lanes,
  and were integrated without conflict. Combined gates: Core solution
  **1324/1324**, MAUI **36/36**, focused desktop **7/7**, loaded MAUI Windows
  Ribbon and Table-filter smokes **success**. Exact combined head
  `d595539d616cba1bb5543ab3530035f927304069` passed full CI #1309, iOS #130
  and Q003C/OpenXML #127. `RIBBON-009 Contextual QAT Key Tips` and `FILTER-007
  Sort Reapply Accessibility` then completed in two isolated worktrees, passed
  independent blocker reviews and integrated without conflict. Combined gates:
  Core solution **1354/1354**, MAUI **40/40**, focused desktop
  Ribbon/Table-filter **13/13**, and both loaded MAUI Windows Ribbon and
  Table-filter smokes **success**. Exact combined head
  `f75e2f103598cbbe7b5c22f92c3ab8dd755ef8c3` passed full CI #1312, iOS #133
  and Q003C/OpenXML #130. `RIBBON-010 Customization SDK` and `TABLE-004 Table
  Style Engine` then completed in isolated worktrees and were integrated without
  conflict. Combined gates: Core solution **1372/1372**, MAUI **41/41**,
  focused desktop **4/4**, loaded MAUI Windows Ribbon and Table-filter smokes
  **success**, architecture and packaging verification **passed**. Exact
  combined head `57a8c0c0fe8eb452bcb054432d2d37b9e9807e73` passed full CI #1318,
  iOS #139 and Q003C/OpenXML #136. Both checkpoints are `DONE`; `TABLE-005 —
  Contextual Table Design` is active from the exact green integration branch in
  a new worktree with `thinking: high`.
  Schedule commit
  `f9340c1bf3c59e2c85336c961cf017d2c9ef8858` passed full CI #1303, iOS #124
  and Q003C/OpenXML #121.
- PR remains Draft; do not merge or mark Ready.

## Q003B/Q003C/Q003D checkpoint

- Floating chart/pivot placement, select/move/resize, Undo/Redo and cross-host native accessibility are closed across WPF, WinForms, MAUI Windows, Android, iOS and Mac Catalyst.
- Managed charts materialize into standard XLSX drawing/chart parts through `SpreadsheetSession.SaveSessionAsync` and remain stable across repeated session round trips.
- Foreign drawing content is preserved when the explicit `PreserveUnknownParts = true` import/export contract is enabled.
- Q003D adds a schema-valid standard Excel PivotTable/PivotCache fixture and proves preservation across repeated `SpreadsheetSession` Load/Save cycles.
- Q003D preserves workbook/cache, worksheet/pivot and pivot/cache relationship IDs, part URIs, pivot identity and worksheet source metadata.
- External standard Excel PivotTables are deliberately not silently reclassified as Nera-managed pivots.
- Q003D required no production serializer change; the existing package-envelope preservation path already satisfies this bounded compatibility contract.

## Ribbon/Bars desktop checkpoint

- Immutable Ribbon/Bars customization, deterministic JSON persistence and legacy migration are integrated.
- Command presentation snapshots/runtime controllers execute through the shared command dispatcher.
- Native WPF/WinForms Ribbon, toolbar, menu, context-menu presenters, customization dialogs and normalized shortcut bindings are integrated.
- Loaded desktop smokes remain green in exact-head CI.

## Validation

At implementation checkpoint `275c1b4e5e24b5c53d27546492c4e343509e127f`:

- Core solution: **1228/1228 passed**.
- Formula: **518/518 passed**.
- OpenXML: **81/81 passed**.
- Build/analyzers: **0 warnings, 0 errors**.
- Architecture verification: **passed**.
- SDK packaging metadata verification: **passed**.
- SDK package pack + `sdk-packages` artifact upload: **passed**.
- External formula provider exception containment tests: **passed**.
- Document save failure recovery test: **passed**.
- Workbook save failure recovery test: **passed**.
- Session save failure recovery test: **passed**.
- Preserve-unknown worksheet replacement atomic rejection test: **passed**.
- OpenXML malformed escaped relationship target rejection test: **passed**.
- OpenXML malformed escaped part URI and relationship type rejection tests:
  **passed**.
- OpenXML package-level malformed escaped part URI, relationship type and
  external target rejection tests: **passed**.
- OpenXML malformed escaped content-type override `PartName` rejection test:
  **passed**.
- OpenXML duplicate ZIP part entry rejection test: **passed**.
- OpenXML common Excel hyperlink/file/relative/fragment target acceptance test:
  **passed**.
- Windows desktop GPU runtime smoke: **passed**.
- Android loaded analytics accessibility smoke: **passed**.
- iOS loaded VoiceOver analytics accessibility smoke: **passed**.
- Mac Catalyst loaded VoiceOver analytics accessibility smoke: **passed**.
- MAUI Windows handler + loaded Table-filter/runtime/analytics/scale smokes: **passed**.
- MAUI Windows loaded Ribbon smoke: **passed**.

For `EXCEL-BASIC-COMPAT-001` local validation on the current uncommitted tree:

- OpenXML: **83/83 passed**;
- Editing: **214/214 passed**;
- Rendering.Spreadsheet: **120/120 passed**;
- Formulas: **522/522 passed**;
- Viewport: **56/56 passed** after retaining stable equality for the new default
  formula-reference color palette;
- Core solution: **1241/1241 passed**;
- focused WPF formula editing/reference smoke: **1/1 passed**;
- supplied workbook in-memory Load -> Save -> Load: **6/6 sheets retained**,
  source SHA-256 unchanged and **0 OpenXML schema validation errors**;
- full Windows.Rendering: **49/51 passed locally**; two unrelated,
  environment-sensitive UI/scale assertions remain red on this desktop
  (`WpfAutomationPeerExposesChartInvokeMoveAndResizePatterns` expects 10 px but
  observes 9.6 px, and a window activation smoke cannot activate). Exact-head
  GitHub CI remains the release gate.

For `EXCEL-BASIC-NAV-002` local validation:

- Viewport: **58/58 passed**;
- focused loaded WPF/WinForms adaptive keyboard smoke: **2/2 passed**;
- Core solution: **1243/1243 passed**;
- build/analyzers: **0 warnings, 0 errors**;
- architecture verification: **passed**;
- external Win11 demo build, internal smoke and supplied-workbook smoke:
  **passed**, with the source workbook unchanged.

For `EXCEL-BASIC-NAV-003` local validation:

- Viewport: **59/59 passed**;
- focused loaded WPF/WinForms adaptive navigation smoke: **2/2 passed**;
- Core solution: **1244/1244 passed**;
- build/analyzers: **0 warnings, 0 errors**;
- architecture verification: **passed**;
- external Win11 demo build and internal smoke: **passed**;
- full Windows.Rendering: **51/53 passed locally**; the same two unrelated
  environment-sensitive WPF automation/activation assertions documented above
  remain red on this desktop;
- supplied workbook smoke could not be rerun because Excel currently holds the
  user's source file open. The preceding navigation checkpoint already passed
  this unchanged serializer path and this lane does not modify OpenXML code.

For `EXCEL-BASIC-COMPAT-002` local validation:

- supplied workbook: **1273 formula cells**, cached **28 errors**, prior Nera
  recalculation **981 errors**, fixed recalculation **28 errors / 0 new**;
- Formulas: **524/524 passed**;
- focused WPF editor/zoom/formula smoke: **2/2 passed**;
- Core solution: **1246/1246 passed**;
- build/analyzers: **0 warnings, 0 errors**;
- architecture verification: **passed**;
- external Win11 demo build and supplied-workbook recalculation/round-trip
  smoke: **passed**;
- full Windows.Rendering: **52/54 passed locally**; the same two unrelated,
  environment-sensitive WPF automation/activation assertions documented above
  remain red on this desktop.

For `EXCEL-BASIC-VISIBILITY-004` local validation:

- Excel desktop: Down from `A107` skipped hidden rows 108-148 to `A149`; Right
  from `A107` skipped a temporarily hidden column B to `C107`; the temporary
  hide was undone and the workbook was not saved;
- Core solution: **1254/1254 passed**;
- focused Core, Editing, Viewport, OpenXML and loaded WPF/WinForms visibility
  tests: **12/12 passed**;
- full solution build/analyzers: **0 warnings, 0 errors**;
- architecture verification and SDK packaging verification: **passed**;
- external Win11 demo build, internal smoke and supplied-workbook smoke:
  **passed**;
- full Windows.Rendering: **54/56 passed locally**; the same two unrelated,
  environment-sensitive WPF automation/activation assertions documented above
  remain red on this desktop.

For `EXCEL-FORMAT-EDIT-HELP-005` local validation:

- Core solution: **1266/1266 passed**;
- Core formatter: **5/5 passed**;
- Formulas: **526/526 passed**; Editing: **221/221 passed**; OpenXML:
  **85/85 passed**; Rendering.Spreadsheet: **121/121 passed**; Skia:
  **14/14 passed**;
- focused WPF formula/editor/shortcut tests: **8/8 passed**;
- WPF analytics accessibility move/resize regression: **1/1 passed** after
  preserving fractional device-to-document deltas at non-100% DPI;
- Windows.Rendering: **58/58 passed** when the known native mouse smoke that
  requires this background process to become the Windows foreground app is
  excluded; that one smoke stops at `window.Activate()` before exercising SDK
  behavior on this desktop;
- build/analyzers: **0 warnings, 0 errors**;
- architecture verification and SDK packaging verification: **passed**;
- external Win11 demo build and supplied-workbook smoke: **passed**;
- supplied workbook: **6 sheets**, **28 cached formula errors before and 28
  after** recalculation, no newly-created errors; source SHA-256 remained
  `FD999A5AD06FB66668C6E296D45156F32A08B86CB07A9B908CB94C10F70FF772`.

For `RIBBON-ICONOGRAPHY-006` local validation:

- generated catalog: **272 semantic keys**, **242 unique SVG masters** and
  **4,840 PNG variants**;
- Core solution: **1270/1270 passed**;
- icon catalog: **4/4 passed**; focused loaded WPF/WinForms Ribbon:
  **4/4 passed**; MAUI: **34/34 passed**;
- loaded MAUI Windows Ribbon smoke: **success**, including default Ribbon and
  Bar image-source resolution;
- WPF, WinForms and MAUI Windows builds/analyzers: **0 warnings, 0 errors**;
- architecture verification and SDK packaging verification: **passed**;
- `NeraSpreadSheet.Iconography.0.1.0.nupkg` packs the assembly, XML docs,
  README, Fluent MIT license and NOTICE;
- light and dark contact sheets were inspected. The full Windows suite is
  **58/59 locally** because the already documented foreground-only native
  mouse smoke stops at `window.Activate()` before exercising SDK behavior.
- exact-head GitHub gates at `661504994f2b411c5f7d5a7c88fe836176335ba5`:
  full CI `33869186494` / #1301, iOS `33869186323` / #122 and Q003C/OpenXML
  `33869186310` / #119 — all **success**;
- GitHub `sdk-packages` artifact: `9935206623`, digest
  `sha256:5f852ced9c8c5fe5a12862c06f82eb42201cd3bd3df5d34dbc80408a3f91a8d1`.

## Historical next step before Ribbon visual integration

Monitor the isolated `TABLE-005 — Contextual Table Design` worktree. Review its
pushed handoff, integrate only its approved commit chain, then require full CI,
iOS and Q003C/OpenXML success at the resulting exact SHA before opening
`TABLE-006`.

## Remaining limits

- Pivot refresh/calculation equivalence, user-mode destination-cell modeling,
  slicers/timelines and broader Excel UI parity remain outside the current
  standard pivot lane.
- Broader drawing/media compatibility remains beyond the managed chart + foreign drawing preservation gates.
- User-facing drawing/image editing tools and rich media semantic import remain
  outside the first Drawing/Media preservation lane.
- Plugin trust/isolation/recovery, broader performance/security corpora and
  final release acceptance remain incomplete.
- Split-pane adaptive scrollbar topology, the MAUI adaptive host opt-in and
  style-only whole-row/whole-column used-tail discovery remain outside the
  adaptive navigation contract.
