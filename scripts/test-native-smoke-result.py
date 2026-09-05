"""Bounded synthetic regressions for shared native result transport parsing."""

import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location("native_result", Path(__file__).with_name("verify-native-smoke-result.py"))
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


@contextlib.contextmanager
def private_fixture():
    base = Path(tempfile.gettempdir()).resolve()
    workspace = tempfile.TemporaryDirectory(prefix="nera-native-fixture-")
    directory = Path(workspace.name).resolve()
    try:
        yield directory
    finally:
        if (Path(workspace.name).resolve() != directory or directory.parent != base
                or not directory.name.startswith("nera-native-fixture-")):
            raise AssertionError("Unsafe synthetic fixture cleanup path")
        workspace.cleanup()


class FileResultTests(unittest.TestCase):
    nonce = "a" * 32

    def result(self, **extra):
        return json.dumps({"status": "success", "frameCount": 3, **extra},
                          ensure_ascii=False, separators=(",", ":")).encode("utf-8")

    def envelope(self, data, **extra):
        return {"schema": MODULE.FILE_PROTOCOL, "status": "success", "frameCount": 3,
                "transportNonce": self.nonce, "sha256": hashlib.sha256(data).hexdigest(), **extra}

    def testLargeUnicodeResultIsBoundToTheExactCompactEnvelope(self):
        data = self.result(details="Nội dung kiểm chứng đầy đủ " * 200)
        envelope = self.envelope(data)
        self.assertGreater(len(data), 1024)
        self.assertLess(len(json.dumps(envelope)), 400)
        self.assertEqual(json.loads(data), MODULE.verify_file_result(envelope, data, self.nonce, 3))

    def testFileAndMarkerMustBothExistWithTheKnownEnvelopeSchema(self):
        data = self.result()
        for envelope in (None, {}, json.loads(data), self.envelope(data, schema="other"),
                         self.envelope(data, extra="unknown")):
            with self.assertRaisesRegex(MODULE.NativeResultError, "invalid-file-envelope"):
                MODULE.verify_file_result(envelope, data, self.nonce)
        with self.assertRaises(MODULE.NativeResultError):
            MODULE.verify_file_result(self.envelope(b""), b"", self.nonce)
        # A file cannot change the existing missing-marker/pending decision.
        self.assertIsNone(MODULE.parse_result("unrelated output", "TEST:"))

    def testNonceCannotBorrowAnotherLaunchResult(self):
        data = self.result()
        for nonce in ("b" * 32, "", "A" * 32, None):
            with self.assertRaisesRegex(MODULE.NativeResultError, "nonce-mismatch"):
                MODULE.verify_file_result(self.envelope(data), data, nonce)

    def testChangedBytesTruncationAndOversizeCannotBorrowTheOriginalHash(self):
        data = self.result(details="same-length")
        for other in (data[:-1], data.replace(b"same", b"fake"), data + b" "):
            with self.assertRaisesRegex(MODULE.NativeResultError, "size-or-hash-mismatch"):
                MODULE.verify_file_result(self.envelope(data), other, self.nonce)
        oversized = b"x" * (MODULE.MAX_LOG_BYTES + 1)
        with self.assertRaisesRegex(MODULE.NativeResultError, "size-or-hash-mismatch"):
            MODULE.verify_file_result(self.envelope(oversized), oversized, self.nonce)

    def testFullDocumentUsesStrictJsonEvenWithAMatchingHash(self):
        for data in (b'\xff', b'{"status":', b'[]', self.result() + self.result(),
                     b'{"status":"failure","status":"success","frameCount":3}',
                     b'{"status":"success","frameCount":3,"value":NaN}'):
            with self.assertRaises(MODULE.NativeResultError):
                MODULE.verify_file_result(self.envelope(data), data, self.nonce)

    def testEnvelopeCannotHideFileFailureOrChangedFrames(self):
        for change in ({"status": "failure"}, {"status": "unknown"}, {"frameCount": 4},
                       {"frameCount": True}, {"frameCount": 3.0}, {"frameCount": 2}):
            data = self.result(**change)
            with self.assertRaises(MODULE.NativeResultError):
                MODULE.verify_file_result(self.envelope(data), data, self.nonce, 3)
        data = self.result()
        for change in ({"status": "failure"}, {"frameCount": 4}, {"frameCount": 3.0}):
            with self.assertRaises(MODULE.NativeResultError):
                MODULE.verify_file_result(self.envelope(data, **change), data, self.nonce)

    def testCompactMarkersStillRejectConflictFailureAndMalformedStreams(self):
        data = self.result()
        marker = "TEST:" + json.dumps(self.envelope(data))
        for other in (self.envelope(data, status="failure"), self.envelope(data, transportNonce="b" * 32)):
            with self.assertRaises(MODULE.NativeResultError):
                MODULE.reconcile_unified_duplicates(marker, "TEST:" + json.dumps(other), "TEST:")
        with self.assertRaises(MODULE.NativeResultError):
            MODULE.parse_result(marker + "\nTEST:{", "TEST:")

    def testPrivateReadIsBoundedAndMissingFilesCannotPass(self):
        with private_fixture() as directory:
            path = directory / "result.json"
            with self.assertRaises(OSError):
                MODULE.read_private_bytes(path, 8)
            path.write_bytes(b"12345678")
            self.assertEqual(b"12345678", MODULE.read_private_bytes(path, 8))
            with self.assertRaises(MODULE.NativeResultError):
                MODULE.read_private_bytes(path, 7)
            with self.assertRaises(MODULE.NativeResultError):
                MODULE.read_private_bytes(directory, 8)

    def testPrivateReadRejectsSymbolicLinks(self):
        with private_fixture() as directory:
            target = directory / "actual.json"
            target.write_bytes(b"{}")
            alias = directory / "alias.json"
            alias.symlink_to(target.name)
            with self.assertRaisesRegex(MODULE.NativeResultError, "nonregular-evidence-file"):
                MODULE.read_private_bytes(alias, 8)

    def testContextMustMatchThePrivateProtocolAndAbsoluteResultPath(self):
        with private_fixture() as directory:
            result_path = directory / "result.json"
            data = self.result()
            result_path.write_bytes(data)
            context_path = directory / "context.json"
            context = {"schema": MODULE.FILE_CONTEXT_PROTOCOL, "path": str(result_path),
                       "transportNonce": self.nonce}
            context_path.write_text(json.dumps(context), encoding="utf-8")
            self.assertEqual(json.loads(data), MODULE.resolve_file_result(self.envelope(data), context_path))
            for changes in ({"schema": "unknown"}, {"path": "relative.json"}, {"transportNonce": "b" * 32},
                            {"unknown": True}):
                context_path.write_text(json.dumps({**context, **changes}), encoding="utf-8")
                with self.assertRaises(MODULE.NativeResultError):
                    MODULE.resolve_file_result(self.envelope(data), context_path)

    def testFileModeCliRequiresBothEvidenceChannelsAndNeverOverwritesOutput(self):
        with private_fixture() as directory:
            data = self.result(details="Đủ dữ liệu " * 200)
            payload = directory / "result.json"
            payload.write_bytes(data)
            context = directory / "context.json"
            context.write_text(json.dumps({"schema": MODULE.FILE_CONTEXT_PROTOCOL,
                "path": str(payload), "transportNonce": self.nonce}), encoding="utf-8")
            log = directory / "console.log"
            marker = "TEST:" + json.dumps(self.envelope(data))
            log.write_text(marker, encoding="utf-8")
            unified = directory / "unified.json"
            unified.write_text(json.dumps([{"eventMessage": marker}]), encoding="utf-8")
            output = directory / "verified.json"
            arguments = ["verifier", "--log", str(log), "--json-log", str(unified), "--prefix", "TEST:",
                         "--file-context", str(context), "--minimum-frames", "3", "--output", str(output)]
            with patch.object(MODULE.sys, "argv", arguments), contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(0, MODULE.main())
                self.assertEqual(json.loads(data), json.loads(output.read_bytes()))
                original = output.read_bytes()
                self.assertEqual(1, MODULE.main())
                self.assertEqual(original, output.read_bytes())

    def testFileModeCliCannotAcceptAFileWithoutMarkerOrIgnoreFailure(self):
        for kind in ("missing-marker", "failure-marker", "missing-file"):
            with self.subTest(kind=kind), private_fixture() as directory:
                data = self.result()
                payload = directory / "result.json"
                if kind != "missing-file":
                    payload.write_bytes(data)
                context = directory / "context.json"
                context.write_text(json.dumps({"schema": MODULE.FILE_CONTEXT_PROTOCOL,
                    "path": str(payload), "transportNonce": self.nonce}), encoding="utf-8")
                log = directory / "console.log"
                marker = "TEST:" + json.dumps(self.envelope(data))
                if kind == "missing-marker":
                    marker = "no result"
                elif kind == "failure-marker":
                    marker += "\nTEST:" + json.dumps(self.envelope(data, status="failure"))
                log.write_text(marker, encoding="utf-8")
                output = directory / "verified.json"
                arguments = ["verifier", "--log", str(log), "--prefix", "TEST:",
                             "--file-context", str(context), "--output", str(output)]
                with patch.object(MODULE.sys, "argv", arguments), contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                    self.assertEqual(2 if kind == "missing-marker" else 1, MODULE.main())
                self.assertFalse(output.exists())

    def testFileModeCliRejectsTruncatedCompactDuplicateDespiteValidFileAndConsole(self):
        with private_fixture() as directory:
            data = self.result(details="full result" * 200)
            payload = directory / "result.json"
            payload.write_bytes(data)
            context = directory / "context.json"
            context.write_text(json.dumps({"schema": MODULE.FILE_CONTEXT_PROTOCOL,
                "path": str(payload), "transportNonce": self.nonce}), encoding="utf-8")
            full = json.dumps(self.envelope(data), separators=(",", ":"))
            fragment = full[:full.index('"transportNonce"')]
            self.assertTrue(MODULE.has_complete_success_header(fragment, self.envelope(data)))
            log = directory / "console.log"
            log.write_text("TEST:" + full, encoding="utf-8")
            unified = directory / "unified.json"
            unified.write_text(json.dumps([{"eventMessage": "TEST:" + fragment}]), encoding="utf-8")
            # Legacy reconciliation is intentionally still available without file mode.
            self.assertEqual(self.envelope(data), MODULE.read_result([log], "TEST:", json_paths=[unified]))
            output = directory / "verified.json"
            arguments = ["verifier", "--log", str(log), "--json-log", str(unified), "--prefix", "TEST:",
                         "--file-context", str(context), "--output", str(output)]
            with patch.object(MODULE.sys, "argv", arguments), contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(1, MODULE.main())
            self.assertFalse(output.exists())


class NativeResultTests(unittest.TestCase):
    def marker(self, status="success", frames=3, **extra):
        return json.dumps({"status": status, "frameCount": frames, **extra}, separators=(",", ":"))

    def testRawAndroidResult(self):
        value = self.marker()
        self.assertEqual(json.loads(value), MODULE.parse_result("--------- beginning of main\n" + value, ""))

    def testIosConsoleAndUnifiedDuplicate(self):
        value = self.marker(nonce="same-run")
        self.assertEqual(json.loads(value), MODULE.parse_result("TEST:" + value + "\n2026 app TEST:" + value, "TEST:"))

    def testUnifiedJsonPreservesEscapedMessageFraming(self):
        value = self.marker(description='Nội dung "được giữ"\ntrong JSON', details="x" * 2000)
        events = json.dumps([
            {"eventMessage": "unrelated log", "processImagePath": "/private/not-exported"},
            {"eventMessage": "TEST:" + value},
        ], ensure_ascii=False)
        messages = MODULE.extract_unified_messages(events, "TEST:")
        self.assertNotIn("not-exported", messages)
        self.assertEqual(json.loads(value), MODULE.parse_result("TEST:" + value + "\n" + messages, "TEST:"))

    def testUnifiedFailureCannotBeHiddenByConsoleSuccess(self):
        events = json.dumps([{"eventMessage": "TEST:" + self.marker(status="failure")}])
        with self.assertRaisesRegex(MODULE.NativeResultError, "failure-or-unknown-status"):
            MODULE.parse_result("TEST:" + self.marker() + "\n" + MODULE.extract_unified_messages(events, "TEST:"), "TEST:")

    def testMalformedUnifiedTransportFailsClosedAndEmptyStaysPending(self):
        for value in ('[{"eventMessage":', '{}', '[{}]', '[{"eventMessage":false}]'):
            with self.subTest(value=value), self.assertRaises(MODULE.NativeResultError):
                MODULE.extract_unified_messages(value, "TEST:")
        self.assertIsNone(MODULE.parse_result(MODULE.extract_unified_messages("[]", "TEST:"), "TEST:"))

    def testTruncatedUnifiedDuplicateRequiresExactCompleteConsolePayload(self):
        value = self.marker(nonce="current-run", details="x" * 1500)
        console = "TEST:" + value
        fragment = value[:990]
        normalized = MODULE.reconcile_unified_duplicates(console, "TEST:" + fragment, "TEST:")
        self.assertEqual(console, normalized)
        self.assertEqual(json.loads(value), MODULE.parse_result(console + "\n" + normalized, "TEST:"))

    def testTruncatedUnifiedMarkerCannotSucceedWithoutFullConsoleEvidence(self):
        fragment = "TEST:" + self.marker(details="x" * 1500)[:990]
        with self.assertRaises(MODULE.NativeResultError):
            MODULE.reconcile_unified_duplicates("", fragment, "TEST:")
        with self.assertRaises(MODULE.NativeResultError):
            MODULE.reconcile_unified_duplicates(fragment, fragment, "TEST:")

    def testUnknownTruncatedStatusOrFrameCannotBorrowConsoleSuccess(self):
        value = self.marker(details="x" * 1500)
        for fragment in ('{', '{"status":"suc', '{"status":"success",', '{"status":"success","frameCount":3'):
            with self.subTest(length=len(fragment)), self.assertRaises(MODULE.NativeResultError):
                MODULE.reconcile_unified_duplicates("TEST:" + value, "TEST:" + fragment, "TEST:")

    def testUnifiedMismatchFailureAndCorruptionCannotUseConsoleFallback(self):
        value = self.marker(nonce="current-run", details="x" * 1500)
        for other in (
            self.marker(nonce="old-run", details="x" * 1500)[:990],
            self.marker(status="failure", details="x" * 1500)[:990],
            value[:500] + "broken" + value[506:990],
            self.marker(nonce="different-complete-result"),
            self.marker(status="failure"),
        ):
            with self.subTest(other_length=len(other)), self.assertRaises(MODULE.NativeResultError):
                MODULE.reconcile_unified_duplicates("TEST:" + value, "TEST:" + other, "TEST:")

    def testCompleteUnifiedResultStillWorksWithoutConsoleMarker(self):
        value = self.marker()
        normalized = MODULE.reconcile_unified_duplicates("no console marker", "TEST:" + value, "TEST:")
        self.assertEqual(json.loads(value), MODULE.parse_result(normalized, "TEST:"))

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
