# RELEASE-009 — WPF split editor routing

## Phạm vi

Owner WPF chuyển thao tác đưa ô vào vùng nhìn thấy và metadata công thức tới
split controller đang bật. Controller, native editor, workbook, formula assistant
và continuous scroll state hiện có vẫn là nguồn sự thật duy nhất. Không thêm
package, viewport engine, editor model hoặc history model.

## Cuộn tới ô

- `NeraSpreadsheetControl.ScrollCellIntoView` và split controller cùng gọi một
  đường xử lý trong adorner. Native Enter, Tab và điều hướng selection dùng đường
  này sau khi chọn ô đích bằng `SpreadsheetVisibleCellNavigation` hiện có.
- Chỉ pane active được cuộn. Offset `double`, các pane khác, topology, selection,
  workbook identity và frozen axes không bị thay đổi bởi lời gọi visibility.
- Dùng frame/metric hiện có, bounds đầy đủ của ô hoặc ô gộp, frozen clip và
  `ScrollPaneTo`; không quét toàn worksheet hoặc materialize row/column axis.
- Ô vừa pane được đưa vào vùng nhìn thấy với dịch chuyển tối thiểu. Ô lớn hơn
  vùng cuộn giữ cạnh đầu trong vùng nhìn thấy để các lời gọi lặp không dao động.
  Vùng cuộn bằng không trên một trục không làm offset trục đó thay đổi.
- Ô ẩn, pane chưa có kích thước hoặc không cần cuộn trả `false`. Ô thuộc frozen
  axis không cuộn axis đó. Ô gộp được xét theo merged anchor.
- Cuộn tự động theo selection không thêm workbook hoặc split-view undo entry.
  Commit thành công vẫn qua canonical editor một lần; validation failure không
  điều hướng. Các lệnh scroll trực tiếp hiện có giữ nguyên history semantics.

## Metadata và trợ giúp công thức

- Owner `CurrentFormulaSuggestions`, `CurrentStructuredReferenceSuggestions`
  và `CurrentFormulaHelp` phản ánh danh sách/context mà split popup đang dùng.
- Context argument do `Session.FormulaEditing.GetFunctionHelp` tính tại caret;
  popup vẫn hiện trợ giúp khi không có completion candidate. Candidate được chọn
  hiển thị signature/description hoặc tên Table column giống host độc lập.
- Structured-reference acceptance giữ nguyên kiểm tra caret/selection và stable
  Table identity. Không thay canonical draft, validation hoặc provisional range
  bằng một editor/assistant khác.
- `CurrentFormulaReferenceHighlights` và split compositor dùng cùng tập reference
  trên worksheet active, cho draft hoặc formula cell đang chọn. Tôn trọng
  `ShowFormulaReferenceHighlights`, palette rỗng và nested display-list semantics.
- Cancel, canonical cancellation và chuyển worksheet dọn metadata/completion/help.
  Reference của một formula cell đang chọn sau cancel vẫn theo selection như
  contract host độc lập; không giữ provisional draft cũ.

## Giới hạn

- Chưa triển khai formula bar của sample hoặc split paged-filter popup.
- API `UpdateEditorDraft(text, selectionStart, selectionLength)` mô tả một range,
  không có tham số hướng selection/caret riêng. Regression native kiểm tra
  backward selection và round-trip; không tự mở rộng public API trong slice này.
- Không thay đổi host WinForms/MAUI hoặc tuyên bố hardware/performance acceptance.

## Cổng kiểm chứng

Loaded standalone/split native tests phải kiểm tra metadata, nested argument,
stable Table rename, highlight opt-out/cleanup, backward selection, Enter/Tab ở
mép pane với hidden axes, frozen/merged/oversized cells, fractional offsets,
độc lập pane và workbook/view history. Giữ nguyên các regression full-cell text
measure, queued-scroll và cancellation đã tích hợp. Build/analyzers, architecture,
ảnh native cùng năm workflows tại đúng HEAD cuối là cổng riêng.
