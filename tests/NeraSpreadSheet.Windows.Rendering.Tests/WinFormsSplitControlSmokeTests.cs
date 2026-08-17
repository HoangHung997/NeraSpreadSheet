using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.WinForms;
using DrawingPoint = System.Drawing.Point;
using DrawingSize = System.Drawing.Size;
using WinFormsApplication = System.Windows.Forms.Application;
using WinFormsDockStyle = System.Windows.Forms.DockStyle;
using WinFormsForm = System.Windows.Forms.Form;
using WinFormsFormBorderStyle = System.Windows.Forms.FormBorderStyle;
using WinFormsFormStartPosition = System.Windows.Forms.FormStartPosition;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WinFormsSplitControlSmokeTests
{
    private static readonly TimeSpan StaThreadTimeout = TimeSpan.FromSeconds(60d);

    [TestMethod]
    [Timeout(90_000)]
    public void PublicControlRendersSplitPanesAcrossAllWinFormsBackends()
    {
        RunInSta(() =>
        {
            using var form = CreateOffscreenHost(720, 480);
            using var control = CreateSpreadsheetControl();
            form.Controls.Add(control);
            control.BringToFront();
            WinFormsApplication.DoEvents();

            using var split = control.EnableSplitPanes(SpreadsheetSplitPaneMode.Both);
            split.SetSplit(280.5d, 170.25d);
            split.ScrollPaneTo(
                SpreadsheetPaneId.BottomRight,
                43.75d,
                61.5d);
            RenderAndAssertSplitFrame(split);

            foreach (var backend in Enum.GetValues<WinFormsRenderingBackend>())
            {
                control.RenderingBackend = backend;
                control.SwapChainVSync = false;
                WinFormsApplication.DoEvents();
                split.RenderNow();
                split.RenderNow();
                WinFormsApplication.DoEvents();
                RenderAndAssertSplitFrame(split);
                AssertBackendDiagnostics(split, backend);
            }

            form.ClientSize = new DrawingSize(810, 540);
            WinFormsApplication.DoEvents();
            split.RenderNow();
            var resized = split.LastFrame;
            Assert.IsNotNull(resized);
            Assert.AreEqual(4, resized.Panes.Count);
            Assert.IsTrue(resized.Layout.ViewportSize.Width > 700d);
            Assert.IsTrue(resized.Layout.ViewportSize.Height > 450d);

            var bottomRight = resized.Panes.Single(
                pane => pane.Pane.PaneId == SpreadsheetPaneId.BottomRight);
            var theme = control.RenderTheme;
            var clientX = theme.RowHeaderWidth + bottomRight.Pane.Bounds.Left + 24d;
            var clientY = theme.ColumnHeaderHeight + bottomRight.Pane.Bounds.Top + 24d;
            Assert.IsTrue(split.TryHitTest(clientX, clientY, out var paneId, out _));
            Assert.AreEqual(SpreadsheetPaneId.BottomRight, paneId);

            Assert.IsTrue(control.DisableSplitPanes());
            Assert.IsFalse(control.TryGetSplitPaneController(out _));
        });
    }

    private static NeraSpreadsheetControl CreateSpreadsheetControl()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), "Nera split runtime smoke");
        sheet.SetValue(new CellAddress(40, 12), 42d);
        sheet.SetFormula(new CellAddress(41, 12), "=M41*2");
        var session = new SpreadsheetSession(workbook);
        session.Recalculate();

        return new NeraSpreadsheetControl
        {
            Dock = WinFormsDockStyle.Fill,
            Session = session,
            RenderingBackend = WinFormsRenderingBackend.GdiPlus,
        };
    }

    private static void RenderAndAssertSplitFrame(NeraSpreadsheetSplitController split)
    {
        split.RenderNow();
        WinFormsApplication.DoEvents();

        var frame = split.LastFrame;
        Assert.IsNotNull(frame);
        Assert.IsTrue(frame.Layout.HasVerticalSplit);
        Assert.IsTrue(frame.Layout.HasHorizontalSplit);
        Assert.AreEqual(4, frame.Panes.Count);
        Assert.AreEqual(SpreadsheetPaneId.TopLeft, frame.ActivePane);

        var bottomRight = split.GetPaneScroll(SpreadsheetPaneId.BottomRight);
        Assert.AreEqual(43.75d, bottomRight.X, 0.001d);
        Assert.AreEqual(61.5d, bottomRight.Y, 0.001d);
        var topLeft = split.GetPaneScroll(SpreadsheetPaneId.TopLeft);
        Assert.AreEqual(0d, topLeft.X, 0.001d);
        Assert.AreEqual(0d, topLeft.Y, 0.001d);
    }

    private static void AssertBackendDiagnostics(
        NeraSpreadsheetSplitController split,
        WinFormsRenderingBackend backend)
    {
        switch (backend)
        {
            case WinFormsRenderingBackend.Direct2D:
            {
                var diagnostics = split.Direct2DDiagnostics;
                Assert.IsNotNull(diagnostics);
                Assert.IsTrue(diagnostics.PixelWidth > 0);
                Assert.IsTrue(diagnostics.PixelHeight > 0);
                Assert.IsTrue(diagnostics.CachedTextLayouts > 0);
                break;
            }
            case WinFormsRenderingBackend.Direct2DSwapChain:
            {
                var diagnostics = split.SwapChainDiagnostics;
                Assert.IsNotNull(diagnostics);
                Assert.IsTrue(diagnostics.PixelWidth > 0);
                Assert.IsTrue(diagnostics.PixelHeight > 0);
                Assert.IsFalse(string.IsNullOrWhiteSpace(diagnostics.AdapterName));
                Assert.IsFalse(diagnostics.VSync);
                break;
            }
        }
    }

    private static WinFormsForm CreateOffscreenHost(int width, int height)
    {
        var form = new WinFormsForm
        {
            ClientSize = new DrawingSize(width, height),
            FormBorderStyle = WinFormsFormBorderStyle.FixedToolWindow,
            Location = new DrawingPoint(-32_000, -32_000),
            ShowInTaskbar = false,
            StartPosition = WinFormsFormStartPosition.Manual,
            Text = "NeraSpreadSheet split control smoke host",
        };
        form.Show();
        WinFormsApplication.DoEvents();
        Assert.AreNotEqual(IntPtr.Zero, form.Handle);
        return form;
    }

    private static void RunInSta(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        })
        {
            IsBackground = true,
            Name = "NeraSpreadSheet WinForms split smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        if (!thread.Join(StaThreadTimeout))
        {
            Assert.Fail("The WinForms split smoke thread did not complete within the timeout.");
        }

        failure?.Throw();
    }
}
