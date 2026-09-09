# AVALONIA-PRIMARY-RIBBON-002

## Kiểm tra kết nối và đính chính trạng thái — 09/09/2026

Người dùng yêu cầu kiểm tra tại sao ChatGPT báo không ghi/sửa được GitHub.
Đọc trực tiếp PR7, ref nhánh, quyền repository, các file và Actions cho thấy
báo cáo trước trong chat rằng chưa ghi source/chưa có commit là không đúng.
Không dùng báo cáo đó để bỏ qua hoặc làm lại các thay đổi đã lưu.

Trước commit nhật ký này, source HEAD là
`f85850fcfd562e7b5a4fd86204f2491c1308c397`, ahead4/behind0 so với base
`f890ae2610e0e08175e45ea1b0d8bb7650a24754`, 16 changed paths, +1322/-140.
Đã có README/AGENTS/ARCHITECTURE, quyết định Avalonia-first và yêu cầu
locale/initial-view; SDK RibbonChrome.axaml/NeraRibbonChrome.cs; preset Ribbon;
sample FullShellWindow.Ribbon.cs, các cập nhật shell/smoke và hai file test.
Đây là source trên nhánh riêng, chưa nhập vào root hoặc PR4.

Actions run `34317273525`, attempt1, exact source `f85850fc...` đã
completed/SUCCESS. Đã đọc metadata run và từng job/step: Windows
`102356021786`, Ubuntu `102356021503`, macOS `102356021772` đều SUCCESS;
build/analyzers, regressions, architecture, existing native smokes và pack
được đánh dấu SUCCESS. Lượt chẩn đoán này không chạy lại tests, chưa tải hoặc
review ảnh của run, không suy số test chi tiết từ tên step và không coi các
step xanh là visual acceptance hoặc toàn bộ Ribbon đã hoàn tất.

Kết nối đang xác thực đúng tài khoản HoangHung997; repository trả quyền
`push=true`, không archived. Các action fetch_file/update_file được cung cấp.
Một phép kiểm tra shell tối thiểu trong lượt này vẫn trả TransportTimeoutError;
đó là lỗi execution local, không phải phản hồi từ GitHub Contents API.
Không có bằng chứng trong các lần gọi GitHub vừa đọc rằng Codex khóa nhánh,
repository mất quyền ghi hoặc cần đổi quyền/bỏ branch protection.

Commit nhật ký này chỉ sửa file worklog của task bằng Contents API với expected
blob SHA, không sửa src/tests/workflow, main/develop/root/PR4/PR5/CURRENT hoặc
status của coordinator. Không force-push, không merge/Ready, không đổi lịch,
không bỏ gates. CI của f85850fc là evidence cho source đó; commit chứa nhật ký
này cần được phân biệt, không mặc nhiên kế thừa exact-head CI của commit cha.

Một bước tiếp theo: review artifact hình ảnh tại exact source đã xác minh,
đối chiếu mốc WPF rồi xác định phần visual còn thiếu trước khi sửa tiếp.
Locale/input và initial view vẫn theo ownership PR5/coordinator; không có bản
sửa số thập phân hoặc khôi phục worksheet state từ lượt chẩn đoán này.

## Checkpoint ban đầu — lịch sử, đã được cập nhật bằng receipt ở trên

Branch `feature/avalonia-primary-ribbon-002`, base PR4
`f890ae2610e0e08175e45ea1b0d8bb7650a24754`. Root được đọc riêng tại
`2e80250b1b1c51ad56a415d1bbeb70d7b2e85f25`.

Đã commit tài liệu Avalonia-first: README/AGENTS/ARCHITECTURE, strategy và
locale/initial-view requirements. Không sửa root CURRENT/status, PR4 branch,
PR5 hoặc B/C. Source của task riêng chưa được tích hợp vào root.

Bản chrome đầu thêm ResourceDictionary/ControlTheme scoped vào SDK Ribbon:
compact 32-DIP tabs/13pt text, transparent commands, native toggles, compact
combos, hierarchical menus/overflow, focus states và bốn palettes. Giữ shared
runtime/geometry/command IDs; không nhúng WPF hoặc đưa UI dependency vào Core.
Có test density, bounds, palette isolation, toggle dispatch và focus; giữ tất
cả regression cũ. Không gọi style mới là visual accepted trước khi có ảnh thật.

Native CI riêng chạy trên exact source SHA, cùng SDK/test/toolchain cũ. Local
container và Python đều TransportTimeoutError; không có local build/test PASS.
Source đang ở checkpoint ban đầu; rich sample definition/icons/capture matrix
và visual review còn OPEN cho đến khi có implementation/evidence tiếp theo.

Phần số/initial view: PR5 hiện197d6660 đã có V1/V2 source nhưng chưa CI. Không
sửa chồng Session/View/Clipboard serializer. Yêu cầu locale/input/restore theo
`docs/excel-locale-initial-view-requirements.md`; coordinator cấp shared/H1 paths
và tích hợp sau independent review. Bản Ribbon không sửa lỗi dữ liệu bằng UI.

Một bước tiếp theo: đọc actual CI của chrome candidate, sửa lỗi nếu có trước khi
mở rộng sample và ma trận visual. Không xóa legacy gates/package, không claim
Excel parity, mobile/browser/hardware hoặc performance acceptance.
