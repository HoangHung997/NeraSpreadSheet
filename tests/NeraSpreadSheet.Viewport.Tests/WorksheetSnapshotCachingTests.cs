using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Viewport.Tests;

[TestClass]
public sealed class WorksheetSnapshotCachingTests
{
    [TestMethod]
    public void PureScrollAndSelectionChangesReuseWorksheetSnapshot()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, "Nera");
        var session = new SpreadsheetSession(workbook);
        var engine = new SpreadsheetViewportEngine(session);

        engine.Compose(0d, 0d, 320d, 180d, 0d);
        session.Selection.SetActiveCell(new CellAddress(2, 2));
        engine.Compose(13.25d, 7.75d, 320d, 180d, 0d);

        Assert.AreEqual(1L, engine.SnapshotRefreshCount);
    }

    [TestMethod]
    public void WorksheetOrDimensionMutationRefreshesSnapshot()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        var engine = new SpreadsheetViewportEngine(new SpreadsheetSession(workbook));

        engine.Compose(0d, 0d, 320d, 180d, 0d);
        sheet.SetValue(default, "Changed");
        engine.Compose(0d, 0d, 320d, 180d, 0d);
        sheet.Dimensions.SetColumnWidth(0, 120d);
        engine.Compose(0d, 0d, 320d, 180d, 0d);

        Assert.AreEqual(3L, engine.SnapshotRefreshCount);
    }
}
