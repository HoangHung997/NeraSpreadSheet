# AVALONIA-001 — sửa hai blocker CI, 08/09/2026

Theo yêu cầu người dùng sửa trực tiếp. Base PR #4:
`47919ea669e179ae7995b3c9b959843a2c06a733`, branch `feature/avalonia-001-host`.
Không sửa product host, không đưa bản UI mở rộng chưa commit vào nhiệm vụ này.
Root PR #1 không đổi. Cả hai PR giữ Draft, không Ready/merge.

## Headless lifecycle

Bằng chứng lỗi cũ: run34167878885 attempt1, Windows job101882429479:
13/14; NRE nằm ở HeadlessUnitTestSession.DisposeAsync, không ở assert zoom.
Attempt2 cùng source pass, nên rerun không phải fix.
Đọc upstream Avalonia12.1.2 HeadlessUnitTestSession.StartNew thấy:
`Task? task = null; task = Task.Run(... new Session(... task! ...))`.
Worker có thể chạy trước khi kết quả Task.Run được gán vào biến task; session
sau đó giữ _dispatchTask=null và DisposeAsync dereference nó. Đây là race
cụ thể phù hợp stack CI, không quy lỗi zoom hoặc kéo dài timeout.
Upstream source: https://github.com/AvaloniaUI/Avalonia/blob/12.1.2/src/Headless/Avalonia.Headless/HeadlessUnitTestSession.cs

Không fork Avalonia/không reflection patch/không nuốt DisposeAsync exception.
Dùng public manual-headless APIs (UseHeadless, SetupWithoutStarting, Dispatcher
MainLoop/BeginInvokeShutdown) trong helper test-only HeadlessUiThread.
Mọi completion source và Thread được tạo trước Thread.Start; readiness do
chính dispatcher đang chạy phát ra. AssemblyCleanup chờ dispatcher dừng và
join thread; lỗi startup, dispatch, pump và cleanup vẫn làm test run thất bại.
Giữ assembly DoNotParallelize. Có chủ ý đổi lifetime Application sang mỗi
process/assembly; MỖI test vẫn tạo và đóng riêng window/control/workbook.
Không đổi thư viện/phụ thuộc của production hoặc giả vờ đã sửa upstream.

14 test cũ giữ nguyên tên và toàn bộ assert. Thêm 3 test: ổn định UI-thread/
Application identity và exception propagation, drain queued UI work, 100 vòng
window + edit/cancel/sheet-switch/detach/reattach/commit/Undo/zoom/close cố định.
CI chạy đủ suite trong 5 process mới liên tiếp trên MỖI OS. Mọi lần đều bắt
buộc pass; dừng khi lỗi, không retry-until-green. TRX phải có >=17 tests,
executed=passed=total và run outcome thành công (bao gồm AssemblyCleanup).
Đây không phải physical-input/IME/GPU/performance acceptance.

## Release graph

Bổ sung Bars.Core, Ribbon.Core và Iconography vào Avalonia.slnx; trước đây
ProjectReference vẫn restore được nhưng solution config không bao phủ chúng
và log cho thấy bin/Debug giữa build Release.
Script verify-avalonia-release.py kiểm transitive closure trước build; sau
build kiểm đủ output của mỗi Nera project nằm trong chính bin/Release và
SHA256 mọi Nera DLL copy vào sample phải khớp output Release đã ghi nhận.
Lưu build log, MSBuild binlog, report hashes vào artifact. Có 6 self-tests,
gồm missing transitive dependency, Debug dù cũng có Release, missing output
và consumer DLL không khớp. Script không thay thế isolated NuGet consumer.
Không nới native marker/assertion, timeout25min, analyzer hay architecture gate.

## Evidence và giới hạn

Local: Python gate self-tests 6/6 PASS. Không có .NET SDK trong container,
nên không claim local C#/native PASS. Cần đọc final exact-source Actions
trước khi ghi hai hold được giải phóng; kết quả nằm trong PR comment để
không tạo docs-only SHA mới sau mỗi CI. Không xóa historical failed attempt.
UI đầy đủ Ribbon/Bars/QAT/Table-filter/split/formula assistance/OS clipboard,
icon/preview feature-specific acceptance, full package/performance và các
shared holds QAT inheritance, Excel1900, recovery vẫn OPEN.
Không sửa coordinator CURRENT/status vì lane riêng không sở hữu root handoff.
Bước tiếp theo duy nhất: verify exact final source full Avalonia3-OS gate,
đối chiếu 5 TRX mỗi OS cùng Release graph report và native smoke/pack.
