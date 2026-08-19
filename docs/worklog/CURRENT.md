# Current Work Handoff

- Ngày cập nhật: 2026-08-19
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Implementation commit đã xác minh: `a4174b54acea452cf312a2741680947a38a60139`
- GitHub Actions: run `32270133783`, CI `#438`, kết luận `success`
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
- `PaintSurface` observers chỉ chạy sau khi production lifecycle đã complete frame.

### MAUI production pointer state machine

- `NeraSpreadsheetInputController` là state machine duy nhất cho touch/wheel của public view.
- `NeraSpreadsheetView.OnTouch` và deterministic tests cùng gọi `Process(SKTouchEventArgs)`.
- Không tạo gesture model riêng cho test.
- Pan giữ offset phân số; pinch giữ document anchor; wheel scale theo zoom; tap dùng movement threshold.
- Cancellation chuyển pointer còn lại sang pan mà không sinh tap giả.
- Sau khi một pointer rời gesture ba ngón, pinch được rebase nếu vẫn còn hai pointer.
- Duplicate/unknown pointer events được bỏ qua và ghi diagnostics.
- Handler, workbook, active worksheet và view reset đều hủy gesture đang hoạt động trước khi đổi state.
- Không dùng LINQ/array allocation trên từng pointer move.

### Unit và cross-platform gates

CI `#438` tại `a4174b54acea452cf312a2741680947a38a60139` xanh toàn bộ:

- Core build/tests và architecture verification.
- Windows hosts build/tests cùng desktop GPU runtime smoke.
- Android real-target MAUI build.
- iOS và Mac Catalyst real-target MAUI builds.
- MAUI Windows build.
- 14 MAUI tests: 2 handler-registration, 5 GPU lifecycle và 7 input-controller tests.

### Loaded native MAUI Windows input + resize + recreation smoke

Runtime smoke mở ứng dụng MAUI unpackaged thật, tạo native `SKGLView`/SwapChain surface và live Skia `GRContext`.

Luồng gate:

1. render workbook trên `NeraSpreadsheetView`;
2. phát deterministic touch events qua production `ProcessTouchInput`, là cùng controller được `OnTouch` sử dụng;
3. pinch tới zoom `1.375`;
4. pan tới offset phân số `17.25 / 31.75`;
5. tap góc trên trái để chạy production selection;
6. mutate workbook và render lại;
7. resize native surface từ `944 x 600` xuống `784 x 480`;
8. remove cùng view khỏi visual tree và đặt handler về null;
9. xác nhận context loss, zero active frame và zero active touch;
10. add lại cùng view để MAUI tạo handler/surface/context mới;
11. xác nhận surface mới vẫn `784 x 480`, zoom/offset/workbook state không mất.

Kết quả runtime:

- frame callbacks: `9`;
- first/resized/recreated size: `944 x 600` / `784 x 480` / `784 x 480`;
- handler cũ/mới khác identity;
- `GRContext` cũ/mới khác identity;
- context generation: `1 -> 2`;
- created/lost/recreated context: `2 / 1 / 1`;
- started/completed/abandoned frame: `9 / 9 / 0`;
- input press/move/release: `4 / 2 / 4`;
- pan/pinch/tap: `1 / 1 / 1`;
- active touch sau chuỗi: `0`;
- cached typefaces: `1`;
- exit code thành công.

## Quyết định kỹ thuật đã khóa

- GPU lifecycle thuộc từng public MAUI view, không đặt trong workbook/core.
- Frame lease mở/đóng quanh production rendering, không chỉ trong test shim.
- Handler change là ranh giới context loss rõ ràng.
- Stale completion sau successful render phải fail-fast trước observers.
- `CA2219` chỉ được suppress đúng member production này với justification cụ thể.
- Production input controller là state machine duy nhất; test-only gesture implementation bị cấm.
- Stable pointer order được lưu rõ ràng; không phụ thuộc thứ tự enumerate dictionary.
- Pointer topology thay đổi phải rebase gesture trước update kế tiếp.
- Không tạo control hoặc renderer riêng dành cho smoke.

## Giới hạn còn lại

- Chưa có global native pointer injection cho pan, pinch, wheel và tap trên mọi hosted platform.
- Loaded smoke chưa chạy wheel animation tới trạng thái settle.
- Chưa có resize/DPI/orientation stress lặp lại qua nhiều context generation.
- Android/iOS/Mac Catalyst hiện có compile gates; device/emulator runtime cần hạ tầng ổn định riêng.
- Chưa preserve unknown OpenXml parts khi load/save.
- Chưa có theme, named/differential style, conditional formatting và toàn bộ Excel format-code semantics.
- PR tiếp tục Draft; không merge nếu exact-head CI đỏ hoặc chưa xác định.

## File trọng tâm

- `src/NeraSpreadSheet.Maui/NeraGpuContextLifecycle.cs`
- `src/NeraSpreadSheet.Maui/NeraSpreadsheetInputController.cs`
- `src/NeraSpreadSheet.Maui/NeraSpreadsheetView.cs`
- `src/NeraSpreadSheet.Maui/GlobalSuppressions.cs`
- `src/NeraSpreadSheet.Maui/Properties/AssemblyInfo.cs`
- `tests/NeraSpreadSheet.Maui.Tests/NeraGpuContextLifecycleTests.cs`
- `tests/NeraSpreadSheet.Maui.Tests/NeraSpreadsheetInputControllerTests.cs`
- `tests/NeraSpreadSheet.Maui.Tests/MauiHandlerRegistrationTests.cs`
- `tests/NeraSpreadSheet.Maui.Windows.Smoke/SmokePage.cs`
- `scripts/run-maui-windows-smoke.ps1`
- `.github/workflows/ci.yml`
- `docs/current-status.md`

## Bước tiếp theo duy nhất

Gia cố **MAUI repeated runtime stress** trên cùng public view:

1. thêm wheel production input và đợi animated scroll settle trong loaded runtime;
2. chạy nhiều resize nhỏ/lớn xen kẽ;
3. detach/recreate handler/context qua nhiều generation thay vì một lần;
4. giữ nguyên workbook, selection và fractional viewport qua toàn bộ chuỗi;
5. xác nhận mọi frame được complete/fail/abandon đúng một lần, không stale transition;
6. sau đó bổ sung DPI/display-scale và orientation/size-class transitions;
7. chỉ ghi nhận mốc mới sau exact-head Core/Windows/Android/Apple/loaded-runtime CI xanh.
