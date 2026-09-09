"""Synthetic counterexamples for packaging and branch-cleanup safety; no network calls."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile


def load(name, filename):
    spec=importlib.util.spec_from_file_location(name,Path(__file__).with_name(filename))
    module=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


packing=load('packing','pack.py')
assembling=load('assembling','assemble.py')
archiving=load('archiving','archive-branches.py')
SHA='a'*40
OLD='b'*40


class BranchSafetyTests(unittest.TestCase):
    def setUp(self):
        self.manifest=dict(defaultBranch='main',temporaryBranch='feature/consolidate-checkout-001',
            branches=[dict(name='main',sha=OLD,archiveTag=archiving.PREFIX+'001'),
                      dict(name='feature/old',sha=OLD,archiveTag=archiving.PREFIX+'002')],
            pullRequests=[dict(number=5,sha=OLD)])
        self.current={'refs/heads/main':SHA,'refs/heads/feature/old':OLD,
                      'refs/heads/feature/consolidate-checkout-001':SHA}

    def testArchivesOldMainButNeverDeletesCurrentMain(self):
        tags,deletes=archiving.build_plan(self.manifest,self.current,SHA)
        self.assertEqual(OLD,tags['refs/tags/'+archiving.PREFIX+'001'])
        self.assertNotIn('refs/heads/main',deletes)
        self.assertEqual(2,len(deletes))
        self.assertEqual(SHA,tags['refs/tags/'+archiving.PREFIX+'integration-candidate'])

    def testRejectsNewBranchInsteadOfDeletingOtherWritersWork(self):
        self.current['refs/heads/feature/new']=OLD
        with self.assertRaisesRegex(ValueError,'Unreviewed'):archiving.build_plan(self.manifest,self.current,SHA)

    def testRejectsMovedSourceBranch(self):
        self.current['refs/heads/feature/old']=SHA
        with self.assertRaisesRegex(ValueError,'advanced'):archiving.build_plan(self.manifest,self.current,SHA)

    def testRejectsMovedMain(self):
        self.current['refs/heads/main']=OLD
        with self.assertRaisesRegex(ValueError,'Main moved'):archiving.build_plan(self.manifest,self.current,SHA)

    def testRejectsMovedIntegrationBranch(self):
        self.current['refs/heads/feature/consolidate-checkout-001']=OLD
        with self.assertRaisesRegex(ValueError,'Integration branch advanced'):archiving.build_plan(self.manifest,self.current,SHA)

    def testMissingBranchStillGetsArchiveMapping(self):
        del self.current['refs/heads/feature/old']
        archives,deletes=archiving.build_plan(self.manifest,self.current,SHA)
        self.assertIn('refs/tags/'+archiving.PREFIX+'002',archives)
        self.assertNotIn('refs/heads/feature/old',deletes)

    def testRejectsDuplicateArchive(self):
        self.manifest['branches'][1]['archiveTag']=self.manifest['branches'][0]['archiveTag']
        with self.assertRaisesRegex(ValueError,'Duplicate archive'):archiving.build_plan(self.manifest,self.current,SHA)

    def testRejectsForeignPullRequestUsingSourceAsBase(self):
        pr=dict(number=25,head=dict(ref='other',sha=OLD),base=dict(ref='feature/old'))
        with self.assertRaisesRegex(ValueError,'Unreviewed open PR'):
            archiving.safe_pull_requests(self.manifest,[pr],{'refs/heads/feature/old':OLD},SHA)

    def testRejectsAdvancedKnownPullRequest(self):
        pr=dict(number=5,head=dict(ref='feature/old',sha=SHA),base=dict(ref='main'))
        with self.assertRaisesRegex(ValueError,'Source PR moved'):
            archiving.safe_pull_requests(self.manifest,[pr],{'refs/heads/feature/old':OLD},SHA)

    def testRecognizesOnlyExactIntegratedPullRequest(self):
        pr=dict(number=5,head=dict(ref='feature/old',sha=OLD),base=dict(ref='main'))
        self.assertEqual([pr],archiving.safe_pull_requests(self.manifest,[pr],{'refs/heads/feature/old':OLD},SHA))


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory()
        self.root=Path(self.temp.name)
        self.app=self.root/'app';self.app.mkdir()
        (self.app/'NeraSpreadSheet.Avalonia.Sample').write_bytes(b'synthetic test executable')
        (self.app/'libhostfxr.so').write_bytes(b'synthetic runtime marker')
        self.images=self.root/'images';self.images.mkdir()
        self.visual=self.images/'ribbon-visual';self.visual.mkdir()
        (self.visual/'manifest.json').write_text('{}')
        for name in ('Light-1024-home.png','Dark-1024-home.png','full-window.png','Light-customization.png'):
            (self.visual/name).write_bytes(b'synthetic image, not a native screenshot')
        self.prefixes={ 'smoke':('NERA_AVALONIA_SMOKE_SUCCESS ',12),
            'full-ui-smoke':('NERA_AVALONIA_FULL_UI_SUCCESS ',23),
            'formula-ux-smoke':('NERA_AVALONIA_FORMULA_UX_SUCCESS ',23),
            'ribbon-visual-smoke':('NERA_AVALONIA_RIBBON_VISUAL_SUCCESS ',198)}
        for name,(prefix,count) in self.prefixes.items():
            (self.images/(name+'.log')).write_text(prefix+json.dumps(dict(sha=SHA,nativeWindow=True,assertions=count))+'\n')
        (self.root/'docs').mkdir();(self.root/'docs/third-party-notices.md').write_text('synthetic notice')
        self.output=self.root/'delivery'
    def tearDown(self):self.temp.cleanup()
    def pack(self):packing.pack(self.root,self.app,self.images,self.output,SHA,'linux-x64','https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/1')

    def testPackageContainsRealCheckOutHierarchyAndMatchingDigest(self):
        self.pack()
        meta=json.loads((self.output/'package.json').read_text())
        archive=self.output/meta['filename']
        self.assertEqual(packing.digest(archive),meta['sha256'])
        with zipfile.ZipFile(archive) as zip:
            self.assertIn('Check out/App/NeraSpreadSheet.Avalonia.Sample',zip.namelist())
            report=json.loads(zip.read('Check out/Reports/package.json'))
            self.assertEqual(SHA,report['sourceSha']);self.assertFalse(report['physicalInputTested'])
            self.assertTrue(all(name.startswith('Check out/') for name in zip.namelist()))

    def testWrongHeadCannotProduceDownload(self):
        name='smoke';prefix,count=self.prefixes[name]
        (self.images/(name+'.log')).write_text(prefix+json.dumps(dict(sha=OLD,nativeWindow=True,assertions=count)))
        with self.assertRaisesRegex(ValueError,'evidence'):self.pack()
        self.assertFalse(self.output.exists())

    def testDuplicateSuccessCannotProduceDownload(self):
        p=self.images/'smoke.log';p.write_text(p.read_text()*2)
        with self.assertRaisesRegex(ValueError,'evidence'):self.pack()

    def testMissingSmokeCannotProduceDownload(self):
        (self.images/'formula-ux-smoke.log').unlink()
        with self.assertRaises(FileNotFoundError):self.pack()

    def testFrameworkDependentAppCannotBeCalledSelfContained(self):
        (self.app/'libhostfxr.so').unlink()
        with self.assertRaisesRegex(ValueError,'self-contained'):self.pack()

    def testMissingExecutableFails(self):
        (self.app/'NeraSpreadSheet.Avalonia.Sample').unlink()
        with self.assertRaises(FileNotFoundError):self.pack()

    def testAssemblerRejectsMissingPlatformsBeforePublication(self):
        with self.assertRaisesRegex(ValueError,'Exactly one package cohort'):
            assembling.assemble(self.root,self.output,SHA,'HoangHung997/NeraSpreadSheet','1',False)

    def testAssemblerRejectsAnotherRepository(self):
        with self.assertRaisesRegex(ValueError,'Unexpected repository'):
            assembling.assemble(self.root,self.output,SHA,'somebody/else','1',False)


if __name__=='__main__':unittest.main()
