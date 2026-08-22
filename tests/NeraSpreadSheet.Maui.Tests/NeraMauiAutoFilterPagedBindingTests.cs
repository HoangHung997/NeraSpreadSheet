using Microsoft.Maui.Dispatching;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Maui.Tests;

[TestClass]
public sealed class NeraMauiAutoFilterPagedBindingTests
{
    [TestMethod]
    public async Task BindingPublishesOnlyTheCurrentPage()
    {
        var fixture = CreateFixture();
        Assert.IsTrue(fixture.Session.TryResolveActiveAutoFilterTarget(
            out var target));
        var presenter = new SpreadsheetAutoFilterPagedPresenter(
            fixture.Session,
            target,
            pageSize: 2);
        await using var binding = new NeraMauiAutoFilterPagedBinding(
            presenter,
            new ImmediateDispatcher());

        await binding.InitializeAsync();
        Assert.AreEqual(2, binding.Items.Count);
        Assert.AreEqual(5, binding.TotalItemCount);
        Assert.AreEqual(0, binding.PageOffset);
        Assert.IsTrue(binding.HasNextPage);

        Assert.IsTrue(await binding.MoveNextPageAsync());
        Assert.AreEqual(2, binding.Items.Count);
        Assert.AreEqual(2, binding.PageOffset);
        Assert.IsTrue(binding.HasPreviousPage);
        Assert.IsTrue(binding.HasNextPage);
    }

    [TestMethod]
    public async Task SearchAndSelectionFlowThroughSharedPresenter()
    {
        var fixture = CreateFixture();
        Assert.IsTrue(fixture.Session.TryResolveActiveAutoFilterTarget(
            out var target));
        var presenter = new SpreadsheetAutoFilterPagedPresenter(
            fixture.Session,
            target,
            pageSize: 10);
        await using var binding = new NeraMauiAutoFilterPagedBinding(
            presenter,
            new ImmediateDispatcher());

        await binding.InitializeAsync();
        await binding.SearchAsync("Value4");
        Assert.AreEqual(1, binding.Items.Count);
        Assert.AreEqual("Value4", binding.Items[0].DisplayText);
        await binding.SetSelectedAsync(0, selected: false);
        Assert.IsFalse(binding.Items[0].IsSelected);
    }

    private static Fixture CreateFixture()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        worksheet.SetValue(new CellAddress(0, 0), "Value");
        for (var index = 0; index < 5; index++)
        {
            worksheet.SetValue(
                new CellAddress(index + 1, 0),
                $"Value{index}");
        }
        worksheet.AddTable(new SpreadsheetTable(
            tableId,
            "Values",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(5, 0)),
            [new SpreadsheetTableColumn(columnId, "Value")]));
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        return new Fixture(session);
    }

    private sealed record Fixture(SpreadsheetSession Session);

    private sealed class ImmediateDispatcher : IDispatcher
    {
        public bool IsDispatchRequired => false;

        public bool Dispatch(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            action();
            return true;
        }

        public bool DispatchDelayed(TimeSpan delay, Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            action();
            return true;
        }

        public IDispatcherTimer CreateTimer() =>
            new ImmediateDispatcherTimer();
    }

    private sealed class ImmediateDispatcherTimer : IDispatcherTimer
    {
        public TimeSpan Interval { get; set; }

        public bool IsRepeating { get; set; }

        public bool IsRunning { get; private set; }

        public event EventHandler? Tick;

        public void Start()
        {
            IsRunning = true;
            Tick?.Invoke(this, EventArgs.Empty);
            if (!IsRepeating)
            {
                IsRunning = false;
            }
        }

        public void Stop() => IsRunning = false;
    }
}
