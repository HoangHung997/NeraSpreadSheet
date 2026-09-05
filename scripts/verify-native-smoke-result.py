"""Read bounded synthetic native smoke markers without printing raw app logs."""

import argparse
import json
from pathlib import Path
import sys


MAX_LOG_BYTES = 2 * 1024 * 1024


class NativeResultError(ValueError):
    """A fixed diagnostic code, never an app log or exception payload."""


def unique_object(pairs):
    value = {}
    for key, item in pairs:
        if key in value:
            raise NativeResultError("duplicate-field")
        value[key] = item
    return value


def reject_nonfinite(value):
    raise NativeResultError("nonfinite-number")


def marker_payloads(text, prefix):
    for line in text.splitlines():
        if prefix:
            offset = line.find(prefix)
            if offset < 0:
                continue
            payload = line[offset + len(prefix):].lstrip()
        else:
            # Android's -v raw logcat transport already filters the exact app tag.
            payload = line.strip()
            if not payload.startswith("{"):
                continue
        yield payload


def parse_result(text, prefix, minimum_frames=2):
    # Legacy analytics probes finish after the creation frame and the native
    # accessibility validation frame. Package consumers impose their own >=3
    # completed-frame/provenance postconditions after this transport gate.
    if type(minimum_frames) is not int or minimum_frames < 2:
        raise NativeResultError("invalid-frame-policy")
    results = []
    decoder = json.JSONDecoder(object_pairs_hook=unique_object, parse_constant=reject_nonfinite)
    for payload in marker_payloads(text, prefix):
        try:
            result, end = decoder.raw_decode(payload)
        except NativeResultError:
            raise
        except json.JSONDecodeError as error:
            # Numeric shape only: diagnose transport framing without exposing
            # JSON keys, app errors, device identities or arbitrary log text.
            start_kind = "object" if payload.startswith("{") else "string" if payload.startswith('"') else "other"
            raise NativeResultError(
                f"malformed-marker chars={len(payload)} json-offset={error.pos} "
                f"start={start_kind} ends-object={int(payload.rstrip().endswith('}'))}") from error
        if payload[end:].strip() or not isinstance(result, dict):
            raise NativeResultError("ambiguous-marker")
        results.append(result)
    if not results:
        return None
    if any(result.get("status") != "success" for result in results):
        raise NativeResultError("failure-or-unknown-status")
    if any(result != results[0] for result in results[1:]):
        raise NativeResultError("conflicting-success-markers")
    frames = results[0].get("frameCount")
    if type(frames) is not int or frames < minimum_frames:
        raise NativeResultError("insufficient-frame-evidence")
    return results[0]


def extract_unified_messages(text, prefix):
    """Decode eventMessage values, without treating compact log formatting as JSON."""
    if not text.strip():
        return ""
    try:
        events = json.loads(text, object_pairs_hook=unique_object, parse_constant=reject_nonfinite)
    except NativeResultError:
        raise
    except json.JSONDecodeError as error:
        raise NativeResultError("malformed-unified-log") from error
    if not isinstance(events, list):
        raise NativeResultError("invalid-unified-log-shape")
    messages = []
    for event in events:
        if not isinstance(event, dict) or not isinstance(event.get("eventMessage"), str):
            raise NativeResultError("invalid-unified-event-shape")
        message = event["eventMessage"]
        if prefix in message:
            messages.append(message)
    return "\n".join(messages)


def has_complete_success_header(payload, expected):
    """Require complete root status/frame fields before reconciling a prefix."""
    decoder = json.JSONDecoder(object_pairs_hook=unique_object, parse_constant=reject_nonfinite)
    position = 1
    fields = {}
    try:
        while position < len(payload):
            while position < len(payload) and payload[position].isspace():
                position += 1
            key, position = decoder.raw_decode(payload, position)
            if not isinstance(key, str) or key in fields:
                return False
            while position < len(payload) and payload[position].isspace():
                position += 1
            if position >= len(payload) or payload[position] != ":":
                return False
            position += 1
            while position < len(payload) and payload[position].isspace():
                position += 1
            value, position = decoder.raw_decode(payload, position)
            while position < len(payload) and payload[position].isspace():
                position += 1
            if position >= len(payload) or payload[position] not in ",}":
                return False
            fields[key] = value
            if "status" in fields and "frameCount" in fields:
                return fields["status"] == expected["status"] and fields["frameCount"] == expected["frameCount"]
            if payload[position] != ",":
                return False
            position += 1
    except (ValueError, json.JSONDecodeError):
        return False
    return False


def reconcile_unified_duplicates(console, unified, prefix, minimum_frames=2):
    """Require full console evidence before recognizing a truncated duplicate.

    An incomplete unified event is never an independent success result. Only
    an exact strict text prefix of an already validated full console payload
    may be replaced by that same payload; mismatches and failures still reject.
    """
    complete_result = parse_result(console, prefix, minimum_frames)
    complete_payloads = list(marker_payloads(console, prefix))
    messages = []
    for payload in marker_payloads(unified, prefix):
        try:
            parse_result(prefix + payload, prefix, minimum_frames)
        except NativeResultError as error:
            matches = [full for full in complete_payloads if len(payload) < len(full) and full.startswith(payload)]
            if (not str(error).startswith("malformed-marker") or not matches or
                    complete_result is None or not has_complete_success_header(payload, complete_result)):
                raise
            payload = matches[0]
        messages.append(prefix + payload)
    normalized = "\n".join(messages)
    # Do not hide a complete different success or failure in another event.
    parse_result(console + "\n" + normalized, prefix, minimum_frames)
    return normalized


def read_result(paths, prefix, minimum_frames=2, json_paths=()):
    texts = []
    console = ""
    for path, is_json in [(path, False) for path in paths] + [(path, True) for path in json_paths]:
        source = Path(path)
        if not source.is_file():
            continue
        if source.stat().st_size > MAX_LOG_BYTES:
            raise NativeResultError("oversized-log")
        text = source.read_text(encoding="utf-8", errors="replace")
        if is_json:
            text = reconcile_unified_duplicates(console, extract_unified_messages(text, prefix), prefix, minimum_frames)
        else:
            console += text + "\n"
        texts.append(text)
    # Report the failing stream index, not its filesystem path. The final
    # combined pass still rejects contradictory markers across streams.
    for index, text in enumerate(texts):
        try:
            parse_result(text, prefix, minimum_frames)
        except NativeResultError as error:
            raise NativeResultError(f"stream={index} {error}") from error
    return parse_result("\n".join(texts), prefix, minimum_frames)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--log", action="append", required=True)
    parser.add_argument("--json-log", action="append", default=[])
    parser.add_argument("--prefix", default="")
    parser.add_argument("--output", required=True)
    parser.add_argument("--minimum-frames", type=int, default=2)
    args = parser.parse_args()
    try:
        result = read_result(args.log, args.prefix, args.minimum_frames, args.json_log)
        if result is None:
            return 2  # Pending, not a successful result or a retry of the app.
        output = Path(args.output)
        if output.exists():
            raise NativeResultError("existing-result-evidence")
        with output.open("x", encoding="utf-8") as stream:
            json.dump(result, stream, ensure_ascii=False, separators=(",", ":"))
            stream.write("\n")
        print(f"Native result marker verified; frames={result['frameCount']}.")
        return 0
    except NativeResultError as error:
        print(f"Native result rejected: {error}.", file=sys.stderr)
        return 1
    except (OSError, ValueError):
        # Exception/log contents can include device identity, paths or workbook data.
        print("Native result rejected: evidence-io-or-encoding.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
