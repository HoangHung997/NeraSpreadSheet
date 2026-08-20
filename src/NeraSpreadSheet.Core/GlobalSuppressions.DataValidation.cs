using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "SpreadsheetML and spreadsheet UI terminology use 'decimal' as the standard data-validation type name.",
    Scope = "member",
    Target = "~F:NeraSpreadSheet.Core.DataValidationType.Decimal")]
