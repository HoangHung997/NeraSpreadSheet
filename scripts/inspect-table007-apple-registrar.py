#!/usr/bin/env python3
"""Read bounded generated Apple metadata; never build or change the smoke app."""

import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys


MAX_FILE_BYTES = 8 * 1024 * 1024
MAX_TOTAL_BYTES = 64 * 1024 * 1024
MAX_OUTPUT_BYTES = 32 * 1024
PROPERTIES = (
    "TargetFramework", "RuntimeIdentifier", "Configuration", "NETCoreSdkVersion",
    "TargetPlatformIdentifier", "TargetPlatformVersion", "SupportedOSPlatformVersion",
    "Registrar", "MtouchLink", "TrimMode", "PublishTrimmed", "UseInterpreter",
    "MtouchInterpreter", "EnableAssemblyILStripping", "MauiVersion",
)
CLASS = re.compile(r"@interface\s+([A-Za-z_][A-Za-z_0-9]*)\s*:\s*([A-Za-z_][A-Za-z_0-9]*)\b([^@]*?)@end", re.S)
RELEVANT = re.compile(r"(?:Nera[A-Za-z_0-9]*CellTextView|(?:Microsoft_Maui_Platform_)?MauiTextView|UITextView|NSTextInputContext)\Z")
SIGNATURE = re.compile(r"[+-]\s*\([A-Za-z_][A-Za-z_0-9 <>*]*\)\s*[A-Za-z_][A-Za-z_0-9\s:()<>*]*;\Z")
SAFE_VALUE = re.compile(r"[A-Za-z0-9_.+;,*=-]{1,160}\Z")


def generated_metadata(raw):
    """Return declarations only, never arbitrary strings, bodies or comments."""
    text = raw.decode("utf-8", errors="replace")
    declarations = list(CLASS.finditer(text))
    classes = []
    for match in declarations:
        name, base, body = match.groups()
        if not RELEVANT.fullmatch(name):
            continue
        methods = []
        for line in body.splitlines():
            line = line.strip()
            if len(line) <= 400 and SIGNATURE.fullmatch(line):
                methods.append(" ".join(line.split()))
        classes.append({"name": name, "base": base, "signatures": methods[:24]})
    return {"classDeclarationCount": len(declarations), "classes": classes[:16]}


def response_flags(raw):
    # Read only generated response-file options, never arbitrary source comments.
    flags = re.findall(
        r"(?:^|\s)(--(?:registrar|linkmode)(?:=|:|\s+)(?:static|dynamic|managed-static|all|sdkonly|none))(?=\s|$)",
        raw.decode("utf-8", errors="replace"))
    return sorted(set(" ".join(flag.split()) for flag in flags))[:16]


def safe_run(arguments, cwd):
    try:
        result = subprocess.run(arguments, cwd=cwd, capture_output=True, text=True,
                                encoding="utf-8", errors="replace", timeout=30, check=False)
        if result.returncode == 0 and len(result.stdout) <= 128 * 1024:
            return result.stdout
    except (OSError, subprocess.TimeoutExpired):
        pass
    return None


def cohort_report(checkout, rid):
    project = Path("tests/NeraSpreadSheet.Maui.MacCatalyst.AnalyticsSmoke/NeraSpreadSheet.Maui.MacCatalyst.AnalyticsSmoke.csproj")
    report = {"evaluatedProperties": "unavailable", "generatedFiles": [],
              "registrarMetadata": "unavailable", "linkerRootGraph": "unavailable: no root graph requested from build"}
    sha = safe_run(["git", "rev-parse", "HEAD"], checkout)
    report["sourceSha"] = sha.strip() if sha and re.fullmatch(r"[0-9a-f]{40}\s*", sha) else "unavailable"
    output = safe_run(["dotnet", "msbuild", str(project), "-nologo",
                       "-p:Configuration=Release", "-p:TargetFramework=net10.0-maccatalyst",
                       "-p:RuntimeIdentifier=" + rid, "-p:NeraMauiTargetFrameworks=net10.0-maccatalyst",
                       "-getProperty:" + ",".join(PROPERTIES)], checkout)
    if output:
        try:
            properties = json.loads(output).get("Properties", {})
            report["evaluatedProperties"] = {
                name: value if isinstance(value, str) and SAFE_VALUE.fullmatch(value) else "unavailable"
                for name in PROPERTIES for value in [properties.get(name)]}
        except (ValueError, AttributeError):
            pass
    obj = (checkout / project.parent / "obj").resolve()
    total = 0
    files = []
    if obj.is_dir():
        for folder, directories, names in os.walk(obj, followlinks=False):
            directories[:] = sorted(name for name in directories if not (Path(folder) / name).is_symlink())
            for name in sorted(names):
                relative = (Path(folder) / name).relative_to(obj)
                if "net10.0-maccatalyst" not in relative.parts or "Release" not in relative.parts:
                    continue
                if not (("registrar" in name.lower() and Path(name).suffix in (".h", ".m", ".mm", ".c", ".cpp"))
                        or Path(name).suffix == ".rsp"):
                    continue
                file = Path(folder) / name
                if file.is_symlink() or not file.resolve().is_relative_to(obj):
                    continue
                label = relative.as_posix()
                if not re.fullmatch(r"[A-Za-z0-9_./+-]{1,240}", label):
                    continue
                if len(files) >= 48 or total >= MAX_TOTAL_BYTES:
                    report["scanTruncated"] = True
                    break
                size = file.stat().st_size
                if size > MAX_FILE_BYTES or size > MAX_TOTAL_BYTES - total:
                    files.append({"file": label, "metadata": "unavailable: bounded read exceeded"})
                    continue
                with file.open("rb") as stream:
                    raw = stream.read(MAX_FILE_BYTES + 1)
                if len(raw) > MAX_FILE_BYTES:
                    continue
                total += len(raw)
                metadata = generated_metadata(raw)
                if file.suffix == ".rsp":
                    metadata["generatedFlags"] = response_flags(raw)
                files.append({"file": label, "sha256": hashlib.sha256(raw).hexdigest(),
                              "bytes": len(raw), **metadata})
                if metadata["classes"]:
                    report["registrarMetadata"] = "available"
            if report.get("scanTruncated"):
                break
    report["generatedFiles"] = files
    report["effectiveModeEvidence"] = (
        "Only generated flags are effective build evidence; evaluated or absent properties do not prove a mode.")
    return report


def bounded_json(report):
    while True:
        output = json.dumps(report, ensure_ascii=True, sort_keys=True, indent=2)
        if len(output.encode("utf-8")) + 1 <= MAX_OUTPUT_BYTES:
            return output
        report["outputTruncated"] = True
        cohorts = report["cohorts"]
        largest = max(cohorts.values(), key=lambda item: len(json.dumps(item["generatedFiles"])))
        if not largest["generatedFiles"]:
            return json.dumps({"metadata": "unavailable: output budget exceeded"})
        largest["generatedFiles"].pop()


def main():
    checkout = Path.cwd().resolve()
    rid = os.environ.get("NERA_MACCATALYST_SMOKE_RID", "")
    if rid not in ("maccatalyst-arm64", "maccatalyst-x64"):
        raise SystemExit("The shared Mac runtime is unavailable.")
    sdk = safe_run(["dotnet", "--version"], checkout)
    workloads = safe_run(["dotnet", "workload", "list"], checkout)
    report = {"diagnosticOnly": True, "runtime": rid,
              "sdkVersion": sdk.strip() if sdk and SAFE_VALUE.fullmatch(sdk.strip()) else "unavailable",
              "workloads": "unavailable", "cohorts": {}}
    if workloads:
        report["workloads"] = re.findall(
            r"^\s*((?:maui(?:-ios|-maccatalyst)?|ios|maccatalyst))\s+([0-9][0-9A-Za-z_./+-]*)\s+", workloads, re.M) or "unavailable"
    for name, path in (("baseline", checkout / "baseline"), ("candidate", checkout)):
        if path.is_symlink() or not path.resolve().is_relative_to(checkout):
            raise SystemExit("A cohort checkout is outside the diagnostic workspace.")
        report["cohorts"][name] = cohort_report(path, rid)
    print(bounded_json(report))


if __name__ == "__main__":
    main()
