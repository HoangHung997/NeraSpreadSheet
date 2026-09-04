# RIBBON-007 — responsive Ribbon layout

- Base SHA: `21d496cad0d54506b0015d62e4ae80de57c34e6a`.
- Branch: `feature/ribbon-007-responsive-layout`.
- Implementation SHA: `0878f0f3e57783215431662894a504cbe18eefef`.
- Owner scope: `Ribbon.Core`, Ribbon presenters/tests của WPF, WinForms, MAUI và
  contract/worklog checkpoint này.

## Implementation

- Thêm snapshot measurement/layout host-neutral theo available physical width,
  scale, preferred item size và group collapse priority.
- Large giảm xuống small, sau đó compact; group ít quan trọng và bên phải collapse
  trước; group cuối cùng đi vào một overflow surface chung theo thứ tự gốc.
- WPF, WinForms và MAUI tiêu thụ cùng snapshot, bỏ horizontal scrolling khỏi vùng
  group chính và giữ stable selected-tab/focused-command identity qua rebuild.
- WPF/WinForms tự đọc DPI; MAUI cung cấp `LayoutScale` để host cập nhật khi đổi màn
  hình.

## Validation

- Core solution: **1.277/1.277 passed**; Commands **67/67 passed**.
- Focused loaded WPF/WinForms Ribbon: **5/5 passed**; MAUI presenter:
  **5/5 passed**.
- Loaded MAUI Windows Ribbon smoke: **success**.
- Core solution, WPF, WinForms, MAUI Windows, Android, iOS và Mac Catalyst
  Release builds/analyzers: **0 warnings, 0 errors**.
- Architecture verification và SDK packaging verification: **passed**.
- `git diff --check`: **passed**; không có secret, đường dẫn máy cá nhân,
  Machine ID hoặc token trong diff.
- Máy cục bộ thiếu SDK repo-ghim `10.0.302`; validation cục bộ chạy bằng SDK
  `10.0.201` từ ngoài worktree mà không sửa `global.json`. Android API 36 được
  cài vào SDK tạm cấp người dùng vì SDK hệ thống không cho phép ghi.
- Integrated exact head `05c6974fa907f5022f28c85f13f06dbb35288556`
  passed full CI run `33883244367` / #1307, iOS run `33883244356` / #128 and
  Q003C/OpenXML run `33883244366` / #125.

## Remaining limit

- Complex Ribbon item kinds và item-specific measurement callback thuộc
  `RIBBON-008`.

## Rollback

Revert implementation commit của branch này. Definition/customization JSON hiện
có vẫn tương thích vì `CollapsePriority` mặc định bằng 0.
