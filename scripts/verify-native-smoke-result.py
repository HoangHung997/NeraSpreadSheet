"""Read bounded synthetic native smoke markers without printing raw app logs."""

import argparse
import json
from pathlib import Path
import sys


MAX_LOG_BYTES = 2 * 1024 * 1024


def unique_object(pairs):
    value = {}
    for key, item in pairs:
        if key in value:
            raise ValueError("Duplicate native result field")
        value[key] = item
    return value


def parse_result(text, prefix):
    results = []
    decoder = json.JSONDecoder(object_pairs_hook=unique_object)
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
        except (ValueError, json.JSONDecodeError) as error:
            raise ValueError("Malformed native result marker") from error
        if payload[end:].strip() or not isinstance(result, dict):
            raise ValueError("Ambiguous native result marker")
        results.append(result)
    if not results:
        return None
    if any(result.get("status") != "success" for result in results):
        raise ValueError("Native result contains failure or unknown status")
    if any(result != results[0] for result in results[1:]):
        raise ValueError("Conflicting native success markers")
    frames = results[0].get("frameCount")
    if type(frames) is not int or frames < 3:
        raise ValueError("Native result lacks completed frame evidence")
    return results[0]


def read_result(paths, prefix):
    texts = []
    for path in paths:
        source = Path(path)
        if not source.is_file():
            continue
        if source.stat().st_size > MAX_LOG_BYTES:
            raise ValueError("Native log exceeds its diagnostic bound")
        texts.append(source.read_text(encoding="utf-8", errors="replace"))
    return parse_result("\n".join(texts), prefix)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--log", action="append", required=True)
    parser.add_argument("--prefix", default="")
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    try:
        result = read_result(args.log, args.prefix)
        if result is None:
            return 2  # Pending, not a successful result or a retry of the app.
        output = Path(args.output)
        if output.exists():
            raise ValueError("Refusing to replace previous native result evidence")
        with output.open("x", encoding="utf-8") as stream:
            json.dump(result, stream, ensure_ascii=False, separators=(",", ":"))
            stream.write("\n")
        print(f"Native result marker verified; frames={result['frameCount']}.")
        return 0
    except (OSError, ValueError):
        # Exception/log contents can include device identity, paths or workbook data.
        print("Native result rejected: malformed, conflicting, failed, oversized or unsafe evidence.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
