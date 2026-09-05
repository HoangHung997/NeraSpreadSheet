# Đợt song song Table/Ribbon — 05/09/2026

## Cập nhật tích hợp

Hai task đã bàn giao source final: A `d283a55b`, B `7f73a97d`; coordinator
đã kiểm tra đủ ba workflow/mọi job xanh cho từng source. Đã lấy sáu commit A
rồi chỉ ba commit sau `cf923db2` của B vào integration, không conflict.
Combined implementation `e29acb44` bổ sung regression và native dialog smoke
cho giá trị Convert-to-range. Build/tests local và đủ ba combined exact-head
gates đã xanh tại `e29acb44`: full `33953936497`, iOS `33953936520`, Q003C
`33953936475`. TABLE-005/TABLE-RIBBON-012 đóng defined scope; tài liệu cuối
cũng phải xanh tại SHA của nó, theo
[integration worklog](TABLE_RIBBON_INTEGRATION_20260905.md).
Từ lúc bàn giao, coordinator sở hữu sửa integration; không còn hai writer đang
chạy trên các file lane. Các mục ACTIVE bên dưới là lịch sử dispatch, không
phải trạng thái hiện tại. TABLE-006 chỉ bàn giao headless scope; native UX và
producer corpus vẫn là công việc chưa đóng. Không mở thêm task trong lượt này.

## Quyết định và mốc xuất phát

Theo yêu cầu người dùng, mở đúng hai task worktree riêng với
`model: gpt-6-astra`, `thinking: xhigh`. Không tự chuyển model hoặc mở thêm
tác nhân. Coordinator là single writer của tài liệu điều phối và nhánh
`feature/bootstrap-architecture-v0.1`; PR #1 vẫn Draft, open, unmerged.

- Ribbon đã tích hợp và xanh tại
  `488c61ea75ea6c8f7a6ceb480035a341f24c6c19`: full CI `33949294692`,
  iOS `33949294721`, Q003C `33949294687` đều success ở đúng SHA.
- TABLE-005 hoàn thành ở nhánh riêng tại
  `cf923db2bf88d9f67f980b4f78a3364bcfddbe47`: full CI `33940937260`,
  iOS `33940938443`, Q003C `33940939892` đều success ở đúng SHA.
  Đây chưa phải checkpoint tích hợp TABLE-005.
- TABLE-005 gồm đúng ba commit từ base `57a8c0c0`:
  `c8793a4f9181dc0f7c2f0f6e393ab7df51b198f8`,
  `0c2c64f65d51c9f21a7bcd3cadb9f53b4aa1060b`,
  `cf923db2bf88d9f67f980b4f78a3364bcfddbe47`.

Ngoại lệ tăng tốc có giới hạn so với lịch tuần tự: TABLE-006 được bắt đầu
phần headless trên source TABLE-005 đã xanh, trong lúc lane A ghép source đó
vào Ribbon mới. Không coi TABLE-005 là integrated và không bỏ cổng combined CI.

## Phân công độc quyền

| Lane | Task | Branch dự kiến | Base | Công việc |
| --- | --- | --- | --- | --- |
| A | TABLE-RIBBON-012 — Table Design integration and UX | `feature/table-ribbon-012-integration` | `488c61ea` | Nhập TABLE-005, nối contextual tab/gallery/commands và UI tham số vào dense Ribbon, kiểm tra rộng/hẹp/keyboard/theme |
| B | TABLE-006 — XLSX compatibility and hardening | `feature/table-006-compat-hardening` | `cf923db2` | Table/Core/Editing/OpenXML compatibility, structured references, round-trip, atomic validation và sparse performance |

Hai task đã ACTIVE và đã xác nhận turn `inProgress` bằng task wait snapshot:

- A: `01a0704c-1f36-7232-a54f-bc67c965e89c`, branch
  `feature/table-ribbon-012-integration` từ `488c61ea`.
- B: `01a0704c-92f9-7990-b266-066ba4580db6`, branch
  `feature/table-006-compat-hardening` từ `cf923db2`.

Không dùng client ID tạm cho công cụ yêu cầu task ID thật. Các lần khởi tạo
ban đầu thất bại vì checkout chỉ fetch integration branch, thiếu mapping cho
`origin/main`. Đã fetch đúng remote ref, bổ sung riêng mapping main và tạo
local tracking branch ở đúng remote SHA trước khi tạo lại thành công. Không
thay đổi code hoặc lịch sử main. Các attempt thất bại không có task chạy.

### Lane A

- Sở hữu `src/NeraSpreadSheet.Ribbon.Core`, các file Ribbon/Bar và
  TableDesign binding của WPF/WinForms/MAUI, cùng presentation/native tests.
- Riêng wave này, coordinator ủy quyền WPF sample shell và script capture
  Ribbon hiện hữu cho lane A; lane B và coordinator không sửa đồng thời.
- Được nhập nguyên ba commit TABLE-005 làm baseline bất biến. Core, Editing,
  OpenXml, Commands abstraction và Rendering.Spreadsheet trong các commit nhập
  không trở thành quyền phát triển tiếp của lane A.
- Conflict làm thay đổi core semantics phải được bàn giao, không tự resolve
  bằng một implementation cạnh tranh.
- Gắn 19 Table commands qua `SpreadsheetSession.Tables`/command dispatcher;
  style gallery phải mutate thật và Undo được. Rename/resize/formula/duplicates
  dùng typed parameters hiện hữu, cancel không tạo history.
- Giữ VISUAL-011 ba hàng, caption đáy, bốn palette, QAT/customization/key tips,
  overflow và focus restoration. Screenshot phải có smoke thực thi command.
- Chỉ viết `docs/worklog/TABLE-RIBBON-012.md` và contract riêng
  `docs/table-ribbon-integration-contract.md`.

### Lane B

- Sở hữu Table-specific Core, Editing, OpenXml, Formulas/structured-reference
  engine và headless tests/benchmark/corpus tương ứng.
- Không sửa Commands abstraction, Ribbon.Core, rendering/viewport, platform
  host, native smoke, sample hoặc resources dùng chung.
- Kiểm chứng Save -> Load -> Save và mutation/Undo/Redo cho style, filter/sort,
  header/totals, calculated columns, rename và stable identity/relationships.
- Malformed/unsupported input phải có atomic rejection hoặc preservation
  đúng contract; không âm thầm mất dữ liệu hoặc đổi nghĩa formula.
- Corpus synthetic có provenance rõ. Không gọi fixture là Excel/LibreOffice
  native khi chưa có chứng cứ. Không điều khiển desktop đang mở trong wave
  này; native UI thuộc lane A. Gap chưa kiểm chứng phải được ghi rõ.
- Structured-reference point-mode chỉ làm phần shared/headless trong lane B;
  native wiring cần handoff riêng. Không dựng model/engine song song.
- Chỉ viết `docs/worklog/TABLE-006.md` và contract riêng
  `docs/table-compatibility-hardening-contract.md`.

## Khóa file và thứ tự tích hợp

1. Mỗi task claim base và owned paths trong worklog riêng trước khi sửa.
2. Chỉ coordinator sửa `CURRENT.md`, `current-status.md`, execution board,
   delivery plan và file wave này. Project/solution/CI/shared resources cũng
   thuộc coordinator, trừ ngoại lệ WPF sample/capture đã cấp cho lane A.
3. Task cần file ngoài quyền ghi WAITING và yêu cầu transfer. Task đang sở hữu
   phải dừng sửa, commit/handoff rồi coordinator mới chuyển quyền. Không cùng
   ghi và hẹn giải quyết conflict sau.
4. Hai lane chỉ push nhánh riêng; không merge PR #1 hoặc tự tích hợp lẫn nhau.
5. Review lane A trước: delta `488c61ea..A_HEAD`, bao gồm TABLE-005 nhập một
   lần. Sau đó chỉ lấy delta `cf923db2..B_HEAD` của lane B, không lấy lại ba
   commit TABLE-005 hoặc status cũ từ nhánh nguồn.
6. Combined HEAD phải qua build/regression/native smoke/architecture/packaging
   và đủ ba workflow xanh ở đúng SHA cuối. CI xanh riêng từng lane không thay
   cổng này. Không tự phát hành app demo/NuGet từ source chưa tích hợp.

## Cổng bàn giao

- Build/analyzers 0 warning/error; regression modules bị ảnh hưởng và full
  Core suite; architecture và packaging verifier.
- A: loaded desktop/MAUI Ribbon smokes, gallery/totals mutation + Undo,
  adaptive layout/theme/keyboard matrix và ảnh không có dữ liệu cá nhân.
- B: in-memory session/round-trip smoke, schema/preservation/identity checks,
  bounded performance evidence; native CI smokes hiện hữu vẫn phải xanh.
- Cả hai: final SHA, implementation commits, base/delta, số test, URLs của
  full CI/iOS/Q003C tại exact HEAD, giới hạn và rollback trong worklog riêng.
- Đây là thay đổi điều phối bằng Markdown, không đổi runtime/API; không cần
  regression mới hay chạy lại desktop local chỉ vì nội dung lịch. Các verifier
  và exact-head GitHub gates vẫn là cổng của commit tài liệu.

## Bước tiếp theo duy nhất

Xác minh ba workflow và mọi job ở đúng HEAD tích hợp cuối, cập nhật bàn giao
trên PR #1; không mở thêm checkpoint phụ thuộc trước cổng tích hợp xanh.
