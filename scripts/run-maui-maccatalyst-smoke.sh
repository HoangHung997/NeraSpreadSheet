#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 1 ] || [ "$#" -gt 2 ]; then
  echo "Usage: $0 <app-bundle-path> [result-json-path]" >&2
  exit 64
fi

APP="$1"
RESULT="${2:-${RUNNER_TEMP:-/tmp}/nera-maccatalyst-analytics-smoke.json}"
RESULT_FILE_NAME="nera-maccatalyst-analytics-smoke.json"
TRACE_FILE_NAME="nera-maccatalyst-analytics-smoke.trace"
FALLBACK_RESULT="${TMPDIR:-/tmp}/$RESULT_FILE_NAME"
WORK_DIR="${RUNNER_TEMP:-/tmp}/nera-maccatalyst-smoke-launch"
LAUNCHER="$WORK_DIR/LaunchNeraMacCatalystSmoke.swift"
INFO_PLIST="$APP/Contents/Info.plist"
EXPECTED_BUNDLE_ID="com.neraspreadsheet.maccatalystanalyticssmoke"

if [ ! -d "$APP" ]; then
  echo "Mac Catalyst smoke app bundle does not exist: $APP" >&2
  exit 1
fi
if [ ! -f "$INFO_PLIST" ]; then
  echo "Mac Catalyst smoke Info.plist is missing: $INFO_PLIST" >&2
  exit 1
fi

mkdir -p "$WORK_DIR" "$(dirname "$RESULT")" "$(dirname "$FALLBACK_RESULT")"
rm -f "$RESULT" "$FALLBACK_RESULT"

PROCESS_NAME="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$INFO_PLIST" 2>/dev/null || true)"
BUNDLE_ID="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$INFO_PLIST" 2>/dev/null || true)"
if [ -z "$PROCESS_NAME" ]; then
  echo "Could not resolve CFBundleExecutable from $INFO_PLIST" >&2
  exit 1
fi
if [ "$BUNDLE_ID" != "$EXPECTED_BUNDLE_ID" ]; then
  echo "Unexpected Mac Catalyst smoke bundle id: '$BUNDLE_ID' (expected '$EXPECTED_BUNDLE_ID')." >&2
  exit 1
fi

CONTAINER_ROOT="$HOME/Library/Containers/$BUNDLE_ID/Data"
SANDBOX_RESULT="$CONTAINER_ROOT/tmp/$RESULT_FILE_NAME"
SANDBOX_TRACE="$CONTAINER_ROOT/tmp/$TRACE_FILE_NAME"
mkdir -p "$(dirname "$SANDBOX_RESULT")"
rm -f "$SANDBOX_RESULT" "$SANDBOX_TRACE"
if [ -d "$CONTAINER_ROOT" ]; then
  find "$CONTAINER_ROOT" -type f -name "$RESULT_FILE_NAME" ! -path "$SANDBOX_RESULT" -delete 2>/dev/null || true
  find "$CONTAINER_ROOT" -type f -name "$TRACE_FILE_NAME" ! -path "$SANDBOX_TRACE" -delete 2>/dev/null || true
fi

echo "Mac Catalyst smoke bundle id: $BUNDLE_ID"
echo "Mac Catalyst smoke executable: $PROCESS_NAME"
echo "Mac Catalyst requested host result: $RESULT"
echo "Mac Catalyst host fallback result: $FALLBACK_RESULT"
echo "Mac Catalyst sandbox result passed to app: $SANDBOX_RESULT"
echo "Mac Catalyst sandbox trace: $SANDBOX_TRACE"
echo "Mac Catalyst container root: $CONTAINER_ROOT"

print_diagnostics() {
  echo "--- Mac Catalyst smoke process ---"
  if [ -n "${APP_PID:-}" ]; then
    ps -p "$APP_PID" -o pid=,ppid=,stat=,etime=,command= || true
  fi
  echo "--- Mac Catalyst result files ---"
  for candidate in "$SANDBOX_RESULT" "$RESULT" "$FALLBACK_RESULT"; do
    if [ -f "$candidate" ]; then
      echo "$candidate"
    fi
  done
  if [ -d "$CONTAINER_ROOT" ]; then
    find "$CONTAINER_ROOT" -type f -name "$RESULT_FILE_NAME" -print 2>/dev/null || true
  fi
  echo "--- Mac Catalyst managed stage trace ---"
  if [ -f "$SANDBOX_TRACE" ]; then
    cat "$SANDBOX_TRACE" || true
  elif [ -d "$CONTAINER_ROOT" ]; then
    TRACE_CANDIDATE="$(find "$CONTAINER_ROOT" -type f -name "$TRACE_FILE_NAME" -print 2>/dev/null | head -n 1 || true)"
    if [ -n "$TRACE_CANDIDATE" ]; then
      echo "$TRACE_CANDIDATE"
      cat "$TRACE_CANDIDATE" || true
    else
      echo "No Mac Catalyst managed stage trace was produced."
    fi
  else
    echo "No Mac Catalyst managed stage trace was produced."
  fi
  echo "--- Mac Catalyst unified log ---"
  /usr/bin/log show \
    --style compact \
    --last 5m \
    --predicate "process == \"$PROCESS_NAME\"" \
    2>/dev/null | tail -n 400 || true
  echo "--- Mac Catalyst diagnostic reports ---"
  find "$HOME/Library/Logs/DiagnosticReports" \
    -maxdepth 1 \
    -type f \
    -name "$PROCESS_NAME*" \
    -mmin -10 \
    -print \
    -exec sh -c 'echo "--- $1 ---"; tail -n 300 "$1"' _ {} \; \
    2>/dev/null || true
}

consume_result_file() {
  local result_file="$1"
  local status
  if [ ! -f "$result_file" ]; then
    return 3
  fi

  echo "Mac Catalyst smoke result file: $result_file"
  cat "$result_file"
  status="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1], encoding="utf-8"))["status"])' "$result_file")"
  if [ "$status" = "success" ]; then
    return 0
  fi
  echo "Mac Catalyst analytics smoke reported status: $status" >&2
  return 2
}

find_container_result() {
  if [ ! -d "$CONTAINER_ROOT" ]; then
    return 3
  fi
  local container_result
  container_result="$(find "$CONTAINER_ROOT" -type f -name "$RESULT_FILE_NAME" -print 2>/dev/null | head -n 1 || true)"
  if [ -z "$container_result" ]; then
    return 3
  fi
  printf '%s\n' "$container_result"
}

consume_any_result() {
  local code container_result
  for candidate in "$SANDBOX_RESULT" "$RESULT" "$FALLBACK_RESULT"; do
    if consume_result_file "$candidate"; then
      return 0
    else
      code=$?
    fi
    if [ "$code" -ne 3 ]; then
      return "$code"
    fi
  done

  if container_result="$(find_container_result)"; then
    if consume_result_file "$container_result"; then
      return 0
    else
      return $?
    fi
  fi
  return 3
}

cleanup() {
  if [ -n "${APP_PID:-}" ] && kill -0 "$APP_PID" 2>/dev/null; then
    kill "$APP_PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT

cat >"$LAUNCHER" <<'SWIFT'
import AppKit
import Foundation

func stderr(_ message: String) {
    FileHandle.standardError.write(Data((message + "\n").utf8))
}

guard CommandLine.arguments.count == 3 else {
    stderr("Usage: launcher <app-bundle-path> <result-json-path>")
    exit(64)
}

let appURL = URL(fileURLWithPath: CommandLine.arguments[1], isDirectory: true)
let resultPath = CommandLine.arguments[2]
let configuration = NSWorkspace.OpenConfiguration()
configuration.activates = true
configuration.addsToRecentItems = false
configuration.arguments = ["--nera-smoke-result", resultPath]
var environment = ProcessInfo.processInfo.environment
environment["NERA_MAUI_SMOKE_RESULT"] = resultPath
configuration.environment = environment

var finished = false
var status: Int32 = 1

NSWorkspace.shared.openApplication(at: appURL, configuration: configuration) { app, error in
    defer { finished = true }
    if let error {
        stderr("LaunchServices launch failed: \(error)")
        return
    }
    guard let app else {
        stderr("LaunchServices returned no running application and no error.")
        return
    }
    print("launched_pid=\(app.processIdentifier)")
    fflush(stdout)
    status = 0
}

let deadline = Date().addingTimeInterval(30)
while !finished && Date() < deadline {
    _ = RunLoop.current.run(mode: .default, before: Date().addingTimeInterval(0.1))
}

if !finished {
    stderr("LaunchServices did not complete the launch request within 30 seconds.")
    exit(124)
}
exit(status)
SWIFT

echo "Launching Mac Catalyst smoke through LaunchServices: $APP"
if LAUNCH_OUTPUT="$(xcrun swift "$LAUNCHER" "$APP" "$SANDBOX_RESULT" 2>&1)"; then
  LAUNCH_EXIT=0
else
  LAUNCH_EXIT=$?
fi
printf '%s\n' "$LAUNCH_OUTPUT"
if [ "$LAUNCH_EXIT" -ne 0 ]; then
  echo "LaunchServices helper exited with code $LAUNCH_EXIT." >&2
  print_diagnostics
  exit 1
fi

APP_PID="$(printf '%s\n' "$LAUNCH_OUTPUT" | sed -n 's/^launched_pid=//p' | tail -n 1)"
if ! [[ "$APP_PID" =~ ^[0-9]+$ ]]; then
  echo "LaunchServices did not return a valid application PID." >&2
  print_diagnostics
  exit 1
fi

echo "Mac Catalyst smoke PID: $APP_PID"

for _ in $(seq 1 90); do
  if consume_any_result; then
    exit 0
  else
    RESULT_CODE=$?
  fi
  if [ "$RESULT_CODE" -eq 2 ]; then
    print_diagnostics
    exit 1
  fi

  if ! kill -0 "$APP_PID" 2>/dev/null; then
    if consume_any_result; then
      exit 0
    else
      RESULT_CODE=$?
    fi
    if [ "$RESULT_CODE" -eq 2 ]; then
      print_diagnostics
      exit 1
    fi
    echo "Mac Catalyst smoke exited before producing a sandbox or fallback result file." >&2
    print_diagnostics
    exit 1
  fi
  sleep 1
done

echo "Mac Catalyst analytics accessibility smoke timed out." >&2
print_diagnostics
exit 1
