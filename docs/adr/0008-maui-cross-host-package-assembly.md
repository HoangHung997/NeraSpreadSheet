# ADR 0008 — Ghép package MAUI từ build trên nhiều hệ điều hành

- Trạng thái: Proposed; implementation/gates đang được xác minh.
- Ngày: 2026-09-06.

## Quyết định

Giữ nguyên project MAUI và các override `NeraMauiTargetFrameworks` hiện có.
Một workflow tạo cùng version/source SHA cho 15 package neutral và bốn shard
MAUI. Mỗi shard chỉ chứa target được build trên runner phù hợp. Shard không
được đưa vào feed consumer vì chúng có cùng package ID/version nhưng khác bytes.

Assembler kiểm SHA/version/SDK, evaluated MAUI dependency versions, assembly
informational version và hashes. Nó hợp nhất payload cùng metadata framework
được NuGet generate, từ chối thiếu target, conflict hoặc đường dẫn không an toàn.
Một wrapper SDK trung lập dùng `NuspecFile`, `NoBuild` và `IncludeBuildOutput=false`
để NuGet tạo package cuối. Không thêm NuGet library, đổi dependency của SDK hoặc
tạo model/runtime thứ hai. Python chỉ dùng standard library cho kiểm chứng XML/ZIP.

## Lý do và hệ quả

Windows-only pack không cung cấp Apple/Android assets. Source build từ
ProjectReference không kiểm được package dependency graph. Một canonical feed
hash được dùng bởi cả bốn consumer ngoài checkout, với cache mới, exact
PackageReference và nonce mới. Consumer có identity riêng, chỉ dùng public API.

SDK 10.0.302 được resolve bằng global.json trong scratch có rollForward=disable.
Các dependencies Microsoft.Maui.* xuất hiện trên nhiều platform phải cùng version;
dependencies chỉ thuộc một platform được giữ riêng. Canonical TFM lấy từ nuspec
thật, không suy platform-version từ tên alias trong csproj.

Full CI cũ chưa xuất MAUI shards hoặc version cohort này; không đổi riêng nuspec
của DLL cũ để reuse. Partial retry cần chạy lại toàn cohort, không trộn run attempts.
Build stage xanh chỉ chứng minh package/consumer build. Native runtime còn OPEN
tới khi shared launcher được chuyển quyền và kiểm exit/frame/nonce/provenance thật.
TABLE-007 native editor và final combined performance là gates riêng, không được stub.

Rollback: revert các file gate/consumer/ADR mới; không có workbook migration hoặc
public feed publish.

Phép so sánh metadata chuẩn hóa spelling TFM tương đương và Boolean
`requireLicenseAcceptance` bị bỏ qua thành false, theo default của NuGet
ManifestMetadata. True vẫn khác false; Boolean không hợp lệ/duplicate bị từ chối.
Không bỏ kiểm tra metadata, payload hoặc dependency/framework groups.

## Tài liệu gốc

- [NuGet nuspec pack qua MSBuild](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets#packing-using-a-nuspec-file).
- [Package nhiều target frameworks](https://learn.microsoft.com/en-us/nuget/create-packages/supporting-multiple-target-frameworks).
- [Canonical target platform versions](https://learn.microsoft.com/en-us/dotnet/standard/frameworks#os-version-in-tfms).
- [NuGet ManifestMetadata defaults](https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Packaging/PackageCreation/Authoring/ManifestMetadata.cs).
