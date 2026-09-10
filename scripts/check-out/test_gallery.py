"""Synthetic gallery provenance/path tests. No external files or requests."""
import base64
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

spec=importlib.util.spec_from_file_location('gallery_under_test',Path(__file__).with_name('gallery.py'))
gallery=importlib.util.module_from_spec(spec)
spec.loader.exec_module(gallery)
SHA='a'*40
PNG=base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9L8AAAAASUVORK5CYII=')


class GalleryTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory()
        self.root=Path(self.temp.name)
        self.gallery=gallery.Gallery(self.root/'Check out/Images',SHA,'https://github.com/HoangHung997/NeraSpreadSheet/actions/runs/1')
    def tearDown(self):self.temp.cleanup()
    def package(self, source=SHA, expected=None, name='Check out/Images/dialogs/test.png'):
        path=self.root/'app.zip'
        report=dict(sourceSha=source,rid='win-x64',files={name[len('Check out/'):]:expected or hashlib.sha256(PNG).hexdigest()})
        with zipfile.ZipFile(path,'w') as archive:
            archive.writestr('Check out/Reports/package.json',json.dumps(report))
            archive.writestr(name,PNG)
        return path
    def testAllPublishedImagesAreRetainedAndIndexed(self):
        self.gallery.published(self.package(),'win-x64')
        self.gallery.add('CI/Other/second.png',PNG,'ci/Other')
        report=self.gallery.finish()
        self.assertEqual(2,report['imageCount'])
        self.assertEqual(SHA,report['sourceSha'])
        self.assertTrue(report['captures'][1]['packageHashChecked'])
        self.assertFalse(report['allImagesVisuallyReviewed'])
        self.assertIn('Published/win-x64/dialogs/test.png',(self.gallery.root/'index.html').read_text())
    def testWrongSourceCannotEnterGallery(self):
        with self.assertRaisesRegex(ValueError,'source mismatch'):self.gallery.published(self.package(source='b'*40),'win-x64')
    def testChangedImageCannotEnterGallery(self):
        with self.assertRaisesRegex(ValueError,'hash mismatch'):self.gallery.published(self.package(expected='0'*64),'win-x64')
    def testArchiveTraversalIsRejected(self):
        with self.assertRaisesRegex(ValueError,'Unsafe'):self.gallery.published(self.package(name='Check out/Images/../../outside.png'),'win-x64')
        self.assertFalse((self.root/'outside.png').exists())
    def testCaseInsensitiveDuplicateIsRejected(self):
        self.gallery.add('Folder/Test.png',PNG,'test')
        with self.assertRaisesRegex(ValueError,'Duplicate'):self.gallery.add('folder/test.png',PNG,'test')
    def testMissingCategoryIsNotSilentlyDropped(self):
        with self.assertRaisesRegex(ValueError,'Missing'):self.gallery.directory(self.root/'absent','legacy')
    def testNestedDownloadZipImagesAreIncluded(self):
        directory=self.root/'legacy';directory.mkdir()
        with zipfile.ZipFile(directory/'demo.zip','w') as archive:archive.writestr('captures/screen.png',PNG)
        self.gallery.directory(directory,'Legacy-demo')
        report=self.gallery.finish()
        self.assertEqual(1,report['imageCount'])
        self.assertIn('demo.zip.contents/captures/screen.png',report['captures'][0]['path'])
    def testUnrecognizedContentIsNotAnImage(self):
        with self.assertRaisesRegex(ValueError,'Invalid'):self.gallery.add('bad.png',b'not a screenshot','test')
    def testImageBudgetIsEnforcedBeforeWriting(self):
        self.gallery.total_bytes=gallery.MAX_TOTAL_BYTES
        with self.assertRaisesRegex(ValueError,'budget'):self.gallery.add('overflow.png',PNG,'test')
        self.assertFalse((self.gallery.root/'overflow.png').exists())
    def testImageNamesAreEscapedInHtml(self):
        self.gallery.add('test/<tag>.png',PNG,'test')
        self.gallery.finish()
        text=(self.gallery.root/'index.html').read_text()
        self.assertIn('&lt;tag&gt;',text)
        self.assertNotIn('src="test/<tag>',text)


if __name__=='__main__':unittest.main()
