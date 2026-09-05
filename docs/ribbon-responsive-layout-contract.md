# Contract responsive layout cho Ribbon

## Phạm vi RIBBON-007 và RIBBON-VISUAL-011

`RibbonResponsiveLayoutEngine` là nguồn quyết định duy nhất cho measurement,
large/small/compact và group overflow của WPF, WinForms và MAUI. Engine chỉ đọc
`RibbonPresentationSnapshot` và tạo `RibbonLayoutSnapshot` bất biến; không sở
hữu workbook, selection hay native control.

## Measurement và DPI

`RibbonLayoutRequest.AvailableWidth` dùng physical pixel. `Scale` là số physical
pixel trên một logical unit. Metrics mặc định dùng logical unit và được nhân scale
đúng một lần. Vì vậy cùng logical width cho cùng kết quả ở 100%, 125%, 150% và
200% DPI. Metrics tùy chỉnh phải hữu hạn và không âm; scale phải hữu hạn và lớn
hơn 0.

Presenter WPF chuyển DIP sang physical pixel bằng DPI của visual. WinForms dùng
client pixel và `DeviceDpi`. MAUI dùng width logical cùng `LayoutScale` do host cập
nhật khi cửa sổ đổi màn hình. Theme/icon refresh và resize dựng lại cây command
chrome nhỏ, không đi qua worksheet scroll/render frame. Resize liên tục phải được
coalesce vào dispatcher/UI-frame kế tiếp để không dựng lại Ribbon theo từng raw
event.

## Collapse deterministic

Mỗi group bắt đầu bằng kích thước preferred hiện có (`IsLarge` hoặc small). Khi
không đủ chỗ, engine lần lượt:

1. đổi large thành small;
2. đổi toàn group thành compact;
3. chuyển toàn group vào một overflow surface chung của tab.

Ba bước trên là ba lượt qua toàn tab: tận dụng khả năng giảm kích thước inline
của các group trước khi đưa group vào overflow. Trong mỗi lượt, group có
`CollapsePriority` thấp bị co trước. Khi priority bằng nhau, group bên phải bị
co trước để giữ ổn định các lệnh bên trái. Nếu caption một dòng của small rộng
hơn caption hai dòng của large, engine bỏ qua bước làm tăng chiều rộng; bước
compact hoặc overflow kế tiếp vẫn giữ thứ tự xác định. Một overflow affordance dùng
chung cho mọi group đã overflow; presenter không dùng cuộn ngang làm giải pháp
chính và command vẫn kích hoạt qua `RibbonRuntimeController`.

## Dense layout và hình học RIBBON-VISUAL-011

Engine xếp command theo thứ tự definition, từ trên xuống trong từng cột. Small
và compact chiếm một hàng; large, gallery và separator chiếm toàn chiều cao vùng
command. Khi cột còn chỗ nhưng không đủ cho item tiếp theo, engine mở cột mới.
Các item cùng cột dùng chiều rộng của item rộng nhất để baseline thẳng hàng.
Không đổi identity hoặc thứ tự keyboard traversal để lấp khoảng trống.

Metrics mặc định: ba hàng cao 24 logical px, khoảng cách hàng/cột/group 2 px,
padding trong group 4 px và caption đáy 18 px. Vùng command cao 76 px; caption
bắt đầu tại Y=80; group cao 102 px. `GroupChromeWidth` mặc định là 8 px và ít
nhất bằng hai lần padding. Các width large/small/compact 64/64/28 px là mức tối
thiểu, không phải kích thước để cắt caption. Số hàng có thể cấu hình từ 1 đến 3;
row height phải dương, các metrics còn lại phải hữu hạn và không âm.

`RibbonItemLayout.X/Y/Width/Height` là tọa độ physical pixel **tương đối với
group**, đã chứa padding. `Row`, `RowSpan`, `Column` công bố semantics packing;
`CaptionVisible` và `CaptionMaxLines` công bố visibility/wrapping. Large cho
phép tối đa hai dòng, small một dòng. `RibbonGroupLayout.Height/CaptionY/
CaptionHeight` dành vùng caption riêng bên dưới mọi item. Presenter không thêm
margin bên ngoài bounds đã cấp và không tự xếp lại item. Khoảng cách giữa group
và overflow do engine tính vào `InlineWidth`.

Caption mặc định được đo bảo thủ theo advance của UI font 12 px, bỏ qua dấu
kết hợp Unicode; large chọn điểm ngắt từ cho hai dòng. Group luôn đủ chỗ cho
caption đáy. Split/dropdown/menu/combo/color picker có thêm 18 px dành riêng
cho mũi tên ở mọi size, kể cả compact icon-only. Caption của phần chính split
button được đặt trong phần width còn lại sau khi trừ mũi tên. Combo và color
picker luôn giữ caption của selected value nếu có; gallery có width mặc định
224/180/120 px cho large/small/compact. Measurement callback tường minh vẫn
có quyền quyết định width; ứng dụng override chịu trách nhiệm minimum width
cho typography của chính nó. Callback được cache một lần cho mỗi item/size
trong một lần layout, không chạy lại theo số vòng collapse.

Presenter truyền `RibbonLayoutRequest.IsIconAvailable` để engine dành width
caption khi native resolver trả null. Nếu không truyền callback, key rỗng được
coi là không có icon và key khác rỗng được coi là resolve được. Presenter phải
giữ tooltip/automation caption cả khi compact dùng icon-only. Layout không gọi
gallery-preview callback, không query lại command state và không chạm workbook.

## Identity và snapshot

Request mang stable selected-tab ID và focused-command ID. Layout giữ identity
nếu target còn tồn tại, chọn tab đầu tiên nếu tab đã biến mất và xóa focus ID nếu
command không còn trong presentation. Presenter capture identity trước rebuild,
khôi phục native focus khi command vẫn inline và tiếp tục giữ logical focus ID khi
command tạm nằm trong overflow để lần resize rộng sau có thể khôi phục. Presenter
chỉ khôi phục native focus nếu focus trước rebuild thực sự thuộc Ribbon; resize
không được giành focus từ worksheet/editor hoặc control ngoài Ribbon.

Khi semantic icon key không resolve được, presenter phải giữ caption nhìn thấy ở
mọi mode thay vì tạo một nút compact trống. Tooltip và automation name luôn có
caption làm fallback. Overflow của MAUI là surface dọc có chiều cao bị chặn và có
thể cuộn; số command lớn không được làm surface vượt vô hạn khỏi viewport.

## Validation và giới hạn

Regression tests kiểm tra row packing, caption đáy, item bounds không overlap,
immutable collection, fallback caption, thứ tự collapse qua 2.341 width liên tục,
identity và ma trận 1536/1280/1024/820 logical px tại 100/125/150/200% DPI.
`RibbonLayoutBenchmarks` đo layout/collapse snapshot 720 command trên chín tab;
không đo worksheet render hay formula calculation. Khi toàn bộ
available width nhỏ hơn chính overflow affordance, nền tảng có thể bị hệ điều hành
clip vì không tồn tại biểu diễn command hữu dụng nhỏ hơn.

### Đo trước/sau RIBBON-VISUAL-011

Benchmark ngày 05/09/2026 dùng cùng `RibbonLayoutBenchmarks` (720 command,
chín tab), BenchmarkDotNet 0.15.8, .NET SDK 10.0.302/runtime 10.0.10, Windows
11 x64, i5-13500H. Baseline lấy Ribbon.Core/Commands từ commit
`284ccb76e5f69e170356ffc66915dd6e290b68fb`; bản sau dùng engine dense layout.
Hai harness có source/output riêng trong thư mục artifact bị Git ignore.

| Logical width | Trước, trung bình | Sau, trung bình | Cấp phát trước | Cấp phát sau |
| --- | ---: | ---: | ---: | ---: |
| 1536 | 231,4 µs | 122,2 µs | 445,73 KiB | 400,18 KiB |
| 1280 | 187,7 µs | 126,2 µs | 439,40 KiB | 415,72 KiB |
| 1024 | 184,9 µs | 121,6 µs | 439,40 KiB | 415,72 KiB |
| 820 | 189,4 µs | 129,5 µs | 454,30 KiB | 415,30 KiB |

Đây là short run một launch, một warmup và ba iteration, không phải ngưỡng
hiệu năng phát hành. Mẫu 1536 trước có độ lệch chuẩn 43,60 µs và mẫu 820 sau
6,99 µs; các khoảng tin cậy còn rộng. Engine sau thực hiện packing hai chiều,
caption và collapse mới nên kết quả mô tả cùng workload command, không phải
cùng output hình học. Không suy diễn các số này thành hiệu năng worksheet
scroll/render. Chạy lại bằng:

```powershell
dotnet run --project benchmarks/NeraSpreadSheet.Benchmarks -c Release -- --filter '*RibbonLayoutBenchmarks*' --job short --warmupCount 1 --iterationCount 3
```
