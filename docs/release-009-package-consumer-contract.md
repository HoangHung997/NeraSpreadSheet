# RELEASE-009 — Consumer NuGet Windows cô lập

Gate R3 Windows bổ sung bằng chứng còn thiếu của artifact 18 core packages.
Không có package dependency mới, không thay SDK model/host, không sửa existing
CI của lane UX; dùng workflow riêng và public SDK API hiện hữu.

- Pack closure ProjectReference thật của WPF, WinForms và OpenXml, gồm Direct2D,
  từ cùng source SHA. Version thử nghiệm duy nhất theo run/attempt/SHA; nuspec,
  manifest SHA-256 và informational version của assembly phải khớp.
- Consumer được copy ra ngoài repository, không chịu Directory.Build.props,
  central package management hoặc ProjectReference trong cây nguồn. Chỉ có ba
  PackageReference với exact version; NuGet source mapping dành NeraSpreadSheet.*
  riêng cho artifact feed, cache mới và assets không được chứa project library.
- Build nullable/analyzers/warnings-as-errors. Loaded default WPF/WinForms host
  có grid, Ribbon/Table binding, filter open/close, resize và editor controller
  commit/cancel/Undo; synthetic XLSX Table/literal roundtrip. Không sửa file thật.
- Đây là package integration smoke, không thay native OS-keyboard/GPU/performance
  hoặc walkthrough visual gates. MAUI chưa nằm trong package matrix này; R3
  toàn bộ nền tảng vẫn OPEN tới khi có multi-target package/consumer evidence.
- Chỉ chạy native trong Windows CI riêng; local dùng `-PlanOnly` đọc closure,
  không chiếm desktop lease hoặc tạo output/build nặng. Không xóa output/cache.
- Upload package cùng manifest và tóm tắt assets/assembly đã bỏ đường dẫn máy;
  không upload raw project.assets.json hoặc NuGet.Config chứa runner paths.
  Gói chỉ là artifact thử nghiệm, không publish NuGet feed công khai.

Chạy `scripts/run-release-009-packages.ps1 -PlanOnly` để kiểm tra closure.
Workflow `release-009-packages.yml` chạy source checkpoint và phải chạy lại
trên HEAD kết hợp UX-007/TABLE-007 cuối; source-only pass không đóng R3 combined.
Rollback bằng revert các file gate/consumer mới, không có workbook migration.
