# RELEASE-009 — MAUI package và consumer cô lập

Phạm vi: một bộ 15 neutral packages + một MAUI package chứa Windows, Android,
iOS và Mac Catalyst. Existing Windows desktop/OpenXml consumer không bị đổi.
Source của checkpoint này từ baseline tích hợp `50cb357a`; chưa nhận whole B.

## Gates

1. SHA checkout bằng workflow HEAD; version gồm run/attempt/SHA. Mọi producer
   build và pack với cùng version từ đầu, kiểm informational version SDK DLL.
2. Năm shard manifests cùng SDK/source/version và closure. Hash archive và từng
   assembly phải khớp; dependency/framework groups, lib/ref folders đồng nhất.
   Metadata/chung khác bytes, thiếu TFM, case collision, traversal hoặc foreign
   version đều bị từ chối. Không upload/restore partial MAUI packages vào feed chung.
3. NuGet pack canonical giữ nguyên verified payload và framework metadata;
   feed manifest bao gồm source, target groups và stable identity của package hashes.
4. Consumer ngoài checkout có props/targets/CPM riêng, exact PackageReference,
   source mapping và cache mới. Assets không có project library hoặc cache ngoài;
   compile/runtime MAUI asset phải đúng platform trong package đã kiểm.
5. App consumer mang source/version/feed hash/target/nonce trong assembly riêng.
   Public API smoke kiểm native handler/GPU frames, controller commit/Undo/Cancel,
   filter với 20 checkbox native đã load và resize thật. Controller test không
   được gọi native draft/editor proof. Runtime kiểm assembly provenance sau khi
   dùng các capability, loại riêng consumer assembly theo identity.
6. Native launcher phải kiểm đúng app hash, marker schema/cohort/nonce mới,
   target, frame count và exit code. Missing/failure marker không được PASS.
   Shared Windows/Mac launchers vẫn thuộc B; Android/iOS extraction thuộc root.
   Chưa có wiring native ở checkpoint build này; `runtimeAcceptance=OPEN`.

## Matrix và giới hạn

| Target | Host build | Probe dự kiến |
|---|---|---|
| Windows | Windows, maui-windows, win-x64 | Unpackaged WinUI executable |
| Android | Ubuntu, maui-android, JDK17, android-x64 | APK trên emulator API35 |
| iOS | macOS, maui-ios, Xcode | Debug simulator consumer của Release package |
| Mac Catalyst | macOS, maui-maccatalyst, Xcode | App bundle theo kiến trúc runner |

Android host probe giữ AOT-disabled như source gate hiện hữu. Simulator/debug
không thay device/AOT/signing/hardware acceptance. Native editor bridge chờ B
release; P3 chờ whole combined B. Không gọi workflow build-only success là R3
runtime acceptance hoặc sản phẩm hoàn chỉnh.

## Privacy và rollback

Artifact chỉ chứa package bytes, relative path/hash manifests và sanitized
summaries. Raw assets, NuGet.Config, stderr, binlog, app bundle/SDK caches,
machine paths và device identifiers không được upload. Launcher input chứa
absolute path chỉ ở RUNNER_TEMP. Source package/output debug symbols bị loại.
Không dùng workbook thật, không publish feed công khai, không local heavy build.

Local chỉ chạy `-PlanOnly`, PowerShell parser và in-memory negative fixtures
`eng/release-009-maui/test_package_matrix.py`. Rollback bằng revert các file mới,
không sửa shared source/launchers hoặc migration dữ liệu.
