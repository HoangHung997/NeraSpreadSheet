# XLSX-COMPAT-004 — inner border import và toàn bộ ảnh tại Check out

## Chỉ đạo và nguồn

Người dùng báo ảnh lỗi `Differential border contains unsupported element ... vertical` và yêu cầu sửa, giữ mọi ảnh/build tại **Check out**. Bắt đầu từ task source `2742459e9e26b1153c438d7c623778e7c8364957`, main `66730639278f4304cf0493aa85c0c499df3e8ab7`; tiếp tục cùng PR #9 / nhánh review tạm `feature/ribbon-dialogs-003`. Không mở lại lane/queue cũ, không sửa H2/workbook gốc, không ghi shared CURRENT/status.

Đã đối chiếu AGENTS/README/ARCHITECTURE, status/CURRENT, contract basic Excel interaction, dxf/Table preservation, IO sample và Check out packaging. Quy tắc kỹ thuật chính thức: `vertical`/`horizontal` là inner borders của vùng trong dxf, có từ Office2007, không phải lỗi tên file/phiên bản Excel hay căn chữ dọc:

- https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.spreadsheet.verticalborder
- https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.spreadsheet.horizontalborder

## Thay đổi API và hành vi

`OpenXmlImportOptions.ForMode(Strict|Compatibility)` là factory bổ sung trên **PreserveUnknownParts hiện hữu**. Constructor/options import và export vẫn default false. Tính năng chưa hỗ trợ được chẩn đoán riêng, không gộp với dữ liệu hỏng. `OpenXmlImportDiagnostics.Get(workbook)` trả report bất biến gồm feature/sheet/sqref/priority/dxfId, warning count có giới hạn512 + omitted count; `TryGetFailure` phân loại các failure đã được đánh dấu nhưng không nói lỗi khác an toàn để bỏ qua.

Decoder dxf được tách partial import, giữ code xuất/model/renderer dùng chung. Các phần chưa mô hình hóa được nhận diện **trước** khi gọi decoder cũ và phải qua schema check trước khi được giữ opaque. Bỏ catch mọi InvalidDataException thành style rỗng. Original dxf slot vẫn giữ index, nhưng rule tham chiếu opaque slot không bị intern như một style rỗng. Cả CF chưa hỗ trợ phải được tính vào limit100000, sqref limit và metadata dxfId/priority/type trước khi bỏ qua modeling. XML/ZIP bounds, DTD prohibition và cancellation không bị bỏ.

**Đọc số XLSX vẫn invariant.** Không chuyển dấu phẩy/chấm hoặc tự đổi text thành number. Checkpoint này chưa phải sửa toàn bộ locale editor/clipboard.

## Preservation có giới hạn rõ ràng

Chỉ cần **dxf chưa hỗ trợ**, kể cả không có rule lạ hoặc không có CF nào, cũng giữ trọn original dxfs/conditionalFormatting. Bảo toàn original tableStyles/extension/filter/validation/relationships bằng envelope/merger cũ. Không có serializer/model workbook thứ hai.

Save-boundary guard lưu fingerprint metadata (không cell grid scan), so identity/version/topology/rules/dxfs/dimensions/merges/tables/filter/DV, rồi đối chiếu protected output XML trước khi ghi destination. Phần patcher sở hữu Table/Filter/DV không được tái tạo metadata đã bị khóa; cell data/base styles vẫn được merger cập nhật. Không cho sửa CF rồi export âm thầm bỏ thay đổi như trước.

Giới hạn bảo thủ: sau structural/dimension/worksheet-name/order/Table/Filter/CF changes, **preserve-save bị từ chối trước khi chạm destination**. Đây **không** phải model rollback hay cơ chế pre-mutation guard cho mọi API; Undo của versioned structural change không tự phục hồi đủ bằng chứng. Reopen source, chỉ áp dụng cell value/formula/base-style edits. UI sample vô hiệu các metadata commands tương ứng. Full safe structural preservation tiếp tục OPEN.

### Hai test cũ được sửa theo bằng chứng, không làm xanh bằng bỏ assertion

Run34430165918:194/196OpenXml PASS,2FAIL. Fixture RichAutoFilter đặt CF ngay sau sheetData nhưng trước autoFilter, sai thứ tự chuẩn. Fixture được đặt sau filter/sortState, thêm schema validation **đầu vào**; giữ kiểm tra output/color/dxf bindings cũ.

Test NativeTableDxf opaque=true trước đây chủ động thêm managed rule rồi **đòi export bỏ mất rule đó**, trái yêu cầu người dùng. Nhánh này nay kiểm save rejection + destination/source không bị ghi đè, in-memory addition không bị xóa; sau đó reopen source và cell-only round-trip3cycles với CF/General dxf/native Table graph intact. Nonopaque edit/filter/customstyle cycle vẫn giữ nguyên toàn bộ assertions. Đây là behavior correction có chủ đích; không được coi silent data loss là backward compatibility cần giữ.

## Sample Avalonia

File picker đọc `.xlsx` và `.dlda`; cùng đường Stream không dựa vào đuôi file. Actual Open dùng Compatibility, actual Save dùng PreserveUnknownParts và staging đã có. Banner tồn tại riêng với status: thông báo số warning/giới hạn; khi mở lỗi nêu file bị lỗi và tên workbook **vẫn đang hiển thị**. Không xóa session cũ hoặc trả workbook rỗng. Work chạy import background, attach UI chỉ khi session/owner còn hợp lệ. Không sao chép dữ liệu riêng lên artifacts.

Native `--compatibility-smoke` sử dụng chính OpenCompatibleStreamAsync/SaveCompatibleStreamAsync của file picker: strict vs compatibility, 2 inner borders + unknown/known rules, numeric/formula/base-style edit, hai save/reload, corruption rejection và retained visible document. Ba ảnh riêng open/edited/rejected. SHA, source fixture hash, manifest và warning/state postconditions; smoke không chứng minh inner-border rendering hoặc Excel oracle.

## Check out

App ZIP vẫn chứa `Check out/App`, `Images`, `Reports`, `Licenses`. Evidence ZIP nay có **toàn bộ PNG** của published apps3OS và các native-capture artifacts cùng run, không chỉ13ảnh Windows. `Check out/Images/index.html` là gallery offline, `index.json` có từng hash/path/category/source, thumbnails lazy; giới hạn tổng1GiB/10000images/24MiB mỗi ảnh, chặn traversal/symlink/duplicates/PNG hỏng header. Các ảnh published đối chiếu thêm với package files manifest và outer package SHA256.

Source repository có `Check out/Images/README.md`, Check out landing dẫn tới ảnh/gói/CI. Binary nặng vẫn ở Releases, không commit EXE/ZIP/hàng trăm ảnh vào git hoặc tạo vòng build tự commit. Tất cả ảnh là từ same-run artifact downloads; không mượn ảnh commit cha. Preview branch chỉ artifact, official latest chỉ từ qualified exact main.

## Kiểm thử và đo lường

Fixture hoàn toàn tổng hợp: vertical/horizontal; supported rule+opaque dxf; 29 opaque CF/5sheets; unused dxf; invalid style/font/dxfId/priority/type;100001rules; cancellation; destination sentinel; repeated save/reload; cell edits và metadata rejects. Giữ tất cả Core/legacy/Avalonia/native/package gates.

Paired serializer probe:100000storedcells/5sheets/29duplicate rules, full-column sqref; cùng code harness trênbaseline274 và candidate, cùngrunner/runtime;1warmup+3samples, thời gian load/save và allocated bytes. Không gọi đây là BDN statistical benchmark, scroll benchmark hoặc giải thích H2 3.9–4.1s. Không nới correctness gate để đổi tốc độ.

**Trạng thái source này:** còn cần full exact-head CI, native image/release qualification. Actual commit/run/test/benchmark receipt cập nhật trong PR9 sau khi chạy. Preparatory working-tree tests không thay final commit. Local shell/Python lỗi không đồng nghĩa connector không ghi được. Không nhận đã xem ảnh nếu chỉ kiểm manifest/hash.

## Còn mở

Inner borders và duplicate/unique là opaque retention, **chưa evaluate/render đầy đủ**. Không hỗ trợ thêm XLS/XLSB/CSV/macro execution, không cam kết mọi extension/format/Strict OpenXML. No input real-workbook recheck in this checkpoint. Safe structural updating opaque references, locale no-op editor/TSV, H1/IME/DPI/hardware/screenreader và full Excel fidelity vẫn OPEN. Rollback bằng reviewed revert, không force-rewrite main; original files không bị migration.

Bước tiếp theo: verify canonical Check out trên exact source chứa các thay đổi này, review native/capture evidence, rồi quyết định integration của nguồn đủ điều kiện, không tự nhận toàn SDK hoàn tất.
