# RELEASE-009 — Artifact demo Windows độc lập

Workflow riêng `release-009-demo.yml` publish sample WPF hiện hữu với
`win-x64`, self-contained, không trimming/single-file. Chỉ property
`NeraRibbonDemo=true` thêm runtime switch để double-click exe vào thẳng Ribbon;
regular sample build không đổi chế độ mặc định. Không tạo workbook, Ribbon
model hoặc renderer thứ hai, không thêm package mới.

Push filter chỉ integration branch khi demo workflow/script/sample/README đổi,
giúp đăng ký workflow mới ngoài default branch; các lượt sau có workflow_dispatch.
Không lấy artifact cha thay exact final combined source: coordinator dispatch
lại ở HEAD nghiệm thu cuối ngay cả khi thay đổi SDK không trúng push path filter.

Script từ chối local native run (chỉ `-PlanOnly`), source bẩn, SHA khác workflow
hoặc thư mục output tồn tại. Kiểm PE AMD64, runtime files/config và mọi SDK DLL
informational version/SHA, chạy đúng exe trong publish output KHÔNG truyền
`--ribbon-preview`, giữ nguyên 180-second timeout và complete capture/command
assertions. Manifest phải success, đủ 128 layouts và toàn bộ image paths hợp lệ.
Không dựa vào source ProjectReference test thay cho published apphost smoke.

Artifact chỉ được upload khi tất cả bước thành công: app, ảnh synthetic,
README giới hạn và relative-path/hash manifest. Không upload runtime stderr
hoặc đường dẫn máy; không dùng workbook thật. Không public release/feed,
installer, registry/file association hoặc OS/security setting changes.

Artifact còn kèm `COMMANDS.md`: phân loại 49 session IDs và 36 host actions,
behavior/disabled conditions và gaps thực tế. Manifest có hashes riêng cho
README/COMMANDS; danh sách này là tài liệu đối chiếu actual registry, không
phải runtime command model hoặc tuyên bố toàn bộ public SDK đã có UI.

Đây là phần R2 nghiệm thu executable, không thay R1 command coverage, R3
isolated PackageReference consumer, final combined A+B CI/performance hoặc
physical-device gates. Snapshot demo trước A/B final phải ghi là thử nghiệm,
không báo 100%. Rollback bằng revert gate/runtime switch/docs, không migration.
