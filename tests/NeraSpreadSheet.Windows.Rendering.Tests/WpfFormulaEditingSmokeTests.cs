using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Wpf;

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
