# Current Work Handoff

- Ngày cập nhật: 2026-08-22
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Implementation commit đã xác minh: `023835495a5c56aea19830aff299765808ab5598`
- GitHub Actions: run `32543422821`, CI `#586`, kết luận `success`
- Nguồn sự thật: `docs/current-status.md`
- Roadmap: `ROADMAP.md`

## Batch vừa hoàn thành: Worksheet AutoFilter Preservation + Paged Session Foundation

### 1. Direct worksheet AutoFilter

- Worksheet sở hữu một `WorksheetAutoFilter` độc lập với Table.
- Phạm vi có hàng tiêu đề, vùng dữ liệu và các criterion theo column offset.
- Dùng chung `TableFilterColumn` và predicate engine với Table AutoFilter.
- Production API: `SpreadsheetSession.WorksheetFilters`.
- Set range, value/custom filter, clear column/criteria và remove đều đi qua Undo/Redo.
- Insert/delete/reorder ánh xạ range và column criteria; xóa riêng header bị từ chối trước mutation.
- Không cho chồng lên Table hoặc merged cells trong contract hiện tại.

### 2. Rich predicates dùng chung

- Equal/not-equal và các phép so sánh lớn/nhỏ.
- BeginsWith, EndsWith, Contains, DoesNotContain.
- IsBlank và IsNotBlank.
- On/Before/After date.
- This/Last/Next week, month và year với reference date rõ ràng.
- Blank không bị ép thành 0 hoặc empty text.

### 3. Compressed row projection

- Table và direct worksheet filter cùng đi qua `GetFilteredOutRowSpans()`.
- Các hàng liền nhau được nén thành sparse spans rồi merge trước khi đưa vào layout.
- Hàng bị lọc không chiếm viewport extent, không tạo row slot và bị hit-test bỏ qua.

### 4. Standard worksheet AutoFilter XLSX

Đã thêm `OpenXmlWorksheetAutoFilterCodec` cho:

- `autoFilter@ref`;
- `filterColumn@colId`;
- value filters;
- blank matching;
- một/hai custom comparisons và AND/OR;
- wildcard SpreadsheetML cho begins-with, ends-with, contains và does-not-contain;
- empty equal/not-equal cho blank/nonblank.

Output vượt `OpenXmlValidator(FileFormatVersions.Office2013)`.

Malformed input bị từ chối gồm duplicate AutoFilter, invalid range/index, conflicting child, unsupported wildcard và unsupported `top10`/dynamic/date-group/color/icon markup.

### 5. Copy-and-patch preservation

Đã thêm `OpenXmlWorksheetAutoFilterPackagePatcher`:

- refresh đúng worksheet `autoFilter` do Nera sở hữu;
- giữ opaque worksheet part/relationship bytes;
- giữ AutoFilter `extLst` và namespaced attributes;
- repeated save cập nhật criterion mới ở từng lần save;
- package vẫn schema-valid và giữ save atomicity.

### 6. Paged session foundation

Đã thêm `SpreadsheetTableFilterPagedSession`:

- một immutable menu snapshot theo generation;
- refresh mới hủy refresh cũ;
- request cũ không thể publish đè generation mới;
- `GetPageAsync` hỗ trợ search, offset, bounded page size và cancellation;
- mutation worksheet không thay đổi snapshot đã publish cho tới lần refresh tiếp theo;
- dispose hủy work và từ chối request mới.

Đây mới là platform-neutral paging foundation; native WPF/WinForms/MAUI list chưa bind bằng virtualization thật.

## Các lỗi CI đã bắt và sửa

1. CA1859 yêu cầu schema-order helper trả `Dictionary` cụ thể thay vì interface.
2. Cancellation test kỳ vọng exact `OperationCanceledException`, trong khi `Task.Run` trả `TaskCanceledException` chuẩn; test được khóa theo kiểu thực tế, production behavior không đổi.

## CI #586

Toàn bộ exact implementation matrix xanh tại `023835495...`:

- Core restore/build/tests.
- Architecture verification.
- Rich filter, direct worksheet AutoFilter, structural/history và paged-session tests.
- 49 OpenXml tests, gồm round-trip, wildcard, malformed input và repeated preservation.
- Windows full build/tests và desktop GPU runtime smoke.
- MAUI Android build.
- MAUI iOS và Mac Catalyst builds.
- MAUI Windows build/handler tests.
- Loaded MAUI Table-filter smoke.
- Loaded MAUI context-recreation smoke.
- Loaded MAUI scale/orientation smoke.

## Giới hạn có chủ ý

- Native WPF/WinForms/MAUI value lists chưa dùng paged session.
- Direct worksheet AutoFilter chưa có shared header-button geometry/native presenter.
- Chưa có first-class `top10`, dynamic/date-group/color/icon filter và `sortState`.
- Chưa có Table design/resize/style manager đầy đủ.
- MAUI IME/virtual-keyboard và accessibility certification vẫn pending.

## Tiến độ tổng thể

- Nền móng engine/viewport/renderer: khoảng `90%`.
- MVP bảng tính cơ bản: khoảng `84–87%`.
- Toàn bộ roadmap chuyên nghiệp: khoảng `56%`.
- Production release readiness: khoảng `32–35%`.

## Bước tiếp theo duy nhất

Triển khai **Native Paged Filter Binding + Direct Worksheet Filter UI**:

1. WPF virtualized/paged value list và stale-request cancellation.
2. WinForms virtual mode/paged value list.
3. MAUI incremental paged sheet và cancellation.
4. Shared direct worksheet AutoFilter header-button geometry.
5. Native direct-filter entry points cho WPF, WinForms và MAUI.
6. `top10`, dynamic/date-group filters và `sortState` khi semantic model hoàn tất.
7. External XLSX compatibility corpus.
8. Exact-head Core/Windows/MAUI CI.

PR tiếp tục Draft; không merge khi exact-head CI đỏ hoặc chưa xác định.