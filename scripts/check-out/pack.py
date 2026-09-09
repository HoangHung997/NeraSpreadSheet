#!/usr/bin/env python3
"""Build a user-facing Check out bundle from already-smoked published binaries."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import zipfile


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def pack(root, app, images, output, sha, rid, run_url):
    if not re.fullmatch(r'[0-9a-f]{40}', sha):
        raise ValueError('A full immutable source SHA is required')
    executable = app / ('NeraSpreadSheet.Avalonia.Sample.exe' if rid.startswith('win-') else 'NeraSpreadSheet.Avalonia.Sample')
    if not executable.is_file():
        raise FileNotFoundError(executable)
    if not any(app.glob('**/hostfxr*')) and not any(app.glob('**/libhostfxr*')):
        raise ValueError('Expected a self-contained runtime, not a framework-dependent package')
    requirements = {
        'smoke': ('NERA_AVALONIA_SMOKE_SUCCESS ', 12),
        'full-ui-smoke': ('NERA_AVALONIA_FULL_UI_SUCCESS ', 23),
        'formula-ux-smoke': ('NERA_AVALONIA_FORMULA_UX_SUCCESS ', 23),
        'ribbon-visual-smoke': ('NERA_AVALONIA_RIBBON_VISUAL_SUCCESS ', 190),
        'dialogs-smoke': ('NERA_AVALONIA_DIALOGS_SUCCESS ', 70),
    }
    smokes = {}
    for name, (prefix, minimum) in requirements.items():
        records = [json.loads(line.split(prefix, 1)[1]) for line in (images / (name + '.log')).read_text(encoding='utf-8-sig').splitlines() if line.startswith(prefix)]
        if len(records) != 1 or records[0]['sha'] != sha or not records[0]['nativeWindow'] or records[0]['assertions'] < minimum:
            raise ValueError('Published app smoke evidence failed: ' + name)
        smokes[name] = records[0]
    if not (images / 'ribbon-visual/manifest.json').is_file():
        raise FileNotFoundError('Missing Ribbon image manifest')
    output.mkdir(parents=True, exist_ok=True)
    staging = output / 'staging' / 'Check out'
    if staging.exists():
        raise FileExistsError('Refusing to mix two package cohorts')
    shutil.copytree(app, staging / 'App')
    shutil.copytree(images, staging / 'Images')
    (staging / 'Reports').mkdir()
    (staging / 'Licenses').mkdir()
    shutil.copy2(root / 'docs/third-party-notices.md', staging / 'Licenses/third-party-notices.md')
    for notice in (root / 'src/NeraSpreadSheet.Iconography/ThirdPartyLicenses').glob('*.txt'):
        shutil.copy2(notice, staging / 'Licenses' / notice.name)
    # Fonts shipped as SDK resources remain in their assemblies. Do not collect host-machine fonts.
    (staging / 'README.txt').write_text(
        'NERASPREADSHEET — BẢN CHẠY THỬ AVALONIA\n'
        f'Source: {sha}\nPlatform: {rid}\nCI: {run_url}\n\n'
        'Giữ toàn bộ thư mục App. Windows chạy App/NeraSpreadSheet.Avalonia.Sample.exe.\n'
        'Linux/macOS chạy executable cùng tên, không có .exe. Không cần cài .NET.\n'
        'Linux cần desktop/X11 và các thư viện hệ thống; macOS là bản ARM64 chưa ký/notarize.\n'
        'Không tắt bảo vệ hệ điều hành toàn cục. Đây không phải bộ cài hoặc bản production.\n'
        'Images chứa ảnh thật của gói đã publish; Reports chứa source, smoke, checksums.\n'
        'Chưa chứng nhận full Excel fidelity, H1/locale, phần cứng, IME hay screen reader.\n', encoding='utf-8')
    files = {p.relative_to(staging).as_posix(): digest(p) for p in sorted(staging.rglob('*')) if p.is_file()}
    evidence = dict(schema='nera.check-out.package.v1', sourceSha=sha, rid=rid, runUrl=run_url,
                    selfContained=True, smokes=smokes, files=files, physicalInputTested=False)
    (staging / 'Reports/package.json').write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    filename = f'Nera-Avalonia-{rid}.zip'
    with zipfile.ZipFile(output / filename, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path in sorted(staging.rglob('*')):
            if path.is_file():
                if path.is_symlink():
                    raise ValueError('Unexpected symbolic link in published distribution')
                archive.write(path, path.relative_to(staging.parent).as_posix())
    (output / 'package.json').write_text(json.dumps(dict(sourceSha=sha, rid=rid, filename=filename,
        sha256=digest(output / filename), bytes=(output / filename).stat().st_size, smokes=smokes), indent=2) + '\n')
    for target, source in {'ribbon-light.png':'Light-1024-home.png', 'ribbon-dark.png':'Dark-1024-home.png',
                           'full-window.png':'full-window.png', 'customization.png':'Light-customization.png'}.items():
        shutil.copy2(images / 'ribbon-visual' / source, output / target)
    for target, source in {'format-number.png': 'number', 'format-font.png': 'font', 'format-alignment.png': 'alignment', 'format-border.png': 'border', 'format-fill.png': 'fill', 'page-setup.png': 'page', 'page-margins.png': 'margins', 'page-sheet.png': 'sheet', 'zoom-dialog.png': 'zoom'}.items():
        shutil.copy2(images / 'dialogs' / ('Light-' + source + '.png'), output / target)
    shutil.rmtree(output / 'staging')
    print(json.dumps(dict(sourceSha=sha, rid=rid, package=filename, sha256=digest(output / filename))))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    for field in ('root', 'app', 'images', 'output', 'sha', 'rid', 'run-url'):
        parser.add_argument('--' + field, required=True)
    args = parser.parse_args()
    pack(Path(args.root), Path(args.app), Path(args.images), Path(args.output), args.sha, args.rid, args.run_url)
