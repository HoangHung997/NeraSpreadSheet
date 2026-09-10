# Check out — xem và tải bản thử nghiệm Nera

**Một nơi duy nhất để xem ảnh, tải ứng dụng Avalonia và SDK. Source hiện hành ở `main`.**

## Tải về

| Gói | Liên kết tải trực tiếp |
|---|---|
| **Windows x64 — giải nén, chạy EXE; không cần cài .NET hoặc Visual Studio** | [Nera-Avalonia-win-x64.zip](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/Nera-Avalonia-win-x64.zip) |
| Linux x64 — self-contained, cần thư viện desktop hệ điều hành | [Nera-Avalonia-linux-x64.zip](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/Nera-Avalonia-linux-x64.zip) |
| macOS Apple Silicon — bản thử nghiệm chưa ký/notarize | [Nera-Avalonia-osx-arm64.zip](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/Nera-Avalonia-osx-arm64.zip) |
| SDK NuGet để thử từ local feed, không publish nuget.org | [Nera-SDK-packages.zip](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/Nera-SDK-packages.zip) |
| Ảnh và báo cáo gọn | [Nera-Check-out-evidence.zip](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/Nera-Check-out-evidence.zip) |
| Source SHA, mã CI, checksum | [CHECKOUT.json](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/CHECKOUT.json) / [SHA256SUMS.txt](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/SHA256SUMS.txt) |

[Mở trang bản thử nghiệm và toàn bộ file](https://github.com/HoangHung997/NeraSpreadSheet/releases/tag/check-out-latest).
Link này không dùng URL tạm trong chat hay artifact tự hết hạn. Trước khi lần xuất bản đầu tiên hoàn tất, trang có thể chưa tồn tại; xem workflow `Check out — integrated SDK and test applications` thay vì dùng một gói khác giả là bản mới.

## Ảnh UI của bản được xuất bản

Ảnh thật từ cửa sổ Avalonia trên runner Windows; source SHA nằm trong CHECKOUT.json. Ảnh/test native không thay nghiệm thu thị giác với Excel, chuột/IME/DPI vật lý hoặc screen reader.

### Ribbon sáng
![Ribbon sáng](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/ribbon-light.png)

### Ribbon tối
![Ribbon tối](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/ribbon-dark.png)

### Toàn cửa sổ
![Bảng tính](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/full-window.png)

### Tùy biến Ribbon
![Tùy biến Ribbon](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/customization.png)

## Hộp thoại cài đặt nhóm Ribbon

Ở **Trang đầu**, nhấn mũi tên nhỏ góc dưới bên phải nhóm **Số**, **Phông chữ** hoặc **Căn chỉnh** để mở đúng thẻ trong **Định dạng ô**. `Ctrl+1` mở Định dạng ô. Có thêm thẻ Đường viền/Màu nền. Ở **Bố trí trang** có Thiết lập trang/Tùy chọn in; ở **Xem** có Thu phóng.

![Định dạng số](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/format-number.png)
![Phông chữ](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/format-font.png)
![Căn chỉnh](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/format-alignment.png)
![Thiết lập trang](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/page-setup.png)

OK áp dụng các thuộc tính đã chỉnh qua Undo/Redo; Cancel/Esc không áp dụng. Dialog đọc vùng hiện hành; vùng lớn không bị materialize để lấy thông tin. Đây là tập dialog đầu tiên, không phải toàn bộ Format Cells/Print/Protection của Excel. Xem [phạm vi và giới hạn](../docs/worklog/RIBBON_DIALOGS_003.md).

## Sau khi giải nén

```text
Check out/
  App/       ứng dụng và runtime self-contained
  Images/    ảnh Ribbon, bảng tính, formula UX và manifest
  Reports/   thông tin source, smoke và SHA256 các file
  README.txt hướng dẫn chạy và giới hạn
```

Windows chạy `Check out/App/NeraSpreadSheet.Avalonia.Sample.exe`. Linux/macOS chạy executable cùng tên không có `.exe`; giữ toàn bộ thư mục App. Không chỉ chép riêng EXE. macOS chưa ký là giới hạn của gói thử nghiệm, không được tắt bảo vệ hệ thống toàn cục.

Gói nặng giữ tại Releases, **không** cho vào Git history để tránh làm repository nặng. Thư mục này là landing page; trong mọi gói tải về cũng có đúng thư mục `Check out` chứa ảnh và app. Mỗi source còn có release bất biến `check-out-<SHA>-<run>`; alias latest chỉ được cập nhật sau khi CI của chính SHA đó đạt và main chưa tiến sang bản khác.

## Lịch sử và việc còn mở

[Hợp nhất, sửa lỗi build và các giới hạn](../docs/worklog/CONSOLIDATION_20260909.md) · [Nhánh cũ/mốc khôi phục](branches.md).
Không dùng việc thu gọn nhánh để xóa source thử nghiệm chưa đạt: mã đó nằm ở archive tags, không được nhập lại mù vào main.


## Toàn bộ ảnh của từng bản build

Mở **[Check out/Images](Images/README.md)**. Gói `Nera-Check-out-evidence.zip` chứa **toàn bộ ảnh PNG** trong cùng lượt CI: ứng dụng self-contained Windows/Linux/macOS, ma trận Avalonia, SDK cũ và các smoke MAUI/demo. Sau khi giải nén, mở **`Check out/Images/index.html`** để duyệt ảnh; `index.json` ghi nguồn/hash cho từng ảnh. Không chỉ còn vài ảnh Windows xem nhanh.

**Tương thích XLSX:** bản có checkpoint XLSX-COMPAT-004 mở `.xlsx`/`.dlda` theo chế độ Compatibility, thông báo các định dạng chỉ bảo toàn (ví dụ `dxf/border/vertical`, `horizontal`, rule chưa hỗ trợ). Khi mở lỗi, banner nói rõ workbook cũ vẫn đang hiển thị. Không chạy macro, không tự hỗ trợ XLS/XLSB/CSV; chưa tuyên bố vẽ inner borders hoặc duplicate/unique đúng như Excel. Xem [phạm vi và giới hạn](../docs/worklog/XLSX_COMPAT_004.md).

Chỉ dùng source SHA trong `CHECKOUT.json` để xác định bản tải. Nếu main/latest chưa cập nhật, file review mới ở artifact `Check-out-index-<SHA>` của [workflow Check out](https://github.com/HoangHung997/NeraSpreadSheet/actions/workflows/check-out.yml); không lấy link latest cũ làm bằng chứng fix mới. Các source branch tạm không phải nhánh phát triển dài hạn.
