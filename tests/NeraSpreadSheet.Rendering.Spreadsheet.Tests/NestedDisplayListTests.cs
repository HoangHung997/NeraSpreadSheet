using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class NestedDisplayListTests
{
    [TestMethod]
    public void AppendKeepsChildDisplayListByReferenceInsteadOfFlatteningCommands()
    {
        var childBuilder = new DisplayListBuilder();
        childBuilder.FillRectangle(new RectD(0d, 0d, 10d, 10d), ColorRgba.White);
        childBuilder.DrawLine(new PointD(0d, 0d), new PointD(10d, 10d), 1d, ColorRgba.Black);
        var child = childBuilder.Build();

        var parentBuilder = new DisplayListBuilder();
        parentBuilder.PushTranslation(5d, 7d);
        parentBuilder.Append(child);
        parentBuilder.PopTranslation();
        var parent = parentBuilder.Build();

        Assert.AreEqual(3, parent.Count);
        Assert.IsInstanceOfType<PushTranslationCommand>(parent.Commands[0]);
        var nested = parent.Commands[1] as DrawDisplayListCommand;
        Assert.IsNotNull(nested);
        Assert.AreSame(child, nested.DisplayList);
        Assert.IsInstanceOfType<PopTranslationCommand>(parent.Commands[2]);
    }

    [TestMethod]
    public void DrawDisplayListAndAppendShareReferenceSemantics()
    {
        var childBuilder = new DisplayListBuilder();
        childBuilder.DrawText(
            "Nera",
            new RectD(0d, 0d, 80d, 20d),
            new TextStyle("Segoe UI", 12d, 400, ColorRgba.Black));
        var child = childBuilder.Build();

        var builder = new DisplayListBuilder();
        builder.DrawDisplayList(child);
        builder.Append(child);
        var parent = builder.Build();

        Assert.AreEqual(2, parent.Count);
        Assert.IsTrue(parent.Commands.All(command => command is DrawDisplayListCommand));
        Assert.IsTrue(parent.Commands.Cast<DrawDisplayListCommand>().All(command => ReferenceEquals(command.DisplayList, child)));
    }
}
