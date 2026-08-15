using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public readonly record struct FormulaCellKey(string WorksheetName, CellAddress Address);

public sealed class FormulaDependencyGraph
{
    private readonly Dictionary<FormulaCellKey, FormulaDependency[]> _dependencies = [];

    public int FormulaCount => _dependencies.Count;

    public void Replace(FormulaCellKey formulaCell, IEnumerable<FormulaDependency> dependencies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formulaCell.WorksheetName);
        ArgumentNullException.ThrowIfNull(dependencies);
        _dependencies[formulaCell] = dependencies.Distinct().ToArray();
    }

    public bool Remove(FormulaCellKey formulaCell) => _dependencies.Remove(formulaCell);

    public IReadOnlyList<FormulaDependency> GetDependencies(FormulaCellKey formulaCell) => _dependencies.TryGetValue(formulaCell, out var dependencies) ? dependencies : Array.Empty<FormulaDependency>();

    public IReadOnlyList<FormulaCellKey> GetDirectDependents(string worksheetName, CellRange changedRange)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);
        var result = new List<FormulaCellKey>();
        foreach (var (formulaCell, dependencies) in _dependencies)
        {
            foreach (var dependency in dependencies)
            {
                var dependencyWorksheet = dependency.WorksheetName ?? formulaCell.WorksheetName;
                if (string.Equals(dependencyWorksheet, worksheetName, StringComparison.OrdinalIgnoreCase) && dependency.Range.Intersects(changedRange))
                {
                    result.Add(formulaCell);
                    break;
                }
            }
        }

        return result;
    }

    public IReadOnlyList<FormulaCellKey> GetTransitiveDependents(string worksheetName, CellRange changedRange)
    {
        var result = new HashSet<FormulaCellKey>();
        var queue = new Queue<FormulaCellKey>(GetDirectDependents(worksheetName, changedRange));
        while (queue.TryDequeue(out var dependent))
        {
            if (!result.Add(dependent))
            {
                continue;
            }

            var dependentRange = new CellRange(dependent.Address, dependent.Address);
            foreach (var next in GetDirectDependents(dependent.WorksheetName, dependentRange))
            {
                if (!result.Contains(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return result.ToArray();
    }

    public void Clear() => _dependencies.Clear();
}
