#!/usr/bin/env python3
"""Collect validated platform bundles and publish preview assets, never nuget.org."""
import argparse
import base64
from datetime import datetime, timedelta, timezone
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
from urllib.parse import quote
import zipfile
import gallery
import verify_compatibility_probe


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def gh(*args):
    return subprocess.check_output(['gh', *args], text=True).strip()


def put_repo_text(repo, path, text, message, branch='main'):
    """Create/update a small UTF-8 registry file in GitHub without committing binaries."""
    endpoint = f'repos/{repo}/contents/{quote(path, safe="/")}'
    current = subprocess.run(['gh', 'api', '--method', 'GET', endpoint, '-f', f'ref={branch}'], capture_output=True, text=True)
    sha = None
    if current.returncode == 0:
        sha = json.loads(current.stdout)['sha']
    elif '404' not in current.stderr and 'Not Found' not in current.stderr:
        raise RuntimeError(current.stderr)
    args = ['api', '--method', 'PUT', endpoint,
            '-f', f'message={message} [skip ci]',
            '-f', 'content=' + base64.b64encode(text.encode('utf-8')).decode('ascii'),
            '-f', f'branch={branch}']
    if sha:
        args += ['-f', f'sha={sha}']
    gh(*args)


def publish_repo_registry(repo, sha, run_id, tag, platforms):
    """Persist latest package identity/history in Check out/ after validated main publication."""
    ict = timezone(timedelta(hours=7))
    created = datetime.now(ict)
    created_iso = created.isoformat(timespec='seconds')
    created_display = created.strftime('%d/%m/%Y %H:%M:%S') + ' (ICT, UTC+7)'
    filename = created.strftime('%Y-%m-%d_%H%M_ICT_') + sha[:8] + '.json'
    record_path = 'Check out/Packages/' + filename
    release_base = f'https://github.com/{repo}/releases/download/{tag}'
    package_rows = []
    for rid in ('win-x64', 'linux-x64', 'osx-arm64'):
        meta = platforms[rid]
        package_rows.append(dict(
            rid=rid,
            filename=meta['filename'],
            bytes=meta['bytes'],
            sha256=meta['sha256'],
            download=f'{release_base}/{meta["filename"]}'))
    record = dict(
        schema='nera.checkout.package-registry.v1',
        status='validated',
        createdAt=created_iso,
        createdAtDisplay=created_display,
        sourceSha=sha,
        workflowRunId=int(run_id),
        workflowRun=f'https://github.com/{repo}/actions/runs/{run_id}',
        immutableReleaseTag=tag,
        immutableRelease=f'https://github.com/{repo}/releases/tag/{tag}',
        packages=package_rows)
    put_repo_text(repo, record_path, json.dumps(record, ensure_ascii=False, indent=2) + '\n',
                  'docs(checkout): record validated package set')
    latest = dict(
        schema='nera.checkout.package-registry.latest.v1',
        status='validated',
        createdAt=created_iso,
        createdAtDisplay=created_display,
        sourceSha=sha,
        workflowRunId=int(run_id),
        record=filename,
        immutableReleaseTag=tag,
        isLatest=True)
    put_repo_text(repo, 'Check out/Packages/latest.json', json.dumps(latest, ensure_ascii=False, indent=2) + '\n',
                  'docs(checkout): update latest package registry')

    readme_endpoint = f'repos/{repo}/contents/{quote("Check out/README.md", safe="/")}'
    readme_meta = json.loads(gh('api', '--method', 'GET', readme_endpoint, '-f', 'ref=main'))
    readme = base64.b64decode(readme_meta['content']).decode('utf-8')
    start = '<!-- CHECKOUT-LATEST:START -->'
    end = '<!-- CHECKOUT-LATEST:END -->'
    if start not in readme or end not in readme:
        raise RuntimeError('Check out README is missing auto-update markers')
    block = f'''{start}\n## Bản Check out mới nhất\n\n- **Ngày giờ tạo gói:** **{created_display}**\n- **Trạng thái:** VALIDATED — full `Check out` PASS\n- **Source SHA đã build:** `{sha}`\n- **Full workflow run:** `{run_id}`\n- **Registry mới nhất trong repo:** [`Packages/latest.json`](Packages/latest.json)\n- **Bản ghi bất biến của lượt build này:** [`Packages/{filename}`](Packages/{filename})\n- **Immutable Release:** [`{tag}`](https://github.com/{repo}/releases/tag/{tag})\n\n> Muốn biết gói mới hay cũ: ưu tiên **ngày giờ tạo gói + source SHA + workflow run ID**. Không dùng tên `latest` một mình để kết luận.\n{end}'''
    prefix, remainder = readme.split(start, 1)
    _, suffix = remainder.split(end, 1)
    put_repo_text(repo, 'Check out/README.md', prefix + block + suffix,
                  'docs(checkout): refresh latest package date')


def assemble(source, output, sha, repo, run_id, publish):
    if not re.fullmatch(r'[0-9a-f]{40}', sha) or repo != 'HoangHung997/NeraSpreadSheet':
        raise ValueError('Unexpected repository/source cohort')
    output.mkdir(parents=True, exist_ok=True)
    evidence = output / 'evidence' / 'Check out'
    evidence.mkdir(parents=True)
    platforms = {}
    images = gallery.Gallery(evidence / 'Images', sha, f'https://github.com/{repo}/actions/runs/{run_id}')
    for rid in ('win-x64', 'linux-x64', 'osx-arm64'):
        candidates = list(source.glob(f'check-out-{rid}-{sha}/package.json'))
        if len(candidates) != 1:
            raise ValueError('Exactly one package cohort required: ' + rid)
        meta_path = candidates[0]
        meta = json.loads(meta_path.read_text())
        if meta['filename'] != f'Nera-Avalonia-{rid}.zip':
            raise ValueError('Unexpected package filename')
        package = meta_path.parent / meta['filename']
        if meta['sourceSha'] != sha or meta['rid'] != rid or digest(package) != meta['sha256'] or package.stat().st_size != meta['bytes']:
            raise ValueError('Package hash/provenance mismatch: ' + rid)
        shutil.copy2(package, output / package.name)
        (evidence / 'Reports').mkdir(exist_ok=True)
        shutil.copy2(meta_path, evidence / 'Reports' / (rid + '.json'))
        platforms[rid] = {key: meta[key] for key in ('filename', 'bytes', 'sha256')}
        images.published(package, rid)
        if rid == 'win-x64':
            (evidence / 'Images').mkdir(exist_ok=True)
            for name in ('ribbon-light.png', 'ribbon-dark.png', 'full-window.png', 'customization.png', 'format-number.png', 'format-font.png', 'format-alignment.png', 'format-border.png', 'format-fill.png', 'page-setup.png', 'page-margins.png', 'page-sheet.png', 'zoom-dialog.png', 'compatibility-open.png', 'compatibility-edited.png', 'compatibility-rejected.png'):
                shutil.copy2(meta_path.parent / name, output / name)
                shutil.copy2(meta_path.parent / name, evidence / 'Images' / name)
    for category in ('Avalonia-build','Legacy-SDK','MAUI-Ribbon','MAUI-Filter','Legacy-demo'):
        images.directory(source / 'additional-images' / category, category)
    image_report = images.finish()
    verify_compatibility_probe.verify(source / 'compatibility-probe', sha)
    shutil.copytree(source / 'compatibility-probe', evidence / 'Reports/Compatibility-probe')
    packages = list((source / 'sdk-packages').glob('*.nupkg'))
    packages += list((source / 'ribbon-sdk' / 'packages').glob('NeraSpreadSheet.Avalonia.*.nupkg'))
    if len(packages) < 18 or not any(p.name.startswith('NeraSpreadSheet.Avalonia.') for p in packages):
        raise ValueError('Incomplete neutral/Avalonia SDK feed')
    with zipfile.ZipFile(output / 'Nera-SDK-packages.zip','w',zipfile.ZIP_DEFLATED) as archive:
        names = set()
        for package in packages:
            if package.name in names: raise ValueError('Duplicate package identity')
            names.add(package.name)
            archive.write(package, 'Check out/Packages/' + package.name)
        archive.writestr('Check out/README.txt', f'Local NuGet feed, source {sha}. Add the Packages directory as a NuGet source. Not a nuget.org release.\n')
    report = dict(schema='nera.check-out.v1', sourceSha=sha, workflowRun=run_id,
        workflowUrl=f'https://github.com/{repo}/actions/runs/{run_id}', platforms=platforms,
        sdkPackages=sorted(p.name for p in packages),
        screenshots=dict(count=image_report["imageCount"], groups=image_report["groups"], index="Check out/Images/index.html"),
        requiredGates=['Core and legacy hosts','OpenXML','iOS analytics','Windows packages','MAUI packages',
                       'legacy demo','Avalonia Ribbon','published native app smoke'],
        physicalInputTested=False, excelParityClaimed=False,
        openItems=['H1 host wiring and cross-session structural identity','locale input/display no-op round-trip',
                   'native Mac investigations','hardware/IME/accessibility acceptance','full Excel fidelity'])
    (output / 'CHECKOUT.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n')
    shutil.copy2(output / 'CHECKOUT.json', evidence / 'Reports/CHECKOUT.json')
    (evidence / 'README.txt').write_text('Mở Check out/Images/index.html để xem TẤT CẢ ảnh của từng nền tảng/host. Ảnh và manifest của đúng source; xem workflowUrl trong Reports/CHECKOUT.json để đọc toàn bộ CI. Không phải chứng nhận Excel parity.\n',encoding='utf-8')
    with zipfile.ZipFile(output / 'Nera-Check-out-evidence.zip','w',zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(evidence.rglob('*')):
            if path.is_file(): archive.write(path,path.relative_to(evidence.parent).as_posix())
    shutil.rmtree(evidence.parent)
    files = sorted(path for path in output.iterdir() if path.is_file())
    (output/'SHA256SUMS.txt').write_text(''.join(digest(path)+'  '+path.name+'\n' for path in files))
    if not publish:
        print('Validated Check out preview assembled; no release or branch change.')
        return
    def assert_current_main():
        current = json.loads(gh('api', f'repos/{repo}/git/ref/heads/main'))['object']['sha']
        if current != sha: raise RuntimeError('Main advanced; refusing to publish stale latest assets')
    assert_current_main()
    tag = f'check-out-{sha[:12]}-{run_id}'
    notes = output/'release-notes.md'
    notes.write_text(f'# Check out — engineering preview\n\nSource **`{sha}`**. [Exact-source CI]({report["workflowUrl"]}).\n\n'
        'Giải nén gói theo hệ điều hành, mở thư mục **Check out/App**. Ảnh trong Images, hashes/source trong Reports. '
        'Không cần Visual Studio/.NET riêng. Linux cần desktop libraries; macOS ARM64 chưa ký/notarize.\n\n'
        'SDK NuGet chỉ để thử bằng local feed; không phát hành nuget.org. H1/locale, native Mac investigations, '
        'hardware/IME/accessibility và full Excel fidelity vẫn OPEN. Đây không phải production release.\n',encoding='utf-8')
    assets = sorted(str(path) for path in output.iterdir() if path.is_file() and path != notes)
    gh('release','create',tag,'--repo',repo,'--target',sha,'--prerelease','--title',f'Check out {sha[:12]}', '--notes-file',str(notes),*assets)
    assert_current_main()
    result = subprocess.run(['gh','api',f'repos/{repo}/git/ref/tags/check-out-latest'],capture_output=True,text=True)
    if result.returncode:
        if '404' not in result.stderr and 'Not Found' not in result.stderr: raise RuntimeError(result.stderr)
        gh('api',f'repos/{repo}/git/refs','-f','ref=refs/tags/check-out-latest','-f','sha='+sha)
    else:
        previous = json.loads(result.stdout)['object']
        if previous['type'] != 'commit': raise ValueError('Latest tag is not our lightweight alias')
        if previous['sha'] != sha:
            subprocess.run(['git','merge-base','--is-ancestor',previous['sha'],sha],check=True)
            gh('api','--method','PATCH',f'repos/{repo}/git/refs/tags/check-out-latest','-f','sha='+sha,'-F','force=false')
    check = subprocess.run(['gh','release','view','check-out-latest','--repo',repo],capture_output=True,text=True)
    if check.returncode:
        gh('release','create','check-out-latest','--repo',repo,'--target',sha,'--prerelease','--title','Check out — bản thử nghiệm mới nhất','--notes-file',str(notes),*assets)
    else:
        # Immutable version above remains available if updating the convenience alias is interrupted.
        gh('release','upload','check-out-latest','--repo',repo,'--clobber',*assets)
        gh('release','edit','check-out-latest','--repo',repo,'--prerelease','--title','Check out — bản thử nghiệm mới nhất','--notes-file',str(notes))
    assert_current_main()
    publish_repo_registry(repo, sha, run_id, tag, platforms)
    print(json.dumps(dict(published=True,sourceSha=sha,immutableTag=tag,latestTag='check-out-latest',repoRegistry=True)))
    notes.unlink()


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    for field in ('source','output','sha','repo','run-id'):parser.add_argument('--'+field,required=True)
    parser.add_argument('--publish',action='store_true')
    args=parser.parse_args()
    assemble(Path(args.source),Path(args.output),args.sha,args.repo,args.run_id,args.publish)
