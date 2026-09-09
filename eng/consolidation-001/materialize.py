#!/usr/bin/env python3
"""Readable one-time integration recipe. Never changes original lane refs or main."""
import json
from pathlib import Path
import shutil
import subprocess

ROOT = '2e80250b1b1c51ad56a415d1bbeb70d7b2e85f25'
STAGE = Path('eng/consolidation-001')

def git(*args):
    return subprocess.check_output(['git',*args],text=True).strip()

def write(path,text):
    p=Path(path);p.parent.mkdir(parents=True,exist_ok=True);p.write_text(text,encoding='utf-8')

def replace(path,old,new,count=1):
    p=Path(path);s=p.read_text(encoding='utf-8')
    if s.count(old)!=count:raise ValueError('Unexpected edit context: '+path)
    p.write_text(s.replace(old,new),encoding='utf-8')

# Verify the source files were not changed by another writer or merge resolution.
expected={
 'src/NeraSpreadSheet.OpenXml/NeraOpenXmlSpreadsheetSessionSerializer.cs':'082b59016a22a9ec9b5797511e48f39d0cfc46e1',
 'tests/NeraSpreadSheet.Editing.Tests/ClipboardSafetyTests.cs':'126fc3372a9cf5ac003009ac7696144d0b21b6bb',
 'tests/NeraSpreadSheet.OpenXml.Tests/WorksheetViewStatePersistenceTests.cs':'d5ea2cc175be25e8029c6f9834c9695c44431bfe',
 'tests/NeraSpreadSheet.OpenXml.Tests/WorksheetViewStateWindowBindingTests.cs':'ff1255917cf956a6657460e9f42d48c805b69be4'}
for path,sha in expected.items():
    if git('hash-object',path)!=sha:raise ValueError('Unexpected baseline blob: '+path)
replace('src/NeraSpreadSheet.OpenXml/NeraOpenXmlSpreadsheetSessionSerializer.cs',
        'document.WorkbookPart?.Workbook.GetFirstChild<BookViews>()?',
        'document.WorkbookPart?.Workbook?.GetFirstChild<BookViews>()?')
replace('tests/NeraSpreadSheet.Editing.Tests/ClipboardSafetyTests.cs',
        '        session.ActiveWorksheet.MergedCells.Add(new CellRange(new CellAddress(4, 4), new CellAddress(5, 5)));',
        '        // The public read-only view currently exposes a mutable backing collection.\n'
        '        // Exercise a mutation without Worksheet.Version changing, not the normal MergeCells API.\n'
        '        var version = session.ActiveWorksheet.Version;\n'
        '        var ranges = session.ActiveWorksheet.MergedCells.Ranges as ICollection<CellRange>\n'
        '            ?? throw new AssertFailedException("Expected the current mutable range backing collection.");\n'
        '        ranges.Add(new CellRange(new CellAddress(4, 4), new CellAddress(5, 5)));\n'
        '        Assert.AreEqual(version, session.ActiveWorksheet.Version);')
methods={
 'WorksheetViewStatePersistenceTests.cs':['ExistingUnsplitViewsShouldRetainSiblingIdsUnknownAttributesAndChildren',
  'EditingChosenViewShouldNotResetOtherWorkbookViewOrUnknownExtensions','ClearingSplitShouldRemoveOnlyOwnedPaneAndKeepViews'],
 'WorksheetViewStateWindowBindingTests.cs':['SavingChosenWindowShouldPreserveOtherWindowOnEverySheet',
  'EditingMissingChosenViewShouldCreateMatchingIdWithoutOverwritingSibling','TwoRoundTripsShouldKeepChosenWindowAndIndependentExactOffsets']}
for name,tests in methods.items():
    path=Path('tests/NeraSpreadSheet.OpenXml.Tests')/name
    s=path.read_text(encoding='utf-8').replace('.Workbook.','.Workbook!.').replace('.Worksheet.','.Worksheet!.')
    s=s.replace('var xml = document.WorkbookPart!.Workbook;',
                'var xml = document.WorkbookPart!.Workbook ?? throw new AssertFailedException("Expected workbook fixture markup.");')
    for name in tests:
        start=s.index('    public async Task '+name+'(')
        end=s.find('\n    [TestMethod]',start)
        if end<0:end=s.index('\n    private ',start)
        piece=s[start:end]
        if 'new OpenXmlImportOptions()' not in piece:raise ValueError('Expected preservation import test: '+name)
        piece=piece.replace('new OpenXmlImportOptions()','new OpenXmlImportOptions { PreserveUnknownParts = true }')
        piece=piece.replace('new OpenXmlExportOptions()','new OpenXmlExportOptions { PreserveUnknownParts = true }')
        s=s[:start]+piece+s[end:]
    path.write_text(s,encoding='utf-8')
path='tests/NeraSpreadSheet.OpenXml.Tests/WorksheetViewStatePersistenceTests.cs'
marker='    private static async Task<MemoryStream> CreateStandardFixture()'
replace(path,marker,(STAGE/'view-opt-in-test.txt').read_text(encoding='utf-8')+marker)

# Centralize triggers, preserve all jobs and acceptance assertions unchanged.
for name in ['ci','chatgpt-ios-analytics-smoke','q003c-openxml-analytics-gate','release-009-packages',
             'release-009-maui-packages','release-009-demo','avalonia-primary-ribbon-002']:
    path=Path('.github/workflows')/(name+'.yml');s=path.read_text(encoding='utf-8')
    start=s.index('\non:');end=s.index('\npermissions:',start)
    path.write_text(s[:start]+'\non:\n  workflow_call:\n  workflow_dispatch:\n'+s[end:],encoding='utf-8')
path=Path('.github/workflows/avalonia-001.yml');s=path.read_text(encoding='utf-8')
path.write_text(s[:s.index('\non:')]+'\non:\n  workflow_dispatch:\n'+s[s.index('\npermissions:'):],encoding='utf-8')
for name,branch in [('perf-008','feature/perf-008-harness'),('excel-file-audit','feature/excel-file-audit-20260909')]:
    replace('.github/workflows/'+name+'.yml',"  push:\n    branches: ['"+branch+"']\n",'')
for name in ['avalonia-offline-dev','avalonia-source-evidence','consolidate-checkout-audit']:
    Path('.github/workflows/'+name+'.yml').unlink()
path=Path('AGENTS.md');s=path.read_text(encoding='utf-8')
s=s[:s.index('## 6. Quy trình Git')]+(STAGE/'git-policy.md').read_text(encoding='utf-8')+s[s.index('## 7. Cổng hoàn thành'):]
path.write_text(s,encoding='utf-8')
path=Path('Directory.Build.props');s=path.read_text(encoding='utf-8')
if 'avalonia;' not in s:s=s.replace('<PackageTags>','<PackageTags>avalonia;')
path.write_text(s,encoding='utf-8')
for path in ['docs/current-status.md','docs/worklog/CURRENT.md']:
    p=Path(path);s=p.read_text(encoding='utf-8');line=s.index('\n')
    link='worklog/CONSOLIDATION_20260909.md' if path.endswith('current-status.md') else 'CONSOLIDATION_20260909.md'
    s=s[:line+1]+'\n> **Hồ sơ lịch sử trước hợp nhất.** Chỉ đạo nhánh và handoff hiện hành ở [CONSOLIDATION_20260909]('+link+').\n> Không tiếp tục queue/lease/nhánh trong các checkpoint cũ dưới đây. Source canonical là `main`; xem `Check out`.\n\n'+s[line+1:]
    p.write_text(s,encoding='utf-8')
for path in sorted((STAGE/'files').rglob('*')):
    if path.is_file():
        target=Path(path.relative_to(STAGE/'files'));target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,target)

# Immutable original inventory is from the read-only Actions run, not a guessed list.
inv=json.loads(Path('/tmp/consolidation-inventory/branch-inventory.json').read_text())
if inv['sourceSha']!='5d4131245d281164fd747c0192ca5e71e0f96c03' or inv['rootSha']!=ROOT:
    raise ValueError('Wrong original inventory')
source_names={'feature/bootstrap-architecture-v0.1','feature/avalonia-001-host','feature/avalonia-primary-ribbon-002',
              'feature/chatgpt-clipboard-view-state','feature/excel-file-audit-20260909'}
rows=[]
for b in inv['branches']:
    if b['name']=='feature/consolidate-checkout-001':continue
    if b['name']=='main':
        status='previous-main';reason='Previous default snapshot; kept as a rollback anchor, main itself is not deleted.'
    elif b['name'] in source_names:
        status='integration-source';reason='Source included in the consolidated candidate; final combined tests required.'
    elif b['containedInRoot']:
        status='already-contained';reason='Exact commit is an ancestor of the accepted root source.'
    else:
        cherry=git('cherry',ROOT,b['sha']).splitlines()
        if cherry and all(row.startswith('- ') for row in cherry):
            status='patch-equivalent';reason='Every branch-only patch already appears in root by git cherry.'
        else:
            status='archive-only';reason='Historical, partial, diagnostic or superseded work. All original commits retained; not blindly replayed into product.'
    rows.append(dict(name=b['name'],sha=b['sha'],archiveTag=f'archive/consolidation-20260909/{len(rows)+1:03d}',
                     disposition=status,reason=reason,branchOnly=b['branchOnly'],rootOnly=b['rootOnly']))
if len(rows)!=62:raise ValueError('Unexpected original inventory size')
manifest=dict(schema=1,approvedPurpose='User-directed one-canonical-branch consolidation, 2026-09-09',
    defaultBranch='main',temporaryBranch='feature/consolidate-checkout-001',rootSha=ROOT,
    requiredSources=[ROOT,'c4a63fc19a4a45f515175ea542a0e1569e16fa5a','197d666022a8c05991f70202c7e9defc0c36e513','84d71084a7cfc070cb8c80a87e2645101b44b462'],
    pullRequests=[dict(number=n,sha=s) for n,s in [(1,ROOT),(4,'f890ae2610e0e08175e45ea1b0d8bb7650a24754'),
        (5,'197d666022a8c05991f70202c7e9defc0c36e513'),(6,'84d71084a7cfc070cb8c80a87e2645101b44b462'),
        (7,'c4a63fc19a4a45f515175ea542a0e1569e16fa5a')]],branches=rows)
write('Check out/branches.json',json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
text='# Danh mục nhánh trước hợp nhất — 09/09/2026\n\n62 nhánh có sẵn; snapshot workflow34329250330. Phân loại bằng ancestry và patch-id. **archive-only không có nghĩa đã tích hợp/đã nghiệm thu**. Mọi SHA được giữ bằng tag trước khi xóa tên nhánh.\n\n| Nhánh ban đầu | SHA | Tag phục hồi | Xử lý |\n|---|---|---|---|\n'
for b in rows:text+=f"| `{b['name']}` | `{b['sha']}` | `{b['archiveTag']}` | {b['disposition']} |\n"
text+='\n## Khôi phục local\n\n```sh\ngit fetch origin --tags\ngit switch --detach archive/consolidation-20260909/001\n```\n\nChọn đúng tag trong bảng. Không merge nguyên nhánh cũ chỉ để hết nhánh: đó có thể là materializer/probe/lease hoặc diagnostic chưa đạt. Cleanup cần exact-source CI, archive tags, expected SHA và không PR phụ thuộc. Ref mới/tiến ngoài inventory phải được giữ; không đổi lịch ngoài repository.\n'
write('Check out/branches.md',text)
print('Materialized reviewed integration delta; no build/native/CI acceptance claimed by this recipe.')
