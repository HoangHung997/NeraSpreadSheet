#!/usr/bin/env bash
set -euo pipefail

# Shared transport for the existing analytics app and isolated package consumers.
# Package identity/hash/nonce verification is an additional caller-owned gate.
if [ "$#" -lt 1 ] || [ "$#" -gt 5 ] || [ "${CI:-}" != "true" ] || [ -z "${RUNNER_TEMP:-}" ]; then
  echo "Usage (isolated CI only): $0 <app-bundle> [bundle-id] [marker-prefix] [result-json] [marker|app-file-v1]" >&2
  exit 64
fi
APP="$1"
BUNDLE_ID="${2:-com.neraspreadsheet.iosanalyticssmoke}"
PREFIX="${3:-NERA_IOS_ANALYTICS_SMOKE:}"
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
WORK_DIR="$(mktemp -d "$RUNNER_TEMP/nera-ios-launch-XXXXXX")"
LOG="$WORK_DIR/console.log"
UNIFIED_LOG="$WORK_DIR/unified.json"
RESULT="${4:-$WORK_DIR/result.json}"
MODE="${5:-marker}"
FILE_CONTEXT="$WORK_DIR/file-context.json"
UDID=""
if [ "$MODE" != "marker" ] && [ "$MODE" != "app-file-v1" ]; then
  echo "Unknown iOS result protocol." >&2
  exit 64
fi
if [ ! -d "$APP" ] || [ ! -f "$APP/Info.plist" ] || [ -e "$RESULT" ]; then
  echo "Missing iOS app bundle or existing result evidence." >&2
  exit 1
fi
if [[ ! "$BUNDLE_ID" =~ ^[A-Za-z0-9][A-Za-z0-9.-]+$ ]] || [[ ! "$PREFIX" =~ ^[A-Z0-9_]+:$ ]]; then
  echo "Invalid synthetic app identity or marker prefix." >&2
  exit 64
fi
if [ "$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$APP/Info.plist")" != "$BUNDLE_ID" ]; then
  echo "iOS bundle identity differs from the requested app." >&2
  exit 1
fi
cleanup() {
  if [ -n "$UDID" ]; then
    xcrun simctl terminate "$UDID" "$BUNDLE_ID" >/dev/null 2>&1 || true
    xcrun simctl shutdown "$UDID" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT
UDID="$(xcrun simctl list devices available -j | python3 -c '
import json, sys
data = json.load(sys.stdin)
for runtime in sorted(data.get("devices", {}), reverse=True):
    if "SimRuntime.iOS-" not in runtime:
        continue
    for device in data["devices"][runtime]:
        if device.get("isAvailable", True) and device.get("name", "").startswith("iPhone"):
            print(device["udid"])
            raise SystemExit(0)
raise SystemExit(1)
')"
if [ -z "$UDID" ]; then
  echo "No available iPhone simulator was found." >&2
  exit 1
fi
xcrun simctl boot "$UDID" >/dev/null 2>&1 || true
xcrun simctl bootstatus "$UDID" -b >"$WORK_DIR/boot.log" 2>&1
xcrun simctl install "$UDID" "$APP"
SMOKE_STARTED_AT="$(date -u '+%Y-%m-%d %H:%M:%S')"
set +e
python3 - "$UDID" "$BUNDLE_ID" "$LOG" "$MODE" "$FILE_CONTEXT" <<'PY'
import json, os, secrets, subprocess, sys, tempfile
from pathlib import Path
udid, bundle, log_path, mode, context_path = sys.argv[1:]
environment = os.environ.copy()
for name in ("RESULT", "PROTOCOL", "NONCE"):
    environment.pop("SIMCTL_CHILD_NERA_MAUI_SMOKE_" + name, None)
if mode == "app-file-v1":
    try:
        container_result = subprocess.run(["xcrun", "simctl", "get_app_container", udid, bundle, "data"],
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=30, check=True)
        container_text = container_result.stdout.decode("utf-8").strip()
        if len(container_text) > 4096 or not Path(container_text).is_absolute():
            raise ValueError("invalid container path")
        container = Path(container_text).resolve(strict=True)
        if container == Path(container.anchor):
            raise ValueError("invalid container root")
        temporary = (container / "tmp").resolve(strict=True)
        if not container.is_dir() or not temporary.is_dir() or not temporary.is_relative_to(container):
            raise ValueError("invalid synthetic container")
        directory = Path(tempfile.mkdtemp(prefix="nera-smoke-", dir=temporary)).resolve(strict=True)
        if directory.parent != temporary:
            raise ValueError("invalid evidence directory")
        result_path = directory / "result.json"
        nonce = secrets.token_hex(16)
        with open(context_path, "x", encoding="utf-8") as stream:
            json.dump({"schema": "native-result-file-context-v1", "path": str(result_path),
                       "transportNonce": nonce}, stream)
        environment.update({"SIMCTL_CHILD_NERA_MAUI_SMOKE_RESULT": str(result_path),
                            "SIMCTL_CHILD_NERA_MAUI_SMOKE_PROTOCOL": "native-result-file-v1",
                            "SIMCTL_CHILD_NERA_MAUI_SMOKE_NONCE": nonce})
    except (OSError, ValueError, subprocess.SubprocessError):
        print("iOS private file transport setup failed.", file=sys.stderr)
        raise SystemExit(1)
try:
    completed = subprocess.run(["xcrun", "simctl", "launch", "--console", udid, bundle],
        env=environment, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=90, check=False)
    output, code = completed.stdout or b"", completed.returncode
except subprocess.TimeoutExpired as error:
    output, code = error.stdout or b"", 124
with open(log_path, "xb") as stream:
    stream.write(output)
raise SystemExit(code)
PY
LAUNCH_STATUS=$?
set -e
if [ "$LAUNCH_STATUS" -ne 0 ]; then
  echo "iOS console launch failed or timed out (status=$LAUNCH_STATUS); no successful acceptance." >&2
  exit 1
fi
poll_result() {
  local parser_args=(--log "$LOG" --json-log "$UNIFIED_LOG" --prefix "$PREFIX" --output "$RESULT")
  if [ "$MODE" = "app-file-v1" ]; then parser_args+=(--file-context "$FILE_CONTEXT"); fi
  python3 "$SCRIPT_DIR/verify-native-smoke-result.py" "${parser_args[@]}"
}
# The existing bounded unified-log fallback is retained. Combine both streams
# before accepting; a failure in either stream wins over a success marker.
for attempt in $(seq 1 12); do
  xcrun simctl spawn "$UDID" log show --start "$SMOKE_STARTED_AT" --style json \
    --predicate "eventMessage CONTAINS \"${PREFIX%:}\"" >"$UNIFIED_LOG" 2>"$WORK_DIR/unified-error.log" || true
  set +e
  poll_result
  RESULT_STATUS=$?
  set -e
  if [ "$RESULT_STATUS" -eq 0 ]; then
    echo "Loaded iOS app transport passed with an explicit completed-frame marker."
    exit 0
  fi
  if [ "$RESULT_STATUS" -ne 2 ]; then exit 1; fi
  echo "No iOS result marker yet (attempt $attempt/12)."
  sleep 3
done
echo "iOS smoke produced no result after bounded unified-log polling." >&2
exit 1
