using System.Runtime.CompilerServices;

namespace NeraSpreadSheet.Core;

/// <summary>
/// Associates dynamic-array ownership with a worksheet without expanding the
/// sparse cell model. Spill values are materialized into normal sparse cells;
/// this store records which formula owns those values and keeps replacement or
/// invalidation atomic at the worksheet boundary.
/// </summary>
public static class WorksheetFormulaSpillExtensions
{
    private static readonly ConditionalWeakTable<Worksheet, SpillState>
        States = new();

    public static int GetFormulaSpillCount(this Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        var state = GetState(worksheet);
        lock (state.Gate)
        {
            return state.Spills.Count;
        }
    }

    public static IReadOnlyList<FormulaSpillRange> GetFormulaSpills(
        this Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        var state = GetState(worksheet);
        lock (state.Gate)
        {
            return state.Spills.Values
                .OrderBy(static spill => spill.Owner.RowIndex)
                .ThenBy(static spill => spill.Owner.ColumnIndex)
                .Select(static spill => spill.Copy())
                .ToArray();
        }
    }

    public static bool TryGetFormulaSpill(
        this Worksheet worksheet,
        CellAddress owner,
        out FormulaSpillRange? spill)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        var state = GetState(worksheet);
        lock (state.Gate)
        {
            if (state.Spills.TryGetValue(owner, out var stored))
            {
                spill = stored.Copy();
                return true;
            }
        }

        spill = null;
        return false;
    }

    public static bool TryGetFormulaSpillOwner(
        this Worksheet worksheet,
        CellAddress address,
        out CellAddress owner)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        var state = GetState(worksheet);
        lock (state.Gate)
        {
            return state.OwnersByCell.TryGetValue(address, out owner);
        }
    }

    public static bool IsFormulaSpillChild(
        this Worksheet worksheet,
        CellAddress address) =>
        worksheet.TryGetFormulaSpillOwner(address, out var owner) &&
        owner != address;

    public static FormulaSpillApplyResult TryApplyFormulaSpill(
        this Worksheet worksheet,
        CellAddress owner,
        FormulaArrayValue values)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(values);
        var state = GetState(worksheet);
        lock (state.Gate)
        {
            var ownerCell = worksheet.GetCell(owner);
            if (ownerCell.Formula is null)
            {
                return new FormulaSpillApplyResult(
                    FormulaSpillApplyStatus.InvalidOwner);
            }

            if (!TryCreateRange(owner, values, out var targetRange))
            {
                return new FormulaSpillApplyResult(
                    FormulaSpillApplyStatus.OutOfBounds);
            }

            state.Spills.TryGetValue(owner, out var previous);
            if (TryFindBlockingAddress(
                    worksheet,
                    state,
                    owner,
                    targetRange,
                    previous,
                    out var blockingAddress))
            {
                return new FormulaSpillApplyResult(
                    FormulaSpillApplyStatus.Blocked,
                    BlockingAddress: blockingAddress);
            }

            var replacement = new FormulaSpillRange(
                owner,
                targetRange,
                values);
            var updates = new Dictionary<CellAddress, CellData>();
            if (previous is not null)
            {
                foreach (var pair in previous.EnumerateValues())
                {
                    if (pair.Key == owner || targetRange.Contains(pair.Key))
                    {
                        continue;
                    }
                    var current = worksheet.GetCell(pair.Key);
                    updates[pair.Key] = new CellData(
                        CellValue.Blank,
                        styleId: current.StyleId);
                }
            }

            foreach (var pair in replacement.EnumerateValues())
            {
                var current = worksheet.GetCell(pair.Key);
                updates[pair.Key] = pair.Key == owner
                    ? new CellData(
                        pair.Value,
                        ownerCell.Formula,
                        current.StyleId)
                    : new CellData(
                        pair.Value,
                        styleId: current.StyleId);
            }

            ExecuteInternalMutation(
                worksheet,
                state,
                () => worksheet.SetCells(updates));
            RemoveMetadata(state, owner);
            AddMetadata(state, replacement);
            return new FormulaSpillApplyResult(
                FormulaSpillApplyStatus.Applied,
                replacement.Copy());
        }
    }

    public static bool ClearFormulaSpill(
        this Worksheet worksheet,
        CellAddress owner)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        var state = GetState(worksheet);
        lock (state.Gate)
        {
            if (!state.Spills.TryGetValue(owner, out var spill))
            {
                return false;
            }

            RemoveMetadata(state, owner);
            var updates = CreateChildClearUpdates(worksheet, spill);
            if (updates.Count > 0)
            {
                ExecuteInternalMutation(
                    worksheet,
                    state,
                    () => worksheet.SetCells(updates));
            }
            return true;
        }
    }

    public static void SetFormulaSpillError(
        this Worksheet worksheet,
        CellAddress owner,
        string errorCode = "#SPILL!")
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        var state = GetState(worksheet);
        lock (state.Gate)
        {
            state.Spills.TryGetValue(owner, out var spill);
            RemoveMetadata(state, owner);
            var updates = spill is null
                ? new Dictionary<CellAddress, CellData>()
                : CreateChildClearUpdates(worksheet, spill);
            var ownerCell = worksheet.GetCell(owner);
            if (ownerCell.Formula is null)
            {
                throw new InvalidOperationException(
                    "A spill error can be assigned only to a formula cell.");
            }
            updates[owner] = new CellData(
                CellValue.FromError(errorCode.Trim()),
                ownerCell.Formula,
                ownerCell.StyleId);
            ExecuteInternalMutation(
                worksheet,
                state,
                () => worksheet.SetCells(updates));
        }
    }

    public static int ClearAllFormulaSpills(this Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        var state = GetState(worksheet);
        lock (state.Gate)
        {
            if (state.Spills.Count == 0)
            {
                return 0;
            }

            var spills = state.Spills.Values.ToArray();
            var updates = new Dictionary<CellAddress, CellData>();
            foreach (var spill in spills)
            {
                foreach (var pair in CreateChildClearUpdates(
                             worksheet,
                             spill))
                {
                    updates[pair.Key] = pair.Value;
                }
            }
            state.Spills.Clear();
            state.OwnersByCell.Clear();
            if (updates.Count > 0)
            {
                ExecuteInternalMutation(
                    worksheet,
                    state,
                    () => worksheet.SetCells(updates));
            }
            return spills.Length;
        }
    }

    private static SpillState GetState(Worksheet worksheet) =>
        States.GetValue(
            worksheet,
            static key => new SpillState(key));

    private static bool TryCreateRange(
        CellAddress owner,
        FormulaArrayValue values,
        out CellRange range)
    {
        var bottom = (long)owner.RowIndex + values.RowCount - 1L;
        var right = (long)owner.ColumnIndex + values.ColumnCount - 1L;
        if (bottom >= SpreadsheetLimits.MaxRows ||
            right >= SpreadsheetLimits.MaxColumns)
        {
            range = default;
            return false;
        }
        range = new CellRange(
            owner,
            new CellAddress((int)bottom, (int)right));
        return true;
    }

    private static bool TryFindBlockingAddress(
        Worksheet worksheet,
        SpillState state,
        CellAddress owner,
        CellRange targetRange,
        FormulaSpillRange? previous,
        out CellAddress blockingAddress)
    {
        foreach (var mergedRange in worksheet.MergedCells.Ranges)
        {
            if (mergedRange.Intersects(targetRange))
            {
                blockingAddress = mergedRange.TopLeft;
                return true;
            }
        }
        foreach (var table in worksheet.Tables)
        {
            if (table.Range.Intersects(targetRange))
            {
                blockingAddress = table.Range.TopLeft;
                return true;
            }
        }

        for (var row = targetRange.Top; row <= targetRange.Bottom; row++)
        {
            for (var column = targetRange.Left;
                 column <= targetRange.Right;
                 column++)
            {
                var address = new CellAddress(row, column);
                if (address == owner)
                {
                    if (state.OwnersByCell.TryGetValue(
                            address,
                            out var existingOwner) &&
                        existingOwner != owner)
                    {
                        blockingAddress = address;
                        return true;
                    }
                    continue;
                }
                if (previous?.Range.Contains(address) == true)
                {
                    continue;
                }
                if (state.OwnersByCell.TryGetValue(
                        address,
                        out var spillOwner) &&
                    spillOwner != owner)
                {
                    blockingAddress = address;
                    return true;
                }

                var cell = worksheet.GetCell(address);
                if (cell.Formula is not null || !cell.Value.IsBlank)
                {
                    blockingAddress = address;
                    return true;
                }
            }
        }

        blockingAddress = default;
        return false;
    }

    private static Dictionary<CellAddress, CellData>
        CreateChildClearUpdates(
            Worksheet worksheet,
            FormulaSpillRange spill)
    {
        var updates = new Dictionary<CellAddress, CellData>();
        foreach (var pair in spill.EnumerateValues())
        {
            if (pair.Key == spill.Owner)
            {
                continue;
            }
            var current = worksheet.GetCell(pair.Key);
            if (current.Formula is null && current.Value == pair.Value)
            {
                updates[pair.Key] = new CellData(
                    CellValue.Blank,
                    styleId: current.StyleId);
            }
        }
        return updates;
    }

    private static void AddMetadata(
        SpillState state,
        FormulaSpillRange spill)
    {
        state.Spills.Add(spill.Owner, spill);
        foreach (var pair in spill.EnumerateValues())
        {
            state.OwnersByCell[pair.Key] = spill.Owner;
        }
    }

    private static void RemoveMetadata(
        SpillState state,
        CellAddress owner)
    {
        if (!state.Spills.Remove(owner, out var spill))
        {
            return;
        }
        foreach (var pair in spill.EnumerateValues())
        {
            if (state.OwnersByCell.TryGetValue(
                    pair.Key,
                    out var mappedOwner) &&
                mappedOwner == owner)
            {
                state.OwnersByCell.Remove(pair.Key);
            }
        }
    }

    private static void ExecuteInternalMutation(
        Worksheet worksheet,
        SpillState state,
        Action mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        state.InternalMutationDepth++;
        try
        {
            mutation();
        }
        finally
        {
            state.InternalMutationDepth--;
        }
    }

    private sealed class SpillState
    {
        private readonly Worksheet _worksheet;

        public SpillState(Worksheet worksheet)
        {
            _worksheet = worksheet;
            worksheet.CellsChanged += OnCellsChanged;
        }

        public object Gate { get; } = new();

        public Dictionary<CellAddress, FormulaSpillRange> Spills
        {
            get;
        } = [];

        public Dictionary<CellAddress, CellAddress> OwnersByCell
        {
            get;
        } = [];

        public int InternalMutationDepth { get; set; }

        private void OnCellsChanged(
            object? sender,
            CellsChangedEventArgs e)
        {
            lock (Gate)
            {
                if (InternalMutationDepth > 0 || Spills.Count == 0)
                {
                    return;
                }

                var invalid = Spills.Values
                    .Where(spill => spill.Range.Intersects(e.Range))
                    .Where(spill => !IsMaterializationValid(
                        _worksheet,
                        spill))
                    .ToArray();
                if (invalid.Length == 0)
                {
                    return;
                }

                var updates = new Dictionary<CellAddress, CellData>();
                foreach (var spill in invalid)
                {
                    RemoveMetadata(this, spill.Owner);
                    foreach (var pair in CreateChildClearUpdates(
                                 _worksheet,
                                 spill))
                    {
                        updates[pair.Key] = pair.Value;
                    }
                }
                if (updates.Count > 0)
                {
                    ExecuteInternalMutation(
                        _worksheet,
                        this,
                        () => _worksheet.SetCells(updates));
                }
            }
        }

        private static bool IsMaterializationValid(
            Worksheet worksheet,
            FormulaSpillRange spill)
        {
            foreach (var pair in spill.EnumerateValues())
            {
                var cell = worksheet.GetCell(pair.Key);
                if (cell.Value != pair.Value)
                {
                    return false;
                }
                if (pair.Key == spill.Owner)
                {
                    if (cell.Formula is null)
                    {
                        return false;
                    }
                }
                else if (cell.Formula is not null)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
