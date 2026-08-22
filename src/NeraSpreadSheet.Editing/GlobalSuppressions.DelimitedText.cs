using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Usage",
    "CA2208:Instantiate argument exceptions correctly",
    Justification =
        "The serializer validates named properties of an options object and " +
        "intentionally reports the invalid property rather than the container parameter.",
    Scope = "type",
    Target =
        "~T:NeraSpreadSheet.Editing.DelimitedTextWorkbookSerializer")]
