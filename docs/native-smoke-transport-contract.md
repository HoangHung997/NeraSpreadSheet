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

Twenty in-memory parser regressions cover raw/prefixed/duplicate/missing/malformed/
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
