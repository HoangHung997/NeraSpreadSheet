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
FALLBACK_TRACE="${TMPDIR:-/tmp}/$TRACE_FILE_NAME"
RUNNER_TRACE="${RUNNER_TEMP:-/tmp}/$TRACE_FILE_NAME"
WORK_DIR="${RUNNER_TEMP:-/tmp}/nera-maccatalyst-smoke-launch"
LAUNCHER="$WORK_DIR/LaunchNeraMacCatalystSmoke.swift"
INFO_PLIST="$APP/Contents/Info.plist"
EXPECTED_BUNDLE_ID="com.neraspreadsheet.maccatalystanalyticssmoke"
LAUNCH_DIAG_START=""
NERA_MAUI_NATIVE_STDERR_RUN="$(uuidgen | tr '[:upper:]' '[:lower:]')"
export NERA_MAUI_NATIVE_STDERR_RUN

if [ ! -d "$APP" ]; then
  echo "Mac Catalyst smoke app bundle does not exist: $APP" >&2
  exit 1
fi
if [ ! -f "$INFO_PLIST" ]; then
  echo "Mac Catalyst smoke Info.plist is missing: $INFO_PLIST" >&2
  exit 1
fi

mkdir -p \
  "$WORK_DIR" \
  "$(dirname "$RESULT")" \
  "$(dirname "$FALLBACK_RESULT")" \
  "$(dirname "$FALLBACK_TRACE")" \
  "$(dirname "$RUNNER_TRACE")"
rm -f "$RESULT" "$FALLBACK_RESULT" "$FALLBACK_TRACE" "$RUNNER_TRACE"

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
APP_EXECUTABLE="$APP/Contents/MacOS/$PROCESS_NAME"
if [ ! -x "$APP_EXECUTABLE" ]; then
  echo "Mac Catalyst smoke executable is missing or not executable: $APP_EXECUTABLE" >&2
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
echo "Mac Catalyst smoke executable path: $APP_EXECUTABLE"
echo "Mac Catalyst requested host result: $RESULT"
echo "Mac Catalyst host fallback result: $FALLBACK_RESULT"
echo "Mac Catalyst sandbox result passed to app: $SANDBOX_RESULT"
echo "Mac Catalyst sandbox trace: $SANDBOX_TRACE"
echo "Mac Catalyst host TMPDIR trace: $FALLBACK_TRACE"
echo "Mac Catalyst runner temp trace: $RUNNER_TRACE"
echo "Mac Catalyst container root: $CONTAINER_ROOT"

echo "--- Mac Catalyst code-signing identity ---"
codesign --display --verbose=4 "$APP" 2>&1 || true
if ! codesign --verify --deep --strict --verbose=4 "$APP"; then
  echo "Mac Catalyst smoke app failed strict code-signature verification before launch." >&2
  exit 1
fi
echo "Mac Catalyst strict code-signature verification: PASS"

native_stderr_diagnostics() {
  python3 - "$1" "$CONTAINER_ROOT/tmp" "${APP_PID:-}" "$NERA_MAUI_NATIVE_STDERR_RUN" "${LAUNCH_DIAG_START:-}" <<'PY'
import datetime
import os
from pathlib import Path
import re
import sys


def method_frames(raw):
    frames = []
    for line in raw.splitlines()[1:]:
        # Only native symbolized frame formats, never exception messages,
        # registers, managed locals, environment values or arbitrary stderr.
        match = re.fullmatch(r"\s*0x[0-9a-fA-F]+\s+-\s+(.+?)\s+:\s+(.+?)\s*", line)
        if match is None:
            match = re.fullmatch(r"\s*\d+\s+(\S+)\s+0x[0-9a-fA-F]+\s+(.+?)\s*", line)
        if match is None:
            continue
        module, symbol = match.groups()
        module = Path(module).name
        symbol = re.sub(r"\s+\+\s+\d+\s*$", "", symbol)
        symbol = symbol.split("(", 1)[0].strip()
        if not re.fullmatch(r"[A-Za-z0-9_.+-]{1,120}", module):
            continue
        if not (re.fullmatch(r"[A-Za-z_$][A-Za-z0-9_$.:<>~]{0,239}", symbol)
                or re.fullmatch(r"[-+]\[[A-Za-z_$][A-Za-z0-9_$.]* [A-Za-z_$][A-Za-z0-9_$:]*\]", symbol)):
            continue
        if re.search(r"0x[0-9a-fA-F]+|[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}", module + symbol):
            continue
        frames.append(module + "!" + symbol)
        if len(frames) == 64:
            break
    return frames


def main():
    action, directory, pid, run_key, launch_text = sys.argv[1:6]
    if not pid.isdecimal() or not re.fullmatch(r"[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}", run_key) or not launch_text:
        return
    path = Path(directory) / ("nera-native-stderr-" + run_key + "-" + pid + ".log")
    try:
        launch = datetime.datetime.strptime(launch_text, "%Y-%m-%d %H:%M:%S").timestamp()
        if path.is_symlink() or not path.is_file():
            if action == "read":
                print("No current-process native stderr capture was produced.")
            return
        stat = path.stat()
        if stat.st_uid != os.getuid() or stat.st_mtime < launch:
            return
        with path.open("rb") as stream:
            raw = stream.read(64 * 1024).decode("utf-8", errors="replace")
        if raw.splitlines()[0] != "NERA_NATIVE_STDERR_V1:" + pid + ":" + run_key:
            return
        if action == "cleanup":
            path.unlink()
        elif action == "read":
            frames = method_frames(raw)
            print("Native stderr capture matched the current process and run.")
            print("Native stderr shape: bytes=" + str(len(raw.encode("utf-8")))
                  + "; lines=" + str(len(raw.splitlines()))
                  + "; nativeHeader=" + str("Native stacktrace" in raw)
                  + "; managedHeader=" + str("Managed Stacktrace" in raw)
                  + "; addressFrameLines=" + str(sum(bool(re.match(r"\s*(?:0x[0-9a-fA-F]+|\d+\s+\S+\s+0x[0-9a-fA-F]+)", line)) for line in raw.splitlines())))
            for frame in frames:
                print(frame)
            if not frames:
                print("No whitelisted symbolized native frames were captured.")
    except (OSError, ValueError, IndexError):
        if action == "read":
            print("Current-process native stderr diagnostics were unavailable.")


if __name__ == "__main__":
    main()
PY
}

print_trace_file() {
  local trace_file="$1"
  if [ ! -f "$trace_file" ]; then
    return 1
  fi
  echo "--- $trace_file ---"
  cat "$trace_file" || true
  return 0
}

find_live_app_pid() {
  local excluded_pid="${1:-}"
  local candidate
  while IFS= read -r candidate; do
    [ -n "$candidate" ] || continue
    if [ -n "$excluded_pid" ] && [ "$candidate" = "$excluded_pid" ]; then
      continue
    fi
    if kill -0 "$candidate" 2>/dev/null; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done < <(pgrep -f -- "$APP_EXECUTABLE" 2>/dev/null || true)
  return 1
}

print_matching_processes() {
  echo "--- Mac Catalyst matching processes ---"
  pgrep -alf -- "$APP_EXECUTABLE" 2>/dev/null || true
  ps -axo pid=,ppid=,stat=,etime=,command= | grep -F -- "$PROCESS_NAME" | grep -v '[g]rep' || true
}

print_diagnostics() {
  echo "--- Mac Catalyst smoke process ---"
  if [ -n "${APP_PID:-}" ]; then
    ps -p "$APP_PID" -o pid=,ppid=,stat=,etime=,command= || true
  fi
  print_matching_processes
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
  TRACE_FOUND=0
  for trace_candidate in "$SANDBOX_TRACE" "$FALLBACK_TRACE" "$RUNNER_TRACE"; do
    if print_trace_file "$trace_candidate"; then
      TRACE_FOUND=1
    fi
  done
  if [ -d "$CONTAINER_ROOT" ]; then
    while IFS= read -r trace_candidate; do
      [ -n "$trace_candidate" ] || continue
      case "$trace_candidate" in
        "$SANDBOX_TRACE") continue ;;
      esac
      if print_trace_file "$trace_candidate"; then
        TRACE_FOUND=1
      fi
    done < <(find "$CONTAINER_ROOT" -type f -name "$TRACE_FILE_NAME" -print 2>/dev/null || true)
  fi
  while IFS= read -r trace_candidate; do
    [ -n "$trace_candidate" ] || continue
    case "$trace_candidate" in
      "$FALLBACK_TRACE"|"$RUNNER_TRACE") continue ;;
    esac
    if print_trace_file "$trace_candidate"; then
      TRACE_FOUND=1
    fi
  done < <(find /var/folders -type f -name "$TRACE_FILE_NAME" -mmin -10 -print 2>/dev/null | head -n 20 || true)
  if [ "$TRACE_FOUND" -eq 0 ]; then
    echo "No Mac Catalyst managed stage trace was produced in sandbox, runner temp, TMPDIR, or recent /var/folders locations."
  fi

  echo "--- Mac Catalyst code-signing system log ---"
  /usr/bin/log show \
    --style compact \
    --last 5m \
    --predicate '(eventMessage CONTAINS[c] "CODESIGNING") OR (eventMessage CONTAINS[c] "Invalid Page") OR (eventMessage CONTAINS[c] "code signature")' \
    2>/dev/null | tail -n 300 || true

  echo "--- Mac Catalyst system termination log ---"
  local diag_start="${LAUNCH_DIAG_START:-}"
  if [ -z "$diag_start" ]; then
    diag_start="$(date -v-2M '+%Y-%m-%d %H:%M:%S')"
  fi
  /usr/bin/log show \
    --style compact \
    --start "$diag_start" \
    --predicate '(process == "runningboardd") OR (process == "launchservicesd") OR (process == "WindowServer") OR (process == "kernel") OR (process == "amfid") OR (process == "taskgated-helper")' \
    2>/dev/null | \
    grep -Ei -- "$BUNDLE_ID|$PROCESS_NAME|${APP_PID:-no-pid}|termination|terminate|exited|exit code|kill|SIG[A-Z]+|AMFI|code signature|GPU|Metal|drawable|IOMobileFramebuffer|WindowServer" | \
    tail -n 500 || true

  echo "--- Mac Catalyst unified log ---"
  /usr/bin/log show \
    --style compact \
    --last 5m \
    --predicate "process == \"$PROCESS_NAME\"" \
    2>/dev/null | tail -n 400 || true
  echo "--- Mac Catalyst sanitized current-process crash diagnostics ---"
  native_stderr_diagnostics read
  python3 - "$PROCESS_NAME" "${APP_PID:-}" "${LAUNCH_DIAG_START:-}" <<'PY' || true
import datetime
import json
import os
from pathlib import Path
import re
import sys
import time


def safe_text(value):
    text = str(value)[:1000]
    text = re.sub(r"[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}", "<id>", text)
    return re.sub(r"(?:[A-Za-z]:[\\/]|/)[^\s\"']+", "<path>", text)


def selected_fields(source, names):
    return {name: safe_text(source[name]) for name in names if name in source}


def summarize_report(report, expected_pid, expected_name):
    matches_name = report.get("procName") == expected_name or Path(str(report.get("procPath", ""))).name == expected_name
    if report.get("pid") != expected_pid or not matches_name:
        return None
    images = report.get("usedImages", [])
    threads = report.get("threads", [])
    fault = report.get("faultingThread")
    selected = [index for index, thread in enumerate(threads)
                if thread.get("triggered") or index == fault]
    if not selected:
        selected = list(range(min(3, len(threads))))
    stacks = []
    for index in selected[:3]:
        frames = []
        for frame in threads[index].get("frames", [])[:40]:
            image_index = frame.get("imageIndex", -1)
            module = images[image_index].get("name", "unknown") if isinstance(image_index, int) and 0 <= image_index < len(images) else "unknown"
            fields = selected_fields(frame, ("symbol", "symbolLocation", "imageOffset"))
            fields["module"] = safe_text(module)
            frames.append(fields)
        stacks.append({"index": index, "frames": frames})
    return {
        "matchedCurrentProcess": True,
        "exception": selected_fields(report.get("exception", {}), ("type", "signal", "subtype", "codes")),
        "termination": selected_fields(report.get("termination", {}), ("namespace", "code", "indicator")),
        "threads": stacks,
    }


def read_report(path):
    try:
        if path.stat().st_size > 8 * 1024 * 1024:
            return None
        raw = path.read_text(encoding="utf-8")
        first, end = json.JSONDecoder().raw_decode(raw)
        remainder = raw[end:].strip()
        return json.loads(remainder) if remainder else first
    except (OSError, UnicodeError, json.JSONDecodeError):
        return None


def main():
    name, pid_text, launch_text = sys.argv[1:4]
    if not pid_text.isdecimal() or not launch_text:
        print("No exact launched process identity is available for crash-report matching.")
        return
    pid = int(pid_text)
    launch = datetime.datetime.strptime(launch_text, "%Y-%m-%d %H:%M:%S").timestamp()
    try:
        os.kill(pid, 0)
        wait = 0
    except ProcessLookupError:
        wait = 10
    except PermissionError:
        wait = 0
    deadline = time.monotonic() + wait
    directory = Path.home() / "Library/Logs/DiagnosticReports"
    while True:
        try:
            candidates = [path for path in directory.glob("*.ips")
                          if path.is_file() and path.stat().st_mtime >= launch]
        except OSError:
            candidates = []
        for path in candidates:
            report = read_report(path)
            if not isinstance(report, dict):
                continue
            result = summarize_report(report, pid, name)
            if result is not None:
                print(json.dumps(result, ensure_ascii=True, indent=2))
                return
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            print("No matching current-run JSON crash report appeared within the bounded diagnostic wait.")
            return
        time.sleep(min(0.5, remaining))


if __name__ == "__main__":
    main()
PY
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
  while IFS= read -r replacement_pid; do
    [ -n "$replacement_pid" ] || continue
    if [ "$replacement_pid" != "${APP_PID:-}" ]; then
      kill "$replacement_pid" 2>/dev/null || true
    fi
  done < <(pgrep -f -- "$APP_EXECUTABLE" 2>/dev/null || true)
  native_stderr_diagnostics cleanup
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

LAUNCH_DIAG_START="$(date '+%Y-%m-%d %H:%M:%S')"
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

    if REPLACEMENT_PID="$(find_live_app_pid "$APP_PID")"; then
      echo "Mac Catalyst smoke process replacement detected: $APP_PID -> $REPLACEMENT_PID"
      APP_PID="$REPLACEMENT_PID"
      continue
    fi

    echo "Mac Catalyst smoke exited before producing a sandbox or fallback result file, and no replacement process was found." >&2
    print_diagnostics
    exit 1
  fi
  sleep 1
done

echo "Mac Catalyst analytics accessibility smoke timed out." >&2
print_diagnostics
exit 1
