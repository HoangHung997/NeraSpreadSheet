using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class FormulaArrayValueTests
{
    [TestMethod]
    public void CreateUsesRowMajorShapeAndTransposePreservesValues()
    {
        var array = FormulaArrayValue.Create(
            2,
            3,
            static (row, column) =>
                CellValue.FromNumber((row * 10d) + column));

        Assert.AreEqual(2, array.RowCount);
        Assert.AreEqual(3, array.ColumnCount);
        Assert.AreEqual(6, array.Count);
        Assert.AreEqual(0d, array[0, 0].RawValue);
        Assert.AreEqual(2d, array[0, 2].RawValue);
        Assert.AreEqual(11d, array[1, 1].RawValue);

        var transpose = array.Transpose();
        Assert.AreEqual(3, transpose.RowCount);
        Assert.AreEqual(2, transpose.ColumnCount);
        Assert.AreEqual(array[1, 2], transpose[2, 1]);
        Assert.AreEqual(array, transpose.Transpose());
    }

    [TestMethod]
    public void ToArrayReturnsADetachedCopy()
    {
        var array = FormulaArrayValue.Create(
            1,
            2,
            static (_, column) => CellValue.FromNumber(column + 1d));

        var copy = array.ToArray();
        copy[0] = CellValue.FromNumber(99d);

        Assert.AreEqual(1d, array[0, 0].RawValue);
        Assert.AreEqual(99d, copy[0].RawValue);
    }

    [TestMethod]
    public void FromRowsRequiresANonEmptyRectangle()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            FormulaArrayValue.FromRows([]));
        Assert.ThrowsExactly<ArgumentException>(() =>
            FormulaArrayValue.FromRows([
                [CellValue.FromNumber(1d)],
                [CellValue.FromNumber(2d), CellValue.FromNumber(3d)],
            ]));
    }

    [TestMethod]
    public void ConstructorRejectsShapeAndSafetyLimitViolations()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new FormulaArrayValue(
                2,
                2,
                [CellValue.FromNumber(1d)]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            FormulaArrayValue.Create(
                FormulaArrayValue.MaximumCellCount,
                2,
                static (_, _) => CellValue.Blank));
    }
}
