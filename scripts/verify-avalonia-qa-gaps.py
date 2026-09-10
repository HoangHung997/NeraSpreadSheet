"""Validate bounded QA-GAPS-005 native evidence without calling it hardware input proof."""
import hashlib
import json
from pathlib import Path
import struct
import sys

EXPECTED_WIDTHS=[768,820,1024,1366,1920]
EXPECTED_IMAGES={*(f'qa-home-{width}.png' for width in EXPECTED_WIDTHS),'qa-zoom-110.png','qa-table-design.png','qa-filter-window.png'}
REQUIRED={
    'exact-source','native-window','zoom-110-selected','view-a-selection','view-a-zoom','view-a-scroll-x','view-a-scroll-y',
    'view-b-selection','view-b-zoom','view-b-scroll-x','view-b-scroll-y','table-create-command-visible','table-create-executed',
    'table-created','table-context-present','table-context-selectable','filter-target','filter-command','filter-window-open','filter-page-bounded',
    'filter-reapply-command','filter-clear-command','format-menu-four-actions','row-hide','row-unhide','column-hide','column-unhide',
    'roundtrip-table','roundtrip-formula','captures-complete',
}
REQUIRED.update(f'responsive-{width}' for width in EXPECTED_WIDTHS)


def verify(folder, sha):
    root=Path(folder)
    report=json.loads((root/'manifest.json').read_text(encoding='utf-8'))
    if report.get('schema')!='nera.qa-gaps.native.v1' or report.get('sha')!=sha:
        raise ValueError('QA evidence source mismatch')
    if not report.get('nativeWindow') or report.get('physicalInputTested'):
        raise ValueError('QA native/hardware boundary mismatch')
    if report.get('widths')!=EXPECTED_WIDTHS:
        raise ValueError('Responsive width matrix mismatch')
    checks=report.get('checks') or []
    if report.get('assertions')!=len(checks) or len(checks)<35 or len(set(checks))!=len(checks):
        raise ValueError('Incomplete QA assertions')
    if not REQUIRED.issubset(checks):
        raise ValueError('Missing required QA scenarios: '+', '.join(sorted(REQUIRED-set(checks))))
    captures=report.get('captures') or []
    names={item['name'] for item in captures}
    if len(captures)!=len(EXPECTED_IMAGES) or names!=EXPECTED_IMAGES:
        raise ValueError('QA capture set mismatch')
    for item in captures:
        path=root/item['name']
        if path.is_symlink() or not path.is_file() or path.stat().st_size>16*1024*1024:
            raise ValueError('Invalid QA image')
        data=path.read_bytes()
        if data[:8]!=b'\x89PNG\r\n\x1a\n' or hashlib.sha256(data).hexdigest()!=item['sha256']:
            raise ValueError('QA image type/hash mismatch')
        width,height=struct.unpack('>II',data[16:24])
        if (width,height)!=(item['width'],item['height']) or not (250<=width<=4096 and 150<=height<=4096):
            raise ValueError('QA image dimensions mismatch')
    print(f'Verified QA-GAPS-005: {len(checks)} native assertions, {len(captures)} images, exact source {sha}; no physical-input claim.')


if __name__=='__main__':
    if len(sys.argv)!=3: raise SystemExit('usage: verify-avalonia-qa-gaps.py <folder> <sha>')
    verify(sys.argv[1],sys.argv[2])
