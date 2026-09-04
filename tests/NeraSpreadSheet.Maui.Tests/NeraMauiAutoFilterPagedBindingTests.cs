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
        CollectionAssert.Contains(
            binding.MenuKinds.ToArray(),
            SpreadsheetAutoFilterMenuKind.Text);
        Assert.Contains("5 kết quả", binding.AccessibilityAnnouncement);

        Assert.IsTrue(await binding.MoveNextPageAsync());
        Assert.AreEqual(2, binding.Items.Count);
        Assert.AreEqual(2, binding.PageOffset);
        Assert.IsTrue(binding.HasPreviousPage);
        Assert.IsTrue(binding.HasNextPage);
        Assert.IsTrue(await binding.ApplyColumnSortAsync(descending: true));
        Assert.AreEqual("Value4", fixture.Session.ActiveWorksheet
            .GetCell(new CellAddress(1, 0)).Value.RawValue);
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

    [TestMethod]
    public async Task RichCriterionFlowsThroughSharedPresenterAndHistory()
    {
        var fixture = CreateFixture();
        Assert.IsTrue(fixture.Session.TryResolveActiveAutoFilterTarget(out var target));
        await using var binding = new NeraMauiAutoFilterPagedBinding(
            new SpreadsheetAutoFilterPagedPresenter(fixture.Session, target),
            new ImmediateDispatcher());
        await binding.InitializeAsync();

        await binding.ApplyRichFilterAsync(new SpreadsheetAutoFilterRichCriterion(
            topBottom: new SpreadsheetTopBottomFilter(top: true, percent: false, value: 2)));

        Assert.AreEqual(1, fixture.Session.History.UndoCount);
        Assert.IsNotNull(fixture.Session.ActiveWorksheet.Tables.Single()
            .AutoFilter!.Columns.Single().TopBottom);
    }

    [TestMethod]
    public async Task DateTreeAndTwoConditionsFlowThroughNativeBinding()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var columnId = Guid.NewGuid();
        worksheet.SetValue(new CellAddress(0, 0), "Date");
        worksheet.SetValue(new CellAddress(1, 0), new DateTime(2025, 1, 2));
        worksheet.SetValue(new CellAddress(2, 0), new DateTime(2026, 3, 4));
        worksheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "Dates",
            new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)),
            [new SpreadsheetTableColumn(columnId, "Date")]));
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        Assert.IsTrue(session.TryResolveActiveAutoFilterTarget(out var target));
        await using var binding = new NeraMauiAutoFilterPagedBinding(
            new SpreadsheetAutoFilterPagedPresenter(session, target),
            new ImmediateDispatcher());
        await binding.InitializeAsync();

        var dates = await binding.GetDatePageAsync(
            new SpreadsheetAutoFilterDateParent(null, null),
            0,
            100);
        Assert.AreEqual(2, dates.TotalNodeCount);
        CollectionAssert.Contains(
            binding.MenuKinds.ToArray(),
            SpreadsheetAutoFilterMenuKind.Date);

        await binding.ApplyCustomFilterAsync(
            new TableFilterCondition(
                TableFilterComparisonOperator.AfterDate,
                CellValue.FromDateTime(new DateTime(2025, 1, 1))),
            new TableFilterCondition(
                TableFilterComparisonOperator.BeforeDate,
                CellValue.FromDateTime(new DateTime(2027, 1, 1))),
            combineWithAnd: true);
        var filter = worksheet.Tables.Single().AutoFilter!.Columns.Single();
        Assert.IsNotNull(filter.FirstCondition);
        Assert.IsNotNull(filter.SecondCondition);
        Assert.IsTrue(filter.CombineWithAnd);
    }

    [TestMethod]
    public async Task DisposeCancelsAndDrainsAnInFlightDispatcherOperation()
    {
        var fixture = CreateFixture();
        Assert.IsTrue(fixture.Session.TryResolveActiveAutoFilterTarget(out var target));
        using var dispatcher = new QueuedDispatcher();
        var binding = new NeraMauiAutoFilterPagedBinding(
            new SpreadsheetAutoFilterPagedPresenter(fixture.Session, target),
            dispatcher);

        var initialize = binding.InitializeAsync();
        await dispatcher.WaitForDispatchAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var dispose = binding.DisposeAsync().AsTask();
        dispatcher.RunNext();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () =>
            await initialize);
        await dispose.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () =>
            await binding.RefreshAsync());
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

    private sealed class QueuedDispatcher : IDispatcher, IDisposable
    {
        private readonly Queue<Action> _actions = new();
        private readonly SemaphoreSlim _signal = new(0);

        public bool IsDispatchRequired => true;

        public bool Dispatch(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            lock (_actions)
            {
                _actions.Enqueue(action);
            }
            _signal.Release();
            return true;
        }

        public bool DispatchDelayed(TimeSpan delay, Action action) =>
            Dispatch(action);

        public IDispatcherTimer CreateTimer() =>
            new ImmediateDispatcherTimer();

        public Task WaitForDispatchAsync() => _signal.WaitAsync();

        public void RunNext()
        {
            Action action;
            lock (_actions)
            {
                action = _actions.Dequeue();
            }
            action();
        }

        public void Dispose() => _signal.Dispose();
    }
}
