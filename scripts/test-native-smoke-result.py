"""In-memory regressions for shared native result transport parsing."""

import importlib.util
import json
from pathlib import Path
import unittest

SPEC = importlib.util.spec_from_file_location("native_result", Path(__file__).with_name("verify-native-smoke-result.py"))
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class NativeResultTests(unittest.TestCase):
    def marker(self, status="success", frames=3, **extra):
        return json.dumps({"status": status, "frameCount": frames, **extra}, separators=(",", ":"))

    def testRawAndroidResult(self):
        value = self.marker()
        self.assertEqual(json.loads(value), MODULE.parse_result("--------- beginning of main\n" + value, ""))

    def testIosConsoleAndUnifiedDuplicate(self):
        value = self.marker(nonce="same-run")
        self.assertEqual(json.loads(value), MODULE.parse_result("TEST:" + value + "\n2026 app TEST:" + value, "TEST:"))

    def testMissingAndWrongPrefixStayPending(self):
        self.assertIsNone(MODULE.parse_result("other:" + self.marker(), "TEST:"))
        self.assertIsNone(MODULE.parse_result("runtime has not finished", ""))

    def testFailureWinsInEitherOrder(self):
        for statuses in (("success", "failure"), ("failure", "success")):
            with self.subTest(statuses=statuses), self.assertRaises(ValueError):
                MODULE.parse_result("\n".join("TEST:" + self.marker(status) for status in statuses), "TEST:")

    def testConflictingSuccessNonceIsRejected(self):
        with self.assertRaises(ValueError):
            MODULE.parse_result(self.marker(nonce="old") + "\n" + self.marker(nonce="new"), "")

    def testMalformedAndTrailingPayloadAreRejected(self):
        for payload in ('{"status":"success"', self.marker() + " another result", '[]'):
            with self.subTest(payload=payload), self.assertRaises(ValueError):
                MODULE.parse_result("TEST:" + payload, "TEST:")

    def testFramesMustBeAnActualPositiveInteger(self):
        for frames in (None, False, "3", 0, 1, 3.5):
            with self.subTest(frames=frames), self.assertRaises(ValueError):
                MODULE.parse_result(self.marker(frames=frames), "")

    def testLegacyAnalyticsMayCompleteAfterTwoFrames(self):
        value = self.marker(frames=2)
        self.assertEqual(json.loads(value), MODULE.parse_result(value, ""))
        self.assertEqual(json.loads(value), MODULE.parse_result("TEST:" + value, "TEST:"))

    def testStricterConsumerFramePolicyIsNotRelaxed(self):
        with self.assertRaisesRegex(MODULE.NativeResultError, "insufficient-frame-evidence"):
            MODULE.parse_result(self.marker(frames=2), "", minimum_frames=3)
        self.assertEqual(3, MODULE.parse_result(self.marker(), "", minimum_frames=3)["frameCount"])
        with self.assertRaisesRegex(MODULE.NativeResultError, "invalid-frame-policy"):
            MODULE.parse_result(self.marker(), "", minimum_frames=1)

    def testNonfinitePayloadIsRejectedWithoutPrintingItsContent(self):
        for value in ("NaN", "Infinity", "-Infinity"):
            with self.subTest(value=value), self.assertRaisesRegex(MODULE.NativeResultError, "^nonfinite-number$"):
                MODULE.parse_result('{"status":"success","frameCount":3,"privateField":' + value + '}', "")

    def testUnknownStatusIsRejected(self):
        with self.assertRaises(ValueError):
            MODULE.parse_result(self.marker(status="pending"), "")

    def testDuplicateStatusCannotHideFailure(self):
        with self.assertRaises(ValueError):
            MODULE.parse_result('{"status":"failure","status":"success","frameCount":3}', "")


if __name__ == "__main__":
    unittest.main()
