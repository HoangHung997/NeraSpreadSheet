# Current Work Handoff

- Ngày cập nhật: 2026-08-14
- Mốc: M0 bootstrap
- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` vào `develop` (Draft)
- CI-verified source commit: `7dedc313ebad2960f930b79bc7e6d46a073c380c`
- GitHub Actions run: `31766522412`

## Đã hoàn thành

- Tạo module, project reference và solution tách Core/Windows/MAUI.
- Tạo workbook sparse tối thiểu.
- Tạo `SparseAxisMetricIndex`.
- Tạo `ContinuousScrollController` giữ offset pixel dạng `double`.
- Tạo display-list contract.
- Tạo host shell WPF, WinForms và MAUI.
- Tạo unit test, benchmark, CI và ADR.
- Khai báo rõ chính sách serialization cho các thuộc tính WinForms public.

## Kết quả xác minh

GitHub Actions run `31766522412` đã hoàn tất thành công:

- `Core build and tests`: restore, build, unit test và architecture verification đều thành công trên Ubuntu.
- `Windows hosts build`: restore, build toàn solution và test đều thành công trên Windows Server 2025 với Visual Studio 2026 runner.
- WPF, WinForms, Direct2D contract, OpenXml contract, Formula contract, Core, Layout, Scrolling và các test project đều biên dịch trong cấu hình `Release`.

## Giới hạn có chủ ý của M0

Các phần sau mới khóa contract/ranh giới kiến trúc, chưa phải implementation hoàn chỉnh:

- Direct2D device, composition surface và DirectWrite text cache.
- Skia GPU surface và MAUI handler.
- Formula tokenizer, parser, AST, dependency graph và evaluator.
- XLSX round-trip preservation layer.
- Tile cache, dirty-region compositor và editor overlay.

## Bước tiếp theo duy nhất

Review Pull Request `#1`; khi được chủ repository chấp thuận thì merge vào `develop`. Sau đó tạo một branch mới từ `develop` để bắt đầu M1 với workbook snapshot, selection model và dirty-region tracker. Không triển khai Direct2D đầy đủ trước khi các contract M1 và benchmark baseline được khóa.
