using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class NeraFormulaEngineTests
{
    [TestMethod]
    public void EvaluateRespectsArithmeticPrecedence()
    {
        var result = new NeraFormulaEngine().Evaluate("=1+2*3", new DictionaryContext());
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(7d, result.Value.RawValue);
    }

    [TestMethod]
    public void EvaluateSumFlattensCellRangeAndTracksDependency()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(2), [new CellAddress(1, 0)] = CellValue.FromNumber(3), [new CellAddress(2, 0)] = CellValue.FromNumber(5),
        };
        var result = new NeraFormulaEngine().Evaluate("=SUM(A1:A3)", new DictionaryContext(values));
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(10d, result.Value.RawValue);
        Assert.AreEqual(1, result.Dependencies.Count);
        Assert.AreEqual(new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)), result.Dependencies[0].Range);
    }

    [TestMethod]
    public void EvaluateIfUsesOnlySelectedBranch()
    {
        var result = new NeraFormulaEngine().Evaluate("=IF(FALSE,1/0,42)", new DictionaryContext());
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(42d, result.Value.RawValue);
    }

    [TestMethod]
    public void EvaluateDivisionByZeroReturnsFormulaError()
    {
        var result = new NeraFormulaEngine().Evaluate("=10/0", new DictionaryContext());
        Assert.AreEqual(FormulaErrorCode.DivisionByZero, result.ErrorCode);
    }

    private sealed class DictionaryContext : IFormulaEvaluationContext
    {
        private readonly IReadOnlyDictionary<CellAddress, CellValue> _values;
        public DictionaryContext(IReadOnlyDictionary<CellAddress, CellValue>? values = null) { _values = values ?? new Dictionary<CellAddress, CellValue>(); }
        public CellValue GetCellValue(string? worksheetName, CellAddress address) => _values.GetValueOrDefault(address, CellValue.Blank);
    }
}
