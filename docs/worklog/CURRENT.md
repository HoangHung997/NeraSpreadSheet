# Current Work Handoff

- Ngày cập nhật: 2026-08-19
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Implementation commit đã xác minh: `61c871fe3eeccbe0189ac862a4f4c473b8a1cf00`
- GitHub Actions: run `32265344228`, CI `#435`, kết luận `success`
- Tài liệu nguồn sự thật: `docs/current-status.md`
- Contract sparse styles: `docs/whole-axis-style-contract.md`

## Mốc đã xác minh

### XLSX style fidelity và malformed-input hardening

- Standard SpreadsheetML style table bảo toàn font, fill, border, alignment, number format và direct-cell style ID.
- Cell, row và column dùng style index chuẩn; package được kiểm tra bằng `OpenXmlValidator`.
- Versioned Nera custom XML part bảo toàn exact sparse row/column style spans, stable catalog ID và chronological sequence mà không materialize blank cells.
- Từ chối catalog/default sai hoặc trùng, sequence vượt `nextSequence`, patch rỗng, span sai/chồng lấn và nhiều exact style-state part.
- XML, base64 và JSON lỗi được quy về `InvalidDataException` trước khi restore workbook.
- Payload, catalog, worksheet và span đều có giới hạn chống cấp phát bất thường.

### MAUI GPU context/frame lifecycle trong production view

- `NeraGpuContextLifecycle` được gắn trực tiếp vào `NeraSpreadsheetView.OnPaintSurface`.
- Mỗi frame giữ token gồm context generation và frame sequence.
- Context mới tăng generation; completion từ frame của generation cũ bị từ chối.
- Context replacement/loss tự abandon frame cũ nếu còn hoạt động.
- Handler detach/replacement gọi context-loss trước khi native surface cũ bị giải phóng.
- Dispose idempotent và chặn mọi frame mới.
- Public diagnostics gồm created/lost/recreated context, started/completed/failed/abandoned frame và stale transition.
- `PaintSurface` observers chỉ chạy sau khi production lifecycle đã complete frame, nên không thể thấy trạng thái frame còn treo.

### Unit và cross-platform gates

CI `#435` tại `61c871fe3eeccbe0189ac862a4f4c473b8a1cf00` xanh toàn bộ:

- Core build/tests và architecture verification.
- Windows hosts build/tests cùng desktop GPU runtime smoke.
- Android real-target MAUI build.
- iOS và Mac Catalyst real-target MAUI builds.
- MAUI Windows build.
- Bảy MAUI tests: hai handler-registration tests và năm context-lifecycle tests.

### Loaded native MAUI Windows same-view recreation smoke

Runtime smoke mở ứng dụng MAUI unpackaged thật, tạo native `SKGLView`/SwapChain surface và live Skia `GRContext`.

Luồng gate:

1. render workbook trên `NeraSpreadsheetView`;
2. zoom tới `1.375` và cuộn fractional tới `17.25 / 31.75`;
3. mutate workbook và render lại;
4. remove chính view đó khỏi visual tree;
5. đặt handler về null để teardown native surface;
6. kiểm tra context loss đã được ghi nhận và không còn active frame;
7. add lại cùng view để MAUI tạo handler/surface/context mới;
8. xác nhận zoom, fractional scroll và workbook/session state vẫn giữ nguyên.

Kết quả runtime:

- kích thước frame: `944 x 600`;
- frame callbacks: `6`;
- handler cũ/mới khác identity;
- `GRContext` cũ/mới khác identity;
- context generation: `1 -> 2`;
- created/lost/recreated context: `2 / 1 / 1`;
- started/completed/abandoned frame: `6 / 6 / 0`;
- cached typefaces: `1`;
- exit code thành công.

## Quyết định kỹ thuật đã khóa

- Lifecycle thuộc từng public MAUI view, không đặt trong workbook/core hay renderer dùng chung.
- Frame lease được mở/đóng quanh production rendering, không chỉ trong test shim.
- Handler change là ranh giới context loss rõ ràng.
- Stale completion sau successful render phải fail-fast trước khi gọi observers.
- `CA2219` chỉ được suppress đúng member này với justification rằng nhánh throw chỉ chạy khi render đã thành công, nên không thể che exception render gốc.
- Không tạo control hoặc model render thứ hai dành riêng cho smoke.

## Giới hạn còn lại

- Chưa có deterministic native pointer injection cho pan, pinch, wheel và tap trên mọi hosted platform.
- Chưa có resize/DPI/orientation stress lặp lại quanh cùng-view context recreation.
- Android/iOS/Mac Catalyst hiện có compile gates; device/emulator runtime cần hạ tầng ổn định riêng.
- Chưa preserve unknown OpenXml parts khi load/save.
- Chưa có theme, named/differential style, conditional formatting và toàn bộ Excel format-code semantics.
- PR tiếp tục Draft; không merge nếu exact-head CI đỏ hoặc chưa xác định.

## File trọng tâm

- `src/NeraSpreadSheet.Maui/NeraGpuContextLifecycle.cs`
- `src/NeraSpreadSheet.Maui/NeraSpreadsheetView.cs`
- `src/NeraSpreadSheet.Maui/GlobalSuppressions.cs`
- `src/NeraSpreadSheet.Maui/Properties/AssemblyInfo.cs`
- `tests/NeraSpreadSheet.Maui.Tests/NeraGpuContextLifecycleTests.cs`
- `tests/NeraSpreadSheet.Maui.Tests/MauiHandlerRegistrationTests.cs`
- `tests/NeraSpreadSheet.Maui.Windows.Smoke/SmokePage.cs`
- `scripts/run-maui-windows-smoke.ps1`
- `.github/workflows/ci.yml`
- `docs/current-status.md`

## Bước tiếp theo duy nhất

Gia cố **MAUI production input + resize lifecycle gate** mà không tạo test-only interaction model:

1. tách production touch/wheel state transitions thành controller có thể gọi deterministic nhưng vẫn được `OnTouch` sử dụng trực tiếp;
2. kiểm tra pan, pinch, wheel, tap selection và cancellation/lost-touch;
3. chạy controller qua loaded public view với workbook/viewport thật;
4. resize native Window/surface trước và sau handler recreation;
5. xác nhận fractional viewport, selection, frame accounting và context generation vẫn chính xác;
6. chỉ ghi nhận mốc mới sau exact-head Core/Windows/Android/Apple/loaded-runtime CI xanh.
