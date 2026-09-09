# EXCEL-FILE-AUDIT-20260909 — báo cáo và kiểm thử độc lập

Base PR #1 được đọc lại: `2e80250b1b1c51ad56a415d1bbeb70d7b2e85f25`.
Nhánh riêng: `feature/excel-file-audit-20260909`.
Ownership: chỉ `eng/excel-file-audit/`, `tests/fixtures/excel-file-audit/`, workflow
`excel-file-audit.yml` mới và báo cáo này. Không sửa `src/`, existing tests,
CURRENT/status, H2/demo, lịch hoặc grants C1/V1/V2/MAUI/Avalonia.
Table/Filter/Ribbon/UX vẫn tạm dừng theo người dùng; audit không kích hoạt lại.

## Ranh giới bằng chứng

File người dùng đã cho phép công khai nhưng chưa đọc/upload được binary do
TransportTimeoutError trong mọi môi trường xử lý attachment. GitHub text reads
và writes vẫn hoạt động. Nhánh này chuẩn bị runner và được kiểm bằng fixture
TỔNG HỢP sáu sheet/29 duplicate rules trên năm sheet. Không gán kết quả synthetic
cho file thật. Workflow có gate riêng fail `BLOCKED_INPUT_MISSING` khi binary
không tồn tại, không tạo workbook rỗng hoặc coi missing-input là PASS.

## Phạm vi runner

Đọc original bytes, ghi SHA256/ZIP inventory; chạy workbook/session Strict và
Compatibility qua existing PreserveUnknownParts, so cùng byte dưới .dlda/.xlsx.
Kiểm ba lần save/reload, edit value/formula/base style trên copies; inventory
so sánh CF/sqref/priority/dxfId/dxfs/SheetViews/extensions, lost/changed parts và
rich-text counts. Cùng shared session/viewport compose visible+overscan trên
mọi sheet; so formula caches trước/sau recalc (cache KHÔNG phải Excel oracle).
Đo năm edit riêng, không quy số đo cho H2/UI. Chèn/xóa hàng/cột, rename, save,
kiểm destination sentinel và Undo trên bản sao; audit ghi nhận nguyên trạng
SDK, không tự sửa reference hoặc nuốt lỗi thành dữ liệu rỗng.

`EXECUTED` nghĩa probe hoàn thành, không phải feature PASS: cần đọc chi tiết
false/differences/limitation. Raw XML equality không chứng minh đúng nghĩa sau
structural change. Hash model hiện xét tên/thứ tự sheet, stored cells/formulas
và effective base style, không phải full session/selection/dimensions equivalence.
Tên/thông báo probe không tuyên bố rollback đầy đủ chỉ từ hash này.

Native WPF/WinForms/MAUI/Avalonia, Direct2D/Skia pixels, Excel DisplayFormat,
physical input, external functions, all feature parity vẫn NOT_TESTED. Root
không chứa Avalonia PR #4 nên không tự import nó để mở phạm vi.
Chưa có kết quả file thật, benchmark trước/sau sửa hoặc nguyên nhân của độ trễ H2.

## Chạy

```powershell
dotnet run --project eng/excel-file-audit -c Release -- "tests/fixtures/excel-file-audit/Excel_CV_ThachBich - Copy.dlda" artifacts/excel-file-audit
# Chỉ kiểm runner bằng dữ liệu giả, KHÔNG nghiệm thu file người dùng:
dotnet run --project eng/excel-file-audit -c Release -- --self-test artifacts/harness-selftest
```

Mọi probe lưu JSON/Markdown ngay sau hoàn tất để process timeout không xóa các
quan sát trước đó. Exceptions được ghi trạng thái FAILED theo probe; input lỗi
không biến thành workbook thành công. Artifact chỉ có báo cáo, không các bản sao
workbook trung gian. Exact-head outcomes cập nhật trong PR/comment sau CI để
không lấy parent green thay một docs-only HEAD mới.

Một bước tiếp theo: đưa binary đã được phép công khai vào đúng path trên nhánh
này, đối chiếu SHA256 với original, rồi đọc full report/CI exact-head để chốt
findings thật và đề xuất sửa theo ưu tiên đọc dữ liệu → preservation → rendering.
Rollback: bỏ các paths audit mới; SDK production không đổi.
