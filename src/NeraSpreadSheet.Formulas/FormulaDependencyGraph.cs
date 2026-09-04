using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public readonly record struct FormulaCellKey(string WorksheetName, CellAddress Address);

public sealed class FormulaDependencyGraph
{
    private const int RowBlockSize = 256;
    private const int MaximumIndexedDependencyBlocks = 128;
    private readonly Dictionary<FormulaCellKey, FormulaDependency[]> _dependencies = [];
    private readonly Dictionary<string, WorksheetReverseIndex> _reverseIndexes =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<int, HashSet<FormulaCellKey>>>
        _formulaCellsByWorksheet = new(StringComparer.OrdinalIgnoreCase);

    public int FormulaCount => _dependencies.Count;

    public bool IsPrepared { get; private set; }

    public IReadOnlyList<FormulaCellKey> FormulaCells => _dependencies.Keys.ToArray();

    public void Replace(FormulaCellKey formulaCell, IEnumerable<FormulaDependency> dependencies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formulaCell.WorksheetName);
        ArgumentNullException.ThrowIfNull(dependencies);
        if (_dependencies.TryGetValue(formulaCell, out var previous))
        {
            RemoveReverseEntries(formulaCell, previous);
        }
        else
        {
            AddFormulaCellEntry(formulaCell);
        }

        var materialized = dependencies.Distinct().ToArray();
        _dependencies[formulaCell] = materialized;
        AddReverseEntries(formulaCell, materialized);
    }

    public bool Remove(FormulaCellKey formulaCell)
    {
        if (!_dependencies.Remove(formulaCell, out var previous))
        {
            return false;
        }

        RemoveReverseEntries(formulaCell, previous);
        RemoveFormulaCellEntry(formulaCell);
        return true;
    }

    public IReadOnlyList<FormulaDependency> GetDependencies(FormulaCellKey formulaCell) => _dependencies.TryGetValue(formulaCell, out var dependencies) ? dependencies : Array.Empty<FormulaDependency>();

    public IReadOnlyList<FormulaCellKey> GetDirectDependents(string worksheetName, CellRange changedRange)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);
        if (!_reverseIndexes.TryGetValue(worksheetName, out var index))
        {
            return Array.Empty<FormulaCellKey>();
        }

        var result = new List<FormulaCellKey>();
        foreach (var formulaCell in index.GetCandidates(changedRange))
        {
            if (!_dependencies.TryGetValue(formulaCell, out var dependencies))
            {
                continue;
            }
            foreach (var dependency in dependencies)
            {
                var dependencyWorksheet = dependency.WorksheetName ??
                    formulaCell.WorksheetName;
                if (string.Equals(
                        dependencyWorksheet,
                        worksheetName,
                        StringComparison.OrdinalIgnoreCase) &&
                    dependency.Range.Intersects(changedRange))
                {
                    result.Add(formulaCell);
                    break;
                }
            }
        }
        return result;
    }

    public IReadOnlyList<FormulaCellKey> GetFormulaCells(
        string worksheetName,
        CellRange range)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);
        if (!_formulaCellsByWorksheet.TryGetValue(
                worksheetName,
                out var blocks))
        {
            return Array.Empty<FormulaCellKey>();
        }

        var result = new HashSet<FormulaCellKey>();
        var firstBlock = range.Top / RowBlockSize;
        var lastBlock = range.Bottom / RowBlockSize;
        for (var block = firstBlock; block <= lastBlock; block++)
        {
            if (blocks.TryGetValue(block, out var formulas))
            {
                result.UnionWith(formulas);
            }
        }

        return result
            .Where(item => range.Contains(item.Address))
            .ToArray();
    }

    public IReadOnlyList<FormulaCellKey> GetTransitiveDependents(string worksheetName, CellRange changedRange)
    {
        var result = new HashSet<FormulaCellKey>();
        var queue = new Queue<FormulaCellKey>(GetDirectDependents(worksheetName, changedRange));
        while (queue.TryDequeue(out var dependent))
        {
            if (!result.Add(dependent)) continue;
            var dependentRange = new CellRange(dependent.Address, dependent.Address);
            foreach (var next in GetDirectDependents(dependent.WorksheetName, dependentRange))
                if (!result.Contains(next)) queue.Enqueue(next);
        }
        return result.ToArray();
    }

    public void Clear()
    {
        _dependencies.Clear();
        _reverseIndexes.Clear();
        _formulaCellsByWorksheet.Clear();
        IsPrepared = false;
    }

    public void MarkPrepared() => IsPrepared = true;

    private void AddReverseEntries(
        FormulaCellKey formulaCell,
        IReadOnlyList<FormulaDependency> dependencies)
    {
        foreach (var group in dependencies.GroupBy(
                     dependency => dependency.WorksheetName ??
                         formulaCell.WorksheetName,
                     StringComparer.OrdinalIgnoreCase))
        {
            if (!_reverseIndexes.TryGetValue(group.Key, out var index))
            {
                index = new WorksheetReverseIndex();
                _reverseIndexes.Add(group.Key, index);
            }
            index.Add(formulaCell, group.Select(static item => item.Range));
        }
    }

    private void RemoveReverseEntries(
        FormulaCellKey formulaCell,
        IReadOnlyList<FormulaDependency> dependencies)
    {
        foreach (var worksheetName in dependencies
                     .Select(dependency => dependency.WorksheetName ??
                         formulaCell.WorksheetName)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (_reverseIndexes.TryGetValue(worksheetName, out var index))
            {
                index.Remove(formulaCell);
                if (index.IsEmpty)
                {
                    _reverseIndexes.Remove(worksheetName);
                }
            }
        }
    }

    private void AddFormulaCellEntry(FormulaCellKey formulaCell)
    {
        if (!_formulaCellsByWorksheet.TryGetValue(
                formulaCell.WorksheetName,
                out var blocks))
        {
            blocks = [];
            _formulaCellsByWorksheet.Add(formulaCell.WorksheetName, blocks);
        }

        var block = formulaCell.Address.RowIndex / RowBlockSize;
        if (!blocks.TryGetValue(block, out var formulas))
        {
            formulas = [];
            blocks.Add(block, formulas);
        }
        formulas.Add(formulaCell);
    }

    private void RemoveFormulaCellEntry(FormulaCellKey formulaCell)
    {
        if (!_formulaCellsByWorksheet.TryGetValue(
                formulaCell.WorksheetName,
                out var blocks))
        {
            return;
        }

        var block = formulaCell.Address.RowIndex / RowBlockSize;
        if (blocks.TryGetValue(block, out var formulas))
        {
            formulas.Remove(formulaCell);
            if (formulas.Count == 0)
            {
                blocks.Remove(block);
            }
        }
        if (blocks.Count == 0)
        {
            _formulaCellsByWorksheet.Remove(formulaCell.WorksheetName);
        }
    }

    private sealed class WorksheetReverseIndex
    {
        private readonly Dictionary<int, HashSet<FormulaCellKey>> _blocks = [];
        private readonly HashSet<FormulaCellKey> _broad = [];
        private readonly Dictionary<FormulaCellKey, IndexMembership> _memberships = [];

        public bool IsEmpty => _memberships.Count == 0;

        public void Add(
            FormulaCellKey formulaCell,
            IEnumerable<CellRange> ranges)
        {
            var blocks = new HashSet<int>();
            var broad = false;
            foreach (var range in ranges)
            {
                var firstBlock = range.Top / RowBlockSize;
                var lastBlock = range.Bottom / RowBlockSize;
                var blockCount = checked(lastBlock - firstBlock + 1);
                if (blockCount > MaximumIndexedDependencyBlocks)
                {
                    broad = true;
                    continue;
                }
                for (var block = firstBlock; block <= lastBlock; block++)
                {
                    blocks.Add(block);
                }
            }

            foreach (var block in blocks)
            {
                if (!_blocks.TryGetValue(block, out var formulas))
                {
                    formulas = [];
                    _blocks.Add(block, formulas);
                }
                formulas.Add(formulaCell);
            }
            if (broad)
            {
                _broad.Add(formulaCell);
            }
            _memberships[formulaCell] = new IndexMembership(blocks.ToArray(), broad);
        }

        public void Remove(FormulaCellKey formulaCell)
        {
            if (!_memberships.Remove(formulaCell, out var membership))
            {
                return;
            }
            foreach (var block in membership.Blocks)
            {
                if (_blocks.TryGetValue(block, out var formulas))
                {
                    formulas.Remove(formulaCell);
                    if (formulas.Count == 0)
                    {
                        _blocks.Remove(block);
                    }
                }
            }
            if (membership.IsBroad)
            {
                _broad.Remove(formulaCell);
            }
        }

        public HashSet<FormulaCellKey> GetCandidates(CellRange range)
        {
            var result = new HashSet<FormulaCellKey>(_broad);
            var firstBlock = range.Top / RowBlockSize;
            var lastBlock = range.Bottom / RowBlockSize;
            var blockCount = checked(lastBlock - firstBlock + 1);
            if (blockCount > MaximumIndexedDependencyBlocks)
            {
                foreach (var formulas in _blocks.Values)
                {
                    result.UnionWith(formulas);
                }
                return result;
            }

            for (var block = firstBlock; block <= lastBlock; block++)
            {
                if (_blocks.TryGetValue(block, out var formulas))
                {
                    result.UnionWith(formulas);
                }
            }
            return result;
        }

        private readonly record struct IndexMembership(
            int[] Blocks,
            bool IsBroad);
    }
}
