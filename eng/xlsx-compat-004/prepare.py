"""Bounded, one-shot edits on the reviewed XLSX compatibility checkpoint.
The workflow creates objects only; it never advances main or a branch itself.
"""
import json
import os
from pathlib import Path
import subprocess
import sys
import urllib.request

ROOT=Path.cwd()
REPO='HoangHung997/NeraSpreadSheet'
BRANCH='feature/ribbon-dialogs-003'
BASE='2742459e9e26b1153c438d7c623778e7c8364957'
SELF='eng/xlsx-compat-004/prepare.py'
WORKFLOW='.github/workflows/xlsx-compat-004-prepare.yml'
MANIFEST='artifacts/compat004/changed.json'
changed=set()

def write(path, content):
    target=ROOT/path;target.parent.mkdir(parents=True,exist_ok=True)
    target.write_text(content,encoding='utf-8',newline='\n');changed.add(path)

def replace(path,old,new,count=1):
    text=(ROOT/path).read_text(encoding='utf-8-sig')
    if text.count(old)!=count:raise ValueError(f'Patch drift {path}: {old[:100]!r}; expected{count}, actual{text.count(old)}')
    write(path,text.replace(old,new))

def prepare():
    subprocess.run(['git','merge-base','--is-ancestor',BASE,'HEAD'],check=True)
    codec='src/NeraSpreadSheet.OpenXml/OpenXmlConditionalFormattingCodec.cs'
    text=(ROOT/codec).read_text(encoding='utf-8-sig')
    start=text.index('    public static IReadOnlyList<CellStylePatch> ReadDifferentialStyles(')
    end=text.index('    public static OpenXmlConditionalFormattingExportPlan WriteDifferentialStyles(',start)
    if 'catch (InvalidDataException) when (preserveUnsupportedMarkup)' not in text[start:end]:raise ValueError('Dxf import no longer matches audited implementation')
    write(codec,text[:start]+text[end:])
    replace(codec,'internal static class OpenXmlConditionalFormattingCodec','internal static partial class OpenXmlConditionalFormattingCodec')
    template='eng/xlsx-compat-004/OpenXmlConditionalFormattingCodec.Import.cs.txt'
    write('src/NeraSpreadSheet.OpenXml/OpenXmlConditionalFormattingCodec.Import.cs',(ROOT/template).read_text())
    (ROOT/template).unlink();changed.add(template)
    options='src/NeraSpreadSheet.OpenXml/OpenXmlWorkbookSerializer.cs'
    replace(options,'public sealed record OpenXmlImportOptions\n{', '''/// <summary>Strict retains existing defaults; Compatibility requests existing opaque preservation.</summary>
public enum OpenXmlImportMode { Strict, Compatibility }

public sealed record OpenXmlImportOptions
{
    /// <summary>Creates an explicit import policy without changing constructor defaults.
    /// Compatibility requires PreserveUnknownParts on export; inspect OpenXmlImportDiagnostics.Get.</summary>
    public static OpenXmlImportOptions ForMode(OpenXmlImportMode mode) => mode switch
    {
        OpenXmlImportMode.Strict => new(),
        OpenXmlImportMode.Compatibility => new() { PreserveUnknownParts = true },
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };''')
    serializer='src/NeraSpreadSheet.OpenXml/NeraOpenXmlWorkbookSerializer.cs'
    replace(serializer,'        return workbook;\n    }\n\n    private static void SaveCore(', '''        if (differentialStyles is OpenXmlDifferentialImport diagnostics)
            OpenXmlImportDiagnostics.Attach(workbook, diagnostics);
        return workbook;
    }

    private static void SaveCore(''')
    replace(serializer,'        if (envelope is not null)\n        {\n            outputBytes = OpenXmlDataValidationPackagePatcher.Patch(',
        '        if (envelope is not null && !envelope.PreservesOpaqueFormatting)\n        {\n            outputBytes = OpenXmlDataValidationPackagePatcher.Patch(')
    envelope='src/NeraSpreadSheet.OpenXml/OpenXmlPackageEnvelope.cs'
    replace(envelope,'    private readonly WorksheetBinding[] _worksheets;', '    private readonly WorksheetBinding[] _worksheets;\n    private readonly OpenXmlOpaqueFormattingGuard? _formattingGuard;\n    public bool PreservesOpaqueFormatting => _formattingGuard is not null;')
    replace(envelope,'        WorksheetBinding[] worksheets)\n    {', '        WorksheetBinding[] worksheets, Workbook workbook)\n    {')
    replace(envelope,'        _worksheets = worksheets;', '        _worksheets = worksheets;\n        if (OpenXmlImportDiagnostics.Get(workbook).RequiresMetadataPreservation)\n            _formattingGuard = new OpenXmlOpaqueFormattingGuard(workbook);')
    replace(envelope,'        ArgumentNullException.ThrowIfNull(workbook);\n        if (workbook.Worksheets.Count', '        ArgumentNullException.ThrowIfNull(workbook);\n        _formattingGuard?.Validate(workbook);\n        if (workbook.Worksheets.Count')
    replace(envelope,'            OpenXmlPackageGraphValidator.Validate(document);', '''            OpenXmlPackageGraphValidator.Validate(document);
            if (OpenXmlPackageEnvelopeStore.TryGet(workbook, out var original) && original.PreservesOpaqueFormatting)
            {
                original.ValidateWorkbookTopology(workbook);
                OpenXmlOpaqueFormattingGuard.ValidateOutput(original._packageBytes, document, original.Worksheets);
            }''')
    replace(envelope,'            bindings);','            bindings, workbook);')
    replace(envelope,'    public static void Detach(Workbook workbook)\n    {', '    public static void Detach(Workbook workbook)\n    {\n        OpenXmlImportDiagnostics.Detach(workbook);')
    preserver='src/NeraSpreadSheet.OpenXml/OpenXmlPackagePreserver.cs'
    replace(preserver,'var preserveConditionalFormatting = worksheetPairs.Any(', 'var preserveConditionalFormatting = envelope.PreservesOpaqueFormatting || worksheetPairs.Any(')
    replace(preserver,'var differentialStyleMap = preserveTableDifferentialStyles\n', 'var differentialStyleMap = preserveTableDifferentialStyles && !envelope.PreservesOpaqueFormatting\n')
    shell='samples/NeraSpreadSheet.Avalonia.Sample/FullShellWindow.cs'
    replace(shell,'private static readonly string[] ExcelPatterns = ["*.xlsx"];','private static readonly string[] ExcelPatterns = ["*.xlsx", "*.dlda"];')
    replace(shell,'        DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status);', '        DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status);\n        InstallCompatibilityNotice(root);')
    replace(shell,'        var loaded = await _serializer.LoadSessionAsync(stream, new OpenXmlImportOptions());\n        if (!_closed) _split.Session = loaded;', '        await OpenCompatibleStreamAsync(stream, files[0].Name);')
    replace(shell,'await _serializer.SaveSessionAsync(session, staging, new OpenXmlExportOptions());','await _serializer.SaveSessionAsync(session, staging, new OpenXmlExportOptions { PreserveUnknownParts = true });')
    # Keep the existing shell transaction/error path; only block metadata commands
    # under the explicit opaque-format restriction. Cell formatting remains usable.
    ribbon='samples/NeraSpreadSheet.Avalonia.Sample/FullShellWindow.Ribbon.cs'
    replace(ribbon,'context => ResolveSessionHandler(entry.Id) is IStatefulCommandHandler stateful ? stateful.GetState(context) : new CommandState(ResolveSessionHandler(entry.Id).CanExecute(context))',
        'context => RestrictMetadataCommand(entry.Id) ? CommandState.Disabled : ResolveSessionHandler(entry.Id) is IStatefulCommandHandler stateful ? stateful.GetState(context) : new CommandState(ResolveSessionHandler(entry.Id).CanExecute(context))')
    for path in sorted((ROOT/'eng/xlsx-compat-004').glob('*.stage')):
        first,body=path.read_text(encoding='utf-8').split('\n',1)
        if not first.startswith('TARGET='):raise ValueError('Missing staged target')
        target=first[7:]
        if not target.startswith(('src/','tests/','samples/','scripts/','docs/','Check out/')) or '..' in Path(target).parts:raise ValueError('Unsafe staged target')
        if (ROOT/target).exists():raise ValueError('Refusing to overwrite existing staged target '+target)
        write(target,body);rel=path.relative_to(ROOT).as_posix();path.unlink();changed.add(rel)
    Path('artifacts/compat004').mkdir(parents=True,exist_ok=True)
    Path(MANIFEST).write_text(json.dumps(sorted(changed)))
    print('COMPAT004_PREPARED_SOURCE: guarded changes only; no ref moved')

def materialize():
    if os.environ.get('GITHUB_REPOSITORY')!=REPO or os.environ.get('GITHUB_REF_NAME')!=BRANCH:raise ValueError('Wrong repository/ref')
    names=set(json.loads(Path(MANIFEST).read_text()))
    names.update((SELF,WORKFLOW))
    for path in (SELF,WORKFLOW):(ROOT/path).unlink()
    parent=os.environ['GITHUB_SHA'];base_tree=subprocess.check_output(['git','rev-parse','HEAD^{tree}'],text=True).strip()
    def api(endpoint,body):
        request=urllib.request.Request('https://api.github.com/repos/'+REPO+'/'+endpoint,data=json.dumps(body).encode(),headers={'Authorization':'Bearer '+os.environ['GH_TOKEN'],'Accept':'application/vnd.github+json','Content-Type':'application/json'},method='POST')
        with urllib.request.urlopen(request,timeout=60) as response:return json.load(response)
    entries=[];workflow_entries=[]
    for name in sorted(names):
        p=ROOT/name;e=dict(path=name,mode='100644',type='blob')
        if p.exists():e['content']=p.read_text(encoding='utf-8')
        else:e['sha']=None
        if name.startswith('.github/workflows/'):
            if 'content' in e:e['sha']=api('git/blobs',dict(content=e.pop('content'),encoding='utf-8'))['sha']
            workflow_entries.append(e)
        else:entries.append(e)
    tree=api('git/trees',dict(base_tree=base_tree,tree=entries))['sha']
    commit=api('git/commits',dict(message='fix(openxml): retain validated opaque differential formatting with explicit diagnostics and save restrictions',tree=tree,parents=[parent]))['sha']
    receipt=dict(sourceSha=commit,treeSha=tree,parentSha=parent,changedPaths=sorted(names),workflowEntries=workflow_entries,refUpdated=False)
    Path('artifacts/compat004/receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
    print('COMPAT004_SOURCE_OBJECT '+json.dumps(receipt))

if __name__=='__main__':
    if len(sys.argv)>1 and sys.argv[1]=='--materialize':materialize()
    else:prepare()
