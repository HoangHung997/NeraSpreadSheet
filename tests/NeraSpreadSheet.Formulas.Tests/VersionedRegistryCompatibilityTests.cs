using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class VersionedRegistryCompatibilityTests
{
    [TestMethod]
    public void FixedArityBuiltInDoesNotSilentlyConsumeFirstRangeValue()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(-2d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(-3d),
        };

        var result = new NeraFormulaEngine().Evaluate(
            "=ABS(A1:A2)",
            new FormulaSurfaceTestContext(values));

        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            result.ErrorCode);
        Assert.AreEqual("#VALUE!", result.Value.RawValue);
    }

    [TestMethod]
    public void LegacyBuiltInCanStillUseOneRangeAsSeveralFlattenedValues()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(2026d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(8d),
            [new CellAddress(2, 0)] = CellValue.FromNumber(23d),
        };

        var result = new NeraFormulaEngine().Evaluate(
            "=DATE(A1:A3)",
            new FormulaSurfaceTestContext(values));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(
            new DateTime(2026, 8, 23),
            result.Value.RawValue);
        Assert.AreEqual(1, result.Dependencies.Count);
    }

    [TestMethod]
    public void VariableArityAggregateStillFlattensRangeValues()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(2d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(3d),
        };

        var result = new NeraFormulaEngine().Evaluate(
            "=SUM(A1:A2,5)",
            new FormulaSurfaceTestContext(values));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(10d, result.Value.RawValue);
        Assert.AreEqual(1, result.Dependencies.Count);
    }

    [TestMethod]
    public void BuiltInDescriptorsDeclareFlattenedValueCounting()
    {
        var registry = new BuiltInFormulaFunctionRegistry();

        Assert.IsTrue(registry.TryGetDescriptor(
            "DATE",
            out var date));
        Assert.AreEqual(
            FormulaFunctionArgumentCountPolicy.FlattenedValues,
            date.ArgumentCountPolicy);
    }
}
