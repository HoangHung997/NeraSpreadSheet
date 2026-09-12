# Check out — xem và tải bản thử nghiệm Nera

**Một nơi duy nhất trên GitHub để xem ảnh, xem lịch sử gói và tải ứng dụng Avalonia/SDK.**

<!-- CHECKOUT-LATEST:START -->
## Bản Check out mới nhất

- **Ngày giờ tạo gói:** **12/09/2026 09:59:14 (ICT, UTC+7)**
- **Trạng thái:** VALIDATED — full `Check out` PASS
- **Source SHA đã build:** `36bc29a829b82a67c7f7c92d40087c5733361a06`
- **Full workflow run:** `34668687734`
- **Final clean branch HEAD sau khi dọn trigger tạm:** `719503161dc4f2db97aaacdaa9e60dbcfb6c5fe2`
- **Registry mới nhất trong repo:** [`Packages/latest.json`](Packages/latest.json)
- **Bản ghi bất biến của lượt build này:** [`Packages/2026-09-12_0959_ICT_36bc29a8.json`](Packages/2026-09-12_0959_ICT_36bc29a8.json)

> Muốn biết gói mới hay cũ: **ưu tiên ngày giờ tạo gói trong README này và `Packages/latest.json`**, sau đó đối chiếu `sourceSha` và `workflowRunId`. Không dùng tên file hoặc link `latest` một mình để kết luận.
<!-- CHECKOUT-LATEST:END -->

## Tải về

Các gói binary lớn không commit trực tiếp vào Git history để tránh làm repository phình rất nhanh. Tuy nhiên **mọi gói build được quản lý từ chính thư mục `Check out/Packages/` trên GitHub** bằng registry có ngày giờ, source SHA, run ID, artifact ID, checksum và link tải chính xác.

| Gói | Gói của lượt build 12/09/2026 09:59 ICT |
|---|---|
| **Windows x64** | [Artifact `check-out-win-x64-36bc29a8…`](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/34668687734/artifacts/10289896809) |
| Linux x64 | [Artifact `check-out-linux-x64-36bc29a8…`](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/34668687734/artifacts/10290271349) |
| macOS Apple Silicon | [Artifact `check-out-osx-arm64-36bc29a8…`](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/34668687734/artifacts/10290935384) |
| Check out index + evidence + SDK packages | [Artifact `Check-out-index-36bc29a8…`](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/34668687734/artifacts/10289956872) |
| Full CI | [Run `34668687734`](https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/34668687734) |

Khi `main` được xuất bản chính thức, các alias Release vẫn dùng được:

| Gói | Release latest |
|---|---|
| Windows x64 | [Nera-Avalonia-win-x64.zip](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/Nera-Avalonia-win-x64.zip) |
| Linux x64 | [Nera-Avalonia-linux-x64.zip](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/Nera-Avalonia-linux-x64.zip) |
| macOS Apple Silicon | [Nera-Avalonia-osx-arm64.zip](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/Nera-Avalonia-osx-arm64.zip) |
| SDK NuGet local feed | [Nera-SDK-packages.zip](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/Nera-SDK-packages.zip) |
| Ảnh và báo cáo | [Nera-Check-out-evidence.zip](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/Nera-Check-out-evidence.zip) |
| Source SHA/checksum | [CHECKOUT.json](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/CHECKOUT.json) / [SHA256SUMS.txt](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/SHA256SUMS.txt) |

[Mở trang bản thử nghiệm Release](https://github.com/HoangHung997/NeraSpreadSheet/releases/tag/check-out-latest).

## Cách quản lý gói trong repository

```text
Check out/
  Packages/
    latest.json                         <- luôn trỏ bản validated mới nhất
    YYYY-MM-DD_HHMM_ICT_<sha>.json     <- lịch sử từng lượt build, không ghi đè
    README.md                           <- cách đọc registry
  Images/
  Reports/
  README.md                             <- trang này, có ngày giờ gói mới nhất
```

Mỗi bản ghi `Packages/*.json` phải có tối thiểu: `createdAt`, `sourceSha`, `workflowRunId`, `status`, danh sách package/artifact và checksum khi có. Vì vậy chỉ nhìn thư mục `Check out` trên GitHub cũng phân biệt được gói nào mới, gói nào cũ.

## Sau khi giải nén gói ứng dụng

```text
Check out/
  App/       ứng dụng và runtime self-contained
  Images/    ảnh Ribbon, bảng tính, formula UX và manifest
  Reports/   thông tin source, smoke và SHA256 các file
  README.txt hướng dẫn chạy và giới hạn
```

Windows chạy `Check out/App/NeraSpreadSheet.Avalonia.Sample.exe`. Linux/macOS chạy executable cùng tên không có `.exe`; giữ toàn bộ thư mục App. Không chỉ chép riêng EXE. macOS chưa ký là giới hạn của gói thử nghiệm, không được tắt bảo vệ hệ thống toàn cục.

## Ảnh UI và bằng chứng

Mở **[Check out/Images](Images/README.md)**. Gói `Nera-Check-out-evidence.zip` chứa ảnh PNG và manifest từ cùng lượt CI: ứng dụng self-contained Windows/Linux/macOS, ma trận Avalonia, SDK cũ và các smoke MAUI/demo. Sau khi giải nén, mở `Check out/Images/index.html`; `index.json` ghi nguồn/hash cho từng ảnh.

Ảnh/test native không thay nghiệm thu thị giác với Excel, chuột/IME/DPI vật lý hoặc screen reader. `CHECKOUT.json` và registry trong `Check out/Packages/` là nguồn xác định chính xác source đã build.

## Hộp thoại và các checkpoint gần nhất

Bản validated ngày 12/09/2026 bao gồm DIALOG-FIDELITY-007, PAGE-SETUP-008, DATA-REVIEW-009 và DIALOG-VISUAL-010. Dialog visual v2 có 432 semantic assertions và 85 source-bound captures; full Check out sau đó PASS trên Windows/Linux/macOS cùng các host/package gates.

## Lịch sử và việc còn mở

[Hợp nhất, sửa lỗi build và các giới hạn](../docs/worklog/CONSOLIDATION_20260909.md) · [Nhánh cũ/mốc khôi phục](branches.md).

**Tương thích XLSX:** bản có checkpoint XLSX-COMPAT-004 mở `.xlsx`/`.dlda` theo chế độ Compatibility và báo rõ các định dạng chỉ bảo toàn. Không chạy macro, không tự hỗ trợ XLS/XLSB/CSV; chưa tuyên bố full Excel fidelity.
