# TABLE-006 — XLSX compatibility và hardening

> Cập nhật coordinator 05/09/2026: source final `7f73a97d` đã xanh cả ba
> workflow/mọi job và delta sau `cf923db2` đã ghép vào `e29acb44` sau lane A.
> Đây vẫn chỉ là headless scope, chưa đóng native UX/corpus. Các số liệu source
> bên dưới là lịch sử; combined/final gates và runtime nằm trong
> [integration worklog](TABLE_RIBBON_INTEGRATION_20260905.md).

- Trạng thái: `CI` — implementation headless đã qua local gates; chưa đóng toàn
  TABLE-006 vì thiếu native producer corpus và native point-mode wiring.
- Branch: `feature/table-006-compat-hardening`.
- Base: `cf923db2bf88d9f67f980b4f78a3364bcfddbe47` (TABLE-005 source xanh).
- Implementation chính: `79f7296895f2ad01d7ab3a10260134a0b841e0ad`;
  `c7b31253d960016d38d2cca7b350b6e48f5c0b36` chứa edge hardening ban đầu;
  candidate này đã **superseded** bởi fix ConvertToRange bên dưới.
- Chỉ bàn giao delta `cf923db2..HEAD`; ba commit TABLE-005 không nhập lại.
- Owner: lane B, headless Core/Editing/Formulas/OpenXml và tests Table.
- Không sửa UI, Commands/Ribbon, project/CI, sample hay tài liệu điều phối.

## Audit trước sửa

Đã đọc AGENTS, README, ARCHITECTURE, current-status, CURRENT, delivery plan,
TABLE-005 worklog/contract, Table structured-reference/style, structural-editing,
reference-selection, rich-filter contracts và source/tests/benchmark liên quan.
Đã đọc wave contract tại `6d1beca735acc869b1e5b3ea1e2e3794a02644e7`
bằng `git show`; không cherry-pick tài liệu điều phối.

Khoảng trống xác định từ source:

- `totalsRowFunction` bị bỏ qua; `totalsRowShown` bị dùng như số hàng hiện tại.
- Numeric Table ID chưa được kiểm tra, một số unsupported attributes bị bỏ.
- Preservation nối Table theo relationship ID sinh mới, làm rơi Table ngoại.
- Column extensions/attributes chưa được giữ khi cập nhật Table definition.
- Structured rename dùng prefix `@name`; parser có thể đổi union thành range.
- Completion/point-mode và static reference analysis hiện chỉ biết A1.
- Corpus hiện do Nera/OpenXML tạo; chưa có provenance native Excel/LibreOffice.

## Quyền file đã xác nhận

Coordinator đã xác nhận quyền sửa đúng call site Table trong
`NeraOpenXmlWorkbookSerializer.LoadCore`: truyền validation context numeric IDs
toàn workbook và import preservation option cho Table style. Chỉ hai call site
Table được thay đổi; không đổi serializer ngoài Table. Không còn WAITING.

## Implementation và regression

- Pre-fix baseline: corpus mới tái hiện **16/21** XLSX cases thất bại và **5/6**
  structured-reference cases thất bại trước production fix; các test biên còn
  lại được thêm khi audit sâu hơn.
- Sửa totalsRowCount/totalsRowFunction; Table-level sort state; numeric ID
  preflight; InvalidDataException kèm inner cause; invalid stable ID/array/
  duplicate/formula-label/sort-range rejection.
- Preservation ghép foreign Table theo stable identity, giữ relationship/URI,
  numeric Table/column ID và column extensions; remap unknown criteria theo
  retained column identity; supported Clear không resurrect filter/sort cũ.
- Strict unsupported custom style bị từ chối thay vì âm thầm mất style.
- Shared translator sửa prefix rename, escape và unsupported selector; static
  analyzer không bỏ sót A1 trong mixed formulas. Tables.Add từ chối spill.
- Mở rộng assistant hiện hữu qua partial file, không tạo facade hoặc engine
  riêng: bounded completion, stable-ID application, provisional point-mode và
  workbook-aware reference analyzer. Native host không đổi.
- Style/banding/filter-button không tính toán/project cells, Undo/Redo dùng
  `AffectsCalculation=false` hiện hữu. Sparse capture/occupancy dùng bounded
  lookup cho Table nhỏ.

## Local validation

- SDK .NET **10.0.302** có sẵn; không cài SDK/workload/package mới.
- Core solution Release build/analyzers: **0 warnings / 0 errors**.
- Full Core solution: **1450 passed, 0 failed, 0 skipped** (baseline 1388).
- Core **134**, Editing **276**, Formulas **535**, OpenXML **133**, Commands
  **102**, Rendering.Spreadsheet **128**, Skia **15**, Viewport **61**; các
  Foundation/Interaction/Layout/Scrolling/Export/Iconography suite còn lại xanh.
- Corpus TableCompatibilityHardeningTests **36/36**; structured hardening
  **9/9**; new Editing structured/reference/safety/perf regressions **16/16**;
  strict-style regression **1/1**. Tổng tăng **62 tests**.
- Final edge review: reserved column name `#Data` không đổi area selector;
  completion trước closing bracket replace toàn fragment; Add/import từ chối
  duplicate Table GUID toàn workbook; unsupported sort geometry rewrite bị
  từ chối trước khi ghi destination (old bytes giữ nguyên).
- Late blocker tại `c7b31253`: ConvertToRange biến calculated value `20` thành
  `#REF!`. Regression đỏ trước fix; không coi candidate đó được nghiệm thu.
  Convert nay rewrite riêng target Table sang A1 trước remove, bằng
  FormulaRewritingTableOperationBase và RecalculateAffected hiện hữu. Kiểm tra
  giá trị ngay sau convert, mixed A1/structured, cross-sheet formulas, metadata
  của Table khác, relative current-row projection, Undo/Redo và strict/preserved
  repeated session round-trip. Unsupported target reference reject nguyên tử,
  khôi phục cả những worksheet đã rewrite trước khi phát hiện lỗi.
- In-memory session smoke: **20 loại mutation**, mỗi loại đúng một history
  entry, Undo/Redo/cell values/formulas/metadata và Save -> Load -> Save, ở cả
  strict và preserve. Kiểm tra schema, stable table/column IDs, style/options,
  filter/sort, header/totals, calculated/totals formulas và convert-to-range.
- Foreign-graph synthetic corpus: ba cycles giữ relationship ID, part URI,
  numeric IDs, column extensions và rename; unsupported filter + hidden buttons
  + column insertion được kiểm tra riêng. Malformed input giữ nguyên source bytes.
- Architecture verifier, packaging SDK verifier và `git diff --check`: passed.
- Không chạy desktop local trong lane B. Platform runtime acceptance chờ các
  remote smokes hiện hữu trong ba CI gates.

Reproduce:

```powershell
dotnet build NeraSpreadSheet.Core.slnx -c Release
dotnet test NeraSpreadSheet.Core.slnx -c Release --no-build
./scripts/verify-architecture.ps1
./scripts/verify-packaging-sdk.ps1
dotnet run --project benchmarks/NeraSpreadSheet.Benchmarks -c Release -- --filter '*TableCompatibilityBenchmarks*' --job short --warmupCount 1 --iterationCount 3 --launchCount 1
```

## Performance evidence

BenchmarkDotNet 0.15.8, .NET SDK 10.0.302/runtime 10.0.10, Windows x64,
Intel i5-13500H; một launch, một warmup, ba measured iterations. Không commit
raw artifacts hoặc đường dẫn máy.

| Method | Unrelated used cells | Mean | Allocation/op |
| --- | ---: | ---: | ---: |
| FilterButtonToggleAndUndo | 0 | 2.045 µs | 8.408 B |
| FilterButtonToggleAndUndo | 100.000 | 2.018 µs | 8.408 B |
| ColumnCompletion | 0 | 130.6 ns | 648 B |
| ColumnCompletion | 100.000 | 133.8 ns | 648 B |

Đây là so sánh sparse occupancy cùng implementation, không phải số đo trước/
sau trên hai phiên bản. Short run chưa đủ làm performance SLO. Topology edits
và reference safety vẫn phụ thuộc workbook formula/used-cell inventory; chỉ
visual metadata và completion được chứng minh độc lập unrelated cell count.

## Handoff và giới hạn

- Handoff chỉ gồm delta **`cf923db2..HEAD`** trên nhánh riêng. Không lấy lại
  `c8793a4f`, `0c2c64f6`, `cf923db2` và không mang docs/status từ TABLE-005 vào
  integration. Coordinator nhập sau lane A và kiểm chứng combined HEAD.
- Full CI/iOS/Q003C đã dispatch ở implementation chính: runs `33951954012`,
  `33951955611`, `33951956776`. Đây chỉ là checkpoint trước edge hardening;
  không dùng chúng để tuyên bố final HEAD xanh.
- Candidate superseded `c7b31253`: iOS `33952222971` và Q003C `33952224553`
  success; full CI `33952221232` failure ở MAUI Windows Ribbon split-button
  focus restoration. Core/Windows/Android/Apple jobs đều success. Job log
  `101269029022` cho thấy cùng focus timing symptom đã ghi ở TABLE-005; không
  sửa/nới native smoke trong lane B. Final HEAD vẫn phải xanh toàn bộ jobs.
- Final SHA và exact-head run URLs được gửi trong handoff về coordinator sau
  commit này, khi verify đủ ba workflow cùng toàn bộ jobs. Bản ghi trong Git
  phản ánh thời điểm viết trước CI; worker phải dispatch lại trên commit chứa
  tài liệu này và chờ xanh trước khi kết thúc task.
- Corpus chỉ Nera/OpenXML synthetic. **Chưa có native Excel/LibreOffice
  provenance**, chưa live-test Excel, chưa có native point-mode/completion
  wiring. Các gap này giữ toàn TABLE-006 ở trạng thái chưa đóng.
- Unsupported multi-area selectors, nested partial completion, query/XML Table,
  array Table formulas và các totals function ngoài contract chưa hỗ trợ.
- Per-column filter-button visibility vẫn chuẩn hóa theo Table-wide contract.
- Tài liệu semantic-owned/preserve-only và API handoff:
  `docs/table-compatibility-hardening-contract.md`.

Rollback: revert riêng commit implementation/documentation của delta TABLE-006;
không schema/model migration hoặc package dependency mới. PR #1 vẫn Draft,
open và unmerged; worker không sửa PR hoặc integration branch.
