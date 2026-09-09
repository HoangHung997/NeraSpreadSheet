#!/usr/bin/env python3
"""One-time user-authorized cleanup. Archive every ref before compare-and-swap deletion."""
import argparse
import json
from pathlib import Path
import re
import subprocess

REPOSITORY = 'HoangHung997/NeraSpreadSheet'
PREFIX = 'archive/consolidation-20260909/'
COMPLETE = PREFIX + 'complete'


def run(*args):
    return subprocess.check_output(args, text=True).strip()


def remote_refs(kind):
    rows = run('git', 'ls-remote', '--' + kind, 'origin').splitlines()
    return {ref: sha for sha, ref in (row.split('\t', 1) for row in rows) if not ref.endswith('^{}')}


def build_plan(manifest, current, validated_sha):
    if not re.fullmatch('[0-9a-f]{40}', validated_sha):
        raise ValueError('Full exact validated commit required')
    if manifest['defaultBranch'] != 'main' or manifest['temporaryBranch'] != 'feature/consolidate-checkout-001':
        raise ValueError('Unrecognized one-time cleanup manifest')
    branches = manifest['branches']
    if len({row['name'] for row in branches}) != len(branches):
        raise ValueError('Duplicate branch in manifest')
    if current.get('refs/heads/main') != validated_sha:
        raise ValueError('Main moved or was not promoted to the validated source')
    allowed = {'refs/heads/' + row['name'] for row in branches}
    allowed.add('refs/heads/' + manifest['temporaryBranch'])
    extra = set(current) - allowed
    if extra:
        raise ValueError('Unreviewed/new branches must not be deleted: ' + ', '.join(sorted(extra)))
    archives, deletions = {}, {}
    for row in branches:
        if not re.fullmatch('[0-9a-f]{40}', row['sha']) or not row['archiveTag'].startswith(PREFIX):
            raise ValueError('Invalid archive identity')
        tag = 'refs/tags/' + row['archiveTag']
        if tag in archives: raise ValueError('Duplicate archive target')
        archives[tag] = row['sha']
        ref = 'refs/heads/' + row['name']
        if row['name'] == 'main':
            continue  # Archive old main, never remove current main.
        if ref in current:
            if current[ref] != row['sha']:
                raise ValueError('Branch advanced; its owner must release it: ' + row['name'])
            deletions[ref] = row['sha']
    candidate = 'refs/heads/' + manifest['temporaryBranch']
    if candidate in current:
        if current[candidate] != validated_sha:
            raise ValueError('Integration branch advanced during validation')
        deletions[candidate] = validated_sha
    archives['refs/tags/' + PREFIX + 'integration-candidate'] = validated_sha
    return archives, deletions


def safe_pull_requests(manifest, pulls, deletions, validated_sha):
    accepted = {pr['number']: pr['sha'] for pr in manifest['pullRequests']}
    close = []
    for pr in pulls:
        ref = 'refs/heads/' + pr['head']['ref']
        if pr['head']['ref'] == manifest['temporaryBranch']:
            if pr['head']['sha'] != validated_sha: raise ValueError('Integration PR moved')
            close.append(pr)
        elif pr['number'] in accepted:
            if pr['head']['sha'] != accepted[pr['number']]: raise ValueError('Source PR moved')
            close.append(pr)
        elif ref in deletions or 'refs/heads/' + pr['base']['ref'] in deletions:
            raise ValueError('Unreviewed open PR depends on a deletion candidate')
    return close


def cleanup(manifest_path, validated_sha, repository, run_url, apply):
    if repository != REPOSITORY: raise ValueError('Unexpected repository')
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    tags = remote_refs('tags')
    if 'refs/tags/' + COMPLETE in tags:
        print('One-time consolidation already completed; no new branch will be deleted.')
        return
    current = remote_refs('heads')
    archives, deletions = build_plan(manifest, current, validated_sha)
    for tag, sha in archives.items():
        if tag in tags and tags[tag] != sha: raise ValueError('Archive tag already points elsewhere: ' + tag)
    for sha in manifest['requiredSources']:
        subprocess.run(['git','merge-base','--is-ancestor',sha,validated_sha],check=True)
    pages = json.loads(run('gh','api','--paginate','--slurp',f'repos/{repository}/pulls?state=open&per_page=100'))
    pulls = [pr for page in pages for pr in page]
    closing = safe_pull_requests(manifest, pulls, deletions, validated_sha)
    report = dict(sourceSha=validated_sha, archiveRefs=archives, deletedRefs=deletions,
                  closePullRequests=[pr['number'] for pr in closing], applied=False)
    print(json.dumps(report,ensure_ascii=False,indent=2))
    if not apply: return
    # Ensure publication completed for this exact source before any remote deletion.
    asset = json.loads(run('gh','api',f'repos/{repository}/releases/tags/check-out-latest'))
    if not asset['prerelease'] or validated_sha not in (asset.get('body') or ''):
        raise ValueError('Missing exact-source Check out prerelease')
    required = {'CHECKOUT.json','SHA256SUMS.txt','Nera-Avalonia-win-x64.zip',
                'Nera-Avalonia-linux-x64.zip','Nera-Avalonia-osx-arm64.zip','Nera-SDK-packages.zip'}
    if not required <= {a['name'] for a in asset['assets'] if a['size'] > 0}:
        raise ValueError('Incomplete downloadable release')
    if remote_refs('heads') != current: raise ValueError('Remote branches changed before archive')
    additions = [f'{sha}:{tag}' for tag,sha in archives.items() if tag not in tags]
    if additions:
        subprocess.run(['git','push','--atomic','origin',*additions],check=True)
    verified = remote_refs('tags')
    if any(verified.get(tag) != sha for tag,sha in archives.items()):
        raise ValueError('Cannot verify every archive; no deletion')
    if remote_refs('heads') != current: raise ValueError('Remote branches changed after archive')
    for pr in closing:
        latest = json.loads(run('gh','api',f'repos/{repository}/pulls/{pr["number"]}'))
        if latest['head']['sha'] != pr['head']['sha']: raise ValueError('PR advanced before closure')
        subprocess.run(['git','merge-base','--is-ancestor',latest['head']['sha'],validated_sha],check=True)
        body = (f'Đã hợp nhất source của PR vào nhánh `main` tại **`{validated_sha}`** theo yêu cầu chủ repository '
                f'thu gọn nhánh. [CI đúng source và gói Check out]({run_url}).\n\n'
                'Đóng PR này để tránh nhiều đầu mối, không phải tuyên bố mọi hạng mục của PR đã nghiệm thu. '
                'H1/locale/hardware và giới hạn đã ghi trong `Check out/README.md` cùng hồ sơ consolidation vẫn OPEN. '
                'Mọi nhánh nguồn có archive tag và mapping trong `Check out/branches.json`; không mất lịch sử. '
                'Không tiếp tục push nhánh cũ; công việc kế tiếp phải lấy `main` và được cấp phạm vi mới.')
        run('gh','api',f'repos/{repository}/issues/{pr["number"]}/comments','-f','body='+body)
        run('gh','api','--method','PATCH',f'repos/{repository}/pulls/{pr["number"]}','-f','state=closed')
    pages = json.loads(run('gh','api','--paginate','--slurp',f'repos/{repository}/pulls?state=open&per_page=100'))
    for pr in (pr for page in pages for pr in page):
        if any('refs/heads/' + pr[side]['ref'] in deletions for side in ('head','base')):
            raise ValueError('An open PR still depends on a branch; refusing deletion')
    if remote_refs('heads') != current: raise ValueError('A writer changed a branch; refusing deletion')
    # Leases provide exact old-value checks for DELETE, not history-rewriting pushes.
    # Atomic capability guarantees that an advanced source prevents every deletion.
    if deletions:
        leases = [f'--force-with-lease={ref}:{sha}' for ref,sha in deletions.items()]
        subprocess.run(['git','push','--atomic',*leases,'origin',*(':'+ref for ref in deletions)],check=True)
    final = remote_refs('heads')
    if final != {'refs/heads/main':validated_sha}:
        raise ValueError('New branch or main update raced cleanup; no completion marker')
    subprocess.run(['git','push','origin',f'{validated_sha}:refs/tags/{COMPLETE}'],check=True)
    report.update(applied=True,remainingBranches=final)
    Path('consolidation-result.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('CONSOLIDATION_SUCCESS '+json.dumps(dict(sourceSha=validated_sha,archived=len(archives),deleted=len(deletions),remaining=1)))


if __name__ == '__main__':
    parser=argparse.ArgumentParser()
    for name in ('manifest','sha','repo','run-url'): parser.add_argument('--'+name,required=True)
    parser.add_argument('--apply',action='store_true')
    args=parser.parse_args()
    cleanup(Path(args.manifest),args.sha,args.repo,args.run_url,args.apply)
