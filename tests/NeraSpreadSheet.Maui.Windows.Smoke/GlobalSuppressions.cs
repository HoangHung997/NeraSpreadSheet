using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Performance",
    "CA1822",
    Justification =
        "This smoke-stage predicate intentionally remains an instance-shaped orchestration helper beside the other stage validators; it is exercised only through the loaded SmokePage runtime sequence.",
    Scope = "member",
    Target =
        "~M:NeraSpreadSheet.Maui.Windows.Smoke.SmokePage.IsWheelSettled(NeraSpreadSheet.Maui.NeraSpreadsheetView)")]
