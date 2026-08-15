using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Viewport;

public sealed record SpreadsheetViewportFrame(
    ViewportLayout Layout,
    DisplayList DisplayList,
    long WorksheetVersion,
    long SelectionVersion);
