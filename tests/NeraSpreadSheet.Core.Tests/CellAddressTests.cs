using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class CellAddressTests
{
    [TestMethod]
    [DataRow(0, 0, "A1")]
    [DataRow(0, 25, "Z1")]
    [DataRow(0, 26, "AA1")]
    [DataRow(1_048_575, 16_383, "XFD1048576")]
    public void ToA1_Should_ReturnExpectedReference_When_AddressIsValid(
        int rowIndex,
        int columnIndex,
        string expected)
    {
        var address = new CellAddress(rowIndex, columnIndex);

        Assert.AreEqual(expected, address.ToA1());
    }

    [TestMethod]
    [DataRow("A1", 0, 0)]
    [DataRow("$AA$25", 24, 26)]
    [DataRow("xfd1048576", 1_048_575, 16_383)]
    public void ParseA1_Should_ReturnExpectedAddress_When_ReferenceIsValid(
        string text,
        int expectedRow,
        int expectedColumn)
    {
        var address = CellAddress.ParseA1(text);

        Assert.AreEqual(expectedRow, address.RowIndex);
        Assert.AreEqual(expectedColumn, address.ColumnIndex);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("A0")]
    [DataRow("XFE1")]
    [DataRow("A1048577")]
    [DataRow("1A")]
    [DataRow("AAAAAAAAAAAAAAAAAAAAAAAA1")]
    public void TryParseA1_Should_ReturnFalse_When_ReferenceIsInvalid(string text)
    {
        Assert.IsFalse(CellAddress.TryParseA1(text, out _));
    }
}
