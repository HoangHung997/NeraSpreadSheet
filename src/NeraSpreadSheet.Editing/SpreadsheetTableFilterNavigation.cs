using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public enum SpreadsheetTableFilterNavigationCommand
{
    None = 0,
    MovePrevious = 1,
    MoveNext = 2,
    MoveFirst = 3,
    MoveLast = 4,
    PagePrevious = 5,
    PageNext = 6,
    ToggleCurrent = 7,
    SelectAllVisible = 8,
    ClearVisibleSelection = 9,
}

public sealed record SpreadsheetTableFilterNavigationSnapshot(
    int ActiveIndex,
    int ItemCount,
    SpreadsheetTableFilterValueItem? ActiveItem)
{
    public bool HasActiveItem => ActiveItem is not null;

    public bool CanMovePrevious => ActiveIndex > 0;

    public bool CanMoveNext =>
        ActiveIndex >= 0 && ActiveIndex < ItemCount - 1;
}

/// <summary>
/// Platform-neutral navigation state for native Table-filter presenters.
/// The menu remains the authoritative owner of search and selection state.
/// </summary>
public sealed class SpreadsheetTableFilterNavigator : IDisposable
{
    public const int DefaultPageSize = 8;

    private readonly SpreadsheetTableFilterMenu _menu;
    private CellValue? _activeValue;
    private int _activeIndex = -1;
    private bool _disposed;

    public SpreadsheetTableFilterNavigator(
        SpreadsheetTableFilterMenu menu)
    {
        _menu = menu ?? throw new ArgumentNullException(nameof(menu));
        _menu.Changed += OnMenuChanged;
        Reconcile(preferredIndex: 0, notify: false);
    }

    public event EventHandler? Changed;

    public int ActiveIndex
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _activeIndex;
        }
    }

    public SpreadsheetTableFilterNavigationSnapshot Capture()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var items = _menu.Capture().Values;
        var activeItem = _activeIndex >= 0 && _activeIndex < items.Count
            ? items[_activeIndex]
            : null;
        return new SpreadsheetTableFilterNavigationSnapshot(
            _activeIndex,
            items.Count,
            activeItem);
    }

    public bool SetActiveIndex(int index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var items = _menu.Capture().Values;
        var normalized = NormalizeIndex(index, items.Count);
        return SetActiveState(normalized, items, notify: true);
    }

    public bool SetActiveValue(CellValue value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var items = _menu.Capture().Values;
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Value == value)
            {
                return SetActiveState(index, items, notify: true);
            }
        }

        return false;
    }

    public bool Handle(
        SpreadsheetTableFilterNavigationCommand command,
        int pageSize = DefaultPageSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        return command switch
        {
            SpreadsheetTableFilterNavigationCommand.None => false,
            SpreadsheetTableFilterNavigationCommand.MovePrevious =>
                MovePrevious(),
            SpreadsheetTableFilterNavigationCommand.MoveNext =>
                MoveNext(),
            SpreadsheetTableFilterNavigationCommand.MoveFirst =>
                SetActiveIndex(0),
            SpreadsheetTableFilterNavigationCommand.MoveLast =>
                SetActiveIndex(int.MaxValue),
            SpreadsheetTableFilterNavigationCommand.PagePrevious =>
                PagePrevious(pageSize),
            SpreadsheetTableFilterNavigationCommand.PageNext =>
                PageNext(pageSize),
            SpreadsheetTableFilterNavigationCommand.ToggleCurrent =>
                ToggleCurrent(),
            SpreadsheetTableFilterNavigationCommand.SelectAllVisible =>
                SelectAllVisible(),
            SpreadsheetTableFilterNavigationCommand.ClearVisibleSelection =>
                ClearVisibleSelection(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(command),
                command,
                "Unsupported Table-filter navigation command."),
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _menu.Changed -= OnMenuChanged;
        _disposed = true;
    }

    private bool MovePrevious()
    {
        var items = _menu.Capture().Values;
        if (items.Count == 0)
        {
            return false;
        }

        return SetActiveIndex(_activeIndex < 0
            ? items.Count - 1
            : _activeIndex - 1);
    }

    private bool MoveNext()
    {
        var items = _menu.Capture().Values;
        if (items.Count == 0)
        {
            return false;
        }

        return SetActiveIndex(_activeIndex < 0
            ? 0
            : _activeIndex + 1);
    }

    private bool PagePrevious(int pageSize)
    {
        if (_activeIndex < 0)
        {
            return SetActiveIndex(0);
        }

        return SetActiveIndex(_activeIndex - pageSize);
    }

    private bool PageNext(int pageSize)
    {
        if (_activeIndex < 0)
        {
            return SetActiveIndex(0);
        }

        return SetActiveIndex(_activeIndex + pageSize);
    }

    private bool ToggleCurrent()
    {
        var snapshot = Capture();
        if (snapshot.ActiveItem is not { } item)
        {
            return false;
        }

        _menu.SetSelected(item.Value, !item.IsSelected);
        return true;
    }

    private bool SelectAllVisible()
    {
        var snapshot = _menu.Capture();
        if (snapshot.Values.Count == 0 ||
            snapshot.AreAllVisibleValuesSelected)
        {
            return false;
        }

        _menu.SelectAllVisible();
        return true;
    }

    private bool ClearVisibleSelection()
    {
        var snapshot = _menu.Capture();
        if (snapshot.Values.Count == 0 ||
            snapshot.AreNoVisibleValuesSelected)
        {
            return false;
        }

        _menu.ClearVisibleSelection();
        return true;
    }

    private void OnMenuChanged(object? sender, EventArgs e) =>
        Reconcile(_activeIndex, notify: true);

    private void Reconcile(int preferredIndex, bool notify)
    {
        var items = _menu.Capture().Values;
        var nextIndex = -1;
        if (_activeValue is { } activeValue)
        {
            for (var index = 0; index < items.Count; index++)
            {
                if (items[index].Value == activeValue)
                {
                    nextIndex = index;
                    break;
                }
            }
        }

        if (nextIndex < 0)
        {
            nextIndex = NormalizeIndex(preferredIndex, items.Count);
        }

        SetActiveState(nextIndex, items, notify);
    }

    private bool SetActiveState(
        int index,
        IReadOnlyList<SpreadsheetTableFilterValueItem> items,
        bool notify)
    {
        var nextValue = index >= 0 && index < items.Count
            ? items[index].Value
            : (CellValue?)null;
        var changed = index != _activeIndex ||
                      nextValue != _activeValue;
        _activeIndex = index;
        _activeValue = nextValue;
        if (changed && notify)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return changed;
    }

    private static int NormalizeIndex(int index, int itemCount)
    {
        if (itemCount == 0)
        {
            return -1;
        }

        return Math.Clamp(index, 0, itemCount - 1);
    }
}
