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
object, success status and integer frameCount >= 3 are mandatory. Missing
marker returns pending, never success. Malformed/duplicate fields, mixed
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

Nine in-memory parser regressions cover raw/prefixed/duplicate/missing/malformed/
conflicting/failure/frame/duplicate-field cases. Syntax checks are not native
proof: the existing full Android and separate iOS runtime jobs must pass at
the exact extraction HEAD. SDK/render/input code is unchanged by this slice,
so no render performance benchmark is required. Rollback restores the two
inline workflow launch blocks and removes the shared scripts/tests/contract.
