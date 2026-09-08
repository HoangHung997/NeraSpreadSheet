"""Exercise the actual bounded Mac diagnostic block without launching an app."""

import contextlib
import datetime
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch


SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "run-maui-maccatalyst-smoke.sh"
BLOCK = SCRIPT.read_text(encoding="utf-8").split("native_stderr_diagnostics() {", 1)[1]
BLOCK = BLOCK.split("<<'PY'\n", 1)[1].split("\nPY\n}", 1)[0]
MODULE = {"__name__": "native_stderr_fixture"}
exec(compile(BLOCK, "native-stderr-diagnostic", "exec"), MODULE)
CLASSIFY = MODULE["classify_stderr"]
RUN = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
PID = "12345"
HEADER = "NERA_NATIVE_STDERR_V1:" + PID + ":" + RUN
CATEGORIES = (
    "objcDuplicateClassText", "objcClassMetadataText", "objcUncaughtExceptionText",
    "managedUnhandledText", "aotJitRestrictionText", "runtimeAssertionText", "dynamicLoaderText",
    "sigsegvText", "sigabrtText", "sigbusText", "typeLoadExceptionText", "invalidProgramExceptionText",
    "missingMethodExceptionText", "typeInitializationExceptionText",
)


@contextlib.contextmanager
def private_fixture():
    base = Path(tempfile.gettempdir()).resolve()
    workspace = tempfile.TemporaryDirectory(prefix="nera-stderr-fixture-")
    directory = Path(workspace.name).resolve()
    try:
        yield directory
    finally:
        if (Path(workspace.name).resolve() != directory or directory.parent != base
                or not directory.name.startswith("nera-stderr-fixture-")):
            raise AssertionError("Unsafe classifier fixture cleanup path")
        workspace.cleanup()


class ClassifierTests(unittest.TestCase):
    def classify(self, body):
        return CLASSIFY((HEADER + "\n" + body).encode("utf-8"))

    def testEveryApprovedCategoryShouldMatchItsLiteral(self):
        cases = {
            "objcDuplicateClassText": ["Class is implemented in both X and Y. One of the two will be used."],
            "objcClassMetadataText": ["has corrupt data pointer", "future class has superclass",
                                      "superclass is not a class object"],
            "objcUncaughtExceptionText": ["terminating app due to uncaught exception",
                                          "terminating due to uncaught exception"],
            "managedUnhandledText": ["unhandled managed exception", "unhandled exception:"],
            "aotJitRestrictionText": ["attempting to jit compile method in aot-only mode", "failed to load aot module"],
            "runtimeAssertionText": ["assertion at line not met", "assertion failed:"],
            "dynamicLoaderText": ["library not loaded:", "symbol not found:", "dyld missing symbol"],
            "sigsegvText": ["SIGSEGV"], "sigabrtText": ["SIGABRT"], "sigbusText": ["SIGBUS"],
            "typeLoadExceptionText": ["System.TypeLoadException"],
            "invalidProgramExceptionText": ["System.InvalidProgramException"],
            "missingMethodExceptionText": ["System.MissingMethodException"],
            "typeInitializationExceptionText": ["System.TypeInitializationException"],
        }
        for category, samples in cases.items():
            for sample in samples:
                with self.subTest(category=category, sample=sample):
                    result = self.classify(sample.swapcase())
                    self.assertEqual(1, result[category])
                    self.assertEqual(1, sum(result[key] for key in CATEGORIES))
                    self.assertEqual(0, result["unclassifiedLines"])

    def testConjunctionsMustOccurOnTheSameLine(self):
        for text in ("is implemented in both\none of the two", "future class\nsuperclass",
                     "attempting to jit compile method\naot-only", "assertion at\nnot met", "dyld\nmissing symbol"):
            with self.subTest(text=text):
                self.assertEqual(0, sum(self.classify(text)[key] for key in CATEGORIES))

    def testGenericWordsAndNearTokensShouldRemainUnclassified(self):
        for text in ("error failed objc JIT", "SIGSEGV_EXTRA", "prefixSIGABRT", "SIGBUS2",
                     "System.TypeLoadExceptionExtra", "System.InvalidProgramException.Nested",
                     "MySystem.MissingMethodException", "System.TypeInitializationException_suffix"):
            result = self.classify(text)
            self.assertEqual(0, sum(result[key] for key in CATEGORIES))
            self.assertEqual(1, result["unclassifiedLines"])

    def testOutputShouldContainOnlyFixedSchemaAndCounts(self):
        canaries = ["PrivateClassCanary", "/private/user-canary/file", "C:\\secret-canary", "0xabcdef1234",
                    "ffffffff-1234-abcd-5678-aaaaaaaaaaaa", "TOKEN_CANARY=secret", "FreeFormExceptionCanary"]
        result = self.classify("unhandled exception: " + " ".join(canaries))
        encoded = json.dumps(result, separators=(",", ":"))
        self.assertEqual(set(CATEGORIES) | {"schema", "unclassifiedLines", "inputClipped"}, set(result))
        self.assertEqual("nativeStderrClassificationV1", result["schema"])
        self.assertIs(type(result["inputClipped"]), bool)
        for key in CATEGORIES + ("unclassifiedLines",):
            self.assertIs(type(result[key]), int)
            self.assertTrue(0 <= result[key] <= 64)
        for canary in canaries:
            self.assertNotIn(canary, encoded)
        self.assertLessEqual(len(encoded.encode()), 2048)

    def testHeaderShouldNeverBeClassified(self):
        result = CLASSIFY(b"SIGSEGV System.TypeLoadException\nordinary line")
        self.assertEqual(0, sum(result[key] for key in CATEGORIES))
        self.assertEqual(1, result["unclassifiedLines"])

    def testMultipleIndicatorsMayShareOneLineWithoutDuplicatingOneCategory(self):
        result = self.classify("SIGBUS SIGBUS SIGSEGV System.TypeLoadException unhandled exception:")
        for key in ("sigbusText", "sigsegvText", "typeLoadExceptionText", "managedUnhandledText"):
            self.assertEqual(1, result[key])
        self.assertEqual(0, result["unclassifiedLines"])

    def testCountsAndUnclassifiedLinesShouldSaturate(self):
        for body, key in (("SIGSEGV\n" * 128, "sigsegvText"), ("ordinary\n" * 128, "unclassifiedLines")):
            result = self.classify(body)
            self.assertEqual(64, result[key])
            self.assertTrue(result["inputClipped"])

    def testByteLineAndCharacterLimitsShouldExcludeTrailingIndicators(self):
        cases = ["ordinary\n" * 128 + "SIGSEGV", "x" * 4096 + "SIGSEGV",
                 "x" * (64 * 1024) + "\nSIGSEGV"]
        for body in cases:
            result = self.classify(body)
            self.assertEqual(0, result["sigsegvText"])
            self.assertTrue(result["inputClipped"])
        for body in ("ordinary\n" * 128, "x" * 4096):
            self.assertTrue(self.classify(body)["inputClipped"])
        data = (HEADER + "\n").encode() + b"x" * (64 * 1024 - len(HEADER) - 1)
        self.assertEqual(64 * 1024, len(data))
        self.assertTrue(CLASSIFY(data)["inputClipped"])

    def testInvalidUtf8AndNulShouldNotBecomeFreeFormOutput(self):
        result = CLASSIFY((HEADER + "\n").encode() + b"\xff\x00PrivateCanary")
        self.assertEqual(1, result["unclassifiedLines"])
        self.assertNotIn("PrivateCanary", json.dumps(result))

    def invoke(self, directory, action="read", uid=None, launched=None):
        output = io.StringIO()
        launched = launched or (datetime.datetime.now() - datetime.timedelta(seconds=5)).strftime("%Y-%m-%d %H:%M:%S")
        arguments = ["diagnostic", action, str(directory), PID, RUN, launched]
        actual_uid = directory.stat().st_uid if uid is None else uid
        with patch.object(sys, "argv", arguments), patch.object(os, "getuid", return_value=actual_uid, create=True), contextlib.redirect_stdout(output):
            MODULE["main"]()
        return [json.loads(line) for line in output.getvalue().splitlines() if line.startswith("{")]

    def testMatchingCaptureShouldEmitOneClassificationFromActualDiagnostic(self):
        with private_fixture() as directory:
            capture = directory / ("nera-native-stderr-" + RUN + "-" + PID + ".log")
            capture.write_bytes((HEADER + "\nSIGABRT").encode())
            records = self.invoke(directory)
            self.assertEqual(1, len(records))
            self.assertEqual(1, records[0]["sigabrtText"])

    def testWrongHeaderProcessRunAndStaleCaptureShouldNotEmit(self):
        for kind in ("process", "run", "header", "stale", "foreign", "missing"):
            with self.subTest(kind=kind), private_fixture() as directory:
                capture = directory / ("nera-native-stderr-" + RUN + "-" + PID + ".log")
                header = HEADER.replace(PID, "54321") if kind == "process" else HEADER
                if kind == "run": header = header.replace(RUN, "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
                if kind == "header": header = "unrecognized"
                if kind != "missing": capture.write_text(header + "\nSIGSEGV", encoding="utf-8")
                if kind == "stale": os.utime(capture, (1, 1))
                uid = directory.stat().st_uid + 1 if kind == "foreign" else None
                self.assertEqual([], self.invoke(directory, uid=uid))

    def testSymlinkCaptureShouldNotEmit(self):
        with private_fixture() as directory:
            actual = directory / "actual.log"
            actual.write_text(HEADER + "\nSIGBUS", encoding="utf-8")
            capture = directory / ("nera-native-stderr-" + RUN + "-" + PID + ".log")
            capture.symlink_to(actual.name)
            self.assertEqual([], self.invoke(directory))

    def testReadBoundAndCleanupShouldRetainExistingBehavior(self):
        with private_fixture() as directory:
            capture = directory / ("nera-native-stderr-" + RUN + "-" + PID + ".log")
            capture.write_bytes((HEADER + "\n").encode() + b"x" * (64 * 1024) + b"\nSIGSEGV")
            records = self.invoke(directory)
            self.assertEqual(1, len(records))
            self.assertTrue(records[0]["inputClipped"])
            self.assertEqual(0, records[0]["sigsegvText"])
            self.assertTrue(capture.exists())
            self.assertEqual([], self.invoke(directory, action="cleanup"))
            self.assertFalse(capture.exists())


if __name__ == "__main__":
    unittest.main()
