"""Verify and assemble SDK-generated MAUI package shards without rebuilding SDK code."""
from __future__ import annotations

import argparse
import copy
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import xml.etree.ElementTree as ET
import zipfile

PLATFORMS = ("windows", "android", "ios", "maccatalyst")
NEUTRAL_IDS = frozenset("NeraSpreadSheet." + name for name in (
    "Foundation", "Core", "Commands", "Formulas", "Interaction", "Editing",
    "Iconography", "Bars.Core", "Layout", "Ribbon.Core", "Scrolling",
    "Rendering.Abstractions", "Rendering.Spreadsheet", "Rendering.Skia", "Viewport"))
MAUI_ID = "NeraSpreadSheet.Maui"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def safe_path(name):
    require(isinstance(name, str) and name and "\\" not in name and ":" not in name,
            "Non-canonical package path")
    require(not name.startswith("/") and all(p not in ("", ".", "..") for p in name.split("/")),
            "Package path traversal")
    require(str(PurePosixPath(name)) == name, "Non-canonical package path")
    return name


def local_name(element):
    return element.tag.rsplit("}", 1)[-1]


def child(element, name):
    return next((e for e in element if local_name(e) == name), None)


def text_of(element, name):
    found = child(element, name)
    return found.text if found is not None else None


def xml_key(element):
    attributes = {name: canonical_tfm(value) if name == "targetFramework" else value
                  for name, value in element.attrib.items()}
    name = local_name(element)
    value = (element.text or "").strip()
    elements = list(element)
    if name == "requireLicenseAcceptance":
        require(not attributes and not elements and value in ("false", "true", "0", "1"),
                "Invalid license acceptance metadata")
        value = "true" if value in ("true", "1") else "false"
    keys = [xml_key(e) for e in elements]
    if name == "metadata":
        licenses = [e for e in elements if local_name(e) == "requireLicenseAcceptance"]
        require(len(licenses) <= 1, "Duplicate license acceptance metadata")
        # NuGet ManifestMetadata defaults this Boolean to false; pack may emit it.
        if not licenses:
            keys.append(("requireLicenseAcceptance", (), "false", ()))
    return (name, tuple(sorted(attributes.items())), value, tuple(sorted(keys)))


def canonical_tfm(value):
    value = value.lower().replace(".netcoreapp", "net")
    require(re.fullmatch(r"net10\.0(?:-(?:windows|android|ios|maccatalyst)[0-9]+(?:\.[0-9]+)*)?", value),
            "Missing canonical target platform version")
    return value


def inspect_package(data, version, sha):
    entries = {}
    seen = set()
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        for entry in archive.infolist():
            if entry.is_dir():
                continue
            name = safe_path(entry.filename)
            require(name.casefold() not in seen, "Duplicate package path")
            seen.add(name.casefold())
            require((entry.external_attr >> 16) & 0o170000 != 0o120000, "Package symlink")
            entries[name] = archive.read(entry)
    specs = [name for name in entries if name.endswith(".nuspec")]
    require(len(specs) == 1, "Expected one nuspec")
    root = ET.fromstring(entries[specs[0]])
    metadata = child(root, "metadata")
    require(metadata is not None, "Missing package metadata")
    identity = text_of(metadata, "id")
    require(identity in NEUTRAL_IDS | {MAUI_ID}, "Unexpected SDK package identity")
    repository = child(metadata, "repository")
    require(text_of(metadata, "version") == version and repository is not None
            and repository.get("commit") == sha, "Package source/version mismatch")
    payload = {name: blob for name, blob in entries.items()
               if name != specs[0] and name != "[Content_Types].xml"
               and not name.startswith(("_rels/", "package/"))}
    require(payload and not any(name.endswith((".pdb", ".snupkg")) or name.startswith("src/")
                                for name in payload), "Unexpected debug/source package payload")
    groups = {}
    dependencies = child(metadata, "dependencies")
    require(dependencies is not None, "Missing evaluated dependency groups")
    for group in dependencies:
        require(local_name(group) == "group", "Ungrouped package dependency")
        tfm = canonical_tfm(group.get("targetFramework", ""))
        require(tfm not in groups, "Duplicate dependency group")
        for dependency in group:
            dependency_id = dependency.get("id", "")
            if dependency_id.startswith("NeraSpreadSheet."):
                require(dependency_id in NEUTRAL_IDS and dependency.get("version") in (version, f"[{version}]"),
                        "Foreign SDK dependency version/identity")
        groups[tfm] = group
    folders = {canonical_tfm(name.split("/")[1]) for name in payload
               if name.startswith(("lib/", "ref/")) and name.endswith(".dll")}
    require(folders == groups.keys(), "Library/ref and dependency groups differ")
    require(any(name.endswith("/" + identity + ".dll") for name in payload), "Missing SDK assembly")
    return {"id": identity, "metadata": metadata, "payload": payload, "groups": groups, "sha256": digest(data)}


def merge_maui(packages):
    require(set(packages) == set(PLATFORMS), "Missing or unexpected platform shard")
    result = copy.deepcopy(packages[PLATFORMS[0]]["metadata"])
    dependencies = child(result, "dependencies")
    dependencies.clear()
    result_payload = {}
    common_metadata = None
    all_groups = set()
    # NuGet framework-specific metadata is merged by target; root metadata must agree.
    grouped_names = ("dependencies", "frameworkReferences", "frameworkAssemblies", "references")
    grouped = {}
    for platform in PLATFORMS:
        package = packages[platform]
        require(package["id"] == MAUI_ID and len(package["groups"]) == 1, "Not a single-target MAUI shard")
        tfm = next(iter(package["groups"]))
        require(tfm.startswith("net10.0-" + platform) and tfm not in all_groups, "Wrong or duplicate target shard")
        all_groups.add(tfm)
        current = tuple(sorted(xml_key(e) for e in package["metadata"] if local_name(e) not in grouped_names))
        if common_metadata is None:
            common_metadata = current
        require(current == common_metadata, "Conflicting shared package metadata")
        for element in package["metadata"]:
            name = local_name(element)
            if name not in grouped_names:
                continue
            for group in element:
                require(local_name(group) == "group" and canonical_tfm(group.get("targetFramework", "")) == tfm,
                        "Ungrouped or foreign framework metadata")
                grouped.setdefault(name, []).append(copy.deepcopy(group))
        for name, blob in package["payload"].items():
            if name in result_payload:
                require(result_payload[name] == blob, "Conflicting shared package file")
            require(not any(old.casefold() == name.casefold() and old != name for old in result_payload),
                    "Cross-shard case collision")
            result_payload[name] = blob
    for name in grouped_names:
        existing = child(result, name)
        if existing is not None:
            result.remove(existing)
        if name in grouped:
            namespace = result.tag[:-len("metadata")]
            target = ET.SubElement(result, namespace + name)
            target.extend(grouped[name])
    return result, result_payload


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def write_json(path, value):
    Path(path).write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def verify_producer(manifest, sha, version, sdk):
    require(manifest["schemaVersion"] == 1 and manifest["platform"] in (*PLATFORMS, "neutral"), "Unknown shard schema/platform")
    require(manifest["sourceSha"] == sha and manifest["version"] == version and manifest["sdkVersion"] == sdk,
            "Mixed producer source/version/toolchain")
    require(set(manifest["expectedNeutralIds"]) == NEUTRAL_IDS, "SDK closure requires a packaging decision")


def verify_shard_package(data, record, version, sha):
    require(digest(data) == record["sha256"].lower(), "Shard hash mismatch")
    package = inspect_package(data, version, sha)
    assemblies = record["assemblies"]
    expected = {name: digest(blob) for name, blob in package["payload"].items()
                if name.endswith(".dll") and PurePosixPath(name).name.startswith("NeraSpreadSheet.")}
    require(len(assemblies) == len(expected) and {a["file"]: a["sha256"] for a in assemblies} == expected,
            "Assembly provenance does not cover package bytes")
    require(all(a["informationalVersion"] == version + "+" + sha for a in assemblies),
            "Assembly informational version differs from cohort")
    return package


def prepare(source, output, sha, version, sdk):
    require(re.fullmatch(r"[a-f0-9]{40}", sha), "Invalid source SHA")
    output = Path(output)
    require(not output.exists(), "Refusing existing assembly output")
    manifests = sorted(Path(source).rglob("shard.json"))
    require(len(manifests) == 5, "Expected exactly five producer manifests")
    platforms = {}
    neutral = {}
    producer_digests = {}
    maui_dependencies = {}
    for manifest_path in manifests:
        manifest = read_json(manifest_path)
        platform = manifest["platform"]
        require(platform in (*PLATFORMS, "neutral") and platform not in producer_digests, "Duplicate producer")
        verify_producer(manifest, sha, version, sdk)
        if platform != "neutral":
            selected = tuple(manifest["mauiDependencies"])
            require(selected, "Missing evaluated MAUI dependencies")
            for dependency in selected:
                identity, dependency_version = dependency.split("/")
                require(identity.startswith("Microsoft.Maui."), "Unexpected workload dependency")
                require(identity not in maui_dependencies or maui_dependencies[identity] == dependency_version,
                        "Different MAUI dependency versions across producers")
                maui_dependencies[identity] = dependency_version
        producer_digests[platform] = digest(manifest_path.read_bytes())
        records = manifest["packages"]
        require(len(records) == (15 if platform == "neutral" else 1), "Incomplete producer package set")
        for record in records:
            name = safe_path(record["file"])
            require("/" not in name and name.endswith(".nupkg"), "Unsafe shard package path")
            data = (manifest_path.parent / name).read_bytes()
            package = verify_shard_package(data, record, version, sha)
            if platform == "neutral":
                require(package["id"] in NEUTRAL_IDS and package["id"] not in neutral
                        and set(package["groups"]) == {"net10.0"}, "Invalid neutral closure")
                neutral[package["id"]] = (name, data)
            else:
                platforms[platform] = package
    require(set(neutral) == NEUTRAL_IDS, "Incomplete neutral closure")
    metadata, payload = merge_maui(platforms)
    (output / "feed").mkdir(parents=True)
    for name, data in neutral.values():
        (output / "feed" / name).write_bytes(data)
    for name, data in payload.items():
        destination = output / "payload" / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(data)
    root = ET.Element("package")
    # Strip namespace only in the generated document; preserve all attributes/groups.
    for element in metadata.iter():
        element.tag = local_name(element)
    root.append(metadata)
    files = ET.SubElement(root, "files")
    for name in sorted(payload):
        ET.SubElement(files, "file", {"src": name, "target": str(PurePosixPath(name).parent)})
    ET.ElementTree(root).write(output / "Maui.nuspec", encoding="utf-8", xml_declaration=True)
    write_json(output / "assembly-inputs.json", {
        "schemaVersion": 1, "sourceSha": sha, "version": version, "sdkVersion": sdk,
        "producerManifestHashes": producer_digests,
        "targetFrameworks": sorted(tfm for p in platforms.values() for tfm in p["groups"]),
        "payloadHashes": {name: digest(data) for name, data in sorted(payload.items())},
        "metadataHash": digest(json.dumps(xml_key(metadata)).encode()),
        "metadataComponents": {local_name(element): xml_key(element) for element in metadata},
        "mauiDependencies": maui_dependencies,
    })


def feed_identity(records, maui_dependencies):
    require("Microsoft.Maui.Controls" in maui_dependencies and all(
        name.startswith("Microsoft.Maui.") and re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?", version)
        for name, version in maui_dependencies.items()), "Invalid evaluated MAUI dependency cohort")
    return digest(json.dumps({"packages": records, "mauiDependencies": maui_dependencies},
                             sort_keys=True, separators=(",", ":")).encode())


def finalize(output):
    output = Path(output)
    inputs = read_json(output / "assembly-inputs.json")
    packages = list((output / "feed").glob("*.nupkg"))
    require(len(packages) == 16, "Canonical feed must contain one complete SDK closure")
    records = []
    identities = set()
    for path in sorted(packages):
        package = inspect_package(path.read_bytes(), inputs["version"], inputs["sourceSha"])
        require(package["id"] not in identities, "Duplicate canonical package identity")
        identities.add(package["id"])
        if package["id"] == MAUI_ID:
            require(set(package["groups"]) == set(inputs["targetFrameworks"]), "Final package missing target groups")
            require({p: digest(b) for p, b in package["payload"].items()} == inputs["payloadHashes"],
                    "Final pack changed verified shard payload")
            if digest(json.dumps(xml_key(package["metadata"])).encode()) != inputs["metadataHash"]:
                actual = {local_name(element): xml_key(element) for element in package["metadata"]}
                expected = inputs["metadataComponents"]
                changed = [name for name in sorted(set(actual) | set(expected))
                           if json.dumps(actual.get(name)) != json.dumps(expected.get(name))]
                print("Changed metadata components: " + ", ".join(changed), flush=True)
                # Only generated framework/dependency identifiers and ranges, never paths/logs.
                for name in ("dependencies", "frameworkReferences", "references"):
                    if name in changed:
                        print(json.dumps({"component": name, "expected": expected.get(name), "actual": actual.get(name)}), flush=True)
                raise ValueError("Final pack changed verified dependency/framework metadata")
        records.append({"id": package["id"], "file": path.name, "sha256": package["sha256"],
                        "frameworks": sorted(package["groups"])})
    require(identities == NEUTRAL_IDS | {MAUI_ID}, "Missing canonical SDK identity")
    # Stable feed identity includes package bytes, not ZIP timestamps or directory paths.
    feed_hash = feed_identity(records, inputs["mauiDependencies"])
    write_json(output / "feed-manifest.json", {
        "schemaVersion": 1, "status": "package-verified-runtime-open",
        "sourceSha": inputs["sourceSha"], "version": inputs["version"], "sdkVersion": inputs["sdkVersion"],
        "feedHash": feed_hash, "packages": records, "targetFrameworks": inputs["targetFrameworks"],
        "mauiDependencies": inputs["mauiDependencies"],
        "producerManifestHashes": inputs["producerManifestHashes"], "runtimeAcceptance": "OPEN",
    })


def verify_feed(source, sha, version):
    source = Path(source)
    manifest = read_json(source / "feed-manifest.json")
    require(manifest["sourceSha"] == sha and manifest["version"] == version,
            "Canonical feed source/version mismatch")
    records = manifest["packages"]
    require(len(records) == 16 and {record["id"] for record in records} == NEUTRAL_IDS | {MAUI_ID},
            "Incomplete canonical feed")
    require(feed_identity(records, manifest["mauiDependencies"]) == manifest["feedHash"],
            "Canonical feed identity mismatch")
    for record in records:
        name = safe_path(record["file"])
        require("/" not in name, "Unsafe feed package path")
        data = (source / "feed" / name).read_bytes()
        require(digest(data) == record["sha256"], "Canonical package hash mismatch")
        package = inspect_package(data, version, sha)
        require(package["id"] == record["id"] and sorted(package["groups"]) == record["frameworks"],
                "Canonical package metadata mismatch")
        if package["id"] == MAUI_ID:
            require(len(package["groups"]) == 4 and all(any(tfm.startswith("net10.0-" + platform)
                    for tfm in package["groups"]) for platform in PLATFORMS), "Canonical MAUI target missing")
    return manifest


def verify_assets(assets, manifest, cache, platform, rid):
    require(platform in PLATFORMS, "Unexpected consumer platform")
    require(all(library["type"] == "package" for library in assets["libraries"].values()),
            "Consumer contains source ProjectReference")
    require({str(Path(path).resolve()) for path in assets["packageFolders"]} == {str(Path(cache).resolve())},
            "Consumer used a foreign package cache")
    version = manifest["version"]
    sdk_libraries = {identity for identity in assets["libraries"] if identity.startswith("NeraSpreadSheet.")}
    require(sdk_libraries == {record["id"] + "/" + version for record in manifest["packages"]},
            "Consumer resolved a foreign or incomplete SDK closure")
    require("Microsoft.Maui.Controls/" + manifest["mauiDependencies"]["Microsoft.Maui.Controls"] in assets["libraries"],
            "Consumer MAUI Controls differs from the producer cohort")
    rid_targets = [(target, libraries) for target, libraries in assets["targets"].items() if target.endswith("/" + rid)]
    require(len(rid_targets) == 1, "Missing or ambiguous consumer RID target")
    target, libraries = rid_targets[0]
    expected_tfms = {tfm for tfm in manifest["targetFrameworks"] if tfm.startswith("net10.0-" + platform)}
    require(len(expected_tfms) == 1, "Ambiguous packaged target framework")
    maui = libraries[MAUI_ID + "/" + version]
    selected = {}
    for kind in ("compile", "runtime"):
        paths = [path for path in maui.get(kind, {}) if path.endswith("/NeraSpreadSheet.Maui.dll")]
        require(len(paths) == 1 and paths[0].split("/")[1] in expected_tfms,
                "Consumer selected the wrong MAUI framework asset")
        selected[kind] = paths[0]
    return {"target": target, "sdkPackages": sorted(sdk_libraries), "mauiAssets": selected,
            "thirdPartyPackages": sorted(identity for identity in assets["libraries"] if not identity.startswith("NeraSpreadSheet."))}


def inspect_consumer(source, assets, cache, platform, rid, sha, version, output):
    manifest = verify_feed(source, sha, version)
    summary = verify_assets(read_json(assets), manifest, cache, platform, rid)
    write_json(output, {"schemaVersion": 1, "sourceSha": sha, "version": version,
                       "feedHash": manifest["feedHash"], "platform": platform, "resolved": summary})


def verify_app_payload(actual, build):
    require(build.get("schemaVersion") == 1 and build.get("platform") in PLATFORMS,
            "Unexpected consumer build schema/platform")
    expected = build["files"]
    for records in (expected, actual):
        require(records, "Empty consumer app payload")
        names = [safe_path(record["file"]) for record in records]
        require(len({name.casefold() for name in names}) == len(names), "Duplicate consumer app path")
        require(all(type(record["bytes"]) is int and record["bytes"] >= 0 and
                    re.fullmatch(r"[a-f0-9]{64}", record["sha256"]) for record in records),
                "Invalid consumer app file identity")
    def identity(records):
        return {record["file"]: (record["bytes"], record["sha256"]) for record in records}
    actual_identity, expected_identity = identity(actual), identity(expected)
    if actual_identity != expected_identity:
        print(json.dumps({"missingFiles": sorted(expected_identity.keys() - actual_identity.keys())[:8],
                          "extraFiles": sorted(actual_identity.keys() - expected_identity.keys())[:8],
                          "changedFiles": sorted(name for name in actual_identity.keys() & expected_identity.keys()
                                                 if actual_identity[name] != expected_identity[name])[:8]}), flush=True)
        raise ValueError("Consumer app payload differs from the verified build")


def capture_app_payload(app, platform):
    path = Path(app)
    require(platform in PLATFORMS, "Unexpected consumer app platform")
    if platform in ("ios", "maccatalyst"):
        require(path.is_dir() and path.suffix == ".app", "Missing consumer app bundle")
        root = path
        paths = list(root.rglob("*"))
    else:
        require(path.is_file() and path.suffix == (".apk" if platform == "android" else ".exe"),
                "Missing consumer application")
        root = path.parent
        paths = [path] if platform == "android" else list(root.rglob("*"))
    actual = []
    for item in paths:
        require(item.resolve().is_relative_to(root.resolve()), "Consumer app file escapes its root")
        if item.is_file():
            actual.append({"file": item.relative_to(root).as_posix(), "bytes": item.stat().st_size,
                           "sha256": digest(item.read_bytes())})
    return sorted(actual, key=lambda record: record["file"])


def verify_app(app, build):
    require(Path(app).name == build["appName"], "Consumer app entry differs from the verified build")
    verify_app_payload(capture_app_payload(app, build["platform"]), build)


def verify_runtime(result, build):
    require(result.get("schema") == "release009-maui-consumer-v1" and result.get("status") == "success",
            "Runtime marker missing or unsuccessful")
    for result_key, build_key in (("sourceSha", "sourceSha"), ("packageVersion", "version"),
                                  ("feedHash", "feedHash"), ("nonce", "nonce"), ("target", "platform")):
        require(result.get(result_key) == build[build_key], "Runtime marker cohort/nonce/target mismatch")
    require(re.fullmatch(r"[a-f0-9]{32}", result["nonce"]) and result.get("frameCount", 0) >= 3,
            "Runtime marker lacks a fresh nonce or native frames")
    details = result.get("details", {})
    require(details.get("publicApiOnly") is True and details.get("controllerEditUndo") is True
            and details.get("actualResize") is True and details.get("filterValues") == 20,
            "Runtime postconditions incomplete")
    gpu = details.get("gpu", {})
    require(gpu.get("FramesFailed") == 0 and gpu.get("FramesCompleted", 0) >= 3
            and gpu.get("HasActiveFrame") is False, "Runtime GPU frame lifecycle failed")
    assemblies = details.get("assemblies", [])
    required = {"NeraSpreadSheet." + name for name in ("Maui", "Core", "Editing", "Formulas", "Rendering.Skia", "Ribbon.Core")}
    require(required.issubset({a["name"] for a in assemblies}) and all(
        a["informationalVersion"] == build["version"] + "+" + build["sourceSha"] for a in assemblies),
        "Runtime assembly provenance mismatch")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="command", required=True)
    prepare_parser = sub.add_parser("prepare")
    for argument in ("source", "output", "sha", "version", "sdk"):
        prepare_parser.add_argument("--" + argument, required=True)
    final_parser = sub.add_parser("finalize")
    final_parser.add_argument("--output", required=True)
    feed_parser = sub.add_parser("verify-feed")
    for argument in ("source", "sha", "version"):
        feed_parser.add_argument("--" + argument, required=True)
    consumer_parser = sub.add_parser("inspect-consumer")
    for argument in ("source", "assets", "cache", "platform", "rid", "sha", "version", "output"):
        consumer_parser.add_argument("--" + argument, required=True)
    runtime_parser = sub.add_parser("verify-runtime")
    runtime_parser.add_argument("--result", required=True)
    runtime_parser.add_argument("--build", required=True)
    app_parser = sub.add_parser("verify-app")
    app_parser.add_argument("--app", required=True)
    app_parser.add_argument("--build", required=True)
    capture_parser = sub.add_parser("capture-app")
    for argument in ("app", "platform", "output"):
        capture_parser.add_argument("--" + argument, required=True)
    args = vars(parser.parse_args())
    command = args.pop("command")
    if command == "prepare":
        prepare(**args)
    elif command == "finalize":
        finalize(**args)
    elif command == "verify-feed":
        verify_feed(**args)
    elif command == "inspect-consumer":
        inspect_consumer(**args)
    elif command == "verify-app":
        verify_app(args["app"], read_json(args["build"]))
    elif command == "capture-app":
        write_json(args["output"], capture_app_payload(args["app"], args["platform"]))
    else:
        verify_runtime(read_json(args["result"]), read_json(args["build"]))
