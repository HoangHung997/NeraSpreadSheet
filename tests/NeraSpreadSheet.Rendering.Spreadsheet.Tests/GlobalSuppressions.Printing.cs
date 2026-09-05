using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Performance",
    "CA1861:Avoid constant arrays as arguments",
    Justification = "Small expected-value arrays keep pagination assertions local and readable; test execution is not performance-sensitive.",
    Scope = "namespaceanddescendants",
    Target = "~N:NeraSpreadSheet.Rendering.Spreadsheet.Tests")]
