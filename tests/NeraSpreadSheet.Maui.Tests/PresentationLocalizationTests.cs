using System.Globalization;
using Microsoft.Maui.Dispatching;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Maui.Tests;

[TestClass]
public sealed class PresentationLocalizationTests
{
    [TestMethod]
    public async Task PagedBindingShouldLocalizeChromeAndPreserveUserValuesDuringCultureSwitch()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(default, "Trạng thái");
        worksheet.SetValue(new CellAddress(1, 0), "Hủy");
        worksheet.SetValue(new CellAddress(3, 0), "Đã xong");
        worksheet.SetAutoFilter(new WorksheetAutoFilter(new CellRange(default, new CellAddress(3, 0))));
        var session = new SpreadsheetSession(workbook);
        Assert.IsTrue(session.TryResolveActiveAutoFilterTarget(out var target));
        await using var binding = new NeraMauiAutoFilterPagedBinding(
            new SpreadsheetAutoFilterPagedPresenter(session, target), new ImmediateDispatcher());
        await binding.InitializeAsync();
        Assert.AreEqual("(Trống)", binding.Items.Single(static item => item.Value.IsBlank).DisplayText);
        var before = session.History.UndoCount;
        binding.Localization = new PresentationLocalization(CultureInfo.GetCultureInfo("en-US"),
            static (key, _) => key == "{0:N0} kết quả" ? "{0:N0} results" : null);
        await binding.SearchAsync(string.Empty);
        Assert.AreEqual("(Blanks)", binding.Items.Single(static item => item.Value.IsBlank).DisplayText);
        Assert.IsTrue(binding.Items.Any(static item => item.DisplayText == "Hủy"), "Workbook text must not be translated.");
        Assert.Contains("3 results", binding.AccessibilityAnnouncement);
        Assert.AreEqual(before, session.History.UndoCount);
        Assert.AreEqual(3, binding.Items.Count);
    }

    private sealed class ImmediateDispatcher : IDispatcher
    {
        public bool IsDispatchRequired => false;
        public bool Dispatch(Action action) { action(); return true; }
        public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
        public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
    }
}
