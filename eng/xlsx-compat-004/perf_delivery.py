"""Wire one paired synthetic probe and the permanent Check out navigation."""
import json
from pathlib import Path

manifest=Path('artifacts/compat004/changed.json')
changed=set(json.loads(manifest.read_text()))
def replace(path,old,new):
    p=Path(path);text=p.read_text(encoding='utf-8-sig')
    if text.count(old)!=1:raise ValueError(f'Probe wiring drift {path}: {old[:100]!r}')
    p.write_text(text.replace(old,new),encoding='utf-8',newline='\n');changed.add(path)

path='.github/workflows/check-out.yml'
replace(path,'  assemble:\n', '''  compatibility-probe:
    name: Paired sparse XLSX compatibility measurement
    needs: delivery-tests
    runs-on: ubuntu-latest
    timeout-minutes: 20
    steps:
      - uses: actions/checkout@v6
        with:
          ref: ${{ github.sha }}
          persist-credentials: false
      - uses: actions/checkout@v6
        with:
          ref: 2742459e9e26b1153c438d7c623778e7c8364957
          path: compatibility-probe-baseline
          persist-credentials: false
      - uses: actions/setup-dotnet@v6
        with:
          global-json-file: global.json
      - name: Run the identical 100000-cell serializer probe on both source revisions
        run: |
          mkdir -p artifacts/compatibility-probe
          cp -R benchmarks/NeraSpreadSheet.CompatibilityProbe compatibility-probe-baseline/benchmarks/
          diff -r benchmarks/NeraSpreadSheet.CompatibilityProbe compatibility-probe-baseline/benchmarks/NeraSpreadSheet.CompatibilityProbe
          dotnet run --project compatibility-probe-baseline/benchmarks/NeraSpreadSheet.CompatibilityProbe -c Release -- 2742459e9e26b1153c438d7c623778e7c8364957 artifacts/compatibility-probe/before.json
          dotnet run --project benchmarks/NeraSpreadSheet.CompatibilityProbe -c Release -- ${{ github.sha }} artifacts/compatibility-probe/after.json
          python scripts/check-out/verify_compatibility_probe.py artifacts/compatibility-probe ${{ github.sha }}
      - uses: actions/upload-artifact@v4
        with:
          name: compatibility-probe-${{ github.sha }}
          path: artifacts/compatibility-probe
          if-no-files-found: error

  assemble:
''')
replace(path,'needs: [delivery-tests, core-and-hosts, openxml, ios, windows-packages, maui-packages, legacy-demo, avalonia, applications]',
    'needs: [delivery-tests, core-and-hosts, openxml, ios, windows-packages, maui-packages, legacy-demo, avalonia, applications, compatibility-probe]')
replace(path,'      - name: Assemble exact-source downloads; publish prerelease only from validated main', '''      - uses: actions/download-artifact@v4
        with:
          name: compatibility-probe-${{ github.sha }}
          path: incoming/compatibility-probe
      - name: Assemble exact-source downloads; publish prerelease only from validated main''')
path='scripts/check-out/assemble.py'
replace(path,'import gallery\n','import gallery\nimport verify_compatibility_probe\n')
replace(path,'    image_report = images.finish()', '''    image_report = images.finish()
    verify_compatibility_probe.verify(source / 'compatibility-probe', sha)
    shutil.copytree(source / 'compatibility-probe', evidence / 'Reports/Compatibility-probe')''')

path=Path('Check out/README.md');text=path.read_text(encoding='utf-8-sig')
if '## Toàn bộ ảnh của từng bản build' in text:raise ValueError('Check out gallery section already exists')
text+='''

## Toàn bộ ảnh của từng bản build

Mở **[Check out/Images](Images/README.md)**. Gói `Nera-Check-out-evidence.zip` chứa **toàn bộ ảnh PNG** trong cùng lượt CI: ứng dụng self-contained Windows/Linux/macOS, ma trận Avalonia, SDK cũ và các smoke MAUI/demo. Sau khi giải nén, mở **`Check out/Images/index.html`** để duyệt ảnh; `index.json` ghi nguồn/hash cho từng ảnh. Không chỉ còn vài ảnh Windows xem nhanh.

**Tương thích XLSX:** bản có checkpoint XLSX-COMPAT-004 mở `.xlsx`/`.dlda` theo chế độ Compatibility, thông báo các định dạng chỉ bảo toàn (ví dụ `dxf/border/vertical`, `horizontal`, rule chưa hỗ trợ). Khi mở lỗi, banner nói rõ workbook cũ vẫn đang hiển thị. Không chạy macro, không tự hỗ trợ XLS/XLSB/CSV; chưa tuyên bố vẽ inner borders hoặc duplicate/unique đúng như Excel. Xem [phạm vi và giới hạn](../docs/worklog/XLSX_COMPAT_004.md).

Chỉ dùng source SHA trong `CHECKOUT.json` để xác định bản tải. Nếu main/latest chưa cập nhật, file review mới ở artifact `Check-out-index-<SHA>` của [workflow Check out](https://github.com/HoangHung997/NeraSpreadSheet/actions/workflows/check-out.yml); không lấy link latest cũ làm bằng chứng fix mới. Các source branch tạm không phải nhánh phát triển dài hạn.
'''
path.write_text(text,encoding='utf-8',newline='\n');changed.add(path.as_posix())
changed.add('eng/xlsx-compat-004/perf_delivery.py')
manifest.write_text(json.dumps(sorted(changed)))
Path(__file__).unlink()
print('COMPAT004_PROBE_AND_GALLERY_WIRED: same source harness; all captures available through Check out')
