import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";

const repositoryRoot = process.argv[2];
const moduleRoot = process.argv[3] ?? process.env.NERA_NODE_MODULES;

if (!repositoryRoot || !moduleRoot) {
  throw new Error(
    "Usage: node render-ribbon-icon-contact-sheet.mjs <nera-repo> <node_modules>",
  );
}

const sharpModule = pathToFileURL(
  path.join(moduleRoot, "sharp", "dist", "index.mjs"),
).href;
const { default: sharp } = await import(sharpModule);

const iconRoot = path.join(repositoryRoot, "src", "NeraSpreadSheet.Iconography");
const manifest = JSON.parse(
  fs.readFileSync(path.join(iconRoot, "icons.catalog.json"), "utf8"),
);
const outputRoot = path.join(repositoryRoot, "docs", "assets", "ribbon-iconography");
fs.mkdirSync(outputRoot, { recursive: true });

const escapeXml = (value) =>
  value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&apos;");

const entries = [...manifest.icons].sort(
  (left, right) =>
    left.category.localeCompare(right.category) || left.key.localeCompare(right.key),
);
const columns = 6;
const cellWidth = 300;
const cellHeight = 72;
const headerHeight = 72;
const width = columns * cellWidth;
const height = headerHeight + Math.ceil(entries.length / columns) * cellHeight;

for (const theme of ["light", "dark", "high_contrast_light", "high_contrast_dark"]) {
  const dark = theme === "dark" || theme === "high_contrast_dark";
  const background = dark ? "#171717" : "#ffffff";
  const tile = dark ? "#242424" : "#f7f7f7";
  const primary = dark ? "#ffffff" : "#161616";
  const secondary = dark ? "#b8b8b8" : "#606060";
  const border = dark ? "#444444" : "#dedede";
  const composites = [];

  const header = Buffer.from(
    `<svg width="${width}" height="${headerHeight}" xmlns="http://www.w3.org/2000/svg">` +
      `<rect width="100%" height="100%" fill="${background}"/>` +
      `<text x="24" y="31" font-family="Segoe UI, Arial" font-size="22" font-weight="600" fill="${primary}">NeraSpreadSheet Ribbon icons</text>` +
      `<text x="24" y="54" font-family="Segoe UI, Arial" font-size="13" fill="${secondary}">${entries.length} semantic keys · ${theme.replaceAll("_", " ")} · 32 px preview</text>` +
      `</svg>`,
  );
  composites.push({ input: header, left: 0, top: 0 });

  for (const [index, icon] of entries.entries()) {
    const column = index % columns;
    const row = Math.floor(index / columns);
    const left = column * cellWidth;
    const top = headerHeight + row * cellHeight;
    const label = Buffer.from(
      `<svg width="${cellWidth}" height="${cellHeight}" xmlns="http://www.w3.org/2000/svg">` +
        `<rect x="0.5" y="0.5" width="${cellWidth - 1}" height="${cellHeight - 1}" rx="6" fill="${tile}" stroke="${border}"/>` +
        `<text x="58" y="30" font-family="Segoe UI, Arial" font-size="13" font-weight="600" fill="${primary}">${escapeXml(icon.key)}</text>` +
        `<text x="58" y="50" font-family="Segoe UI, Arial" font-size="11" fill="${secondary}">${escapeXml(icon.category)} · ${escapeXml(icon.source)}</text>` +
        `</svg>`,
    );
    composites.push({ input: label, left, top });
    composites.push({
      input: path.join(
        iconRoot,
        "Assets",
        "Generated",
        `${theme}_32_${icon.asset}.png`,
      ),
      left: left + 14,
      top: top + 20,
    });
  }

  await sharp({
    create: {
      width,
      height,
      channels: 4,
      background,
    },
  })
    .composite(composites)
    .png()
    .toFile(path.join(outputRoot, `ribbon-icons-${theme}.png`));
}

console.log(`Rendered ${entries.length} semantic keys in four theme contact sheets.`);
