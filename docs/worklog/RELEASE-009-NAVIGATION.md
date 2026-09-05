# RELEASE-009 Navigation — Handoff lane A

## Checkpoint sau review d5cd3242

- Implementation d5cd3242f9a4953fc6d6b684530f6d66b641dbb4 đã push. Windows
  job101358623874/full33985680001 PASS120/120 native, Core1506/1506,0skip.
  Full tại checkpoint còn mobile jobs; iOS33985681101, Q003C33985682014,
  packages33985682933 và demo33985684451 đều success đúng d5.
- Artifact matrix9975137918 ZIP SHA256
  1aacfc0c10966ff30f526e2af0d737a5316d2fb556fd755d96f760c6fb1e6ff8.
  A đã kiểm cả8ảnh navigation: đủ2 standalone/8 split bars, fractional clipped
  rows, không duplicate topology hoặc lỗi bố cục chặn nghiệm thu navigation.
  Theme đổi Ribbon; worksheet/native bars giữ skin hiện có, không tuyên bố
  whole-window theme parity. Demo artifact9975098511 cũng đúng d5.
- Follow-up từ static review: refresh header/theme metrics qua existing shell
  snapshot; notify active split renderer khi RenderTheme đổi; hủy stale thumb
  nếu có cell navigation mới. Bổ sung native regression Table/merge extent,
  headers trong cả hai host và language isolation hai cửa sổ.
- Root cấp thêm đúng hai resource files PresentationStrings.resx và
  PresentationStrings.en.resx cho hai automation labels mới, không sửa keys khác.
  Existing resource parity tests vẫn bắt buộc. Thay đổi này cùng follow-up phải
  qua exact-new-HEAD gates; không lấy d5 green thay final.

- Branch: feature/release-009-navigation-shell, tạo từ đúng baseline
  50cb357a00d6bb8a6b134cdeebce624a09bd1b21 sau khi kiểm tracked tree sạch.
  Nhánh UX-007 đã release vẫn giữ nguyên ở 24d130c6. PR #1 Draft, không merge.
- Ownership theo mục chuyển quyền mới nhất của UX_TABLE_COMPLETION_WAVE_20260905
  tại workspace root; chỉ sample Navigation/SplitShell/Capture và tests được giao,
  cùng contract/worklog riêng; ngoại lệ hai resource keys nêu trên. Không sửa
  SDK behavior, CI hoặc shared docs.
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
