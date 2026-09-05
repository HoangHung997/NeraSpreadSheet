# Native smoke transport — shared Android/iOS launchers

Root extracts the existing hosted Android/iOS launch recipes into
`scripts/run-maui-android-smoke.sh` and `scripts/run-maui-ios-smoke.sh`.
The existing analytics workflows call them with the same source apps and
default bundle/tag/prefix identities. They require isolated CI and RUNNER_TEMP;
they are not a way to automate applications on the user's desktop.

## Inputs and boundaries

- Android: explicit APK, optional package, exact log tag, marker prefix and
  output JSON. Legacy Android uses raw logcat restricted to its own tag;
  package consumers use their distinct prefix as well.
- iOS: explicit app bundle, optional bundle identity, marker prefix and output
  JSON. The bundle's Info.plist must match; the simulator console and bounded
  unified-log fallback are combined when validating the result.
- Preserve Android API35 x86_64 emulator setup and existing 180-second adb,
  120-by-2-second boot, 120-second install, 60-second activity launch and
  60-by-2-second result bounds. Retain the 20-second initial PID search.
- Preserve iOS 90-second attached console and 12-by-3-second unified-log
  fallback. Nonzero launch exit or console timeout cannot be converted to
  success by a marker; this tightens the prior substring-only acceptance.
- Only the launched synthetic package and simulator/emulator are stopped.
  Logs stay in a fresh RUNNER_TEMP subdirectory, with no raw-log artifact or
  device/path dump to public output. No user workbook, global local desktop,
  signing/security workaround or additional application retry is introduced.

## Result validation

`verify-native-smoke-result.py` reads at most 2 MiB per log. A complete JSON
object, success status and integer frameCount >= 2 are mandatory for the legacy
analytics probes (creation frame followed by native accessibility validation).
The isolated package consumer still requires >= 3 completed frames and all its
additional runtime postconditions; transport does not replace that validator. Missing
marker returns pending, never success. Malformed independent markers/duplicate fields, mixed
success/failure, conflicting success payloads and missing frames fail closed.
Identical console/unified-log duplicates are allowed. Evidence output uses
create-new semantics and is not overwritten by a later attempt.

On Android a short-lived consumer may emit its completed result and exit
before the first PID read. The launcher checks the explicit marker before
declaring a lost process; disappearance without a marker remains failure.
Android activity transport does not provide the managed process exit code:
its acceptance remains the app's explicit postcondition marker, as with the
existing analytics gate. Do not describe it as OS exit-code verification.

This shared transport is **not** package provenance acceptance. A package
caller must verify its built app payload hashes before installation, then
validate the result against the expected source/version/feed hash/target/
fresh nonce and all public-consumer runtime postconditions. C's MAUI package
matrix remains native OPEN until that wrapper is wired and all targets run.
The old analytics apps do not retroactively claim nonce-based provenance.

Thirty-three bounded synthetic parser/CLI/filesystem regressions cover raw/prefixed/duplicate/missing/malformed/
conflicting/failure/frame/duplicate-field cases. Syntax checks are not native
proof: the existing full Android and separate iOS runtime jobs must pass at
the exact extraction HEAD. SDK/render/input code is unchanged by this slice,
so no render performance benchmark is required. Rollback restores the two
inline workflow launch blocks and removes the shared scripts/tests/contract.

Extraction `0a8e531f` rejected both Android and iOS markers. Its hardcoded >=3
transport assumption was incompatible with the unchanged legacy analytics
lifecycle: the preceding green Android job `101358050609` explicitly emitted
success with frameCount=2. Correct the transport policy, not the app assertions.
The iOS rejection reason was not captured by the initial coarse diagnostic;
do not assert the same cause without a new exact-head runtime result. Rejections
now expose a fixed reason code only, never raw app output. Non-finite JSON
numbers are rejected as well. New native CI remains mandatory.

At combined223 iOS job101364379019, numeric diagnostics isolated rejection to
stream1 (unified compact log): object-starting payload, 1116 characters, decoder
offset282, no closing object. The console stream did not reject. The launcher
now requests structured unified JSON and decodes eventMessage values before
marker validation, keeping stdout and command stderr separate. It still rejects
malformed event JSON, malformed markers and any failure across both streams;
it does not discard a broken unified result just because console reports success.
Synthetic tests cover escaped/multiline/long messages, unrelated metadata,
mixed failure and malformed structured transport. Native CI must verify that
the actual platform delivers complete messages in this format.

At7a iOS job101365914743, structured unified eventMessage itself is truncated
(990 characters, decoder offset990). A formatting change cannot restore bytes
already cut by the log transport. Reconciliation is therefore narrowly defined:
the console must contain a complete, strictly validated success payload; the
unified fragment must contain complete root status/frameCount values matching
that result and be an exact strict text prefix of its full console payload.
Only then is that duplicate represented by the already-verified full payload.
Unknown truncated headers, missing full console evidence, a differing nonce,
corruption, failure or contradictory complete result remain failures. A
complete unified result may still supply evidence when the console has none.
No truncated marker can independently establish success; package provenance
checks remain unchanged. This capability has synthetic positive/negative tests
but still requires exact-head iOS runtime proof.

Root f344 subsequently passed actual iOS33988991332 and full33988991344;
combined76d1e68c passed all six workflows, including canonical MAUI/Android.
The longer isolated iOS consumer686 failed with malformed987/offset976 in
job101370724447. The emitted diagnostic does not distinguish a console fragment
from a unified mismatch; neither a specific faulty stream nor an SDK emitter
bug is established. Required assembly/provenance data must not be shortened.

## Opt-in app-file result protocol — native verification pending

iOS caller may pass fifth argument `app-file-v1`; default `marker` remains the
legacy path. After installation the launcher obtains the actual app data
container with `simctl get_app_container`, resolves its `tmp` within that
container and creates a fresh private directory. It does not overwrite a prior
file, infer a container UUID, publish a raw file or change device security.
The private launch context stays in RUNNER_TEMP, never in uploaded evidence.
[Apple's Simulator automation overview](https://developer.apple.com/videos/play/wwdc2019/418/)
describes launch and obtaining app-container paths; this gate remains a simulator
probe, not device/hardware acceptance.

The launcher passes these child environment values:

- `NERA_MAUI_SMOKE_PROTOCOL=native-result-file-v1`.
- `NERA_MAUI_SMOKE_RESULT`: the new absolute `result.json` inside that directory.
- `NERA_MAUI_SMOKE_NONCE`: a fresh 32-character lowercase hexadecimal transport nonce.

The participating synthetic app serializes its existing full result exactly
once as UTF-8 bytes without BOM, writes using create-new semantics, flushes and
closes it before emitting a compact marker with the existing app prefix. The
marker has exactly `schema`, `status`, `frameCount`, `transportNonce`, `sha256`;
schema is `native-result-file-v1` and SHA256 is lowercase over those exact full
file bytes. File mode must not additionally emit the long full JSON marker.
Default app emission on other platforms is unchanged. A second emission/file
write or unsupported protocol must fail closed, not overwrite evidence.

The shared verifier first validates console/unified marker agreement with its
failure/conflict rules. In file mode EVERY compact marker must be complete:
legacy truncated-duplicate reconciliation is disabled, even when a complete
console envelope and matching full file exist. Legacy default retains that
separate behavior. It then verifies private context schema,
absolute canonical file parent, bounded regular non-symlink files (2 MiB for
result, 4 KiB for context), strict UTF-8/one complete JSON document, duplicate
and nonfinite rejection, fresh nonce, exact byte hash and matching successful
status/integer frame count. A full file without a log marker stays pending and
cannot pass; a marker without its file fails. Output still uses create-new.
The caller then verifies full package cohort/provenance and >=3 frames exactly
as before. No assembly list, postcondition, timeout or application retry is removed.

Thirty-three tests include large Unicode payload, altered/truncated/oversized
bytes, stale nonce, malformed/duplicate/nonfinite JSON, status/frame mismatch,
missing file/marker, symbolic link rejection and actual CLI non-overwrite.
An actual CLI regression rejects a truncated compact unified duplicate with
complete status/frame header despite a valid console envelope and full file.
Opt-in transport is released only for C's isolated consumer experiment until
actual iOS consumer CI passes; ordinary root CI exercises the legacy default.
Rollback removes the opt-in caller flag/emitter and this optional shared path,
not the accepted legacy transport or SDK behavior.
