import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { pathToFileURL } from "node:url";

const sourceRoot = process.argv[2];
const repositoryRoot = process.argv[3];
const moduleRoot = process.argv[4] ?? process.env.NERA_NODE_MODULES;

if (!sourceRoot || !repositoryRoot || !moduleRoot) {
  throw new Error(
    "Usage: node generate-ribbon-icons.mjs <fluent-repo> <nera-repo> <node_modules>",
  );
}

const expectedCommit = "5bae3fb7771054c252a54b1d9210e9c03439fa1b";
const commit = execFileSync("git", ["-C", sourceRoot, "rev-parse", "HEAD"], {
  encoding: "utf8",
})
  .trim();
if (commit !== expectedCommit) {
  throw new Error(`Expected Fluent source ${expectedCommit}, found ${commit}`);
}

const sharpModule = pathToFileURL(
  path.join(moduleRoot, "sharp", "dist", "index.mjs"),
).href;
const { default: sharp } = await import(sharpModule);

const group = (category, entries) =>
  entries.map(([key, label, sourceName]) => ({
    key,
    label,
    category,
    source: sourceName.startsWith("nera:") ? "nera" : "fluent",
    sourceName: sourceName.replace(/^nera:/, ""),
  }));

const icons = [
  ...group("File and Quick Access", [
    ["file.new", "New workbook", "Document Add"],
    ["file.open", "Open workbook", "Folder Open"],
    ["file.save", "Save", "Save"],
    ["file.save-as", "Save as", "Save Edit"],
    ["file.print", "Print", "Print"],
    ["file.export.pdf", "Export PDF", "Document PDF"],
    ["file.import.csv", "Import CSV", "Arrow Import"],
    ["file.export.csv", "Export CSV", "Arrow Export"],
    ["edit.undo", "Undo", "Arrow Undo"],
    ["edit.redo", "Redo", "Arrow Redo"],
    ["edit.repeat", "Repeat", "Arrow Repeat All"],
    ["app.search", "Search commands", "Search"],
    ["ribbon.customize", "Customize Ribbon", "Ribbon"],
    ["ribbon.collapse", "Collapse Ribbon", "Chevron Up"],
    ["ribbon.expand", "Expand Ribbon", "Chevron Down"],
    ["ribbon.pin", "Pin Ribbon", "Pin"],
    ["quick-access.add", "Add to Quick Access Toolbar", "Add"],
    ["quick-access.remove", "Remove from Quick Access Toolbar", "Subtract"],
  ]),
  ...group("Clipboard", [
    ["edit.cut", "Cut", "Cut"],
    ["edit.copy", "Copy", "Copy"],
    ["edit.paste", "Paste", "Clipboard Paste"],
    ["edit.paste-special", "Paste special", "Clipboard More"],
    ["format.painter", "Format painter", "Paint Brush"],
    ["cell.clear", "Clear contents", "Text Clear Formatting"],
  ]),
  ...group("Font", [
    ["font.family", "Font family", "Text Font"],
    ["font.size", "Font size", "Text Font Size"],
    ["font.increase", "Increase font size", "Text Font Size"],
    ["font.decrease", "Decrease font size", "Text Font Size Off"],
    ["font.bold", "Bold", "Text Bold"],
    ["font.italic", "Italic", "Text Italic"],
    ["font.underline", "Underline", "Text Underline"],
    ["font.underline-double", "Double underline", "Text Underline Double"],
    ["font.strikethrough", "Strikethrough", "Text Strikethrough"],
    ["font.color", "Font color", "Text Color"],
    ["fill.color", "Fill color", "Paint Bucket"],
    ["format.cells", "Format cells", "Table Cell Edit"],
  ]),
  ...group("Borders", [
    ["border.all", "All borders", "Border All"],
    ["border.bottom", "Bottom border", "Border Bottom"],
    ["border.bottom-double", "Double bottom border", "Border Bottom Double"],
    ["border.bottom-thick", "Thick bottom border", "Border Bottom Thick"],
    ["border.inside", "Inside borders", "Border Inside"],
    ["border.left", "Left border", "Border Left"],
    ["border.none", "No border", "Border None"],
    ["border.outside", "Outside border", "Border Outside"],
    ["border.outside-thick", "Thick outside border", "Border Outside Thick"],
    ["border.right", "Right border", "Border Right"],
    ["border.top", "Top border", "Border Top"],
    ["border.top-bottom", "Top and bottom border", "Border Top Bottom"],
  ]),
  ...group("Alignment", [
    ["align.top", "Top align", "Align Top"],
    ["align.middle", "Middle align", "Align Center Vertical"],
    ["align.bottom", "Bottom align", "Align Bottom"],
    ["align.left", "Align left", "Text Align Left"],
    ["align.center", "Center", "Text Align Center"],
    ["align.right", "Align right", "Text Align Right"],
    ["align.indent-decrease", "Decrease indent", "Text Indent Decrease LTR"],
    ["align.indent-increase", "Increase indent", "Text Indent Increase LTR"],
    ["align.orientation", "Text orientation", "TextBox Rotate 90"],
    ["align.wrap", "Wrap text", "Text Wrap"],
    ["align.merge", "Merge cells", "Table Cells Merge"],
    ["align.merge-center", "Merge and center", "Table Cell Center Arrow Repeat All"],
    ["align.unmerge", "Unmerge cells", "Table Cells Split"],
  ]),
  ...group("Number", [
    ["number.format", "Number format", "Number Symbol"],
    ["number.accounting", "Accounting format", "Money Calculator"],
    ["number.currency", "Currency", "Money"],
    ["number.percent", "Percent", "Text Percent"],
    ["number.comma", "Comma style", "Comma"],
    ["number.decimal-increase", "Increase decimal", "Decimal Arrow Right"],
    ["number.decimal-decrease", "Decrease decimal", "Decimal Arrow Left"],
    ["number.date", "Date format", "Calendar Date"],
    ["number.time", "Time format", "Clock"],
    ["number.fraction", "Fraction format", "nera:number_fraction"],
    ["number.scientific", "Scientific format", "nera:number_scientific"],
  ]),
  ...group("Styles", [
    ["style.conditional", "Conditional formatting", "Table Lightning"],
    ["style.format-as-table", "Format as table", "Table Sparkle"],
    ["style.cell-styles", "Cell styles", "Color Fill Accent"],
  ]),
  ...group("Cells and structure", [
    ["cell.insert", "Insert cells", "Table Cell Add"],
    ["cell.delete", "Delete cells", "Table Cell Cross"],
    ["row.insert", "Insert rows", "Table Insert Row"],
    ["row.delete", "Delete rows", "Table Delete Row"],
    ["column.insert", "Insert columns", "Table Insert Column"],
    ["column.delete", "Delete columns", "Table Delete Column"],
    ["row.hide", "Hide rows", "Table Freeze Row Dismiss"],
    ["row.unhide", "Unhide rows", "Table Freeze Row"],
    ["column.hide", "Hide columns", "Table Freeze Column Dismiss"],
    ["column.unhide", "Unhide columns", "Table Freeze Column"],
    ["row.height", "Row height", "Table Resize Row"],
    ["column.width", "Column width", "Table Resize Column"],
    ["row.autofit", "AutoFit row", "Auto Fit Height"],
    ["column.autofit", "AutoFit column", "Auto Fit Width"],
  ]),
  ...group("Editing", [
    ["formula.autosum", "AutoSum", "AutoSum"],
    ["edit.fill", "Fill", "Color Fill"],
    ["edit.find", "Find", "Search"],
    ["edit.replace", "Replace", "Arrow Swap"],
    ["edit.go-to", "Go to", "Navigation"],
    ["edit.select", "Select", "Select Object"],
    ["formula.recalculate", "Recalculate workbook", "Calculator Arrow Clockwise"],
  ]),
  ...group("Insert", [
    ["insert.table", "Insert table", "Table Add"],
    ["insert.pivot", "Insert pivot table", "Pivot"],
    ["insert.checkbox", "Insert checkbox", "Checkbox Unchecked"],
    ["insert.picture", "Insert picture", "Image Add"],
    ["insert.shape", "Insert shape", "Shapes"],
    ["insert.chart.column", "Column chart", "Data Bar Vertical"],
    ["insert.chart.bar", "Bar chart", "Data Bar Horizontal"],
    ["insert.chart.line", "Line chart", "Data Line"],
    ["insert.chart.pie", "Pie chart", "Data Pie"],
    ["insert.chart.area", "Area chart", "Data Area"],
    ["insert.chart.scatter", "Scatter chart", "Data Scatter"],
    ["insert.chart.histogram", "Histogram", "Data Histogram"],
    ["insert.chart.waterfall", "Waterfall chart", "Data Waterfall"],
    ["insert.chart.combo", "Combo chart", "Chart Multiple"],
    ["insert.chart.pivot", "Pivot chart", "Chart Multiple"],
    ["insert.sparkline", "Sparkline", "Arrow Trending Lines"],
    ["insert.slicer", "Slicer", "Filter Add"],
    ["insert.timeline", "Timeline", "Timeline"],
    ["insert.link", "Link", "Link Add"],
    ["insert.comment", "Comment", "Comment Add"],
    ["insert.text-box", "Text box", "TextBox"],
    ["insert.header-footer", "Header and footer", "Document Header Footer"],
    ["insert.symbol", "Symbol", "Symbols"],
  ]),
  ...group("Page Layout", [
    ["page.theme", "Theme", "Dark Theme"],
    ["page.theme-colors", "Theme colors", "Color"],
    ["page.theme-fonts", "Theme fonts", "Text Font"],
    ["page.theme-effects", "Theme effects", "Text Effects"],
    ["page.margins", "Margins", "Document Margins"],
    ["page.orientation", "Orientation", "Orientation"],
    ["page.size", "Paper size", "Slide Size"],
    ["page.print-area", "Print area", "Document Border Print"],
    ["page.breaks", "Page breaks", "Document Page Break"],
    ["page.background", "Sheet background", "Image"],
    ["page.print-titles", "Print titles", "Document Header"],
    ["page.fit-width", "Fit width", "Arrow Autofit Width"],
    ["page.fit-height", "Fit height", "Arrow Autofit Height"],
    ["page.scale", "Scale", "Scale Fit"],
    ["page.gridlines", "Gridlines", "Grid"],
    ["view.gridlines", "Gridlines", "Grid"],
    ["page.headings", "Headings", "Text Header 1"],
    ["arrange.forward", "Bring forward", "Position Forward"],
    ["arrange.backward", "Send backward", "Position Backward"],
    ["arrange.selection-pane", "Selection pane", "Panel Right"],
    ["arrange.align", "Align objects", "Align Center Horizontal"],
    ["arrange.group", "Group objects", "Group"],
    ["arrange.rotate", "Rotate object", "Arrow Rotate Clockwise"],
  ]),
  ...group("Formulas", [
    ["formula.insert", "Insert function", "Math Formula"],
    ["formula.recent", "Recently used", "History"],
    ["formula.financial", "Financial functions", "Money Calculator"],
    ["formula.logical", "Logical functions", "Branch"],
    ["formula.text", "Text functions", "Text T"],
    ["formula.date-time", "Date and time functions", "Calendar Clock"],
    ["formula.lookup", "Lookup and reference", "Table Search"],
    ["formula.math", "Math and trigonometry", "Math Symbols"],
    ["formula.statistical", "Statistical functions", "Data Histogram"],
    ["formula.engineering", "Engineering functions", "Settings Cog Multiple"],
    ["formula.information", "Information functions", "Info"],
    ["formula.name-manager", "Name manager", "TextBox Settings"],
    ["formula.define-name", "Define name", "Rename"],
    ["formula.use-name", "Use in formula", "Braces Variable"],
    ["formula.create-names", "Create names from selection", "Table Select Range"],
    ["formula.trace-precedents", "Trace precedents", "nera:trace_precedents"],
    ["formula.trace-dependents", "Trace dependents", "nera:trace_dependents"],
    ["formula.remove-arrows", "Remove auditing arrows", "Branch Fork Hint"],
    ["formula.show", "Show formulas", "Eye Lines"],
    ["formula.error-check", "Error checking", "Error Circle"],
    ["formula.evaluate", "Evaluate formula", "Math Formula Sparkle"],
    ["formula.calculation-options", "Calculation options", "Calculator"],
    ["formula.calculate-now", "Calculate now", "Calculator Arrow Clockwise"],
    ["formula.calculate-sheet", "Calculate sheet", "Table Calculator"],
  ]),
  ...group("Data and Filter", [
    ["data.get", "Get data", "Database Arrow Down"],
    ["data.from-csv", "From text or CSV", "Document CSV"],
    ["data.from-web", "From web", "Globe Arrow Up"],
    ["data.from-table", "From table or range", "Document Table Arrow Right"],
    ["data.from-picture", "Data from picture", "Image Table"],
    ["data.recent-sources", "Recent sources", "History"],
    ["data.connections", "Existing connections", "Database Plug Connected"],
    ["data.refresh-all", "Refresh all", "Arrow Sync"],
    ["data.queries", "Queries and connections", "Database Multiple"],
    ["data.workbook-links", "Workbook links", "Document Data Link"],
    ["data.sort-ascending", "Sort ascending", "Arrow Sort Up Lines"],
    ["data.sort-descending", "Sort descending", "Arrow Sort Down Lines"],
    ["data.sort", "Custom sort", "Arrow Sort"],
    ["data.filter", "Filter", "Filter"],
    ["data.filter-clear", "Clear filter", "Filter Dismiss"],
    ["data.filter-reapply", "Reapply filter", "Filter Sync"],
    ["data.filter-advanced", "Advanced filter", "Data Funnel"],
    ["data.filter-text", "Text filters", "nera:filter_text"],
    ["data.filter-number", "Number filters", "nera:filter_number"],
    ["data.filter-date", "Date filters", "nera:filter_date"],
    ["data.filter-color", "Filter by color", "nera:filter_color"],
    ["data.filter-icon", "Filter by icon", "nera:filter_icon"],
    ["data.filter-top", "Top values", "nera:filter_top"],
    ["data.text-columns", "Text to columns", "Text Column Three"],
    ["data.flash-fill", "Flash fill", "Flash"],
    ["data.remove-duplicates", "Remove duplicates", "Table Simple Exclude"],
    ["data.validation", "Data validation", "Table Simple Checkmark"],
    ["data.consolidate", "Consolidate", "Table Stack Below"],
    ["data.what-if", "What-if analysis", "Data Trending"],
    ["data.forecast", "Forecast", "Arrow Trending"],
    ["data.group", "Group", "Group"],
    ["data.ungroup", "Ungroup", "Group Dismiss"],
    ["data.subtotal", "Subtotal", "Table Calculator"],
  ]),
  ...group("Review", [
    ["review.spelling", "Spelling", "Book Search"],
    ["review.thesaurus", "Thesaurus", "Book Open"],
    ["review.statistics", "Workbook statistics", "Data Usage"],
    ["review.performance", "Check performance", "Top Speed"],
    ["review.accessibility", "Check accessibility", "Accessibility Checkmark"],
    ["review.translate", "Translate", "Translate"],
    ["review.changes", "Show changes", "History"],
    ["review.comment-new", "New comment", "Comment Add"],
    ["review.comment-delete", "Delete comment", "Comment Dismiss"],
    ["review.comment-previous", "Previous comment", "Comment Arrow Left"],
    ["review.comment-next", "Next comment", "Comment Arrow Right"],
    ["review.comments", "Show comments", "Comment Multiple"],
    ["review.notes", "Notes", "Comment Note"],
    ["review.protect-sheet", "Protect sheet", "Table Lock"],
    ["review.protect-workbook", "Protect workbook", "Document Lock"],
    ["review.allow-edit-ranges", "Allow edit ranges", "Lock Open"],
  ]),
  ...group("View", [
    ["view.normal", "Normal view", "Table"],
    ["view.page-break", "Page break preview", "Document Page Break"],
    ["view.page-layout", "Page layout view", "Slide Layout"],
    ["view.custom", "Custom views", "Window Settings"],
    ["view.show", "Show options", "Eye"],
    ["view.zoom", "Zoom", "Zoom In"],
    ["view.zoom-100", "Zoom 100 percent", "Document 100"],
    ["view.zoom-selection", "Zoom to selection", "Zoom Fit"],
    ["view.new-window", "New window", "Window New"],
    ["view.arrange", "Arrange windows", "Window Multiple"],
    ["view.freeze-panes", "Freeze panes", "Table Freeze Column And Row"],
    ["view.unfreeze-panes", "Unfreeze panes", "Table Freeze Column And Row Dismiss"],
    ["view.split", "Split", "Table Split"],
    ["view.split-undo", "Undo split change", "Arrow Undo"],
    ["view.split-redo", "Redo split change", "Arrow Redo"],
    ["view.hide-window", "Hide window", "Window Multiple Off"],
    ["view.unhide-window", "Unhide window", "Window Multiple"],
    ["view.side-by-side", "View side by side", "Column Double Compare"],
    ["view.sync-scroll", "Synchronous scrolling", "Arrow Sync"],
    ["view.reset-window", "Reset window position", "Window Multiple Swap"],
    ["view.switch-window", "Switch windows", "Window Multiple Swap"],
    ["view.macros", "Macros", "Code"],
  ]),
  ...group("Table Design", [
    ["table.properties", "Table properties", "Table Settings"],
    ["table.resize", "Resize table", "Resize Table"],
    ["table.summarize-pivot", "Summarize with pivot", "Pivot"],
    ["table.remove-duplicates", "Remove duplicates", "Table Simple Exclude"],
    ["table.convert-range", "Convert to range", "Table Dismiss"],
    ["table.insert-slicer", "Insert slicer", "Filter Add"],
    ["table.export", "Export table", "Table Arrow Up"],
    ["table.refresh", "Refresh table", "Table Arrow Repeat All"],
    ["table.unlink", "Unlink table", "Table Link"],
    ["table.header-row", "Header row", "Table Stack Above"],
    ["table.total-row", "Total row", "Table Bottom Row"],
    ["table.banded-rows", "Banded rows", "nera:table_banded_rows"],
    ["table.first-column", "First column", "Table Stack Left"],
    ["table.last-column", "Last column", "Table Stack Right"],
    ["table.banded-columns", "Banded columns", "nera:table_banded_columns"],
    ["table.filter-buttons", "Filter buttons", "Filter"],
    ["table.styles", "Table styles", "Table Sparkle"],
  ]),
  ...group("Ribbon customization", [
    ["customize.add", "Add command", "Add"],
    ["customize.remove", "Remove command", "Subtract"],
    ["customize.move-up", "Move up", "Arrow Up"],
    ["customize.move-down", "Move down", "Arrow Down"],
    ["customize.move-left", "Move left", "Arrow Left"],
    ["customize.move-right", "Move right", "Arrow Right"],
    ["customize.rename", "Rename", "Rename"],
    ["customize.new-tab", "New tab", "Tab Add"],
    ["customize.new-group", "New group", "Tab Group"],
    ["customize.show", "Show", "Eye"],
    ["customize.hide", "Hide", "Eye Off"],
    ["customize.large", "Large command", "Resize Large"],
    ["customize.small", "Small command", "Resize Small"],
    ["customize.reset", "Reset", "Arrow Counterclockwise"],
    ["customize.import", "Import customization", "Arrow Import"],
    ["customize.export", "Export customization", "Arrow Export"],
    ["customize.more", "More commands", "More Horizontal"],
    ["customize.settings", "Customization settings", "Settings"],
  ]),
];

const duplicateKeys = icons
  .map((icon) => icon.key)
  .filter((key, index, all) => all.indexOf(key) !== index);
if (duplicateKeys.length > 0) {
  throw new Error(`Duplicate icon keys: ${duplicateKeys.join(", ")}`);
}

const customSvgs = {
  number_fraction: customIcon("<path d='M7 6.5a2 2 0 1 1-4 0 2 2 0 0 1 4 0Zm14 11a2 2 0 1 1-4 0 2 2 0 0 1 4 0ZM5 20 19 4'/ >"),
  number_scientific: customIcon("<path d='M4 7h8M4 12h6M4 17h8M15 9l4 6M19 9l-4 6'/ >"),
  trace_precedents: customIcon("<rect x='3' y='4' width='6' height='5' rx='1'/><rect x='15' y='15' width='6' height='5' rx='1'/><path d='M9 6.5h3a4 4 0 0 1 4 4V15m-2-2 2 2 2-2'/ >"),
  trace_dependents: customIcon("<rect x='3' y='15' width='6' height='5' rx='1'/><rect x='15' y='4' width='6' height='5' rx='1'/><path d='M9 17.5h3a4 4 0 0 0 4-4V9m-2 2 2-2 2 2'/ >"),
  filter_text: filterIcon("<path d='M9 10h6M9 14h4'/ >"),
  filter_number: filterIcon("<path d='M9 10h2l-2 4h2M14 10v4M13 11h2'/ >"),
  filter_date: filterIcon("<rect x='8.5' y='9.5' width='7' height='6' rx='1'/><path d='M10.5 8.5v2M13.5 8.5v2M8.5 12h7'/ >"),
  filter_color: filterIcon("<path d='m9 14 3-4 3 4M10 14h4'/ >"),
  filter_icon: filterIcon("<path d='m12 9 1.1 2.2 2.4.4-1.7 1.7.4 2.4-2.2-1.1-2.2 1.1.4-2.4-1.7-1.7 2.4-.4L12 9Z'/ >"),
  filter_top: filterIcon("<path d='M9 14V9m-2 2 2-2 2 2M13 14h3'/ >"),
  table_banded_rows: customIcon("<rect x='3' y='4' width='18' height='16' rx='2'/><path d='M3 8h18M3 12h18M3 16h18M4 9h16v2H4zM4 17h16v2H4z' fill='currentColor' stroke='none'/ >"),
  table_banded_columns: customIcon("<rect x='3' y='4' width='18' height='16' rx='2'/><path d='M8 4v16M13 4v16M18 4v16M9 5h3v14H9zM19 5h1v14h-1z' fill='currentColor' stroke='none'/ >"),
};

function customIcon(body) {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">${body.replaceAll("/ >", "/>")}</svg>`;
}

function filterIcon(body) {
  return customIcon(`<path d='M4 5h16l-6.2 7v5.5l-3.6 1.8V12L4 5Z'/>${body}`);
}

const targetRoot = path.join(repositoryRoot, "src", "NeraSpreadSheet.Iconography");
const svgRoot = path.join(targetRoot, "Assets", "Svg");
const pngRoot = path.join(targetRoot, "Assets", "Generated");
const licenseRoot = path.join(targetRoot, "ThirdPartyLicenses");
fs.rmSync(svgRoot, { force: true, recursive: true });
fs.rmSync(pngRoot, { force: true, recursive: true });
fs.mkdirSync(svgRoot, { recursive: true });
fs.mkdirSync(pngRoot, { recursive: true });
fs.mkdirSync(licenseRoot, { recursive: true });

const assetDefinitions = new Map();
for (const icon of icons) {
  const asset = normalizeAssetName(icon.sourceName);
  icon.asset = asset;
  if (!assetDefinitions.has(asset)) {
    assetDefinitions.set(asset, icon);
  }
}

const themes = new Map([
  ["light", "#1F2937"],
  ["dark", "#F3F4F6"],
  ["high_contrast_light", "#000000"],
  ["high_contrast_dark", "#FFFFFF"],
]);
const sizes = [16, 20, 24, 32, 48];

for (const [asset, definition] of assetDefinitions) {
  let svg;
  if (definition.source === "nera") {
    svg = customSvgs[definition.sourceName];
    if (!svg) {
      throw new Error(`Missing Nera SVG generator '${definition.sourceName}'`);
    }
  } else {
    const sourceDirectory = path.join(
      sourceRoot,
      "assets",
      definition.sourceName,
      "SVG",
    );
    if (!fs.existsSync(sourceDirectory)) {
      throw new Error(`Missing Fluent asset directory '${definition.sourceName}'`);
    }
    const candidates = fs
      .readdirSync(sourceDirectory)
      .filter((name) => name.endsWith("_regular.svg"))
      .sort((left, right) => scoreSvg(left) - scoreSvg(right));
    if (candidates.length === 0) {
      throw new Error(`No regular SVG for '${definition.sourceName}'`);
    }
    svg = fs.readFileSync(path.join(sourceDirectory, candidates[0]), "utf8");
    svg = svg
      .replaceAll(/fill="#[0-9A-Fa-f]{6}"/g, 'fill="currentColor"')
      .replaceAll(/stroke="#[0-9A-Fa-f]{6}"/g, 'stroke="currentColor"');
  }

  fs.writeFileSync(path.join(svgRoot, `${asset}.svg`), svg, "utf8");
  for (const [theme, color] of themes) {
    const themedSvg = svg.replaceAll("currentColor", color);
    for (const size of sizes) {
      await sharp(Buffer.from(themedSvg))
        .resize(size, size, { fit: "contain" })
        .png({ compressionLevel: 9, palette: true })
        .toFile(path.join(pngRoot, `${theme}_${size}_${asset}.png`));
    }
  }
}

const manifest = {
  schema: "neraspreadsheet.icon-catalog",
  version: 1,
  fluentSourceCommit: expectedCommit,
  sizes,
  themes: [...themes.keys()],
  icons: icons.map(({ key, label, category, source, sourceName, asset }) => ({
    key,
    label,
    category,
    source,
    sourceName,
    asset,
  })),
};
fs.writeFileSync(
  path.join(targetRoot, "icons.catalog.json"),
  `${JSON.stringify(manifest, null, 2)}\n`,
  "utf8",
);
fs.copyFileSync(
  path.join(sourceRoot, "LICENSE"),
  path.join(licenseRoot, "Microsoft.FluentUI.SystemIcons.LICENSE.txt"),
);
fs.copyFileSync(
  path.join(sourceRoot, "NOTICE"),
  path.join(licenseRoot, "Microsoft.FluentUI.SystemIcons.NOTICE.txt"),
);

console.log(
  `Generated ${icons.length} semantic keys, ${assetDefinitions.size} unique SVG assets and ${assetDefinitions.size * themes.size * sizes.length} PNG variants.`,
);

function normalizeAssetName(value) {
  return value
    .toLowerCase()
    .replaceAll(/[^a-z0-9]+/g, "_")
    .replaceAll(/^_+|_+$/g, "");
}

function scoreSvg(name) {
  const match = name.match(/_(\d+)_regular\.svg$/);
  const size = match ? Number(match[1]) : 0;
  const priority = [24, 20, 28, 32, 16, 48].indexOf(size);
  return priority < 0 ? 1000 + size : priority;
}
