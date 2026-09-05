using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetChromeDisplayListComposerTests
{
    [TestMethod]
    public void HeadersDisabledReturnsOriginalBodyDisplayList()
    {
        var body = CreateBody();
        var layout = CreateLayout();
        var selection = CreateSelection();

        var result = SpreadsheetChromeDisplayListComposer.Compose(
            body,
            layout,
            selection,
            new SpreadsheetRenderTheme());

        Assert.AreSame(body, result);
    }

    [TestMethod]
    public void HeadersTranslateBodyAndRenderRowAndColumnLabels()
    {
        var body = CreateBody();
        var layout = CreateLayout();
        var theme = new SpreadsheetRenderTheme { ShowHeaders = true };

        var result = SpreadsheetChromeDisplayListComposer.Compose(body, layout, CreateSelection(), theme);

        Assert.IsTrue(result.Commands.OfType<PushTranslationCommand>().Any(command =>
            Math.Abs(command.DeltaX - theme.RowHeaderWidth) < 1e-9 &&
            Math.Abs(command.DeltaY - theme.ColumnHeaderHeight) < 1e-9));
        var texts = result.Commands.OfType<DrawTextCommand>().Select(command => command.Text).ToArray();
        CollectionAssert.Contains(texts, "A");
        CollectionAssert.Contains(texts, "B");
        CollectionAssert.Contains(texts, "1");
        CollectionAssert.Contains(texts, "2");
    }

    [TestMethod]
    public void ColumnLabelsUseNativeSpreadsheetAlphabeticSequence()
    {
        var body = CreateBody();
        var layout = new ViewportLayout(
            0d,
            0d,
            new SizeD(240d, 80d),
            240d,
            80d,
            0d,
            0d,
            [new AxisSlot(0, 0d, 20d)],
            [
                new AxisSlot(0, 0d, 80d),
                new AxisSlot(25, 80d, 80d),
                new AxisSlot(26, 160d, 80d),
            ]);

        var result = SpreadsheetChromeDisplayListComposer.Compose(
            body,
            layout,
            CreateSelection(),
            new SpreadsheetRenderTheme { ShowHeaders = true });

        var texts = result.Commands.OfType<DrawTextCommand>().Select(command => command.Text).ToArray();
        CollectionAssert.Contains(texts, "A");
        CollectionAssert.Contains(texts, "Z");
        CollectionAssert.Contains(texts, "AA");
    }

    [TestMethod]
    public void FrozenHeadersAreRenderedThroughSeparateClips()
    {
        var body = CreateBody();
        var layout = new ViewportLayout(
            13.25d,
            7.75d,
            new SizeD(320d, 200d),
            800d,
            400d,
            80d,
            20d,
            [
                new AxisSlot(0, 0d, 20d, IsFrozen: true),
                new AxisSlot(1, 12.25d, 20d),
            ],
            [
                new AxisSlot(0, 0d, 80d, IsFrozen: true),
                new AxisSlot(1, 66.75d, 80d),
            ]);
        var theme = new SpreadsheetRenderTheme { ShowHeaders = true };

        var result = SpreadsheetChromeDisplayListComposer.Compose(body, layout, CreateSelection(), theme);

        var clips = result.Commands.OfType<PushClipCommand>().Select(command => command.Bounds).ToArray();
        Assert.IsTrue(clips.Any(bounds =>
            Math.Abs(bounds.X - theme.RowHeaderWidth) < 1e-9 &&
            Math.Abs(bounds.Width - layout.FrozenWidth) < 1e-9));
        Assert.IsTrue(clips.Any(bounds =>
            Math.Abs(bounds.Y - theme.ColumnHeaderHeight) < 1e-9 &&
            Math.Abs(bounds.Height - layout.FrozenHeight) < 1e-9));
    }

    private static DisplayList CreateBody()
    {
        var builder = new DisplayListBuilder();
        builder.FillRectangle(new RectD(0d, 0d, 320d, 200d), ColorRgba.White);
        return builder.Build();
    }

    private static ViewportLayout CreateLayout() => new(
        0d,
        0d,
        new SizeD(320d, 200d),
        320d,
        200d,
        0d,
        0d,
        [new AxisSlot(0, 0d, 20d), new AxisSlot(1, 20d, 20d)],
        [new AxisSlot(0, 0d, 80d), new AxisSlot(1, 80d, 80d)]);

    private static SelectionSnapshot CreateSelection() => new(
        default,
        default,
        [new CellRange(default, default)],
        0L);
}
