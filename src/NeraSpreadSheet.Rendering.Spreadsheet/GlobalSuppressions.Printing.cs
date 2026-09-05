using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Usage",
    "CA2208:Instantiate argument exceptions correctly",
    Justification =
        "The planner validates named properties of cohesive setup/context objects " +
        "and intentionally reports the invalid property.",
    Scope = "type",
    Target =
        "~T:NeraSpreadSheet.Rendering.Spreadsheet.SpreadsheetPageLayoutPlanner")]

[assembly: SuppressMessage(
    "Usage",
    "CA2208:Instantiate argument exceptions correctly",
    Justification =
        "The preview engine validates named properties of its options object and " +
        "intentionally reports the invalid property.",
    Scope = "type",
    Target =
        "~T:NeraSpreadSheet.Rendering.Spreadsheet.SpreadsheetPrintPreviewLayoutEngine")]

[assembly: SuppressMessage(
    "Usage",
    "CA2208:Instantiate argument exceptions correctly",
    Justification =
        "The formatter validates named properties of the supplied page context " +
        "and intentionally reports the invalid property.",
    Scope = "type",
    Target =
        "~T:NeraSpreadSheet.Rendering.Spreadsheet.SpreadsheetHeaderFooterFormatter")]

[assembly: SuppressMessage(
    "Performance",
    "CA1859:Use concrete types when possible for improved performance",
    Justification =
        "The page-grid helper intentionally consumes a read-only list contract; " +
        "its bounded per-page use avoids a measurable allocation or dispatch cost.",
    Scope = "type",
    Target =
        "~T:NeraSpreadSheet.Rendering.Spreadsheet.SpreadsheetPrintPageGridBuilder")]
