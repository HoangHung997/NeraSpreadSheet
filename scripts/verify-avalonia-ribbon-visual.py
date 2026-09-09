"""Validate exact-head Ribbon matrix evidence; never call this visual approval."""
import argparse
import hashlib
import itertools
import json
import math
from pathlib import Path
import struct


def verify(directory: Path, sha: str) -> None:
    report = json.loads((directory / "manifest.json").read_text(encoding="utf-8"))
    if report.get("schema") != "nera.ribbon.visual.v1" or report.get("sha") != sha:
        raise ValueError("Visual evidence belongs to another schema or SHA")
    if report.get("nativeWindow") is not True or report.get("physicalInputTested") is not False:
        raise ValueError("Incorrect native/physical evidence flags")
    checks = report["checks"]
    if len(checks) != report["assertions"] or len(checks) != len(set(checks)) or len(checks) < 190:
        raise ValueError("Missing or duplicate native postconditions")
    expected = set(itertools.product(
        ("Light", "Dark", "HighContrastLight", "HighContrastDark"),
        (820, 1024, 1536),
        ("home", "insert", "page-layout", "formulas", "data", "review", "view")))
    actual = {(item["theme"], item["width"], item["tab"]) for item in report["layouts"]}
    if actual != expected or len(report["layouts"]) != len(expected):
        raise ValueError("Incomplete layout matrix")
    captures = report["captures"]
    names = [item["name"] for item in captures]
    if len(captures) != 109 or len(set(names)) != len(names):
        raise ValueError("Incomplete capture matrix")
    for item in captures:
        name = item["name"]
        if Path(name).name != name or not name.endswith(".png"):
            raise ValueError("Unsafe or invalid capture path")
        data = (directory / name).read_bytes()
        if data[:8] != b"\x89PNG\r\n\x1a\n" or hashlib.sha256(data).hexdigest() != item["sha256"]:
            raise ValueError("Capture format/hash mismatch: " + name)
        width, height = struct.unpack(">II", data[16:24])
        if (width, height) != (item["width"], item["height"]) or not (0 < width <= 4096 and 0 < height <= 4096):
            raise ValueError("Capture dimensions mismatch: " + name)
        if not math.isfinite(item["rasterScale"]) or item["rasterScale"] <= 0:
            raise ValueError("Invalid raster scale")
    print(json.dumps({"sha": sha, "layouts": len(expected), "captures": len(captures),
                      "postconditions": len(checks), "hashesVerified": True,
                      "visualReviewPerformed": False, "physicalInputTested": False}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    parser.add_argument("sha")
    args = parser.parse_args()
    verify(args.directory, args.sha)
