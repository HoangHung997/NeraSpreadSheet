# UX-007 và desktop cancellation — tích hợp 06/09/2026

Root nhận A final24d130c6 và hai desktop lifecycle slices đã release của B;
không nhận phần MAUI/editor/corpus B đang điều tra. PR #1 Draft/open/unmerged.

## A — bảy commits, không conflict

| Source | Integration |
| --- | --- |
| 1c855249 | 1662d0b9 |
| 8f5e9882 | f58af7a1 |
| c098c40b | ca5f357d |
| 0e6e02a9 | d7ce6a91 |
| d3fc3242 | e53925e4 |
| d492433f | bcf5149f |
| 24d130c6 | 79fb8aa9 |

Root đã kiểm tất cả A-owned paths byte-equivalent final source. Exact source
full33983346342/iOS33983348485/Q003C33983350512 đều SUCCESS (7jobs, attempt1).
Core1506/1506, Windows108/108, MAUI46/46,0skip, build0warnings/errors; đây là
source evidence, không phải số test ở integration HEAD mới.

Root đã đọc production/contract/tests của shortcut ownership, Apply rollback,
MAUI shell và native HC dictionaries. Giữ một runtime/binding/profile model,
461 resource keys, per-control owned brushes, không MAUI/global-resource override.
ci.yml chỉ thêm upload9captures theo transfer đã cấp, không bỏ existing gates.

Final artifact9974468799 exact24d có ZIP SHA256
40df12e5e026eda5f4b830c83905e8e3b43299beb84f78caa788d76e65b6fd86.
Root đã xem9ảnh d492 trước đó, so final9hashes:4byte-identical; đã xem riêng
5ảnh khác hash ở24d. Không có visual blocker: popup/entry HC legible, selected
row/focus/draft/narrow footer đúng. Final Filter artifact9974480473 SHA256
8ac20945237c83b3233659c866c27434fd887917ead97bf1546f10b13adc4fad có4ảnh
byte-identical bộd492 root đã xem. API xác nhận đúng source/run cho cả2artifact.

## B — chỉ nhận slices desktop độc lập

- WPF49e1debe source/test đã nhận ở7b6e2297. Windows job101350852141 /
  run33982772337 PASS109/109,0skip; root7b nay xanh đủ5workflows: full33983410443,
  iOS33983410457,Q003C33983410421,packages33983406801,demo33983406785.
  Actual tab-switch regression dùng changed native draft/popup, không headless
  controller-only proof.
- WinForms64c04f5f source/test nhận trong checkpoint theo sau79fb8aa9.
  Exact Windows job101352853547 / run33983522435 PASS111/111,0skip. Root review
  diff/test và verify source/test blobs trùng release. Không lấy docs/MAUI code.
- CancelEditor luôn cleanup native editor/suggestions kể cả controller đã cancel
  khi đổi sheet; repeated cancel không đổi selection/history/cells hoặc focus.
  Native regression kiểm visible changed draft trước và hidden UI sau activation.
- Khi tích hợp whole B sau này không nhập trùng source/test hunks49 và64;
  các docs deltas trong hai commit này vẫn chưa được nhận.

## Cổng tiếp theo / giới hạn

New root HEAD phải xanh full/iOS/Q003C/Windows packages và published demo.
Local chỉ architecture/packaging/diff checks, không build/native nặng vì ổ C
còn khoảng207MiB; không xóa file/caches hoặc đổi quyền máy. Demo44f5 artifact
đã được root verify447payload hashes nhưng không thay artifact của HEAD mới.

A source U1/U3/U4 đạt phạm vi Windows automated/native đã ghi; U2 actual reader,
U5 physical DPI/touch, Apple/Android shell interaction vẫn OPEN. B native/corpus
chưa ghép; P3 chờ full A+B. R2 còn editable formula bar và worksheet scrollbars/
split shell; R3 MAUI packages còn OPEN. Không báo100% hoặc Excel parity.

Rollback: revert integration commits và two-path cancellation deltas tương ứng;
không workbook/profile migration. Không merge PR hoặc publish feed công khai.
