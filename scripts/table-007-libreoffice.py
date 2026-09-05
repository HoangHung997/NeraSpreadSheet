"""Run actual Calc on repository-owned synthetic input; never relabel a ZIP."""

import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile


def digest(data):
    return hashlib.sha256(data).hexdigest().upper()


def main():
    root = Path(__file__).resolve().parent.parent
    seed = root / "tests/NeraSpreadSheet.OpenXml.Tests/Fixtures/TableNative/nera-table.xlsx"
    seed_bytes = seed.read_bytes()
    output = root / "artifacts/table-007-libreoffice"
    output.mkdir(parents=True, exist_ok=True)
    version = subprocess.check_output(["libreoffice", "--version"], text=True).strip()
    packages = subprocess.check_output(
        ["dpkg-query", "-W", "-f=${Package} ${Version}\n", "libreoffice-core", "libreoffice-calc"],
        text=True,
    ).strip()
    with tempfile.TemporaryDirectory(prefix="nera-calc-") as temporary:
        work = Path(temporary)
        native = work / "output"
        native.mkdir()
        subprocess.run(
            ["libreoffice", "-env:UserInstallation=" + (work / "profile").as_uri(),
             "--headless", "--norestore", "--convert-to", "xlsx:Calc MS Excel 2007 XML",
             "--outdir", str(native), str(seed)],
            check=True, timeout=120,
        )
        source = native / seed.name
        raw = source.read_bytes()
        changed = []
        payload_hashes = {}
        target = output / "libreoffice-table.xlsx"
        with zipfile.ZipFile(source) as archive, zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as sanitized:
            application = ET.fromstring(archive.read("docProps/app.xml"))
            producer = next(node.text for node in application if node.tag.endswith("}Application"))
            if not producer or "LibreOffice" not in producer:
                raise RuntimeError("Actual producer metadata does not identify LibreOffice")
            for item in archive.infolist():
                data = archive.read(item.filename)
                if item.filename == "docProps/core.xml":
                    document = ET.fromstring(data)
                    for node in list(document):
                        if node.tag.rsplit("}", 1)[-1] in {"creator", "lastModifiedBy", "created", "modified"}:
                            document.remove(node)
                    data = ET.tostring(document, encoding="utf-8", xml_declaration=True)
                    changed.append(item.filename)
                elif item.filename.endswith((".xml", ".rels")):
                    if re.search(rb"(?:[A-Za-z]:[\\/]|file:///|/home/|/Users/)", data):
                        raise RuntimeError("Unexpected machine path in " + item.filename)
                if data == archive.read(item.filename):
                    payload_hashes[item.filename] = digest(data)
                entry = zipfile.ZipInfo(item.filename, (2000, 1, 1, 0, 0, 0))
                entry.compress_type = zipfile.ZIP_DEFLATED
                sanitized.writestr(entry, data)
        manifest = {
            "schemaVersion": 1, "file": target.name,
            "producer": producer, "version": version, "packages": packages,
            "sourceCommit": os.environ.get("GITHUB_SHA", "local"),
            "runUrl": "https://github.com/" + os.environ.get("GITHUB_REPOSITORY", "local")
                + "/actions/runs/" + os.environ.get("GITHUB_RUN_ID", "local"),
            "seed": seed.name, "seedSha256": digest(seed_bytes),
            "originalSha256": digest(raw), "sha256": digest(target.read_bytes()),
            "sanitizedParts": changed, "unchangedPayloadSha256": payload_hashes,
            "recipe": "scripts/table-007-libreoffice.py; fresh isolated profile, actual Calc XLSX import/export",
            "privacy": "Repository-owned synthetic numbers and labels only; no user document; core author/timestamps removed; ZIP timestamps normalized; all other native payloads unchanged.",
            "license": "Original synthetic repository data; same repository rights; no third-party document content.",
        }
        (output / "libreoffice-provenance.json").write_text(
            json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    if seed.read_bytes() != seed_bytes:
        raise RuntimeError("Producer changed checked-in input")
    print(json.dumps({key: manifest[key] for key in ("producer", "version", "sha256", "runUrl")}))


if __name__ == "__main__":
    main()
