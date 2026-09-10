"""Reviewed follow-up wiring. Guard exact anchors, track every edit, then self-delete."""
import json
from pathlib import Path

manifest=Path('artifacts/compat004/changed.json')
changed=set(json.loads(manifest.read_text()))

def replace(path, old, new, count=1):
    p=Path(path);text=p.read_text(encoding='utf-8-sig')
    if text.count(old)!=count:raise ValueError(f'Completion drift {path}: expected {count}, actual {text.count(old)}: {old[:90]!r}')
    p.write_text(text.replace(old,new),encoding='utf-8',newline='\n');changed.add(path)

# The former fixture put CF before autoFilter, contrary to worksheet order.
# Validate the INPUT as well as the output instead of relying on export to heal it.
path='tests/NeraSpreadSheet.OpenXml.Tests/RichAutoFilterRoundTripTests.cs'
replace(path,'''            var sheetData = worksheet.Root!.Element(SpreadsheetNamespace + "sheetData")!;
            sheetData.AddAfterSelf(new XElement(
                SpreadsheetNamespace + "conditionalFormatting",''', '''            var filterAnchor = worksheet.Root!.Element(SpreadsheetNamespace + "sortState") ??
                worksheet.Root.Element(SpreadsheetNamespace + "autoFilter")!;
            filterAnchor.AddAfterSelf(new XElement(
                SpreadsheetNamespace + "conditionalFormatting",''')
text=Path(path).read_text();start=text.index('    public async Task PreservedUnsupportedConditionalFormattingShouldKeepColorFilterDxfBinding(');end=text.index('\n    [TestMethod]',start)
old=text[start:end];new=old.replace('        source.Position = 0;\n        var loaded', '        AssertSchemaValid(source);\n        source.Position = 0;\n        var loaded')
if new==old:raise ValueError('Fixture input validation anchor missing')
replace(path,old,new)

# Old opaque=true case deliberately attempted CF additions, then expected them
# to disappear. The new user contract explicitly prohibits this silent loss.
# Keep the complete editable/non-opaque case; test rejection + immutable bytes
# and separate permitted cell-only preservation in the opaque branch.
path='tests/NeraSpreadSheet.OpenXml.Tests/TableNativeProducerCorpusTests.cs'
replace(path,'''        session.Tables.SetStyle(table.Id, "Native");
        int? previousCount = null;''', '''        session.Tables.SetStyle(table.Id, "Native");
        if (opaque)
        {
            using var rejected = new MemoryStream(); rejected.Write(source); rejected.Position = 3;
            var before = rejected.ToArray();
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                new NeraOpenXmlSpreadsheetSessionSerializer().SaveSessionAsync(session, rejected,
                    new OpenXmlExportOptions { PreserveUnknownParts = true }));
            CollectionAssert.AreEqual(before, rejected.ToArray());
            Assert.AreEqual(3L, rejected.Position);
            Assert.AreEqual(1, sheet.ConditionalFormattingRuleCount, "Failed save must not silently discard the in-memory addition.");
            var retained = await Load(source, true);
            retained.SetValue(new CellAddress(7, 7), 1234.5);
            for (var cycle = 0; cycle < 3; cycle++)
            {
                var bytes = await Save(retained, true);
                AssertSchemaValid(bytes);
                AssertTableGraphPreserved(source, bytes);
                using var stream = new MemoryStream(bytes);
                using var document = SpreadsheetDocument.Open(stream, false);
                var rule = ReadXml(document.WorkbookPart!.WorksheetParts.Single()).Descendants(S + "cfRule").Single();
                Assert.AreEqual("duplicateValues", (string?)rule.Attribute("type"));
                Assert.AreEqual("0", (string?)rule.Attribute("dxfId"));
                Assert.AreEqual("9", (string?)rule.Attribute("priority"));
                Assert.AreEqual("General", (string?)ReadXml(document.WorkbookPart.WorkbookStylesPart!).Descendants(S + "dxf").First().Element(S + "numFmt")?.Attribute("formatCode"));
                retained = await Load(bytes, true);
                Assert.AreEqual(1234.5, retained.ActiveWorksheet.GetValue(new CellAddress(7, 7)));
            }
            return;
        }
        int? previousCount = null;''')

# The file picker and native smoke use one export implementation.
replace('samples/NeraSpreadSheet.Avalonia.Sample/FullShellWindow.cs',
    'await _serializer.SaveSessionAsync(session, staging, new OpenXmlExportOptions { PreserveUnknownParts = true });',
    'await SaveCompatibleStreamAsync(session, staging);')
replace('samples/NeraSpreadSheet.Avalonia.Sample/FullShellWindow.Compatibility.cs',
    '    private bool RestrictMetadataCommand(CommandId id) =>',
    '''    private Task SaveCompatibleStreamAsync(NeraSpreadSheet.Editing.SpreadsheetSession session, Stream destination) =>
        _serializer.SaveSessionAsync(session, destination, new OpenXmlExportOptions { PreserveUnknownParts = true });

    private bool RestrictMetadataCommand(CommandId id) =>''')
replace('samples/NeraSpreadSheet.Avalonia.Sample/Program.cs',
    '                if (desktop.Args?.Contains("--dialogs-smoke", StringComparer.Ordinal) == true)',
    '''                if (desktop.Args?.Contains("--compatibility-smoke", StringComparer.Ordinal) == true)
                    window.Opened += (_, _) => StartRibbonAfterNativeFrame(window, desktop, compatibility: true);
                else if (desktop.Args?.Contains("--dialogs-smoke", StringComparer.Ordinal) == true)''')
replace('samples/NeraSpreadSheet.Avalonia.Sample/Program.cs', 'bool dialogs = false)', 'bool dialogs = false, bool compatibility = false)')
replace('samples/NeraSpreadSheet.Avalonia.Sample/Program.cs',
    '            if (dialogs) window.StartDialogsSmoke(lifetime); else window.StartRibbonVisualSmoke(lifetime);',
    '            if (compatibility) window.StartCompatibilitySmoke(lifetime); else if (dialogs) window.StartDialogsSmoke(lifetime); else window.StartRibbonVisualSmoke(lifetime);')

path='.github/workflows/avalonia-primary-ribbon-002.yml'
replace(path,"@{ Argument = '--dialogs-smoke'; Prefix = 'NERA_AVALONIA_DIALOGS_SUCCESS '; Minimum = 70 }", "@{ Argument = '--dialogs-smoke'; Prefix = 'NERA_AVALONIA_DIALOGS_SUCCESS '; Minimum = 70 },\n            @{ Argument = '--compatibility-smoke'; Prefix = 'NERA_AVALONIA_COMPATIBILITY_SUCCESS '; Minimum = 30 }")
replace(path,"if ($LASTEXITCODE -ne 0) { throw 'Dialog image provenance failed.' }", "if ($LASTEXITCODE -ne 0) { throw 'Dialog image provenance failed.' }\n          python scripts/verify-avalonia-compatibility.py artifacts/avalonia/compatibility $env:NERA_SOURCE_SHA\n          if ($LASTEXITCODE -ne 0) { throw 'Compatibility image provenance failed.' }")
path='.github/workflows/check-out.yml'
replace(path,"'ribbon-visual-smoke', 'dialogs-smoke'))", "'ribbon-visual-smoke', 'dialogs-smoke', 'compatibility-smoke'))")
replace(path,"if ($LASTEXITCODE -ne 0) { throw 'Published dialog evidence validation failed.' }", "if ($LASTEXITCODE -ne 0) { throw 'Published dialog evidence validation failed.' }\n          python scripts/verify-avalonia-compatibility.py artifacts/published-images/compatibility $env:NERA_SOURCE_SHA\n          if ($LASTEXITCODE -ne 0) { throw 'Published compatibility evidence failed.' }")
replace(path,'      - name: Assemble exact-source downloads; publish prerelease only from validated main', '''      - uses: actions/download-artifact@v4
        with:
          pattern: primary-ribbon-*-${{ github.sha }}
          path: incoming/additional-images/Avalonia-build
      - uses: actions/download-artifact@v4
        with:
          name: ribbon-visual-matrix
          path: incoming/additional-images/Legacy-SDK
      - uses: actions/download-artifact@v4
        with:
          name: maui-windows-ribbon-ux007
          path: incoming/additional-images/MAUI-Ribbon
      - uses: actions/download-artifact@v4
        with:
          name: maui-windows-table-filter-ux006
          path: incoming/additional-images/MAUI-Filter
      - uses: actions/download-artifact@v4
        with:
          name: release-009-win11-x64-demo-${{ github.sha }}
          path: incoming/additional-images/Legacy-demo
      - name: Assemble exact-source downloads; publish prerelease only from validated main''')

path='scripts/check-out/pack.py'
replace(path,"'dialogs-smoke': ('NERA_AVALONIA_DIALOGS_SUCCESS ', 70),", "'dialogs-smoke': ('NERA_AVALONIA_DIALOGS_SUCCESS ', 70),\n        'compatibility-smoke': ('NERA_AVALONIA_COMPATIBILITY_SUCCESS ', 30),")
replace(path,"    shutil.rmtree(output / 'staging')", "    for name in ('compatibility-open.png','compatibility-edited.png','compatibility-rejected.png'):\n        shutil.copy2(images / 'compatibility' / name, output / name)\n    shutil.rmtree(output / 'staging')")
path='scripts/check-out/assemble.py'
replace(path,'import zipfile\n','import zipfile\nimport gallery\n')
replace(path,'    platforms = {}', "    platforms = {}\n    images = gallery.Gallery(evidence / 'Images', sha, f'https://github.com/{repo}/actions/runs/{run_id}')")
replace(path,"        if rid == 'win-x64':", "        images.published(package, rid)\n        if rid == 'win-x64':")
replace(path,"'zoom-dialog.png'):", "'zoom-dialog.png', 'compatibility-open.png', 'compatibility-edited.png', 'compatibility-rejected.png'):")
replace(path,'    packages = list(', "    for category in ('Avalonia-build','Legacy-SDK','MAUI-Ribbon','MAUI-Filter','Legacy-demo'):\n        images.directory(source / 'additional-images' / category, category)\n    image_report = images.finish()\n    packages = list(")
replace(path,'        sdkPackages=sorted(p.name for p in packages),', '        sdkPackages=sorted(p.name for p in packages),\n        screenshots=dict(count=image_report["imageCount"], groups=image_report["groups"], index="Check out/Images/index.html"),')
replace(path,"Ảnh và manifest của đúng source; xem workflowUrl", "Mở Check out/Images/index.html để xem TẤT CẢ ảnh của từng nền tảng/host. Ảnh và manifest của đúng source; xem workflowUrl")
path='scripts/check-out/test_delivery.py'
replace(path,"'dialogs-smoke':('NERA_AVALONIA_DIALOGS_SUCCESS ',80)}", "'dialogs-smoke':('NERA_AVALONIA_DIALOGS_SUCCESS ',80),\n            'compatibility-smoke':('NERA_AVALONIA_COMPATIBILITY_SUCCESS ',38)}")
replace(path,'        self.prefixes={', "        compatibility=self.images/'compatibility';compatibility.mkdir()\n        for name in ('compatibility-open.png','compatibility-edited.png','compatibility-rejected.png'):\n            (compatibility/name).write_bytes(b'synthetic delivery fixture, not a native image')\n        self.prefixes={")
replace(path,'    def testFrameworkDependentAppCannotBeCalledSelfContained(self):', '''    def testMissingCompatibilitySmokeCannotProduceDownload(self):
        (self.images/'compatibility-smoke.log').unlink()
        with self.assertRaises(FileNotFoundError):self.pack()

    def testWrongCompatibilityHeadCannotProduceDownload(self):
        prefix,count=self.prefixes['compatibility-smoke']
        (self.images/'compatibility-smoke.log').write_text(prefix+json.dumps(dict(sha=OLD,nativeWindow=True,assertions=count)))
        with self.assertRaisesRegex(ValueError,'evidence'):self.pack()

    def testFrameworkDependentAppCannotBeCalledSelfContained(self):''')

changed.add('eng/xlsx-compat-004/complete.py')
manifest.write_text(json.dumps(sorted(changed)))
Path(__file__).unlink()
print('COMPAT004_COMPLETED_WIRING: contract assertions, native import/save and complete Check out capture gallery')
