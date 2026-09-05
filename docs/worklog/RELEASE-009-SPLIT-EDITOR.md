# RELEASE-009 Split Editor — Handoff lane A

## Hồ sơ bàn giao — implementation b94d2a2e, chờ exact final gates

- Branch `feature/release-009-split-editor-routing`; PR #1 Draft/open/unmerged.
  Implementation WPF cuối `b94d2a2ea6d7f793a1f72399d8a038bc57da8972`.
  HEAD trước hồ sơ này là import-only257611ed; HEAD chứa hồ sơ này phải chạy
  riêng full/iOS/Q003C/packages/demo trước khi release. Chưa lấy Windows xanh
  để tuyên bố all-platform hoặc whole SDK acceptance.
- Source commits theo thứ tự để root nhận sau final gates:
  `7950acb90714e7e3789ea4be70b56bc386074fdb`,
  `47faa24c3c59cd23d00d7696e048ba8b2338a873`,
  `8d957bb192bfe5f7e5d7ae8b79a728b88bc31584`,
  `b94d2a2ea6d7f793a1f72399d8a038bc57da8972`, rồi commit hồ sơ này.
- Hoàn tất trong scope: active-pane visibility qua existing frame/metrics,
  tránh integrated scrollbar overlay, giữ offset lẻ/other panes/freeze/merge/
  hidden navigation và history; actual split metadata/nested argument help,
  shared host formatter/reference projection; identical draft echo giữ native
  backward direction, changed range/validation/notifications/focus vẫn đúng.
- Windows source b94 job101369575444/full33989684232 SUCCESS: build0warnings/
  0errors, Core1515/1515, native146/146,0skip. Cả7native regression cases mới và
  existing full-cell measure/queued-scroll/opt-out/cancel/structured Table tests
  chạy trong bộ này. Source8d Windows101368984456 cũng PASS146/146.
- Artifact b94 matrix9976294755,237PNG; ZIP SHA256
  `9c3f37e6d98e6df49930f07aa38b66e717d8679f04f16a893e180312fc79d87a`.
  Đã xem đủ3PNG mới trong subfolder `split-editor-routing`: native split help
  host, IF argument3 popup, Enter pane-edge sau commit42 với destination nằm
  trên horizontal bar và bên trái vertical bar. Giữ nested display-list behavior.
  Final HEAD vẫn phải kiểm tra artifact/ảnh riêng, không suy từ ảnh commit cha.
- Local architecture/diff PASS. Không local heavy build/native, không cleanup;
  native runtime/captures dùng existing Windows CI. Không chạy benchmark mới
  hoặc tuyên bố cải thiện render/scroll performance; final controlled performance
  và hardware acceptance vẫn thuộc scope root, chưa hoàn tất ở slice này.

### Immutable import — bỏ toàn bộ commit này khi nhận source A

- **SKIP `257611ed2f91a74230608a1cb69e0d02e145b300`**. Đây là import-only root
  transport từ `f344b5ec8060a127e3ce030a717013ce4f2bb637`, nhận nguyên trạng qua
  apply_patch theo grant, không phải implementation lane A. Root đã xác minh
  full33988991344 và iOS33988991332 success tại source transport.
- `scripts/run-maui-ios-smoke.sh` blob
  `41b8d02bad14c812fffcda562b63c17c02336682` đã có nguyên trạng ở base7a.
- `scripts/verify-native-smoke-result.py` blob
  `5fd92e2866fc7231c44c81feda3dd4dcc8c43788` và
  `scripts/test-native-smoke-result.py` blob
  `cf540735e2a20faa72be3a842338b4a4638d49b7` là hai file thay đổi trong import.
  Cả3hash khớp grant; local20/20parser fixtures PASS. Không sửa Android helper.
- iOS7950/47 vẫn FAIL trên parser baseline; 47run33989323486/job101368596429
  báo stream1 malformed-marker chars990/json-offset990. Không nới test hoặc
  dùng desktop success để bỏ gate; final HEAD gồm approved import phải qua CI.

### Ownership, giới hạn và bước tiếp theo

- A giữ11paths:7SDK files (Control, Control.EditorDraft, Control.FormulaEditing,
  SplitController, SplitAdorner.EditorDraft, SplitAdorner.KeyboardEditor,
  SplitAdorner.FormulaEditing), Table007WpfEditorDraftSmokeTests, new
  Release009SplitEditorRoutingSmokeTests, contract và worklog riêng này.
  Root Table007SplitEditorSmokeTests giữ nguyên blob; không sửa sample, Commands,
  resources, shared docs hoặc workflows. Ba immutable parser paths không release
  như source A; root bỏ import commit hoàn toàn.
- Sample formula bar và split paged filter chưa triển khai trong slice này.
  API range không biểu diễn arbitrary cross-control selection direction;
  CaretIndex là WPF public caret, không hứa moving edge khi selection không rỗng.
  Absolute worksheet extent vẫn clamp theo engine, không thêm tail né overlay.
- Rollback: revert riêng5source/doc commits nêu trên, không revert root transport;
  không cần workbook/history migration.
- Một bước tiếp theo duy nhất: push HEAD gồm source + immutable import + hồ sơ,
  verify đủ5exact-final workflows và3PNG, rồi gửi release11paths/skip-import
  manifest cho root. Root phải tự chạy combined gates sau khi nhận.

## Lịch sử triển khai

## Native47 và follow-up sau review ảnh

- Windows47 job101368593377 đã qua build, Core, native runtime và capture matrix.
  Artifact9976188018 có237PNG, ZIP SHA256
  `7613c3902be3129f73381679170dca1e497d965df1cbb92bd39840111c8aa332`.
  Đã xem ba PNG riêng: nested IF help có active argument3 đúng; actual split
  editor ở pane active. Không thay evidence47 cho final HEAD.
- Review pane-edge ảnh cho thấy helper cần tránh phần bị integrated scrollbar
  overlay che. Bổ sung visibleRight/Bottom từ actual frame.ScrollBars.Bounds,
  giữ cell/layout/engine offset maxima; regression yêu cầu destination không
  intersect thanh cuộn. Capture outcome ngay sau commit, trước Undo/Redo.
- Source8d957bb1 đã push extra draft-echo grant và shared help formatter, đang
  chạy năm gates. Follow-up overlay/source tiếp theo vẫn cần CI riêng.

## Bổ sung grant — giữ direction khi echo bản nháp nguyên trạng

- Root cấp thêm riêng Control.EditorDraft.cs và SplitAdorner.EditorDraft.cs.
  Guard native Select bằng actual selection start/length sau text update; không
  normalize selection đã đúng. Giữ validation-before-mutation, notification
  de-duplication, focus/history/canonical controller. Không thêm direction API.
- Regression backward selection nay yêu cầu Shift+Left mở rộng selection sau
  identical echo; changed range vẫn phải áp dụng. CaretIndex là WPF public caret,
  không phải contract moving edge. Chưa khẳng định arbitrary direction transfer.
- Split/standalone dùng cùng existing Vietnamese help formatter trong owner
  FormulaEditing partial; không thêm resource key hoặc hai bản chuỗi khác nhau.
- Source47faa24c đã dispatch follow-up build/test alias fix; source kế tiếp chứa
  grant này vẫn cần đủ năm workflows riêng và native screenshots được review.

## Checkpoint implementation 7950acb9

- Source `7950acb90714e7e3789ea4be70b56bc386074fdb` đã push và dispatch
  full33989178209, iOS33989179836, Q003C33989181142, packages33989182733,
  demo33989184174. Q003C/packages đã success; các run còn lại chưa xác nhận xanh.
- Windows job101368191810 dừng tại build do `KeyEventArgs` ambiguous giữa WPF và
  WinForms trong test mới. Follow-up thêm explicit WPF alias; không sửa product,
  nới analyzer hoặc bỏ test. Native7950 chưa chạy.
- Primary WPF TextBox source xác nhận CaretIndex getter là SelectionStart.
  Backward regression dùng native Shift+Left sau round-trip để quan sát direction;
  snapshot range/text/caret giống nhau không chứng minh moving edge được giữ.
  Root đã nhận báo cáo gap; không thêm API hoặc sửa ngoài granted files.

- Branch `feature/release-009-split-editor-routing`, base chính xác
  `7a378ca133a517820a3e9425423e841513e8d07d`. PR #1 Draft/open/unmerged;
  slice chưa release hoặc được tích hợp. Commit implementation là HEAD chứa
  checkpoint này; SHA và CI sẽ được bổ sung sau khi run có kết quả.
- Root grant desktop-only cho phép bắt đầu trước khi baseline iOS hoàn tất.
  Source base có Windows job101365914847, Core/Q, packages33988335138 và
  demo33988360802 success do coordinator xác minh; đây không phải all-platform
  accepted baseline và không thay cổng exact-final-HEAD của slice mới.
- Giữ nhánh navigation đã release ở
  `bed515b7250e165936c5b59e975abbf9923285b4`, artifacts cũ nguyên trạng.
  Không reset/rebase/cleanup, build/native nặng hoặc lấy desktop lease local.

## Implementation đang chờ kiểm chứng

- Owner ScrollCellIntoView route active split pane; native MoveActiveCell dùng
  cùng helper, existing frame/metric/ScrollPaneTo, giữ frozen axes và pane khác.
  Ô quá lớn giữ cạnh đầu để reveal lặp không dao động; không thêm history entry.
- Owner metadata getter route state split popup. Thêm nested argument help qua
  Session.FormulaEditing; compositor và getter dùng cùng reference projection,
  giữ opt-out, stable Table identity và canonical cancel/sheet-switch cleanup.
- Test mới `Release009SplitEditorRoutingSmokeTests` cho actual loaded standalone/
  split, owner input flags false, metadata/help/Table rename, fractional/frozen/
  merged/oversized visibility, Enter/Tab vượt hidden axes và history. Capture PNG
  tại subfolder `split-editor-routing` trong existing Ribbon matrix artifact.
- `Table007WpfEditorDraftSmokeTests` thêm backward-selection round-trip regression,
  ghi rõ ba-argument setter không biểu diễn direction; chưa sửa/mở rộng API.
  Toàn bộ root tests full-cell measure/queued scroll/highlight opt-out giữ nguyên.
- Paths trọng tâm: năm SDK files Control, Control.FormulaEditing,
  SplitController, SplitAdorner.KeyboardEditor, SplitAdorner.FormulaEditing;
  hai test files nêu trên và contract/worklog riêng này. Shared docs, sample,
  Commands, resources, CI, parser helpers và các B paths khác thuộc root.

## Verification và giới hạn

- Local `scripts/verify-architecture.ps1` PASS; `git diff --check` PASS.
  Chưa local build/runtime vì grant cấm heavy local/native và dung lượng thấp.
  Build/analyzers/native screenshots phải được kiểm chứng qua existing CI.
- Chưa có exact implementation CI xanh, chưa review ảnh mới. Không dùng các
  source runs cũ để xác nhận source mới. Năm final workflows vẫn bắt buộc.
- Formula bar, paged filter split routing, hướng caret khi round-trip và whole
  MAUI/hardware/performance acceptance vẫn OPEN theo phạm vi riêng.
- Rollback: revert riêng commit slice này; workbook/history không cần migration.
- Một bước tiếp theo: push implementation và dispatch đủ full/iOS/Q003C/packages/
  demo tại đúng SHA, xử lý build/native failures trước khi release ownership.
