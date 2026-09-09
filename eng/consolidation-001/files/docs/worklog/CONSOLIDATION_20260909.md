# CONSOLIDATE-CHECKOUT-001 — nguồn thống nhất và phân phối bản thử nghiệm

## Chỉ đạo hiện hành

Ngày 09/09/2026 người dùng yêu cầu thu gọn toàn dự án về một nhánh, ghép phần cần giữ và xóa nhánh thừa, tạo thư mục Check out có ảnh/gói chạy thử. Chỉ đạo này cho phép integration và cleanup sau kiểm chứng; không phải lệnh xóa engine, host cũ, các test hay bỏ gate. Không đổi lịch ngoài repository.

`main` là canonical. Các lane/queue/lease trong báo cáo lịch sử không còn là hướng dẫn tiếp tục làm việc. Mã chưa đạt vẫn được archive, không được gọi là DONE.

## Source nhận vào candidate

| Nguồn | Exact SHA | Phạm vi |
|---|---|---|
| Root PR1 | 2e80250b1b1c51ad56a415d1bbeb70d7b2e85f25 | Engine, legacy hosts, đã tích hợp Table/Filter/Ribbon trước đó |
| Avalonia + Ribbon PR4/7 | c4a63fc19a4a45f515175ea542a0e1569e16fa5a | Avalonia host, Formula UX, chrome/preset, strategy, tests |
| Shared clipboard/view PR5 | 197d666022a8c05991f70202c7e9defc0c36e513 | C1/C2/V1/V2, chưa H1/cross-session structural acceptance |
| XLSX audit PR6 | 84d71084a7cfc070cb8c80a87e2645101b44b462 | Công cụ quan sát và synthetic fixture; không phải đã thử file gốc |

Đã kiểm kê 62 nhánh có sẵn, snapshot bundle SHA256 được kiểm cục bộ. Nhiều nhánh đã cherry-pick: tái merge tất cả sẽ đưa code cũ trở lại. [Inventory và archive mapping](../../Check%20out/branches.md) giữ từng SHA và phân loại. Không merge chiến lược ours rồi khai rằng đã nhận source.

## Sửa lỗi phát hiện khi build/test phần ghép

- Session serializer đọc WorkbookPart.Workbook cần null-conditional đúng nullable API OpenXML.
- Hai bộ test worksheet-view chưa từng compile cần biểu thị fixture Workbook/Worksheet không null; không tắt nullable/analyzer.
- Test direct merged collection dùng Add internal không truy cập được từ test assembly. Fixture sửa dùng public Ranges backing collection, kiểm Worksheet.Version không đổi, vẫn kiểm stale-cut guard; không thay bằng thao tác MergeCells làm yếu đường tấn công cần thử.
- Sáu preservation tests gọi options mặc định false nhưng kỳ vọng giữ sibling/unknown markup, trái contract đã có. Chọn PreserveUnknownParts=true ở cả import/export của đúng các test preservation; giữ tất cả assertion, thêm ma trận opt-in để kiểm default false và phân biệt dữ liệu/state với retention. Không tự đổi mặc định API sản phẩm.

Local .NET 10.0.400/10.0.11 từ toolchain artifact, restore offline không đổi package versions hoặc analyzers. Bản ghép ban đầu Avalonia 153/153 và Editing 416/416; OpenXML trước sửa options 162/168, sau sửa 168/168; ma trận opt-in bổ sung 4 ca đưa OpenXML lên 172/172. Bản direct-collection adversarial cuối cũng chạy lại Editing 416/416; 18 test Python delivery/cleanup an toàn PASS. Kết quả cuối và gates/host trên runner phải đọc từ exact-source Check out manifest/Actions, không kế thừa các số local hay CI của nguồn cũ. Core full local thiếu Skia 4.151.1/BenchmarkDotNet trong offline cache; dùng CI online, không downgrade dependency để build.

## Dọn nhưng không bỏ functionality

Giữ Core/Formulas/Editing/Layout/OpenXML/render backends, WPF/WinForms/MAUI maintenance, toàn bộ tests/assets/contracts. Không xóa solution chỉ vì có nhiều solution: chúng có phạm vi Core/Windows/Avalonia khác nhau. Dọn nhánh đã bảo tồn; gỡ workflow hỗ trợ tải offline/source và inventory tạm khỏi bản hiện hành khi không còn cần. Sáu gate legacy và gate Ribbon được tái sử dụng dưới một workflow Check out, không xóa các bước/assertions để dễ xanh.

## Check out

[Landing page](../../Check%20out/README.md) có ảnh thật và link trực tiếp tới Releases. Mỗi app ZIP có Check out/App, Images, Reports, README và hashes. Các gói Windows x64/Linux x64/macOS arm64 self-contained, được chạy lại smoke sau publish. NuGet chỉ là file cho local feed, không nuget.org. Publish release preview bất biến theo SHA và alias latest sau tất cả gates; không publish nếu main đã tiến. File nặng không làm phình Git history.

## Giới hạn tiếp tục mở

H1 nối state/clipboard vào mọi host, locale input/display/no-op edit, cross-session inactive structural identity, whole-B/native Mac consumer diagnostics, P3/hardware/IME/accessibility và full Excel fidelity vẫn chưa được hoàn tất bởi consolidation. Công cụ audit có thể chạy synthetic; file gốc không được tự thay bằng synthetic hoặc báo đã kiểm Excel. Tính năng chưa hỗ trợ chỉ preserve không tính là render/evaluate đúng.

Các source diagnostic 37 commits của B, Mac package candidate của C, các materializer/probe/lease cũ giữ trong archive tags. Không nhận raw source đó vào main chỉ để hết nhánh. Người tiếp theo phải port/review đúng slice từ archive theo nhiệm vụ mới.

## Quy trình an toàn và rollback

Main chỉ nhận candidate đã qua exact-SHA gates. Không force-push main hoặc đổi protection. Cleanup một lần cần so ref SHA, lưu archive tags trước/xóa trong atomic push với lease, đóng PR đã có source ancestor của main; ref mới/tiến ngoài inventory phải được giữ. Historical PR closure nói rõ source integrated, không phải mọi capability accepted.

Khôi phục bằng fetch tags và detached/local worktree từ tag đã ghi. Rollback sản phẩm bằng revert integration/fix commits có review; không xóa archive tags, không reset --hard/force-push main. Không có workbook migration từ nhiệm vụ này.

Bước tiếp theo duy nhất: xác minh Check out ở main có CI của chính SHA, đủ gói/ảnh/checksum rồi review H1/locale như một task riêng trên main; không tự mở lại nhánh cũ.
