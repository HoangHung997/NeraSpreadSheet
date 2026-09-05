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


def parse_result(text, prefix, minimum_frames=2):
    # Legacy analytics probes finish after the creation frame and the native
    # accessibility validation frame. Package consumers impose their own >=3
    # completed-frame/provenance postconditions after this transport gate.
    if type(minimum_frames) is not int or minimum_frames < 2:
        raise NativeResultError("invalid-frame-policy")
    results = []
    decoder = json.JSONDecoder(object_pairs_hook=unique_object, parse_constant=reject_nonfinite)
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


def read_result(paths, prefix, minimum_frames=2):
    texts = []
    for path in paths:
        source = Path(path)
        if not source.is_file():
            continue
        if source.stat().st_size > MAX_LOG_BYTES:
            raise NativeResultError("oversized-log")
        texts.append(source.read_text(encoding="utf-8", errors="replace"))
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
    parser.add_argument("--prefix", default="")
    parser.add_argument("--output", required=True)
    parser.add_argument("--minimum-frames", type=int, default=2)
    args = parser.parse_args()
    try:
        result = read_result(args.log, args.prefix, args.minimum_frames)
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
