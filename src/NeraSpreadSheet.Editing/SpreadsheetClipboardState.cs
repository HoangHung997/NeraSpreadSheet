using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public enum SpreadsheetClipboardPasteMode
{
    All,
    Values,
    Formulas,
    Formats,
}

public enum SpreadsheetClipboardOperation
{
    None,
    Copy,
    Cut,
    External,
}

public enum SpreadsheetClipboardStatus
{
    Idle, Ready, Busy, Canceled, Failed, Stale,
}

/// <summary>Immutable presentation state. Source identity never follows the current selection.</summary>
public sealed record SpreadsheetClipboardState(
    long Version,
    SpreadsheetClipboardOperation Operation,
    bool IsBusy,
    bool CanPaste,
    bool IsCopyMode,
    Guid? PayloadId,
    Workbook? SourceWorkbook,
    Worksheet? SourceWorksheet,
    CellRange? SourceRange,
    bool HasOsOwnership,
    SpreadsheetClipboardStatus Status,
    Exception? LastError);

/// <summary>Data returned by a host read. A private payload ID is accepted only with matching text.</summary>
public sealed record SpreadsheetClipboardReadResult(string Text, Guid? PayloadId = null);

public sealed partial class SpreadsheetClipboardPackage
{
    private Dictionary<(int Row, int Column), CellValue> _cachedValues = [];

    public Guid PayloadId { get; } = Guid.NewGuid();
    public Workbook? SourceWorkbook { get; private set; }
    public Worksheet? SourceWorksheet { get; private set; }
    public long SourceWorksheetVersion { get; private set; }
    internal IReadOnlyList<CellRange> MergedRanges { get; private set; } = Array.Empty<CellRange>();
    internal IReadOnlyList<DataValidationRule> ValidationRules { get; private set; } = Array.Empty<DataValidationRule>();

    private void InitializeMetadata(Worksheet? worksheet, Workbook? workbook)
    {
        SourceWorksheet = worksheet;
        SourceWorkbook = workbook;
        if (worksheet is null)
        {
            return;
        }
        SourceWorksheetVersion = worksheet.Version;
        _cachedValues = worksheet.EnumerateUsedCells()
            .Where(pair => SourceRange.Contains(pair.Key))
            .ToDictionary(pair => (pair.Key.RowIndex - SourceRange.Top, pair.Key.ColumnIndex - SourceRange.Left),
                pair => pair.Value.Value);
        MergedRanges = Array.AsReadOnly(worksheet.MergedCells.Ranges
            .Where(range => range.Intersects(SourceRange)).ToArray());
        ValidationRules = Array.AsReadOnly(worksheet.DataValidationRules
            .Where(rule => rule.Ranges.Any(range => range.Intersects(SourceRange)))
            .Select(rule => rule.Copy()).ToArray());
    }

    /// <summary>Returns the value cached at copy time, including derived spill children; never recalculates.</summary>
    public CellValue GetCachedValue(int rowOffset, int columnOffset)
    {
        var cell = GetCell(rowOffset, columnOffset);
        if (SourceWorksheet is null && cell.Formula is not null)
        {
            throw new InvalidOperationException("External formula text has no cached value. Paste formulas or all cells instead.");
        }
        return _cachedValues.GetValueOrDefault((rowOffset, columnOffset), cell.Value);
    }
}

public sealed partial class SpreadsheetClipboardController
{
    private SpreadsheetClipboardOperation _clipboardOperation;
    private bool _copyMode;
    private bool _hasOsOwnership;
    private long _publishedGeneration;
    private long _osOwnershipGeneration;

    public event EventHandler? StateChanged
    {
        add => OperationState.Changed += value;
        remove => OperationState.Changed -= value;
    }

    public SpreadsheetClipboardState State => new(
        OperationState.StateVersion, _clipboardOperation, OperationState.IsPending, CanPasteSpecial(SpreadsheetClipboardPasteMode.All),
        _copyMode && IsPublishedPayloadCurrent, Clipboard?.PayloadId, Clipboard?.SourceWorkbook,
        Clipboard?.SourceWorksheet, Clipboard?.SourceRange, HasCurrentOsOwnership,
        OperationState.IsPending ? SpreadsheetClipboardStatus.Busy : OperationState.LastStatus, OperationState.LastError);

    private bool IsPublishedPayloadCurrent => Clipboard is not null &&
        _publishedGeneration == OperationState.ExternalGeneration;

    private bool HasCurrentOsOwnership => _hasOsOwnership && IsPublishedPayloadCurrent &&
        _osOwnershipGeneration == OperationState.OsGeneration;

    /// <summary>Queries basic command availability without evaluating a host authorization callback.</summary>
    public bool CanCopy => !OperationState.IsPending && !_session.Editor.IsEditing &&
        _session.Workbook.Worksheets.Contains(_session.ActiveWorksheet) && _session.Selection.Ranges.Count == 1;

    public bool CanCut => CanCopy;

    public bool CanCancelCopyMode => !_session.Editor.IsEditing &&
        (_copyMode && IsPublishedPayloadCurrent || OperationState.CancelPending is not null);

    public bool CanPasteSpecial(SpreadsheetClipboardPasteMode mode)
    {
        if (!Enum.IsDefined(mode) || !IsPublishedPayloadCurrent || OperationState.IsPending ||
            _session.Editor.IsEditing ||
            !_session.Workbook.Worksheets.Contains(_session.ActiveWorksheet))
        {
            return false;
        }
        var package = Clipboard!;
        if (mode == SpreadsheetClipboardPasteMode.Values && package.SourceWorksheet is null &&
            package.Cells.Any(cell => cell.Data.Formula is not null)) return false;
        var address = _session.Selection.ActiveCell;
        return (long)address.RowIndex + package.RowCount <= SpreadsheetLimits.MaxRows &&
            (long)address.ColumnIndex + package.ColumnCount <= SpreadsheetLimits.MaxColumns;
    }

    /// <summary>Session-wide pure authorization query. Null retains caller-managed protection.</summary>
    public Func<Worksheet, CellRange, SpreadsheetClipboardPasteMode, bool>? PasteAuthorization
    {
        get => OperationState.PasteAuthorization;
        set
        {
            if (ReferenceEquals(OperationState.PasteAuthorization, value)) return;
            OperationState.PasteAuthorization = value;
            if (OperationState.IsPending) OperationState.Invalidated = true;
            SignalStateChanged();
        }
    }

    /// <summary>Ends source highlighting without clearing either clipboard. Editor/IME arbitration belongs to the host.</summary>
    public bool CancelCopyMode()
    {
        if (_session.Editor.IsEditing) return false;
        var canceled = CancelPendingClipboardWrite();
        if (canceled) OperationState.LastStatus = SpreadsheetClipboardStatus.Canceled;
        var changed = _copyMode;
        _copyMode = false;
        if (changed || canceled) SignalStateChanged();
        return changed || canceled;
    }

    /// <summary>Invalidates all private payload leases in this session after actual external ownership loss.</summary>
    public void NotifyExternalClipboardChanged()
    {
        OperationState.ExternalGeneration++;
        OperationState.OsGeneration++;
        OperationState.LastStatus = SpreadsheetClipboardStatus.Stale;
        if (OperationState.IsPending) OperationState.Invalidated = true;
        _copyMode = false;
        _hasOsOwnership = false;
        SignalStateChanged();
    }

    private void PublishPackage(SpreadsheetClipboardPackage package, SpreadsheetClipboardOperation operation, bool osOwned)
    {
        Clipboard = package;
        OperationState.LastStatus = SpreadsheetClipboardStatus.Ready;
        OperationState.LastError = null;
        _clipboardOperation = operation;
        _publishedGeneration = OperationState.ExternalGeneration;
        _copyMode = operation is SpreadsheetClipboardOperation.Copy or SpreadsheetClipboardOperation.Cut;
        _hasOsOwnership = osOwned;
        _osOwnershipGeneration = OperationState.OsGeneration;
    }

    private bool RejectStaleClipboard()
    {
        OperationState.LastStatus = SpreadsheetClipboardStatus.Stale;
        return false;
    }

    private void RecordClipboardFailure(Exception exception, bool canceled)
    {
        OperationState.LastError = exception;
        OperationState.LastStatus = canceled ? SpreadsheetClipboardStatus.Canceled : SpreadsheetClipboardStatus.Failed;
    }

    private void SignalStateChanged()
    {
        OperationState.StateVersion++;
        OperationState.Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool PasteAtActiveCell(SpreadsheetClipboardPasteMode mode) => Paste(_session.Selection.ActiveCell, mode);

    /// <summary>Pastes one rectangular payload through the existing session history. No transpose or skip-blanks mode is implied.</summary>
    public bool Paste(CellAddress destination, SpreadsheetClipboardPasteMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (!IsPublishedPayloadCurrent || OperationState.IsPending) return false;
        EnsurePasteContext();
        var lease = CaptureWriteLease(_session.ActiveWorksheet, _session.Selection.Ranges[0]);
        OperationState.IsPending = true;
        OperationState.Invalidated = false;
        _session.ActiveWorksheetChanged += OnPendingWriteContextChanged;
        _session.Editor.StateChanged += OnPendingWriteContextChanged;
        try
        {
            SignalStateChanged();
            return ApplyPaste(Clipboard!, destination, mode, lease, publishExternal: false);
        }
        catch (OperationCanceledException exception)
        {
            RecordClipboardFailure(exception, canceled: true);
            throw;
        }
        catch (Exception exception)
        {
            RecordClipboardFailure(exception, canceled: false);
            throw;
        }
        finally
        {
            _session.ActiveWorksheetChanged -= OnPendingWriteContextChanged;
            _session.Editor.StateChanged -= OnPendingWriteContextChanged;
            OperationState.Invalidated = false;
            OperationState.IsPending = false;
            SignalStateChanged();
        }
    }

    /// <summary>Reads actual OS data supplied by a host; drops late results and never substitutes a stale private package.</summary>
    public async ValueTask<bool> PasteFromClipboardAsync(
        Func<CancellationToken, ValueTask<SpreadsheetClipboardReadResult?>> readAsync,
        SpreadsheetClipboardPasteMode mode = SpreadsheetClipboardPasteMode.All,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(readAsync);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        EnsureClipboardIdle();
        EnsurePasteContext();
        cancellationToken.ThrowIfCancellationRequested();
        var lease = CaptureWriteLease(_session.ActiveWorksheet, _session.Selection.Ranges[0]);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        OperationState.IsPending = true;
        OperationState.Invalidated = false;
        OperationState.CancelPending = cancellation.Cancel;
        _session.ActiveWorksheetChanged += OnPendingWriteContextChanged;
        _session.Editor.StateChanged += OnPendingWriteContextChanged;
        try
        {
            SignalStateChanged();
            var data = await readAsync(cancellation.Token).ConfigureAwait(true);
            cancellation.Token.ThrowIfCancellationRequested();
            if (!IsWriteLeaseCurrent(lease)) return RejectStaleClipboard();
            if (data is null)
            {
                ObserveExternalClipboardReplacement();
                return false;
            }
            var native = HasCurrentOsOwnership && data.PayloadId == Clipboard!.PayloadId &&
                string.Equals(data.Text, Clipboard.ToTabSeparatedText(), StringComparison.Ordinal);
            if (!native)
            {
                // A successful read has disproved the old private ownership lease.
                // Revoke it even if parsing, authorization or paste later fails. Keep
                // the old package only as recovery data, never as an implicit fallback.
                ObserveExternalClipboardReplacement();
            }
            ArgumentNullException.ThrowIfNull(data.Text);
            if (data.Text.Length > 16 * 1024 * 1024)
                throw new InvalidOperationException("Clipboard text exceeds the 16 Mi-character limit.");
            var package = native ? Clipboard! : CreateExternalPackage(data.Text);
            cancellation.Token.ThrowIfCancellationRequested();
            return ApplyPaste(package, lease.ActiveCell, mode, lease, publishExternal: !native, cancellation.Token);
        }
        catch (OperationCanceledException exception)
        {
            RecordClipboardFailure(exception, canceled: true);
            throw;
        }
        catch (Exception exception)
        {
            RecordClipboardFailure(exception, canceled: false);
            throw;
        }
        finally
        {
            _session.ActiveWorksheetChanged -= OnPendingWriteContextChanged;
            _session.Editor.StateChanged -= OnPendingWriteContextChanged;
            OperationState.CancelPending = null;
            OperationState.Invalidated = false;
            OperationState.IsPending = false;
            SignalStateChanged();
        }
    }

    private void ObserveExternalClipboardReplacement()
    {
        // Unlike the external notification API, this observation belongs to the
        // current read. Do not invalidate its destination lease or cancel it.
        OperationState.ExternalGeneration++;
        OperationState.OsGeneration++;
        OperationState.LastStatus = SpreadsheetClipboardStatus.Stale;
        _copyMode = false;
        _hasOsOwnership = false;
    }

    private void EnsurePasteContext()
    {
        if (_session.Editor.IsEditing ||
            !_session.Workbook.Worksheets.Contains(_session.ActiveWorksheet))
            throw new InvalidOperationException("Paste requires one selection in an attached worksheet and no active cell edit.");
    }

    private bool ApplyPaste(SpreadsheetClipboardPackage package, CellAddress destination,
        SpreadsheetClipboardPasteMode mode, ClipboardWriteLease lease, bool publishExternal, CancellationToken cancellationToken = default)
    {
        if (!IsWriteLeaseCurrent(lease)) return RejectStaleClipboard();
        EnsureTargetFits(package, destination);
        EnsureMaterializationLimit(package.SourceRange);
        var target = CreateTargetRange(package, destination);
        var worksheet = lease.Worksheet;
        if (OperationState.PasteAuthorization is { } authorize && !authorize(worksheet, target, mode))
            throw new InvalidOperationException("The current clipboard policy does not allow pasting into this range.");
        if (!IsWriteLeaseCurrent(lease)) return RejectStaleClipboard();
        if (mode != SpreadsheetClipboardPasteMode.Formats) EnsureTargetDoesNotIntersectSpill(target);
        if (package.MergedRanges.Any(range => !Contains(package.SourceRange, range)) ||
            worksheet.MergedCells.Ranges.Any(range => range.Intersects(target) && !Contains(target, range)))
            throw new InvalidOperationException("Paste cannot split a merged range.");

        CellRange Shift(CellRange range) => new(
            new CellAddress(destination.RowIndex + range.Top - package.SourceRange.Top,
                destination.ColumnIndex + range.Left - package.SourceRange.Left),
            new CellAddress(destination.RowIndex + range.Bottom - package.SourceRange.Top,
                destination.ColumnIndex + range.Right - package.SourceRange.Left));
        var sourceMerges = package.MergedRanges.Select(Shift).ToArray();
        var targetMerges = worksheet.MergedCells.Ranges.Where(range => range.Intersects(target)).ToArray();
        if (mode != SpreadsheetClipboardPasteMode.All &&
            targetMerges.Any(range => !sourceMerges.Contains(range)))
            throw new InvalidOperationException("This paste mode requires matching destination merged ranges.");
        if (mode == SpreadsheetClipboardPasteMode.All && sourceMerges.Any(merge =>
                worksheet.Tables.Any(table => table.Range.Intersects(merge)) ||
                worksheet.AutoFilter is { } filter && filter.Range.Intersects(merge)))
            throw new InvalidOperationException("Pasted merges cannot overlap tables or worksheet filters.");

        var updates = new List<KeyValuePair<CellAddress, CellData>>(checked(package.RowCount * package.ColumnCount));
        for (var row = 0; row < package.RowCount; row++)
        {
            for (var column = 0; column < package.ColumnCount; column++)
            {
                var address = new CellAddress(destination.RowIndex + row, destination.ColumnIndex + column);
                if (mode != SpreadsheetClipboardPasteMode.All && worksheet.ResolveMergedAnchor(address) != address) continue;
                var source = package.GetCell(row, column);
                var current = worksheet.GetCell(address);
                var formula = source.Formula;
                if (mode is SpreadsheetClipboardPasteMode.All or SpreadsheetClipboardPasteMode.Formulas &&
                    formula is not null && package.TranslateFormulasOnPaste)
                    formula = FormulaReferenceTranslator.Translate(formula,
                        new CellAddress(package.SourceRange.Top + row, package.SourceRange.Left + column), address);
                var data = mode switch
                {
                    SpreadsheetClipboardPasteMode.All => new CellData(source.Value, formula, source.StyleId),
                    SpreadsheetClipboardPasteMode.Values => new CellData(package.GetCachedValue(row, column), styleId: current.StyleId),
                    SpreadsheetClipboardPasteMode.Formulas => new CellData(source.Value, formula, current.StyleId),
                    SpreadsheetClipboardPasteMode.Formats => new CellData(current.Value, current.Formula, source.StyleId),
                    _ => throw new ArgumentOutOfRangeException(nameof(mode)),
                };
                updates.Add(new(address, data));
            }
        }
        var operation = new SpreadsheetClipboardPasteOperation(worksheet, target, updates, mode,
            sourceMerges, package.ValidationRules, package.SourceRange, destination);
        if (!IsWriteLeaseCurrent(lease)) return RejectStaleClipboard();
        cancellationToken.ThrowIfCancellationRequested();
        OperationState.CancelPending = null;
        if (publishExternal) PublishPackage(package, SpreadsheetClipboardOperation.External, false);
        _session.Execute(operation);
        // A model observer may switch sheets or move the selection. Never overwrite that newer intent.
        if (ReferenceEquals(_session.ActiveWorksheet, worksheet) && _session.Selection.Version == lease.SelectionVersion)
            _session.Selection.Select(target);
        OperationState.LastStatus = SpreadsheetClipboardStatus.Ready;
        OperationState.LastError = null;
        return true;
    }
}
