# RELEASE-009 — Tích hợp desktop navigation và editor

## Nguồn bàn giao

Root nhận đúng 21 source/test paths B tại
`9bf24af9a44ce25da4826edb2f6039203f5416f1`, patch base `782890b6`,
SHA256 `eb48e632d1eab79b3d49c5d0f43869584414ba4ef34fc30e446829c78f7441e7`.
Windows job `101359345450` / run `33985950156` đã independent-verify SUCCESS;
source báo 121/121 native và 292/292 Editing. Root kiểm toàn bộ 21 Git blobs
trùng source trước commit `ce1a00d2`. Không nhập MAUI/corpus/Viewport/B docs
chưa release, không nhập lại WPF49/WinForms64 cancellation slices.

A final `bed515b7250e165936c5b59e975abbf9923285b4` đã release 10 changed
paths và reserved WorksheetTabs path không đổi. Root xác minh cả năm exact
source workflows SUCCESS: full `33986797867`, iOS `33986799438`, Q003C
`33986800769`, Windows packages `33986801964`, demo `33986803422`.
Source Windows 121/121 không skip, Core 1506/1506; build 0 warnings/errors.

| Commit A | Commit root |
| --- | --- |
| d5cd3242 | fba2de6a |
| 29cffe1c | f63cf1c2 |
| 81d4c045 | d4a23bdd |
| bed515b7 | 8fda1486 |

Tích hợp không conflict; root kiểm cả 10 blobs trùng final A. Artifact final
`9975462914` có 234 PNG/128 layouts, ZIP SHA256
`d4021877463e1708646fca2aeba6f432c6d5e42d79c3647f46f18134c3a75054`.
A đã verify ZIP và so 8 navigation PNG byte-identical nguồn đã review; root
trực tiếp xem cả 8 ảnh final (split/standalone trong 4 palette), không có
navigation layout blocker. Native worksheet/bar skin chưa theo Ribbon theme.

## Root review bổ sung

- Sửa hai split composers còn bỏ qua public highlight opt-out. Regression
  bật/tắt/bật lại kiểm chính composed display list của cả WPF và WinForms.
- Sửa WPF split MeasureOverride còn đo editor theo owner window dù Arrange
  dùng full cell. Dùng full rectangle và invalidate measure khi bounds đổi;
  native regression so actual line breaks trước/sau resize khi cell rộng hơn
  window, giữ native internal scroll. Đây là kiểm thử mới, chưa được gọi PASS
  trước exact combined CI.
- Không làm formula/calculation/layout model mới, không full workbook scan,
  không thêm package. Source builds/runtime mới chạy trên CI, không heavy
  local build khi thiếu disk. Local architecture/packaging/diff PASS.

## Native transport riêng

`0a8e531f` extraction đã pass desktop/package/demo nhưng Android/iOS parser
FAIL. `47951e1b` sửa legacy minimum 2 frames theo bằng chứng source Android
green job101358050609, giữ packaged consumer >=3. iOS vẫn malformed-marker
ở run33987368853; thêm numeric framing/stream diagnostics, không raw logs
hoặc nới malformed/mixed-failure/provenance gates. C chưa nhận launcher.
Không lấy source xanh của A/B làm bằng chứng combined native transport.

## Còn mở và rollback

Formula bar editable/active split metadata/help/filter routing, full B MAUI
Mac crash và Windows reattach, LibreOffice acceptance, canonical MAUI native
consumer, final combined P3 cùng physical DPI/touch/screen reader vẫn OPEN.
Không báo whole Table/Filter/Ribbon/UX 100%, không merge PR #1 hoặc publish feed.

Rollback source A bằng revert bốn commit root trên; source B bằng revert
ce1a00d2 cùng integration hardening theo sau. Không có data/history migration.
Bước tiếp theo duy nhất: xác minh năm exact combined gates rồi giao formula
bar cho A bằng public draft bridge hiện hữu, không tạo editor thứ hai.
