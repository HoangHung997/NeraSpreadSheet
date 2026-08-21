# Current Work Handoff

- Ngày cập nhật: 2026-08-21
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Implementation commit đã xác minh: `e3a814f5c0f6eb0fff75d30ee5ee217069139d71`
- GitHub Actions: run `32474664182`, CI `#570`, kết luận `success`
- Nguồn sự thật: `docs/current-status.md`
- Contract Table model: `docs/table-structured-reference-contract.md`
- Contract Table presenter: `docs/table-filter-presenter-contract.md`
- Roadmap: `ROADMAP.md`

## Batch vừa hoàn thành: Table Manager + Native AutoFilter Presenter

### 1. Platform-neutral Table manager

- `SpreadsheetTablePresenterController.GetManagerSnapshot()` trả về snapshot chỉ đọc của worksheet hiện hành.
- Snapshot dùng stable `Guid` của Table và column, range chuẩn, header/totals state, style và trạng thái filter/formula metadata.
- Snapshot không phải writable model thứ hai; mọi host phải refresh sau mutation.

### 2. Bounded distinct-value filter menu

- Menu được mở bằng Table ID + column ID.
- Giá trị được quét từ một `WorksheetSnapshot` bất biến của canonical Table data range.
- Giới hạn mặc định:
  - `100.000` data row được quét;
  - `10.000` distinct value được giữ.
- Có occurrence count, blank identity và hai cờ truncation độc lập.
- Search dùng trimmed ordinal-ignore-case substring.
- Search không làm mất lựa chọn đang bị ẩn.
- Select-all-visible và clear-visible chỉ tác động projection đang hiển thị.
- Khi enumeration bị truncate, chọn toàn bộ giá trị đã thấy vẫn tạo explicit filter; giá trị chưa quét không bị tự động coi là đã chọn.

### 3. Production commands và history

- Apply value filter.
- Apply một/hai custom condition với AND/OR.
- Clear current column filter.
- Clear all filters của Table.

Tất cả mutation đi qua `SpreadsheetSession.Tables`, vì vậy:

- chỉ tạo một production history entry;
- compressed row projection được rebuild;
- viewport extent/hit test được refresh;
- filter-aware `SUBTOTAL` được recalculation đúng dependency;
- Undo/Redo phục hồi chính xác Table và row visibility.

### 4. Shared header-button geometry

- `SpreadsheetTableFilterButtonGeometry` dùng `WorksheetSnapshot`, `ViewportLayout` và render theme chung.
- Mỗi hit mang Table ID, column ID, worksheet column index, filtered state và bounds.
- Renderer, pointer hit test và native overlay dùng cùng identity/geometry.
- Không tạo control cho từng cell; chỉ tạo native button cho Table header column đang nhìn thấy.

### 5. Keyboard navigator và active-cell resolver

Đã thêm:

- `SpreadsheetTableFilterNavigator`;
- `SpreadsheetTableFilterTargetResolver`;
- active-value identity theo `CellValue`, không phụ thuộc visual index.

Mapping đã xác minh:

- `Alt+Down`: mở filter của Table column chứa active cell;
- Escape: đóng;
- Up/Down/Home/End/Page Up/Page Down: duyệt danh sách;
- Space/Enter: toggle giá trị hiện hành;
- Enter từ search: Apply nếu selection hợp lệ;
- Ctrl+A ngoài search: chọn tất cả giá trị đang hiện;
- Shift+Ctrl+A ngoài search: bỏ chọn giá trị đang hiện.

### 6. Native WPF presenter

- Native `Popup`.
- Automatic visible Table-header button host.
- Search, checkbox values, select-all/clear-visible, clear filter và Apply.
- Keyboard navigation, open-focus và close-time focus restoration.
- Loaded native-window presenter smoke và keyboard/focus smoke đã xanh.

### 7. Native WinForms presenter

- Native `ToolStripDropDown`.
- Automatic visible Table-header button host.
- Cùng menu/history semantics với WPF.
- Keyboard navigation và focus lifecycle được kiểm tra với native handle/message loop.

### 8. Responsive MAUI presenter

- `NeraSpreadsheetTableHost` đặt native visible filter buttons trên GPU spreadsheet surface.
- Responsive overlay/bottom-sheet filter UX, không tạo control theo cell.
- Stable Automation IDs, semantic description, hint và heading metadata.
- Windows keyboard binding chỉ dịch WinUI key events sang navigator chung.
- WinUI search focus dùng `FocusManager.TryFocusAsync` với retry hữu hạn:
  - tối đa 40 attempt;
  - mỗi retry cách 50 ms;
  - dừng khi focus thành công, user chuyển vào value list, sheet đóng, host dispose hoặc hết giới hạn.
- Close giải phóng native search focus và đưa focus về button/surface hợp lệ.

### 9. Các lỗi runtime gate đã bắt và sửa

1. Gọi focus trước khi native WinUI `TextBox` loaded/visible.
2. Gán lại MAUI `AutomationId` sau Apply/Undo/Redo.
3. Search còn giữ native focus sau khi sheet đóng.
4. Smoke dùng fixed delay nên flake giữa Windows runners.

Smoke cuối chuyển sang chờ sự kiện focus thật với timeout hữu hạn thay vì coi một delay cố định là bằng chứng lifecycle.

## CI #570

Toàn bộ exact-head matrix xanh tại `e3a814f5...`:

- Core restore/build/tests.
- Architecture verification.
- Presenter, bounded enumeration, navigator, active-cell resolver và shared geometry tests.
- Toàn bộ Table/Structured Reference/AutoFilter, calculated-column, filter-aware totals, validation, conditional-formatting, shared-formula, sparse-style và package-preservation regressions.
- Windows full build/tests.
- Loaded WPF/WinForms presenter + keyboard/focus smokes.
- Windows desktop GPU runtime smoke.
- MAUI Android build.
- MAUI iOS và Mac Catalyst builds.
- MAUI Windows build/tests.
- Loaded MAUI Windows Table-filter smoke:
  - live Skia `GRContext`;
  - open từ active cell;
  - search focus;
  - accessibility semantics;
  - Apply Open-only filter;
  - compressed row visibility;
  - Undo;
  - Redo;
  - reopen;
  - focus release khi close.
- Loaded MAUI input/context-recreation smoke.
- Loaded MAUI logical/raw scale/orientation smoke.

## Giới hạn có chủ ý

- Chưa virtualize/page native distinct-value list.
- Chưa có Table design/resize/style manager UI đầy đủ.
- Chưa có rich text/date/top/bottom/color/icon/custom-list filters.
- Chưa có direct worksheet AutoFilter ngoài Table.
- Chưa hoàn thiện MAUI virtual keyboard/IME lifecycle.
- Chưa chứng nhận đầy đủ screen reader, high contrast, localization và theme.
- Chưa có external XLSX AutoFilter compatibility corpus đầy đủ.
- Các giới hạn SUBTOTAL, dynamic array, function surface, printing, chart và pivot vẫn giữ như `docs/current-status.md`.

## Tiến độ tổng thể

- Nền móng engine/viewport/renderer: khoảng `89%`.
- MVP bảng tính cơ bản: khoảng `82–85%`.
- Toàn bộ roadmap chuyên nghiệp: khoảng `54%`.
- Production release readiness: khoảng `30–33%`.

Đây là ước lượng theo trọng số kỹ thuật, không phải đếm checkbox.

## Bước tiếp theo duy nhất

Triển khai **Rich AutoFilter + Scalable Filter Values** theo thứ tự:

1. Mở rộng platform-neutral predicate cho rich text/date/top-bottom/custom-list.
2. Direct worksheet AutoFilter dùng chung row-projection/history semantics với Table.
3. Tách value enumeration khỏi native list materialization; thêm paging/virtualization và cancellation.
4. Hoàn thiện Table design/resize/style manager UI.
5. MAUI IME/virtual-keyboard lifecycle.
6. Accessibility/high-contrast/localization/theme hardening.
7. External XLSX AutoFilter corpus và differential tests.
8. Exact-head Core/Windows/MAUI CI.

PR tiếp tục Draft; không merge khi exact-head CI đỏ hoặc chưa xác định.