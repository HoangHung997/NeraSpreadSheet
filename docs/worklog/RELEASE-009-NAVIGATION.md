# RELEASE-009 Navigation — Handoff lane A

## Hồ sơ bàn giao — implementation 81d4c045

- Branch feature/release-009-navigation-shell. Implementation commit chính xác
  81d4c0452d152fd9c056349bbfc105bd1235e0cc, sau d5cd3242 và29cffe1c.
  HEAD kế tiếp chứa hồ sơ này; phải xác minh lại full/iOS/Q003C/packages/demo
  tại đúng HEAD cuối trước release. PR #1 vẫn Draft/open/unmerged.
- Hoàn tất source trong phạm vi được giao: standalone native scrollbars với
  fractional input gom theo frame, adaptive extent refresh và current viewport
  floor; pending thumb không vượt qua sheet/selection navigation; loaded split
  cùng session/state/history, một active hit-test/focus/bar topology; native
  names Việt/Anh đổi theo runtime và cô lập giữa hai cửa sổ.
- Exact81d Windows job101360615445/full33986396930 PASS121/121 native,
  Core1506/1506,0skip; build0warnings/0errors. Native tests đã đi qua actual
  Thumb/Track và Window.InputHitTest, Table/merge extent, header command ở cả
  hai host, freeze/hidden axes/zoom, stale input/switch/dispose, language isolation,
  XLSX state/history/hidden pane offsets và save/reload.
- Exact81d iOS33986398131, Q003C33986399345, packages33986400519 và
  demo33986401520 đều success. Full33986396930 tại lúc ghi còn MAUI jobs;
  không gọi native-green là all-workflow-green hoặc source đã release.
- Matrix artifact9975340538:234PNG/128layouts, ZIP SHA256
  1d23aa297256c921af5eaa6af5315cff7f733dfd2178414ede9d6261deddf8e7.
  Cả8navigation PNG byte-identical với d5 đã xem; digest ZIP được kiểm trước
  trích8PNG vào artifact riêng. Không lưu ZIP lớn trên đĩa local.
- Local architecture và diff checks PASS; kiểm XML xác nhận chỉ2approved keys
  được thêm, mọi resource value cũ giữ nguyên. Không local build/runtime/UI,
  install hoặc cleanup. Tất cả runtime/capture proof đến từ CI đúng SHA.
- File trọng tâm: RibbonPreviewWindow.Navigation.cs / SplitShell.cs / Capture.cs,
  constructor + state refresh trong RibbonPreviewWindow.cs; hai native test
  files; hai PresentationStrings resources; contract và worklog riêng. Không
  sửa WorksheetTabs, Commands.cs, SDK behavior, serializer hoặc CI/shared docs.
- Giới hạn còn lại: formula bar read-only và formula/filter command routing
  trong split chờ B bridge + root integration; B đã báo desktop bridge partial
  release nhưng chưa có trên nhánh này. Whole R2, split native accessibility,
  physical DPI/touch/screen-reader, theme parity cả cửa sổ và combined performance
  không được nghiệm thu bởi slice navigation. Không mở rộng Excel features.
- Rollback: revert riêng chuỗi navigation; không có schema/history migration.
  Chỉ release writer cho coordinator sau exact-final gates; artifact không phải
  commit source. Nhánh UX cũ24d130c6 giữ nguyên, không reset/rebase/merge main.
- Một bước tiếp theo duy nhất: verify5workflows của HEAD chứa hồ sơ này, đối chiếu
  final8PNG với81d, gửi exact SHA/run IDs và release10owned paths cho coordinator
  để ghép cùng source B; không tiếp tục sửa formula bar khi chưa được giao.

## Lịch sử checkpoint 29c

- HEAD29cffe1cee8e7c68a393a00d27caf556ee4a7343 đã push, chứa follow-up source và
  2resource keys grant. Exact workflows: full33986122936, iOS33986124357,
  Q003C33986125616, packages33986127177, demo33986128442; đang kiểm chứng,
  chưa release files. Không nhận kết quả d5 làm xanh cho29c.
- Test refinement chuẩn bị final: actual Window.InputHitTest tại integrated
  scrollbar phải trả split adorner, bổ sung proof input routing trước khi gọi
  native scrollbar state machine. Final commit cần gate riêng.
- 29c Windows job101359815299 dừng ở build vì hai CA2012 trong Headers tests:
  truy cập trực tiếp ValueTask.GetAwaiter().GetResult. Chuyển hai test qua AsTask,
  không suppress analyzer hoặc đổi command behavior. Native regression29c chưa
  chạy; packages33986127177/demo33986128442/Q003C33986125616 đã success.
  Candidate sau sửa này vẫn cần toàn bộ exact-HEAD gates và review ảnh mới.

## Checkpoint sau review d5cd3242

- Implementation d5cd3242f9a4953fc6d6b684530f6d66b641dbb4 đã push. Windows
  job101358623874/full33985680001 PASS120/120 native, Core1506/1506,0skip.
  Full đã đủ5jobs success; iOS33985681101, Q003C33985682014,
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
