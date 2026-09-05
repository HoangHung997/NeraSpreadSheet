#!/usr/bin/env bash
set -euo pipefail

# Only the isolated synthetic app/emulator is controlled by this launcher.
if [ "$#" -lt 1 ] || [ "$#" -gt 5 ] || [ "${CI:-}" != "true" ] || [ -z "${RUNNER_TEMP:-}" ]; then
  echo "Usage (isolated CI only): $0 <apk> [package] [log-tag] [marker-prefix] [result-json]" >&2
  exit 64
fi
APK="$1"
PACKAGE="${2:-com.neraspreadsheet.androidanalyticssmoke}"
LOG_TAG="${3:-NeraAnalyticsSmoke}"
PREFIX="${4:-}"
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
WORK_DIR="$(mktemp -d "$RUNNER_TEMP/nera-android-launch-XXXXXX")"
RESULT="${5:-$WORK_DIR/result.json}"
LOG="$WORK_DIR/app.log"
EMULATOR_LOG="$WORK_DIR/emulator.log"
if [ ! -f "$APK" ] || [ -e "$RESULT" ]; then
  echo "Missing APK or existing result evidence." >&2
  exit 1
fi
if [[ ! "$PACKAGE" =~ ^[A-Za-z0-9][A-Za-z0-9._]+$ ]] || [[ ! "$LOG_TAG" =~ ^[A-Za-z0-9_]+$ ]] ||
   { [ -n "$PREFIX" ] && [[ ! "$PREFIX" =~ ^[A-Z0-9_]+:$ ]]; }; then
  echo "Invalid synthetic Android app identity or marker prefix." >&2
  exit 64
fi
SDKMANAGER="$ANDROID_HOME/cmdline-tools/latest/bin/sdkmanager"
AVDMANAGER="$ANDROID_HOME/cmdline-tools/latest/bin/avdmanager"
EMULATOR="$ANDROID_HOME/emulator/emulator"
ADB="$ANDROID_HOME/platform-tools/adb"
AVD_NAME="nera-analytics-smoke"
SYSTEM_IMAGE="system-images;android-35;google_apis;x86_64"
export ANDROID_USER_HOME="$WORK_DIR/android-user"
export ANDROID_AVD_HOME="$ANDROID_USER_HOME/avd"
mkdir -p "$ANDROID_AVD_HOME"
EMULATOR_PID=""
cleanup() {
  if [ -n "$EMULATOR_PID" ]; then
    "$ADB" shell am force-stop "$PACKAGE" >/dev/null 2>&1 || true
    kill "$EMULATOR_PID" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT
yes | "$SDKMANAGER" --licenses >/dev/null 2>&1 || true
"$SDKMANAGER" "platform-tools" "emulator" "$SYSTEM_IMAGE" >"$WORK_DIR/sdk.log" 2>&1
printf 'no\n' | "$AVDMANAGER" create avd -n "$AVD_NAME" -k "$SYSTEM_IMAGE" >"$WORK_DIR/avd.log" 2>&1
if ! "$EMULATOR" -list-avds | grep -Fxq "$AVD_NAME"; then
  echo "Created Android AVD is not visible to the emulator." >&2
  exit 1
fi
ACCEL="off"
if [ -e /dev/kvm ]; then
  # Preserve the existing hosted-runner acceleration setup; never run locally.
  sudo chmod 666 /dev/kvm || true
  ACCEL="on"
fi
nohup "$EMULATOR" -avd "$AVD_NAME" -no-window -noaudio -no-boot-anim -no-snapshot \
  -no-metrics -gpu swiftshader_indirect -accel "$ACCEL" >"$EMULATOR_LOG" 2>&1 &
EMULATOR_PID=$!
if ! timeout 180 "$ADB" wait-for-device; then
  echo "Android emulator did not expose adb within 180 seconds." >&2
  exit 1
fi
booted=0
for _ in $(seq 1 120); do
  if ! kill -0 "$EMULATOR_PID" 2>/dev/null; then
    echo "Android emulator exited before boot completed." >&2
    exit 1
  fi
  if [ "$("$ADB" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" = "1" ]; then
    booted=1
    break
  fi
  sleep 2
done
if [ "$booted" -ne 1 ]; then
  echo "Android emulator did not finish booting." >&2
  exit 1
fi
timeout 120 "$ADB" install --no-incremental -r "$APK"
"$ADB" logcat -c
RESOLVED="$("$ADB" shell cmd package resolve-activity --brief -c android.intent.category.LAUNCHER "$PACKAGE" | tr -d '\r' || true)"
ACTIVITY="$(printf '%s\n' "$RESOLVED" | grep -E '^[A-Za-z0-9_.]+/[A-Za-z0-9_.$]+' | tail -n 1 || true)"
if [ -z "$ACTIVITY" ] || [[ "$ACTIVITY" != "$PACKAGE/"* ]]; then
  echo "Android package has no matching launcher activity." >&2
  exit 1
fi
if ! timeout 60 "$ADB" shell am start -W -S -n "$ACTIVITY" >"$WORK_DIR/start.log" 2>&1; then
  echo "Android app launcher failed." >&2
  exit 1
fi
poll_result() {
  "$ADB" logcat -d -v raw -s "$LOG_TAG:I" '*:S' >"$LOG" 2>&1 || true
  python3 "$SCRIPT_DIR/verify-native-smoke-result.py" --log "$LOG" --prefix "$PREFIX" --output "$RESULT"
}
read_status() {
  set +e
  poll_result
  RESULT_STATUS=$?
  set -e
  if [ "$RESULT_STATUS" -eq 0 ]; then
    echo "Loaded Android app transport passed with an explicit completed-frame marker."
    exit 0
  fi
  if [ "$RESULT_STATUS" -ne 2 ]; then exit 1; fi
}
PID=""
for _ in $(seq 1 20); do
  # A short-lived package consumer may finish before the first PID read. Its
  # explicit marker is still mandatory; a vanished process is never success.
  read_status
  PID="$("$ADB" shell pidof "$PACKAGE" 2>/dev/null | tr -d '\r' | awk '{print $1}' || true)"
  if [ -n "$PID" ]; then break; fi
  sleep 1
done
if [ -z "$PID" ]; then
  read_status
  echo "Android process disappeared without a result marker." >&2
  exit 1
fi
for _ in $(seq 1 60); do
  read_status
  CURRENT_PID="$("$ADB" shell pidof "$PACKAGE" 2>/dev/null | tr -d '\r' | awk '{print $1}' || true)"
  if [ -z "$CURRENT_PID" ]; then
    read_status
    echo "Android process exited before producing a result marker." >&2
    exit 1
  fi
  sleep 2
done
echo "Android accessibility smoke timed out." >&2
exit 1
