using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Wpf;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WpfFormulaEditingSmokeTests
{
    [TestMethod]
    [Timeout(60_000)]
    public void WpfControlExposesCompletionPointModeAndPrecedentHighlights()
    {
        RunInSta(() =>
        {
            var workbook = new Workbook();
            var worksheet = workbook.Worksheets[0];
            worksheet.SetValue(new CellAddress(0, 0), 10d);
            worksheet.SetValue(new CellAddress(1, 0), 20d);
            worksheet.SetFormula(new CellAddress(0, 1), "=SUM(A1:A2)");
            var session = new SpreadsheetSession(workbook);
            session.Recalculate();
            session.Selection.SetActiveCell(new CellAddress(0, 1));
            using var control = new NeraSpreadsheetControl
            {
                Session = session,
            };

            var highlights = control.CurrentFormulaReferenceHighlights;
            Assert.AreEqual(1, highlights.Count);
            Assert.AreEqual(
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(1, 0)),
                highlights[0].Range);

            control.BeginEdit("=su");
            Assert.IsTrue(control.CurrentFormulaSuggestions.Any(
                static item => item.Name == "SUM"));
            Assert.IsTrue(control.CancelEditor());

            control.BeginEdit("=SUM(");
            Assert.IsTrue(control.InsertFormulaReference(
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(1, 0))));
            Assert.AreEqual("=SUM(A1:A2", control.CurrentEditText);
            Assert.IsTrue(control.CancelEditor());
        });
    }

    [TestMethod]
    [Timeout(60_000)]
    public void WpfEditorMatchesCellTypographyWrappingAndZoomSteps()
    {
        RunInSta(() =>
        {
            var workbook = new Workbook();
            var worksheet = workbook.Worksheets[0];
            var address = new CellAddress(0, 0);
            worksheet.SetValue(address, "Two lines of text");
            var style = CellStyle.Default with
            {
                Font = CellStyle.Default.Font with
                {
                    Family = "Arial",
                    Size = 11d,
                    Weight = 700,
                    Italic = true,
                    Underline = true,
                },
                Alignment = CellStyle.Default.Alignment with
                {
                    WrapText = true,
                    Vertical = CellVerticalAlignment.Top,
                },
            };
            worksheet.SetStyle(address, workbook.Styles.Intern(style));
            var session = new SpreadsheetSession(workbook);
            using var control = new NeraSpreadsheetControl
            {
                Width = 400d,
                Height = 240d,
                Session = session,
            };
            control.Measure(new System.Windows.Size(400d, 240d));
            control.Arrange(new System.Windows.Rect(0d, 0d, 400d, 240d));

            control.BeginEdit();

            var editor = FindVisibleEditor(control);
            Assert.AreEqual("Arial", editor.FontFamily.Source);
            Assert.AreEqual(11d, editor.FontSize, 1e-9);
            Assert.AreEqual(FontWeights.Bold, editor.FontWeight);
            Assert.AreEqual(FontStyles.Italic, editor.FontStyle);
            Assert.AreEqual(TextWrapping.Wrap, editor.TextWrapping);
            Assert.AreEqual(VerticalAlignment.Top, editor.VerticalContentAlignment);
            Assert.IsTrue(editor.AcceptsReturn);

            control.ZoomByWheel(120);
            Assert.AreEqual(1.1d, control.Zoom, 1e-9);
            Assert.AreEqual(1.1d, ((ScaleTransform)control.LayoutTransform).ScaleX, 1e-9);
            control.ZoomByWheel(-120);
            Assert.AreEqual(1d, control.Zoom, 1e-9);
            Assert.IsTrue(control.CancelEditor());
        });
    }

    private static WpfTextBox FindVisibleEditor(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is WpfTextBox { Visibility: Visibility.Visible } editor)
            {
                return editor;
            }
            try
            {
                return FindVisibleEditor(child);
            }
            catch (InvalidOperationException)
            {
                // Continue searching sibling visuals.
            }
        }

        throw new InvalidOperationException("Visible spreadsheet editor was not found.");
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        using var completed = new ManualResetEventSlim();
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
            finally
            {
                completed.Set();
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(
            completed.Wait(TimeSpan.FromSeconds(30d)),
            "The WPF formula editing smoke timed out.");
        thread.Join();
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
