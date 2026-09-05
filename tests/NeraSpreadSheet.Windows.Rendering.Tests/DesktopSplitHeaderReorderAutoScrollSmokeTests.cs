using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using WinFormsApplication = System.Windows.Forms.Application;
using WinFormsControl = System.Windows.Forms.Control;
using WinFormsDockStyle = System.Windows.Forms.DockStyle;
using WinFormsForm = System.Windows.Forms.Form;
using WinFormsFormStartPosition = System.Windows.Forms.FormStartPosition;
using WpfAdornerDecorator = System.Windows.Documents.AdornerDecorator;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfRenderingEventArgs = System.Windows.Media.RenderingEventArgs;
using WpfResizeMode = System.Windows.ResizeMode;
using WpfWindow = System.Windows.Window;
using WpfWindowStartupLocation = System.Windows.WindowStartupLocation;
using WpfWindowStyle = System.Windows.WindowStyle;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopSplitHeaderReorderAutoScrollSmokeTests
{
    private static readonly TimeSpan StaThreadTimeout = TimeSpan.FromSeconds(90d);

    [TestMethod]
    [Timeout(120_000)]
    public void WinFormsSplitHeaderDragAutoScrollsOnlySourcePane()
    {
        RunInSta(() =>
        {
            var session = CreateSession();
            using var form = new WinFormsForm
            {
                Width = 900,
                Height = 650,
                ShowInTaskbar = false,
                StartPosition = WinFormsFormStartPosition.Manual,
                Location = new System.Drawing.Point(-30_000, -30_000),
            };
            using var control = new NeraSpreadSheet.WinForms.NeraSpreadsheetControl
            {
                Dock = WinFormsDockStyle.Fill,
                Session = session,
            };
            form.Controls.Add(control);
            form.Show();
            WinFormsApplication.DoEvents();

            using var split =
                NeraSpreadSheet.WinForms.NeraSpreadsheetSplitExtensions
                    .EnableSplitPanes(
                        control,
                        NeraSpreadSheet.WinForms.SpreadsheetSplitPaneMode.Both);
            split.SetSplit(340d, 230d);
            split.RenderNow();
            var frame = split.LastFrame ??
                throw new AssertFailedException(
                    "The WinForms split frame is unavailable.");
            Assert.IsTrue(frame.TryGetPane(
                SpreadsheetPaneId.TopLeft,
                out var topLeft));
            var chrome = SpreadsheetChromeGeometry.Calculate(
                control.ClientSize.Width,
                control.ClientSize.Height,
                control.RenderTheme);
            var pointerX = chrome.RowHeaderWidth / 2d;
            var pointerY =
                chrome.ColumnHeaderHeight + topLeft.Pane.Bounds.Bottom - 1d;
            var surface = control.Controls
                .Cast<WinFormsControl>()
                .Single(candidate =>
                    candidate.GetType().Name ==
                    "NeraSpreadsheetSplitSurface");
            SetSplitHeaderState(
                surface,
                SpreadsheetPaneId.TopLeft,
                WorksheetAxis.Row,
                2,
                1,
                new PointD(pointerX, pointerY - 10d),
                isActive: false);

            Assert.AreEqual(
                true,
                InvokePrivate(
                    surface,
                    "UpdateHeaderReorder",
                    pointerX,
                    pointerY,
                    true));
            SetPrivateField(
                surface,
                "_headerReorderLastAutoScrollUtc",
                DateTime.UtcNow - TimeSpan.FromMilliseconds(50d));
            InvokePrivate(
                surface,
                "OnHeaderReorderAutoScrollTick",
                null,
                EventArgs.Empty);

            Assert.IsTrue(
                split.GetPaneScroll(SpreadsheetPaneId.TopLeft).Y > 0d);
            Assert.AreEqual(
                default(PointD),
                split.GetPaneScroll(SpreadsheetPaneId.TopRight));
            Assert.AreEqual(
                default(PointD),
                split.GetPaneScroll(SpreadsheetPaneId.BottomLeft));
            Assert.AreEqual(
                default(PointD),
                split.GetPaneScroll(SpreadsheetPaneId.BottomRight));

            form.Close();
            WinFormsApplication.DoEvents();
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void WpfSplitHeaderDragAutoScrollsOnlySourcePane()
    {
        RunInSta(() =>
        {
            var session = CreateSession();
            using var control = new NeraSpreadSheet.Wpf.NeraSpreadsheetControl
            {
                Background = WpfBrushes.White,
                Session = session,
            };
            var decorator = new WpfAdornerDecorator { Child = control };
            var window = CreateWpfWindow(decorator);

            try
            {
                window.Show();
                window.UpdateLayout();
                using var split =
                    NeraSpreadSheet.Wpf.NeraSpreadsheetSplitExtensions
                        .EnableSplitPanes(
                            control,
                            NeraSpreadSheet.Wpf.SpreadsheetSplitPaneMode.Both);
                split.SetSplit(340d, 230d);
                split.RenderNow();
                var frame = split.LastFrame ??
                    throw new AssertFailedException(
                        "The WPF split frame is unavailable.");
                Assert.IsTrue(frame.TryGetPane(
                    SpreadsheetPaneId.TopLeft,
                    out var topLeft));
                var chrome = SpreadsheetChromeGeometry.Calculate(
                    control.ActualWidth,
                    control.ActualHeight,
                    control.RenderTheme);
                var pointerX = chrome.RowHeaderWidth / 2d;
                var pointerY =
                    chrome.ColumnHeaderHeight + topLeft.Pane.Bounds.Bottom - 1d;
                var adorner = GetPrivateField(split, "_adorner") ??
                    throw new AssertFailedException(
                        "The WPF split adorner is unavailable.");
                SetSplitHeaderState(
                    adorner,
                    SpreadsheetPaneId.TopLeft,
                    WorksheetAxis.Row,
                    2,
                    1,
                    new PointD(pointerX, pointerY - 10d),
                    isActive: false);
                Assert.AreEqual(
                    true,
                    InvokePrivate(
                        adorner,
                        "UpdateHeaderReorder",
                        pointerX,
                        pointerY,
                        true));
                SetPrivateField(
                    adorner,
                    "_headerReorderLastAutoScrollRenderingTime",
                    TimeSpan.Zero);
                InvokePrivate(
                    adorner,
                    "OnHeaderReorderAutoScrollRendering",
                    null,
                    CreateRenderingEventArgs(TimeSpan.FromMilliseconds(50d)));

                Assert.IsTrue(
                    split.GetPaneScroll(SpreadsheetPaneId.TopLeft).Y > 0d);
                Assert.AreEqual(
                    default(PointD),
                    split.GetPaneScroll(SpreadsheetPaneId.TopRight));
                Assert.AreEqual(
                    default(PointD),
                    split.GetPaneScroll(SpreadsheetPaneId.BottomLeft));
                Assert.AreEqual(
                    default(PointD),
                    split.GetPaneScroll(SpreadsheetPaneId.BottomRight));
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static SpreadsheetSession CreateSession()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        for (var row = 0; row <= 12; row++)
        {
            worksheet.SetValue(new CellAddress(row, 0), $"row-{row}");
        }
        worksheet.SetValue(new CellAddress(500, 30), "extent");
        var session = new SpreadsheetSession(workbook);
        session.Selection.SelectRow(2);
        return session;
    }

    private static WpfRenderingEventArgs CreateRenderingEventArgs(
        TimeSpan renderingTime)
    {
        var result = Activator.CreateInstance(
            typeof(WpfRenderingEventArgs),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [renderingTime],
            culture: null);
        return result as WpfRenderingEventArgs ??
            throw new AssertFailedException(
                "A WPF RenderingEventArgs instance could not be created.");
    }

    private static void SetSplitHeaderState(
        object target,
        SpreadsheetPaneId sourcePaneId,
        WorksheetAxis axis,
        int sourceIndex,
        int count,
        PointD startPoint,
        bool isActive)
    {
        var stateType = target.GetType().GetNestedType(
            "HeaderReorderState",
            BindingFlags.NonPublic);
        Assert.IsNotNull(stateType);
        var state = Activator.CreateInstance(
            stateType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                sourcePaneId,
                axis,
                sourceIndex,
                count,
                startPoint,
                isActive,
            ],
            culture: null);
        Assert.IsNotNull(state);
        SetPrivateField(target, "_headerReorder", state);
    }

    private static object? InvokePrivate(
        object target,
        string methodName,
        params object?[] arguments)
    {
        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        return method.Invoke(target, arguments);
    }

    private static object? GetPrivateField(object target, string fieldName)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return field.GetValue(target);
    }

    private static void SetPrivateField(
        object target,
        string fieldName,
        object? value)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        field.SetValue(target, value);
    }

    private static WpfWindow CreateWpfWindow(object content) => new()
    {
        Background = WpfBrushes.White,
        Content = content,
        Height = 650d,
        Left = 0d,
        ResizeMode = WpfResizeMode.NoResize,
        ShowActivated = true,
        ShowInTaskbar = false,
        Title = "Nera split header auto-scroll smoke host",
        Top = 0d,
        Topmost = true,
        Width = 900d,
        WindowStartupLocation = WpfWindowStartupLocation.Manual,
        WindowStyle = WpfWindowStyle.None,
    };

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
            Name = "Nera split header auto-scroll smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(StaThreadTimeout))
        {
            Assert.Fail("The split header auto-scroll STA smoke timed out.");
        }
        failure?.Throw();
    }
}
