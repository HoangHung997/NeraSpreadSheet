# AVALONIA-PRIMARY-RIBBON-002

## Triển khai tiếp theo — 09/09/2026

Own branch `feature/avalonia-primary-ribbon-002`, PR7 Draft, base PR4
`f890ae2610e0e08175e45ea1b0d8bb7650a24754`. Đợt này bắt đầu từ
`2c27bd65e2b6022a374560165821921078982241`. Không sửa root/PR4/PR5/B/C,
CURRENT/status/assignment hoặc scheduler. Quyết định Avalonia-first giữ nguyên;
WPF/WinForms/MAUI maintenance, không xóa API/gates và không mở lại Table/Filter.

### Source và sửa lỗi từ CI thật

- `a8fd5407`: chỉ materialize native body của tab đang chọn, đồng bộ native tab
  và LayoutSnapshot identity. Runtime vẫn giữ mọi command/shortcut. Body ẩn,
  minimized/backstage không được tạo; group divider nằm trong shared slot.
- Bar có palette scoped riêng, bỏ việc tạo hai lần cây submenu cho toolbar.
  Customization có IconTheme riêng không làm dirty profile; cột có nhãn,
  actions wrap, JSON expander giới hạn chiều cao. Các affordance dùng icon
  catalog, resolver mới cập nhật projection nhưng caller vẫn sở hữu ảnh.
- CI34323600370 bắt CA1859 ở CreateColumn: `3c2ca6bd` đổi return type thành
  Grid đúng kiểu cụ thể, không suppress analyzer. CI34324227554 tiếp tục bắt
  MSTEST0040 do callback Action bị viết async void. `0e10df81` dùng handler
  CompletedTask có kiểm IsCompletedSuccessfully trước Result, giữ assertions.
- `0e10df81e4ac50313138e769e92c03443be66a79` thêm native 84-layout/109-image
  matrix và paired rebuild probe. CI34325300826: cả ba OS build/analyzers,
  153 tests ×5 processes, architecture và ba native smokes cũ đều PASS;
  riêng native Ribbon matrix FAILED vì sample dùng icon key `formula.calculate`
  không có trong catalog. Pack/probe bị skip; chưa có paired benchmark numbers.
- Commit chứa checkpoint này sửa source sample sang `formula.calculate-now`,
  `view.freeze-panes`/`view.unfreeze-panes` đúng catalog. Giữ gate bắt thiếu icon,
  thêm kiểm toàn bộ command keys trước matrix để báo đầy đủ thay vì từng key.
  Cửa sổ tùy biến mở từ nút thật nhận theme hiện hành và theo đổi theme sau đó;
  matrix gọi chính nút này, không tự dựng một dialog khác để né entry point.

### Bằng chứng và giới hạn

153 test thực thi trên 0e là bộ test của SHA đó, không phải PASS cho commit
mới chứa checkpoint này. Mỗi push phải chạy lại final exact-head workflow.
Native matrix kiểm attached controls/geometry, không phải pixel-perfect WPF
hoặc Excel oracle. Raster 100/125/150/200% không phải đổi DPI màn hình thật.
Bộ ba native smokes cơ bản/full-shell/Formula UX trước đây được giữ nguyên.
Paired probe dùng cùng 720commands/9tabs, baseline2c và candidate trên cùng
runner, same harness/DLL hashes,3warmup+7samples. Không retry đến khi nhanh,
không dùng phép đo này giải thích H2 edit latency hoặc worksheet scrolling.

Container/Python vẫn TransportTimeoutError. Baseline Windows artifact
10091825432 đã tải nhưng chưa giải nén hoặc xem ảnh; không claim visual QA.
Không có local C# build/test. Kết quả final được ghi trong PR body/comment,
không tạo docs-only commit sau green chỉ để tự ghi SHA của chính nó.

### Locale và trạng thái ban đầu — không sửa chồng PR5

Đã đọc root2e và PR5 `197d666022a8c05991f70202c7e9defc0c36e513`, assignment
blob04b129d1. C1/C2/V1/V2 có source mới nhưng chưa được nghiệm thu, H1 chưa
grant. Đã đăng coordination request PR5 comment5598122362: freeze/API/evidence
trước lượt host integration, restore activeTab/workbookViewId/selection/zoom/
scroll/freeze/split trước write-back. Lỗi editor invariant→CurrentCulture cần
phân quyền riêng; không sửa Clipboard/Session/serializer của worker khác.
Không tự tạo model locale/view-state khác trong UI hoặc đổi lịch/assignment.

Một bước tiếp theo duy nhất: đọc actual native matrix/paired-probe ở commit
chứa checkpoint này, sửa failure cụ thể mà không nới gate, rồi review ảnh khi
môi trường cho phép. Không merge/Ready/public NuGet hoặc tuyên bố hoàn tất cả3mục.

## Kiểm tra kết nối và đính chính trạng thái — lịch sử

Trước đợt này, source HEAD là `f85850fcfd562e7b5a4fd86204f2491c1308c397`,
ahead4/behind0 so với basef890,16changedpaths,+1322/-140. Đã có README/AGENTS/
ARCHITECTURE, Avalonia-first, locale/initial-view requirements, SDK chrome và
preset, seven-tab sample cùng tests. Không làm lại từ đầu vì báo cáo sai trong
chat rằng chưa có commit. GitHub Contents API ghi thật thành công; lỗi local
execution không đồng nghĩa GitHub mất quyền hoặc Codex khóa nhánh.

Actions34317273525 attempt1 exactf858 SUCCESS trên Windows102356021786,
Ubuntu102356021503,macOS102356021772. Metadata/jobsteps đã đọc; không dùng
kết quả đó thay CI mới hoặc visual approval. Commit2c27bd65 chỉ bổ sung43dòng
nhật ký và đã được đọc lại từ remote để kiểm chứng khả năng ghi. Những docs
trên branch riêng chưa tự trở thành docs của root/main.

Rollback code đợt mới bằng revert các commit sau2c theo thứ tự ngược. Không
workbook migration; rollback code không tự đảo quyết định sản phẩm Avalonia-first.
Các global correctness/Excel1900/preservation/physical-accessibility holds còn mở.
