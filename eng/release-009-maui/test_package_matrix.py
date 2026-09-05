"""Small in-memory negative fixtures; no SDK, workload, build, or native process."""
import copy
import io
import unittest
import zipfile
import xml.etree.ElementTree as ET

import package_matrix as matrix

SHA = "a" * 40
VERSION = "0.1.0-ci.123.1.g" + SHA[:12]
TFMS = {"windows": "net10.0-windows10.0.19041.0", "android": "net10.0-android36.0",
        "ios": "net10.0-ios18.7", "maccatalyst": "net10.0-maccatalyst18.7"}


def archive(platform="windows", version=VERSION, sha=SHA, extra=None, dependency_version=VERSION):
    tfm = TFMS[platform]
    data = io.BytesIO()
    with zipfile.ZipFile(data, "w") as package:
        package.writestr("NeraSpreadSheet.Maui.nuspec", f"""<package><metadata>
          <id>NeraSpreadSheet.Maui</id><version>{version}</version><authors>Nera</authors>
          <description>Fixture</description><repository type="git" commit="{sha}"/>
          <dependencies><group targetFramework="{tfm}">
          <dependency id="NeraSpreadSheet.Core" version="{dependency_version}"/>
          </group></dependencies></metadata></package>""")
        package.writestr(f"lib/{tfm}/NeraSpreadSheet.Maui.dll", b"fixture-" + platform.encode())
        package.writestr("README.md", b"same synthetic README")
        for name, value in (extra or {}).items():
            package.writestr(name, value)
    return data.getvalue()


class PackageMatrixTests(unittest.TestCase):
    def testFourTargetsShouldPreserveEveryAssemblyAndDependencyGroup(self):
        packages = {p: matrix.inspect_package(archive(p), VERSION, SHA) for p in TFMS}
        metadata, payload = matrix.merge_maui(packages)
        self.assertEqual(4, len(matrix.child(metadata, "dependencies")))
        self.assertEqual(5, len(payload))
        for p, tfm in TFMS.items():
            self.assertEqual(b"fixture-" + p.encode(), payload[f"lib/{tfm}/NeraSpreadSheet.Maui.dll"])

    def testMissingPlatformShouldBeRejected(self):
        with self.assertRaisesRegex(ValueError, "Missing"):
            matrix.merge_maui({p: matrix.inspect_package(archive(p), VERSION, SHA) for p in ("windows", "android", "ios")})

    def testWrongPlatformPayloadShouldBeRejected(self):
        packages = {p: matrix.inspect_package(archive(p), VERSION, SHA) for p in TFMS}
        packages["ios"] = packages["android"]
        with self.assertRaisesRegex(ValueError, "Wrong or duplicate"):
            matrix.merge_maui(packages)

    def testForeignPackageVersionAndSourceShouldBeRejected(self):
        for data in (archive(version="0.1.0"), archive(sha="b" * 40), archive(dependency_version="0.1.0")):
            with self.assertRaisesRegex(ValueError, "version|Version"):
                matrix.inspect_package(data, VERSION, SHA)

    def testUnsafePathsShouldBeRejected(self):
        for path in ("../README.md", "lib/../../bad", "/root/file", "C:/secret", "lib\\x.dll", "lib//x.dll"):
            with self.assertRaises(ValueError):
                matrix.inspect_package(archive(extra={path: b"bad"}), VERSION, SHA)

    def testDuplicateCaseAndSourcePayloadShouldBeRejected(self):
        for path in ("readme.md", "src/source.cs", "private.pdb"):
            with self.assertRaises(ValueError):
                matrix.inspect_package(archive(extra={path: b"bad"}), VERSION, SHA)

    def testConflictingSharedFileShouldBeRejected(self):
        packages = {p: matrix.inspect_package(archive(p), VERSION, SHA) for p in TFMS}
        packages["ios"]["payload"]["README.md"] = b"foreign"
        with self.assertRaisesRegex(ValueError, "Conflicting shared package file"):
            matrix.merge_maui(packages)

    def testConflictingMetadataShouldBeRejected(self):
        packages = {p: matrix.inspect_package(archive(p), VERSION, SHA) for p in TFMS}
        matrix.child(packages["ios"]["metadata"], "authors").text = "different"
        with self.assertRaisesRegex(ValueError, "metadata"):
            matrix.merge_maui(packages)

    def testForeignLibraryFrameworkShouldBeRejected(self):
        with self.assertRaisesRegex(ValueError, "groups differ"):
            matrix.inspect_package(archive(extra={"lib/net10.0-ios18.7/NeraSpreadSheet.Maui.dll": b"foreign"}), VERSION, SHA)

    def testFrameworkMetadataShouldSurviveAssembly(self):
        packages = {p: matrix.inspect_package(archive(p), VERSION, SHA) for p in TFMS}
        for platform, package in packages.items():
            refs = ET.SubElement(package["metadata"], "frameworkReferences")
            group = ET.SubElement(refs, "group", {"targetFramework": TFMS[platform]})
            ET.SubElement(group, "frameworkReference", {"name": "Synthetic.Framework"})
        metadata, _ = matrix.merge_maui(packages)
        self.assertEqual(4, len(matrix.child(metadata, "frameworkReferences")))

    def testMixedProducerCohortShouldBeRejected(self):
        manifest = {"schemaVersion": 1, "platform": "windows", "sourceSha": SHA,
                    "version": VERSION, "sdkVersion": "10.0.302", "expectedNeutralIds": sorted(matrix.NEUTRAL_IDS)}
        matrix.verify_producer(manifest, SHA, VERSION, "10.0.302")
        for key, value in (("sourceSha", "b" * 40), ("version", "0.1.0"), ("sdkVersion", "10.0.400"),
                           ("expectedNeutralIds", ["NeraSpreadSheet.Core"])):
            changed = copy.deepcopy(manifest)
            changed[key] = value
            with self.assertRaises(ValueError):
                matrix.verify_producer(changed, SHA, VERSION, "10.0.302")

    def testArchiveHashAndAssemblyProvenanceShouldBeRequired(self):
        data = archive()
        package = matrix.inspect_package(data, VERSION, SHA)
        record = {"sha256": matrix.digest(data), "assemblies": [
            {"file": name, "sha256": matrix.digest(blob), "informationalVersion": VERSION + "+" + SHA}
            for name, blob in package["payload"].items() if name.endswith(".dll")]}
        matrix.verify_shard_package(data, record, VERSION, SHA)
        changed = copy.deepcopy(record)
        changed["sha256"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "hash"):
            matrix.verify_shard_package(data, changed, VERSION, SHA)
        for change in ([], [{**record["assemblies"][0], "sha256": "0" * 64}],
                       [{**record["assemblies"][0], "informationalVersion": "0.1.0"}]):
            changed = {**record, "assemblies": change}
            with self.assertRaises(ValueError):
                matrix.verify_shard_package(data, changed, VERSION, SHA)

    def testConsumerAssetsShouldRejectSourceCacheAndFrameworkBypasses(self):
        manifest = {"version": VERSION, "packages": [{"id": name} for name in matrix.NEUTRAL_IDS | {matrix.MAUI_ID}],
                    "targetFrameworks": list(TFMS.values())}
        libraries = {record["id"] + "/" + VERSION: {"type": "package"} for record in manifest["packages"]}
        maui = {"compile": {f"lib/{TFMS['windows']}/NeraSpreadSheet.Maui.dll": {}},
                "runtime": {f"lib/{TFMS['windows']}/NeraSpreadSheet.Maui.dll": {}}}
        assets = {"libraries": libraries, "packageFolders": {"synthetic-cache": {}},
                  "targets": {TFMS["windows"] + "/win-x64": {matrix.MAUI_ID + "/" + VERSION: maui}}}
        matrix.verify_assets(assets, manifest, "synthetic-cache", "windows", "win-x64")
        variants = []
        source = copy.deepcopy(assets)
        source["libraries"][matrix.MAUI_ID + "/" + VERSION]["type"] = "project"
        variants.append(source)
        cache = copy.deepcopy(assets)
        cache["packageFolders"]["foreign-cache"] = {}
        variants.append(cache)
        framework = copy.deepcopy(assets)
        framework["targets"][TFMS["windows"] + "/win-x64"][matrix.MAUI_ID + "/" + VERSION]["compile"] = {
            f"lib/{TFMS['android']}/NeraSpreadSheet.Maui.dll": {}}
        variants.append(framework)
        for variant in variants:
            with self.assertRaises(ValueError):
                matrix.verify_assets(variant, manifest, "synthetic-cache", "windows", "win-x64")

    def testRuntimeShouldRejectStaleNonceAndIncompletePostconditions(self):
        build = {"sourceSha": SHA, "version": VERSION, "feedHash": "1" * 64, "nonce": "2" * 32, "platform": "windows"}
        marker = {"schema": "release009-maui-consumer-v1", "status": "success", "sourceSha": SHA,
                  "packageVersion": VERSION, "feedHash": "1" * 64, "nonce": "2" * 32, "target": "windows", "frameCount": 3,
                  "details": {"publicApiOnly": True, "controllerEditUndo": True, "actualResize": True, "filterValues": 20,
                              "gpu": {"FramesFailed": 0, "FramesCompleted": 3, "HasActiveFrame": False},
                              "assemblies": [{"name": "NeraSpreadSheet." + name, "informationalVersion": VERSION + "+" + SHA}
                                             for name in ("Maui", "Core", "Editing", "Formulas", "Rendering.Skia", "Ribbon.Core")]}}
        matrix.verify_runtime(marker, build)
        for key, value in (("nonce", "3" * 32), ("sourceSha", "b" * 40), ("target", "ios"),
                           ("status", "failure"), ("frameCount", 0), ("details", {})):
            changed = copy.deepcopy(marker)
            changed[key] = value
            with self.assertRaises(ValueError):
                matrix.verify_runtime(changed, build)


if __name__ == "__main__":
    unittest.main()
