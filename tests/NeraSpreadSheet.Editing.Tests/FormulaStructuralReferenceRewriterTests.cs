using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class FormulaStructuralReferenceRewriterTests
{
    [TestMethod]
    public void InsertRowsShiftsSingleReferencesAndExpandsRangesIncludingAbsoluteReferences()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            index: 4,
            count: 2);

        var rewritten = FormulaStructuralReferenceRewriter.Rewrite(
            "=A5+$B$10+C3:D9",
            "Sheet1",
            "Sheet1",
            change);

        Assert.AreEqual("=A7+$B$12+C3:D11", rewritten);
    }

    [TestMethod]
    public void DeleteRowsAtStartOfRangeShrinksRangeInsteadOfProducingPartialRefError()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Delete,
            index: 4,
            count: 2);

        var rewritten = FormulaStructuralReferenceRewriter.Rewrite(
            "=SUM(A5:A10)",
            "Sheet1",
            "Sheet1",
            change);

        Assert.AreEqual("=SUM(A5:A8)", rewritten);
    }

    [TestMethod]
    public void DeleteCoveringWholeRangeProducesRefErrorForRangeAsOneUnit()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Delete,
            index: 4,
            count: 6);

        var rewritten = FormulaStructuralReferenceRewriter.Rewrite(
            "=A5:A10",
            "Sheet1",
            "Sheet1",
            change);

        Assert.AreEqual("=#REF!", rewritten);
    }

    [TestMethod]
    public void DeleteContainingSingleReferenceProducesRefError()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Delete,
            index: 4,
            count: 2);

        var rewritten = FormulaStructuralReferenceRewriter.Rewrite(
            "=A5+B8",
            "Sheet1",
            "Sheet1",
            change);

        Assert.AreEqual("=#REF!+B6", rewritten);
    }

    [TestMethod]
    public void UnqualifiedReferencesOnOtherWorksheetStayUnchangedWhileQualifiedChangedSheetMoves()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            index: 4,
            count: 1);

        var rewritten = FormulaStructuralReferenceRewriter.Rewrite(
            "=A5+Sheet1!B5+Other!C5",
            "Summary",
            "Sheet1",
            change);

        Assert.AreEqual("=A5+Sheet1!B6+Other!C5", rewritten);
    }

    [TestMethod]
    public void QuotedSheetQualifierAndAbsoluteMarkersArePreserved()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            index: 4,
            count: 2);

        var rewritten = FormulaStructuralReferenceRewriter.Rewrite(
            "='Data Set'!$A$5:$B$10",
            "Summary",
            "Data Set",
            change);

        Assert.AreEqual("='Data Set'!$A$7:$B$12", rewritten);
    }

    [TestMethod]
    public void EscapedQuotedSheetNameIsDecodedForComparisonAndRawQualifierIsPreserved()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Column,
            WorksheetStructuralChangeKind.Insert,
            index: 1,
            count: 1);

        var rewritten = FormulaStructuralReferenceRewriter.Rewrite(
            "='Team ''A'''!B2:C2",
            "Summary",
            "Team 'A'",
            change);

        Assert.AreEqual("='Team ''A'''!C2:D2", rewritten);
    }

    [TestMethod]
    public void StringLiteralsAreNeverRewrittenIncludingEscapedDoubleQuotes()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            index: 4,
            count: 2);

        var rewritten = FormulaStructuralReferenceRewriter.Rewrite(
            "=\"A5:A10 says \"\"A5\"\"\"&A5",
            "Sheet1",
            "Sheet1",
            change);

        Assert.AreEqual("=\"A5:A10 says \"\"A5\"\"\"&A7", rewritten);
    }

    [TestMethod]
    public void ColumnDeleteShrinksReversedRangeWhilePreservingEndpointOrientation()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Column,
            WorksheetStructuralChangeKind.Delete,
            index: 2,
            count: 2);

        var rewritten = FormulaStructuralReferenceRewriter.Rewrite(
            "=F3:B3",
            "Sheet1",
            "Sheet1",
            change);

        Assert.AreEqual("=D3:B3", rewritten);
    }

    [TestMethod]
    public void RangeWithQualifierOnBothEndpointsRewritesWhenBothTargetChangedSheet()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            index: 0,
            count: 1);

        var rewritten = FormulaStructuralReferenceRewriter.Rewrite(
            "=Sheet1!A1:Sheet1!B2",
            "Summary",
            "Sheet1",
            change);

        Assert.AreEqual("=Sheet1!A2:Sheet1!B3", rewritten);
    }
}
