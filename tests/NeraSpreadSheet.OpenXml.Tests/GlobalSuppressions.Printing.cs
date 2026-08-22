using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Performance",
    "CA1861:Avoid constant arrays as arguments",
    Justification = "Small expected-value arrays keep the print-settings XML assertions local and readable.",
    Scope = "type",
    Target = "~T:NeraSpreadSheet.OpenXml.Tests.WorksheetExtendedPrintSettingsRoundTripTests")]
