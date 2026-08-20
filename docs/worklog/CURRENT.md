# Current Work Handoff

- Ngày cập nhật: 2026-08-20
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` — Draft, chưa merge
- Exact-head đã xác minh: `5ccbf90dacf3c4c4395939ce26d78a7945ac60e3`
- GitHub Actions: run `32323479652`, CI `#445`, kết luận `success`
- Tài liệu nguồn sự thật: `docs/current-status.md`
- Contract scale MAUI: `docs/maui-surface-scale-contract.md`
- Contract sparse styles: `docs/whole-axis-style-contract.md`

## Mốc đã xác minh

### Repeated MAUI input, resize và context recreation

Loaded MAUI Windows smoke chạy trên cùng public `NeraSpreadsheetView`:

1. pinch production tới zoom `1.375`;
2. pan tới offset phân số `17.25 / 31.75`;
3. tap selection và mutate workbook;
4. wheel production, đợi animated scroll settle hoàn toàn;
5. resize xen kẽ `784 x 480`, `896 x 560`, `704 x 420`;
6. sau mỗi resize, tháo cùng view, đặt handler về null rồi gắn lại;
7. yêu cầu handler, platform surface và `GRContext` chưa từng dùng trước đó;
8. giữ nguyên session, workbook, selection, zoom và current/target offsets qua toàn bộ chuỗi.

Kết quả exact run:

- frame callbacks: `43`;
- context generation: `1 -> 4`;
- created/lost/recreated context: `4 / 3 / 3`;
- started/completed/failed/abandoned: `43 / 43 / 0 / 0`;
- stale transition: `0`;
- wheel event: `1`;
- final fractional offset/target: `17.25 / 101.56818181818181`;
- selection version/ranges: `1 / 1`;
- input state cuối chuỗi: zero active touch, pinch và tap.

### MAUI surface scale contract

`NeraSurfaceMetrics` phân biệt ba không gian:

- logical MAUI viewport;
- renderer canvas (`Info`);
- raw backing pixels (`RawInfo`).

Các tỷ lệ canvas/viewport, raw/viewport và raw/canvas chỉ được chụp sau khi production frame lease đã complete. Orientation và width class chỉ dựa trên logical viewport:

- Compact `<600`;
- Medium `600..<840`;
- Expanded `>=840`;
- Portrait/Landscape/Square theo logical width/height.

### Loaded Windows scale smoke

Gate đọc `SKSwapChainPanel.ContentsScale` thật, không giả định DPI runner. Trên cùng public view, gate chạy:

1. physical-canvas Portrait/Compact `420 x 560`;
2. logical-canvas Landscape/Expanded `900 x 500`;
3. logical-canvas Square/Medium `600 x 600`;
4. handler/platform-surface/`GRContext` recreation sau mỗi scenario.

Kết quả exact run:

- frame callbacks: `19`;
- recreation cycles: `3`;
- context generation: `1 -> 4`;
- created/lost/recreated context: `4 / 3 / 3`;
- all started frames completed; failed/abandoned/stale: `0 / 0 / 0`;
- native contents scale observed: `1.0`;
- zoom/offset preserved: `1.25`, `29.5 / 53.75`;
- same session and exact selection version/ranges preserved.

### Unit và cross-platform matrix

CI `#445` xanh toàn bộ:

- Core build/tests và architecture verification.
- Full Windows build/tests cùng desktop GPU runtime smoke.
- Android real-target MAUI build.
- iOS và Mac Catalyst real-target MAUI builds.
- MAUI Windows build.
- 18 MAUI tests: handler registration, GPU lifecycle, input controller và surface metrics.
- repeated loaded runtime smoke.
- loaded scale/orientation/width-class smoke.

## Quyết định kỹ thuật đã khóa

- Không tạo control riêng cho từng ô.
- GPU lifecycle thuộc từng public MAUI view.
- Production input controller là state machine duy nhất cho `OnTouch` và tests.
- Logical viewport, canvas và raw pixels là ba không gian riêng.
- Width class/orientation không được suy ra từ raw pixels.
- DPI runner không được hard-code; gate phải đồi chiếu với `ContentsScale` thật.
- Handler recreation phải tạo mới handler, native platform surface và `GRContext`.
- Workbook/session/selection/zoom/fractional scroll không được mất khi đổi scaling mode, size class hoặc context generation.

## File trọng tâm

- `src/NeraSpreadSheet.Maui/NeraSpreadsheetView.cs`
- `src/NeraSpreadSheet.Maui/NeraSpreadsheetInputController.cs`
- `src/NeraSpreadSheet.Maui/NeraGpuContextLifecycle.cs`
- `src/NeraSpreadSheet.Maui/NeraSurfaceMetrics.cs`
- `tests/NeraSpreadSheet.Maui.Tests/NeraSurfaceMetricsTests.cs`
- `tests/NeraSpreadSheet.Maui.Windows.Smoke/SmokePage.cs`
- `tests/NeraSpreadSheet.Maui.Windows.ScaleSmoke/ScaleSmokePage.cs`
- `docs/maui-surface-scale-contract.md`
- `.github/workflows/ci.yml`

## Giới hạn còn lại

- Hosted runner không mô phỏng chắc chắn việc kéo cửa sổ giữa hai monitor có DPI khác nhau.
- Android/iOS/Mac Catalyst hiện có compile gates; device/emulator runtime cần hạ tầng ổn định riêng.
- Chưa có global OS pointer injection trên mọi platform.
- Chưa preserve unknown OpenXml parts khi load/save.
- Chưa có shared formulas, conditional formatting, validation, tables và drawings.
- PR tiếp tục Draft; không merge nếu exact-head CI đỏ hoặc chưa xác định.

## Bước tiếp theo duy nhất

Hoàn thành **unknown OpenXml part preservation** mà không làm rò kiểu Microsoft/OpenXml vào public Nera model:

1. chụp package envelope/part graph khi load;
2. phân loại Nera-owned, standard-owned và opaque pass-through parts;
3. giữ content type, relationship type/id, URI và raw bytes của opaque parts;
4. khi save, tái tạo package và chỉ thay các parts Nera thực sự sở hữu;
5. từ chối quan hệ/URI độc hại hoặc mâu thuẫn;
6. thêm round-trip tests cho unknown workbook/worksheet parts, rels, custom XML, drawing/media và duplicate/conflict cases;
7. giữ API public độc lập với DocumentFormat.OpenXml;
8. chỉ ghi nhận mốc mới sau exact-head Core/Windows/MAUI CI xanh.
