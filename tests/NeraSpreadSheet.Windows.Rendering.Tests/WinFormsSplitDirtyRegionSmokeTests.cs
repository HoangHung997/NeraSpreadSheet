using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.WinForms;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WinFormsSplitDirtyRegionSmokeTests
{
    [TestMethod]
    [Timeout(90_000)]
    public void CellChangesUsePartialInvalidationExceptForFlipDiscardSwapChain()
    {
        RunSta(() =>
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();

            var workbook = new Workbook();
            var worksheet = workbook.Worksheets[0];
            worksheet.SetValue(new CellAddress(200, 120), "extent");
            var session = new SpreadsheetSession(workbook);
            using var form = new Form
            {
                Width = 1000,
                Height = 760,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(-30000, -30000),
            };
            using var control = new NeraSpreadsheetControl
            {
                Dock = DockStyle.Fill,
                Session = session,
                RenderingBackend = WinFormsRenderingBackend.GdiPlus,
            };
            form.Controls.Add(control);
            form.Show();
            Application.DoEvents();

            using var controller = control.EnableSplitPanes(
                SpreadsheetSplitPaneMode.Both);
            controller.SetSplit(340d, 230d);
            controller.ScrollPaneTo(SpreadsheetPaneId.TopRight, 160d, 0d);
            controller.ScrollPaneTo(SpreadsheetPaneId.BottomLeft, 0d, 100d);
            controller.ScrollPaneTo(SpreadsheetPaneId.BottomRight, 160d, 100d);
            controller.RenderNow();
            var surface = control.Controls
                .Cast<Control>()
                .Single(candidate =>
                    candidate.GetType().Name == "NeraSpreadsheetSplitSurface");
            var invalidated = new List<System.Drawing.Rectangle>();
            surface.Invalidated += (_, args) => invalidated.Add(args.InvalidRect);
            var target = new CellAddress(6, 3);

            var partialBefore = GetLong(surface, "PartialDirtyInvalidationCount");
            var fullBefore = GetLong(surface, "FullDirtyInvalidationCount");
            worksheet.SetValue(target, "gdi-dirty");

            Assert.AreEqual(
                partialBefore + 1L,
                GetLong(surface, "PartialDirtyInvalidationCount"));
            Assert.AreEqual(
                fullBefore,
                GetLong(surface, "FullDirtyInvalidationCount"));
            Assert.AreEqual(4, GetInt(surface, "LastDirtyRegionCount"));
            Assert.IsTrue(invalidated.Count >= 4);
            Assert.IsFalse(invalidated.Any(rectangle => rectangle == surface.ClientRectangle));
            Application.DoEvents();

            control.RenderingBackend = WinFormsRenderingBackend.Direct2D;
            controller.RenderNow();
            invalidated.Clear();
            partialBefore = GetLong(surface, "PartialDirtyInvalidationCount");
            fullBefore = GetLong(surface, "FullDirtyInvalidationCount");
            worksheet.SetValue(target, "direct2d-dirty");
            Application.DoEvents();

            Assert.AreEqual(
                partialBefore + 1L,
                GetLong(surface, "PartialDirtyInvalidationCount"));
            Assert.AreEqual(
                fullBefore,
                GetLong(surface, "FullDirtyInvalidationCount"));
            Assert.AreEqual(4, GetInt(surface, "LastDirtyRegionCount"));
            Assert.IsNotNull(controller.Direct2DDiagnostics);

            control.RenderingBackend = WinFormsRenderingBackend.Direct2DSwapChain;
            controller.RenderNow();
            partialBefore = GetLong(surface, "PartialDirtyInvalidationCount");
            fullBefore = GetLong(surface, "FullDirtyInvalidationCount");
            worksheet.SetValue(target, "swapchain-dirty");

            Assert.AreEqual(
                partialBefore,
                GetLong(surface, "PartialDirtyInvalidationCount"));
            Assert.AreEqual(
                fullBefore + 1L,
                GetLong(surface, "FullDirtyInvalidationCount"));
            Assert.AreEqual(0, GetInt(surface, "LastDirtyRegionCount"));
            Application.DoEvents();

            form.Close();
            Application.DoEvents();
        });
    }

    private static long GetLong(Control surface, string propertyName) =>
        (long)(GetProperty(surface, propertyName).GetValue(surface) ?? 0L);

    private static int GetInt(Control surface, string propertyName) =>
        (int)(GetProperty(surface, propertyName).GetValue(surface) ?? 0);

    private static PropertyInfo GetProperty(Control surface, string propertyName)
    {
        var property = surface.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(property);
        return property;
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(
            thread.Join(TimeSpan.FromSeconds(60)),
            "The WinForms dirty-region STA thread timed out.");
        if (failure is not null)
        {
            throw new AssertFailedException(
                "WinForms split dirty-region smoke failed.",
                failure);
        }
    }
}
