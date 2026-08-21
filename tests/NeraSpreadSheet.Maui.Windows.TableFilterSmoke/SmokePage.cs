using System.Reflection;
using System.Text.Json;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Maui;
using SkiaSharp.Views.Maui;

namespace NeraSpreadSheet.Maui.Windows.TableFilterSmoke;

internal sealed class SmokePage : ContentPage
{
    private static readonly TimeSpan SmokeTimeout =
        TimeSpan.FromSeconds(60d);
    private static readonly JsonSerializerOptions ResultJsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly Workbook _workbook;
    private readonly Worksheet _worksheet;
    private readonly NeraSpreadsheetTableHost _host;
    private readonly TaskCompletionSource<bool> _framesReady = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int _frameCount;
    private int _finished;
    private bool _searchFocused;
    private bool _keyboardRootAttached;
    private bool _filterApplied;
    private bool _undoRedoVerified;
    private string? _semanticDescription;

    public SmokePage()
    {
        Title = "NeraSpreadSheet loaded Table filter smoke";
        (_workbook, _worksheet) = CreateWorkbook();
        _host = new NeraSpreadsheetTableHost
        {
            Workbook = _workbook,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        _host.Spreadsheet.PaintSurface += OnPaintSurface;
        Content = _host;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        _host.Spreadsheet.InvalidateSurface();
        _ = RunSmokeAsync();
    }

    private void OnPaintSurface(
        object? sender,
        SKPaintGLSurfaceEventArgs e)
    {
        if (e.Info.Width <= 0 || e.Info.Height <= 0)
        {
            return;
        }

        if (Interlocked.Increment(ref _frameCount) >= 3)
        {
            _framesReady.TrySetResult(true);
        }
    }

    private async Task RunSmokeAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(
                SmokeTimeout);
            await _framesReady.Task
                .WaitAsync(timeout.Token)
                .ConfigureAwait(false);
            await DispatchAsync(OpenFilterAndValidateHost)
                .ConfigureAwait(false);
            await Task.Delay(
                    TimeSpan.FromMilliseconds(180d),
                    timeout.Token)
                .ConfigureAwait(false);
            await DispatchAsync(ValidateSheetAndApplyFilter)
                .ConfigureAwait(false);
            await Task.Delay(
                    TimeSpan.FromMilliseconds(120d),
                    timeout.Token)
                .ConfigureAwait(false);
            await DispatchAsync(ReopenFilter)
                .ConfigureAwait(false);
            await Task.Delay(
                    TimeSpan.FromMilliseconds(140d),
                    timeout.Token)
                .ConfigureAwait(false);
            await DispatchAsync(CloseFilterAndValidateFocusRelease)
                .ConfigureAwait(false);
            CompleteSuccessfully();
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private void OpenFilterAndValidateHost()
    {
        Require(_frameCount >= 3,
            "The loaded Table-filter smoke did not render three GPU frames.");
        Require(_host.Handler?.PlatformView is not null,
            "The Table host did not receive a native Windows platform view.");
        Require(_host.Spreadsheet.Handler?.PlatformView is not null,
            "The spreadsheet surface did not receive a native Windows platform view.");
        Require(_host.Spreadsheet.GRContext is not null,
            "The loaded Table host did not create a live Skia GRContext.");

        var session = _host.Session ??
            throw new InvalidOperationException(
                "The loaded Table host did not create a spreadsheet session.");
        _keyboardRootAttached = GetPrivateField<object>(
                _host,
                "_platformKeyboardRoot") is not null;
        Require(_keyboardRootAttached,
            "The loaded WinUI host did not attach its keyboard root.");

        session.Selection.SetActiveCell(new CellAddress(1, 0));
        Require(_host.TryOpenForActiveCell(),
            "The loaded host could not open the active Table column filter.");
        Require(_host.IsFilterSheetOpen,
            "The loaded host did not expose an open filter sheet.");
    }

    private void ValidateSheetAndApplyFilter()
    {
        Require(_host.IsFilterSheetOpen,
            "The filter sheet closed before focus validation.");
        var search = GetPrivateField<Entry>(_host, "_search");
        var sheet = GetPrivateField<VerticalStackLayout>(
            _host,
            "_sheetPanel");
        var apply = GetPrivateField<Button>(_host, "_apply");
        var values = GetPrivateField<List<CheckBox>>(
            _host,
            "_valueCheckBoxes");
        var menu = GetPrivateField<SpreadsheetTableFilterMenu>(
            _host,
            "_menu");

        _searchFocused = search.IsFocused;
        Require(_searchFocused,
            "The loaded filter sheet did not focus its search Entry.");
        Require(search.AutomationId == "NeraTableFilterSearch",
            "The loaded search Entry lost its stable AutomationId.");
        Require(values.Count == 3,
            "The loaded filter sheet did not expose all three distinct values.");
        Require(values.All(static value =>
                !string.IsNullOrWhiteSpace(value.AutomationId)),
            "A loaded filter value did not expose an AutomationId.");
        Require(values.All(static value =>
                !string.IsNullOrWhiteSpace(
                    SemanticProperties.GetDescription(value))),
            "A loaded filter value did not expose an accessibility description.");

        _semanticDescription =
            SemanticProperties.GetDescription(sheet);
        Require(!string.IsNullOrWhiteSpace(_semanticDescription) &&
                _semanticDescription.Contains(
                    "Status",
                    StringComparison.Ordinal),
            "The loaded filter sheet did not describe its Table column.");
        Require(!string.IsNullOrWhiteSpace(
                SemanticProperties.GetHint(sheet)),
            "The loaded filter sheet did not expose keyboard guidance.");

        foreach (var item in menu.Capture().Values)
        {
            menu.SetSelected(
                item.Value,
                string.Equals(
                    item.DisplayText,
                    "Open",
                    StringComparison.Ordinal));
        }
        Require(menu.CanApplyValueSelection,
            "The loaded filter menu rejected a valid Open-only selection.");
        Require(apply.IsEnabled,
            "The loaded Apply button did not track valid menu selection state.");
        Require(InvokePrivate<bool>(
                _host,
                "ApplyCurrentFilterAndClose"),
            "The loaded filter sheet did not apply its value selection.");
        Require(!_host.IsFilterSheetOpen,
            "The loaded filter sheet remained open after Apply.");
        _filterApplied = true;

        var filtered = WorksheetSnapshot.Capture(_worksheet);
        Require(filtered.IsRowVisible(1),
            "The selected Open row became hidden.");
        Require(!filtered.IsRowVisible(2),
            "The Closed row remained visible after an Open-only filter.");
        Require(!filtered.IsRowVisible(3),
            "The Pending row remained visible after an Open-only filter.");

        var session = _host.Session ??
            throw new InvalidOperationException(
                "The loaded host lost its spreadsheet session after Apply.");
        Require(session.Undo(),
            "Undo rejected the loaded filter operation.");
        var undone = WorksheetSnapshot.Capture(_worksheet);
        Require(undone.IsRowVisible(1) &&
                undone.IsRowVisible(2) &&
                undone.IsRowVisible(3),
            "Undo did not restore all Table rows.");
        Require(session.Redo(),
            "Redo rejected the loaded filter operation.");
        var redone = WorksheetSnapshot.Capture(_worksheet);
        Require(redone.IsRowVisible(1) &&
                !redone.IsRowVisible(2) &&
                !redone.IsRowVisible(3),
            "Redo did not restore the Open-only filter projection.");
        _undoRedoVerified = true;
    }

    private void ReopenFilter()
    {
        var session = _host.Session ??
            throw new InvalidOperationException(
                "The loaded host lost its spreadsheet session before reopen.");
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        Require(_host.TryOpenForActiveCell(),
            "The loaded filter could not reopen after Apply and Redo.");
        Require(_host.IsFilterSheetOpen,
            "The reopened loaded filter sheet was not visible.");
    }

    private void CloseFilterAndValidateFocusRelease()
    {
        var search = GetPrivateField<Entry>(_host, "_search");
        Require(search.IsFocused,
            "The reopened filter sheet did not return focus to search.");
        _host.CloseFilterSheet();
        Require(!_host.IsFilterSheetOpen,
            "The loaded filter sheet did not close through its public lifecycle.");
        Require(!search.IsFocused,
            "The loaded search Entry retained focus after sheet closure.");
    }

    private Task DispatchAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.Dispatch(() =>
        {
            try
            {
                action();
                completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        });
        return completion.Task;
    }

    private void CompleteSuccessfully()
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }

        WriteResult(new
        {
            status = "success",
            frameCount = _frameCount,
            keyboardRootAttached = _keyboardRootAttached,
            searchFocused = _searchFocused,
            filterApplied = _filterApplied,
            undoRedoVerified = _undoRedoVerified,
            semanticDescription = _semanticDescription,
            cachedTypefaces = _host.Spreadsheet.CachedTypefaceCount,
            contextGeneration =
                _host.Spreadsheet.GpuContextDiagnostics.ContextGeneration,
        });
        Environment.Exit(0);
    }

    private void Fail(Exception exception)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }

        try
        {
            WriteResult(new
            {
                status = "failure",
                frameCount = _frameCount,
                error = exception.ToString(),
            });
        }
        finally
        {
            Environment.Exit(1);
        }
    }

    private static T GetPrivateField<T>(
        object target,
        string fieldName)
        where T : class
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                $"Private field '{fieldName}' was not found.");
        return field.GetValue(target) as T ??
               throw new InvalidOperationException(
                   $"Private field '{fieldName}' did not contain {typeof(T).Name}.");
    }

    private static T InvokePrivate<T>(
        object target,
        string methodName)
    {
        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                $"Private method '{methodName}' was not found.");
        var result = method.Invoke(target, parameters: null);
        return result is T value
            ? value
            : throw new InvalidOperationException(
                $"Private method '{methodName}' did not return {typeof(T).Name}.");
    }

    private static void WriteResult(object result)
    {
        var path = Environment.GetEnvironmentVariable(
            "NERA_MAUI_SMOKE_RESULT");
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                "NERA_MAUI_SMOKE_RESULT must identify the smoke result file.");
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullPath) ??
            throw new InvalidOperationException(
                "The smoke result file has no parent directory."));
        File.WriteAllText(
            fullPath,
            JsonSerializer.Serialize(result, ResultJsonOptions));
    }

    private static (Workbook Workbook, Worksheet Worksheet)
        CreateWorkbook()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var statusColumnId = Guid.NewGuid();
        var amountColumnId = Guid.NewGuid();
        worksheet.SetValue(new CellAddress(0, 0), "Status");
        worksheet.SetValue(new CellAddress(0, 1), "Amount");
        worksheet.SetValue(new CellAddress(1, 0), "Open");
        worksheet.SetValue(new CellAddress(1, 1), 10d);
        worksheet.SetValue(new CellAddress(2, 0), "Closed");
        worksheet.SetValue(new CellAddress(2, 1), 20d);
        worksheet.SetValue(new CellAddress(3, 0), "Pending");
        worksheet.SetValue(new CellAddress(3, 1), 30d);
        for (var row = 4; row < 40; row++)
        {
            for (var column = 0; column < 12; column++)
            {
                worksheet.SetValue(
                    new CellAddress(row, column),
                    $"R{row + 1}C{column + 1}");
            }
        }
        worksheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 1)),
            [
                new SpreadsheetTableColumn(
                    statusColumnId,
                    "Status"),
                new SpreadsheetTableColumn(
                    amountColumnId,
                    "Amount"),
            ]));
        return (workbook, worksheet);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
