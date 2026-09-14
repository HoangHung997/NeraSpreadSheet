using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using WinFormsApplication = System.Windows.Forms.Application;
using WinFormsClipboard = System.Windows.Forms.Clipboard;
using WinFormsControl = NeraSpreadSheet.WinForms.NeraSpreadsheetControl;
using WinFormsForm = System.Windows.Forms.Form;
using WinFormsViewBinding = NeraSpreadSheet.WinForms.NeraWorksheetViewStateBinding;
using WpfClipboard = System.Windows.Clipboard;
using WpfControl = NeraSpreadSheet.Wpf.NeraSpreadsheetControl;
using WpfDispatcher = System.Windows.Threading.Dispatcher;
using WpfDispatcherPriority = System.Windows.Threading.DispatcherPriority;
using WpfViewBinding = NeraSpreadSheet.Wpf.NeraWorksheetViewStateBinding;
using WpfWindow = System.Windows.Window;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class H1HostWiring014Tests
{
    [TestMethod]
    [Timeout(120_000)]
    public void DesktopHostsUseActualClipboardAndRetainRichPayloadWhileOwnershipMatches()
    {
        RunSta(() =>
        {
            VerifyWpfClipboard();
            VerifyWinFormsClipboard();
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void DesktopHostsRestoreSupportedPerWorksheetViewportState()
    {
        RunSta(() =>
        {
            VerifyWpfViewState();
            VerifyWinFormsViewState();
        });
    }

    private static void VerifyWpfClipboard()
    {
        WpfClipboard.Clear();
        var session = CreateSession();
        var sheet = session.ActiveWorksheet;
        sheet.SetFormula(default, "=1+2");
        session.Recalculate();
        using var control = new WpfControl { Session = session };
        var window = new WpfWindow
        {
            Width = 900,
            Height = 650,
            ShowInTaskbar = false,
            Left = -30_000,
            Top = -30_000,
            Content = control,
        };
        try
        {
            window.Show();
            PumpWpf();
            Assert.IsTrue(control.CopyToOperatingSystemClipboard());
            Assert.AreEqual("=1+2", WpfClipboard.GetText());
            Assert.IsTrue(WpfClipboard.ContainsData(SpreadsheetClipboardNativeFormats.PayloadId));

            session.Selection.SetActiveCell(new CellAddress(0, 1));
            Assert.IsTrue(control.PasteFromOperatingSystemClipboard());
            Assert.AreEqual("=1+2", sheet.GetFormula(new CellAddress(0, 1)));
            Assert.AreEqual(3d, sheet.GetValue(new CellAddress(0, 1)));

            WpfClipboard.SetText("external\t7");
            session.Selection.SetActiveCell(new CellAddress(2, 0));
            Assert.IsTrue(control.PasteFromOperatingSystemClipboard());
            Assert.AreEqual("external", sheet.GetValue(new CellAddress(2, 0)));
            Assert.AreEqual(7d, sheet.GetValue(new CellAddress(2, 1)));

            session.Selection.SetActiveCell(default);
            Assert.IsTrue(control.CopyToOperatingSystemClipboard(cut: true));
            Assert.IsNull(sheet.GetValue(default));
            session.Selection.SetActiveCell(new CellAddress(4, 0));
            Assert.IsTrue(control.PasteFromOperatingSystemClipboard());
            Assert.AreEqual("=1+2", sheet.GetFormula(new CellAddress(4, 0)));
        }
        finally
        {
            window.Close();
            PumpWpf();
            WpfClipboard.Clear();
        }
    }

    private static void VerifyWinFormsClipboard()
    {
        WinFormsClipboard.Clear();
        var session = CreateSession();
        var sheet = session.ActiveWorksheet;
        sheet.SetFormula(default, "=2+3");
        session.Recalculate();
        using var form = new WinFormsForm
        {
            Width = 900,
            Height = 650,
            ShowInTaskbar = false,
            Location = new System.Drawing.Point(-30_000, -30_000),
        };
        using var control = new WinFormsControl
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
            Session = session,
        };
        form.Controls.Add(control);
        try
        {
            form.Show();
            WinFormsApplication.DoEvents();
            Assert.IsTrue(control.CopyToOperatingSystemClipboard());
            Assert.AreEqual("=2+3", WinFormsClipboard.GetText());
            Assert.IsTrue(WinFormsClipboard.ContainsData(SpreadsheetClipboardNativeFormats.PayloadId));

            session.Selection.SetActiveCell(new CellAddress(0, 1));
            Assert.IsTrue(control.PasteFromOperatingSystemClipboard());
            Assert.AreEqual("=2+3", sheet.GetFormula(new CellAddress(0, 1)));
            Assert.AreEqual(5d, sheet.GetValue(new CellAddress(0, 1)));

            WinFormsClipboard.SetText("outside\t11");
            session.Selection.SetActiveCell(new CellAddress(2, 0));
            Assert.IsTrue(control.PasteFromOperatingSystemClipboard());
            Assert.AreEqual("outside", sheet.GetValue(new CellAddress(2, 0)));
            Assert.AreEqual(11d, sheet.GetValue(new CellAddress(2, 1)));
        }
        finally
        {
            form.Close();
            WinFormsApplication.DoEvents();
            WinFormsClipboard.Clear();
        }
    }

    private static void VerifyWpfViewState()
    {
        var session = CreateTwoSheetSession(out var first, out var second);
        using var control = new WpfControl { Session = session };
        using var binding = new WpfViewBinding(control);
        var window = new WpfWindow
        {
            Width = 900,
            Height = 650,
            ShowInTaskbar = false,
            Left = -30_000,
            Top = -30_000,
            Content = control,
        };
        try
        {
            window.Show();
            PumpWpf();
            control.Zoom = 1.25;
            control.ScrollTo(123.5, 76.25);
            AssertViewport(session.View.GetWorksheetState(first), 1.25, 123.5, 76.25);

            session.ActivateWorksheet(second);
            PumpWpf();
            Assert.AreEqual(1d, control.Zoom, 1e-9);
            Assert.AreEqual(0d, control.ScrollSnapshot.OffsetX, 1e-9);
            Assert.AreEqual(0d, control.ScrollSnapshot.OffsetY, 1e-9);
            control.Zoom = 0.75;
            control.ScrollTo(48.5, 64.75);

            session.ActivateWorksheet(first);
            PumpWpf();
            Assert.AreEqual(1.25, control.Zoom, 1e-9);
            Assert.AreEqual(123.5, control.ScrollSnapshot.OffsetX, 1e-6);
            Assert.AreEqual(76.25, control.ScrollSnapshot.OffsetY, 1e-6);

            session.ActivateWorksheet(second);
            PumpWpf();
            Assert.AreEqual(0.75, control.Zoom, 1e-9);
            Assert.AreEqual(48.5, control.ScrollSnapshot.OffsetX, 1e-6);
            Assert.AreEqual(64.75, control.ScrollSnapshot.OffsetY, 1e-6);
        }
        finally
        {
            window.Close();
            PumpWpf();
        }
    }

    private static void VerifyWinFormsViewState()
    {
        var session = CreateTwoSheetSession(out var first, out var second);
        session.View.SetWorksheetViewport(first, 0, 0, 1.35);
        session.View.SetWorksheetViewport(second, 0, 0, 0.8);
        using var form = new WinFormsForm
        {
            Width = 900,
            Height = 650,
            ShowInTaskbar = false,
            Location = new System.Drawing.Point(-30_000, -30_000),
        };
        using var control = new WinFormsControl
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
            Session = session,
        };
        using var binding = new WinFormsViewBinding(control);
        form.Controls.Add(control);
        try
        {
            form.Show();
            WinFormsApplication.DoEvents();
            control.ScrollTo(137.25, 88.5);
            AssertViewport(session.View.GetWorksheetState(first), 1.35, 137.25, 88.5);

            session.ActivateWorksheet(second);
            WinFormsApplication.DoEvents();
            Assert.AreEqual(0d, control.ScrollSnapshot.OffsetX, 1e-9);
            Assert.AreEqual(0d, control.ScrollSnapshot.OffsetY, 1e-9);
            control.ScrollTo(51.5, 67.25);
            AssertViewport(session.View.GetWorksheetState(second), 0.8, 51.5, 67.25);

            session.ActivateWorksheet(first);
            WinFormsApplication.DoEvents();
            Assert.AreEqual(137.25, control.ScrollSnapshot.OffsetX, 1e-6);
            Assert.AreEqual(88.5, control.ScrollSnapshot.OffsetY, 1e-6);
            Assert.AreEqual(1.35, session.View.GetWorksheetState(first).Zoom, 1e-9);

            session.ActivateWorksheet(second);
            WinFormsApplication.DoEvents();
            Assert.AreEqual(51.5, control.ScrollSnapshot.OffsetX, 1e-6);
            Assert.AreEqual(67.25, control.ScrollSnapshot.OffsetY, 1e-6);
            Assert.AreEqual(0.8, session.View.GetWorksheetState(second).Zoom, 1e-9);
        }
        finally
        {
            form.Close();
            WinFormsApplication.DoEvents();
        }
    }

    private static SpreadsheetSession CreateSession()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(new CellAddress(300, 100), "extent");
        return new SpreadsheetSession(workbook);
    }

    private static SpreadsheetSession CreateTwoSheetSession(out Worksheet first, out Worksheet second)
    {
        var workbook = new Workbook();
        first = workbook.Worksheets[0];
        second = workbook.AddWorksheet("Second");
        first.SetValue(new CellAddress(300, 100), "extent-a");
        second.SetValue(new CellAddress(300, 100), "extent-b");
        return new SpreadsheetSession(workbook);
    }

    private static void AssertViewport(
        SpreadsheetWorksheetViewState state,
        double zoom,
        double offsetX,
        double offsetY)
    {
        Assert.AreEqual(zoom, state.Zoom, 1e-9);
        Assert.AreEqual(offsetX, state.OffsetX, 1e-6);
        Assert.AreEqual(offsetY, state.OffsetY, 1e-6);
    }

    private static void PumpWpf() =>
        WpfDispatcher.CurrentDispatcher.Invoke(
            WpfDispatcherPriority.ApplicationIdle,
            static () => { });

    private static void RunSta(Action action)
    {
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
            Name = "Nera H1 host wiring smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(90)))
        {
            Assert.Fail("H1 host wiring STA smoke timed out.");
        }
        failure?.Throw();
    }
}
