from pathlib import Path

path = Path(__file__).resolve().parents[2] / "src/NeraSpreadSheet.Rendering.Spreadsheet/SpreadsheetDisplayListComposer.cs"
text = path.read_text(encoding="utf-8")
replacements = {
    "IReadOnlyList<double> pattern)": "double[] pattern)",
    "pattern.Count == 0": "pattern.Length == 0",
    "% pattern.Count;": "% pattern.Length;",
}
for old, new in replacements.items():
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"Expected exactly one generated match for {old!r}, found {count}")
    text = text.replace(old, new, 1)
path.write_text(text, encoding="utf-8")
print("border patterned stroke analyzer fix applied")
