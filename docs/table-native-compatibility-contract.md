# TABLE-006-NATIVE — hợp đồng editor và corpus

## Native editor

WPF/WinForms standalone control nối trực tiếp SpreadsheetFormulaEditingAssistant
và FormulaReferenceAnalyzer có workbook/address. Popup có tối đa 12 Table/cột
và 12 hàm; chỉ đọc metadata, không tính lại hoặc tạo history. Tab/click nhận
candidate; Enter commit, Alt+Enter thêm dòng, Escape cancel. ID được resolve
lại khi nhận item, tên mới được dùng sau rename; deleted/stale candidates bị
từ chối mà không commit fragment. Point-mode thay provisional span và dùng
Table reference chỉ khi selection đúng area/current row; vùng khác dùng A1.
Highlight đọc draft và provisional range khi draft chưa parse hoàn chỉnh.
Provisional span chỉ được dùng lại khi caret còn ở cuối span và selection rỗng.
WinForms kiểm tra lại ngay tại insertion boundary vì TextBox không có
SelectionChanged event; Up/Down/PageUp/PageDown và programmatic Select không
được làm lần point-mode tiếp theo ghi đè reference ở vị trí caret cũ.
Mouse precheck cũng đồng bộ span trước kiểm tra literal/structured context;
click ở context không hợp lệ được consume, không rơi xuống commit draft.
Không tạo editor cho mỗi cell; raw cell rectangle quyết định wrapping và
viewport/frozen panes chỉ clip vùng hiển thị.

TABLE-007 nối assistant hiện có vào split adorner/surface và bổ sung
`NeraSpreadsheetEditorHost` tùy chọn quanh MAUI SKGLView. Host tái sử dụng một
native Editor và `Session.Editor`; không dựng workbook/editor model song song.
Geometry dùng toàn cell rectangle và clip; phép tính layout không tạo display
list hoặc recalculate. Worksheet có Table filter vẫn dùng snapshot refresh/reuse
hiện hữu để phản ánh đúng visible rows. Native runtime/exact-head acceptance
và các giới hạn keyboard được ghi tại [TABLE-007](table-007-editor-corpus-contract.md).

## Transfer codec được coordinator chấp thuận

Ngày 05/09/2026 coordinator chuyển riêng
`src/NeraSpreadSheet.OpenXml/OpenXmlConditionalFormattingCodec.cs` và targeted
`DifferentialStyleImportCompatibilityTests.cs` cho lane A; root không cùng sửa.
Corpus Excel thật có `<dxf><numFmt numFmtId="0" formatCode="General"/></dxf>`.
Đây là patch không rỗng, có thể reset định dạng nền, nhưng khi áp vào default
thì bằng default. Decoder phải validate giá trị patch mà không dùng yêu cầu
non-neutral của Core DifferentialStyleCatalog dành cho managed rules.
Giữ nguyên Core catalog contract; không biến patch default-valued thành empty,
không catch rộng ArgumentException hoặc bỏ dxf native để né lỗi. `<dxf/>`
thực sự rỗng vẫn theo strict/preserve contract hiện hữu. Giữ dxfs indices,
remap, Table dataDxfId/preservation; consumers ngoài file transfer cần xin
quyền trước khi sửa.

Coordinator tiếp tục chuyển riêng `OpenXmlPackagePreserver.cs` cho sửa lỗi
Table native dxf bị xóa trước TablePackagePatcher. Giữ dxfs là quyết định riêng
với giữ opaque CF; dùng MergeGeneratedStyles hiện hữu và chỉ remap generated
cfRule references một lần. Không khóa supported CF edits, không sửa preserved
opaque references, không tạo style thay cho producer reference hỏng. Kiểm tra
collision native/generated index 0 và nhiều vòng save không tăng dxfs vô hạn.

Supported CF edits ở trên chỉ áp dụng workbook không có opaque CF. Theo
`basic-excel-interaction-compatibility-contract.md`, có unsupported CF thì giữ
nguyên toàn bộ CF set cùng dxfs; editing CF trong save cycle đó intentionally
unavailable. Coordinator đã từ chối mở rộng mixed supported/opaque merge trong
wave này. Tests ghi rõ attempted managed addition không được xuất trong case
opaque, nhưng opaque refs/priorities, Table/filter/style bindings phải còn.

## Corpus và giới hạn

Fixture nhỏ do Excel Windows 16.0.20326.20132 và Nera session serializer tạo/lưu,
với recipe/version/hash/expected values/formulas/identities/relationships trong
`tests/NeraSpreadSheet.OpenXml.Tests/Fixtures/TableNative`. Chỉ loại metadata
cá nhân/path/revision pointer; không đổi native Table/styles/formula payload.
Excel Table dataDxfId là preserve-only, strict không giả semantic hỗ trợ.
Save–Load–Save kiểm tra schema và stable identities; Convert/Undo/Redo kiểm tra
giá trị. TABLE-007 bổ sung workbook synthetic do LibreOffice Calc 24.2.7.2
thực sự xuất tại run `33971871140`, artifact `9971160827`; manifest riêng ghi
producer/version/hash và payload audit. Calc không giữ calculated-column,
totals label/formula và style metadata, nhưng cell formulas/values còn đúng.
Không tái tạo metadata bị producer bỏ để giả parity. Empty autoFilter có ref
bằng toàn Table gồm totals được normalize về data range chỉ khi không có
predicate/opaque content/extra attributes/Table sortState; các dạng khác vẫn
theo validation hiện hữu. Corpus có negative tests và ba vòng edit/save/reopen.

Rollback bằng revert delta lane sau `2bc00eb6`; không migration/package mới.
