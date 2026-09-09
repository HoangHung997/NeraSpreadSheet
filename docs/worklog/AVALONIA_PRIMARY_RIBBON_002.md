# AVALONIA-PRIMARY-RIBBON-002

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
