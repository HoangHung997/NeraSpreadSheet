# RELEASE-009 Navigation — Handoff lane A

- Branch: feature/release-009-navigation-shell, tạo từ đúng baseline
  50cb357a00d6bb8a6b134cdeebce624a09bd1b21 sau khi kiểm tracked tree sạch.
  Nhánh UX-007 đã release vẫn giữ nguyên ở 24d130c6. PR #1 Draft, không merge.
- Ownership theo mục chuyển quyền mới nhất của UX_TABLE_COMPLETION_WAVE_20260905
  tại workspace root; chỉ sample Navigation/SplitShell/Capture và tests được giao,
  cùng contract/worklog riêng. Không sửa SDK, CI hoặc shared docs.
- Implementation đang kiểm chứng: hai native scrollbars dùng cached extent và
  fractional frame-coalesced ScrollTo; lifecycle hủy pending input khi đổi sheet;
  loaded split dùng same session/stored mode và chỉ một active input/bar topology.
- Regression mới: RibbonWorksheetNavigationSmokeTests (native thumb/precision/
  wheel, extent/zoom/freeze/hidden axes, switch/dispose); mở rộng
  RibbonLoadedWorkbookSmokeTests với serialized XLSX state/history/hidden panes,
  integrated bar input và save/reload. CaptureMatrix thêm 8 actual shell PNG.
- Local: verify-architecture.ps1 PASS, git diff --check PASS. Không local build,
  native UI hoặc cleanup vì giới hạn dung lượng/lease; build/runtime/screenshots
  phải qua existing CI ở đúng implementation/final HEAD. Chưa có source-green.
- Baseline50 xanh do coordinator xác minh: full33984177819, iOS33984177815,
  Q003C33984177818, Windows packages33984174136, demo33984234305. Những run này
  không phải bằng chứng cho implementation mới.
- Giới hạn: formula bar/bridge, split editor cancellation và formula/filter
  active-host command acceptance còn OPEN, B/root giữ ownership. Không tuyên bố
  performance acceptance, physical DPI/touch/screen-reader hoặc whole R2 DONE.
- Rollback: revert riêng các commit navigation; workbook/history không cần migrate.
- Một bước tiếp theo: commit/push checkpoint này và dispatch existing full/iOS/
  Q003C/package/demo gates đúng SHA; xử lý lỗi native trước release ownership.
