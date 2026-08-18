using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class FormulaAxisMoveReferenceRewriterTests
{
    [TestMethod]
    public void RowMoveMapsLocalAbsoluteAndQuotedSheetReferences()
    {
        var move = new WorksheetAxisMove(
            WorksheetAxis.Row,
            sourceIndex: 1,
            count: 1,
            destinationBoundary: 4);

        var rewritten = FormulaStructuralReferenceRewriter.Rewrite(
            "=A2+$B$4+'Data Set'!C3",
            "Data Set",
            "Data Set",
            move);

        Assert.AreEqual("=A4+$B$3+'Data Set'!C2", rewritten);
    }

    [TestMethod]
    public void CrossSheetMoveRewritesOnlyReferencesToMovedWorksheet()
    {
        var move = new WorksheetAxisMove(
            WorksheetAxis.Column,
            sourceIndex: 1,
            count: 1,
            destinationBoundary: 4);

        var rewritten = FormulaStructuralReferenceRewriter.Rewrite(
            "=Data!B2+Other!B2+C2",
            "Other",
            "Data",
            move);

        Assert.AreEqual("=Data!D2+Other!B2+C2", rewritten);
    }

    [TestMethod]
    public void StringLiteralsRemainUntouched()
    {
        var move = new WorksheetAxisMove(
            WorksheetAxis.Row,
            sourceIndex: 1,
            count: 1,
            destinationBoundary: 4);

        var rewritten = FormulaStructuralReferenceRewriter.Rewrite(
            "=IF(A2=\"A2\",A2,\"Data!A2\")",
            "Data",
            "Data",
            move);

        Assert.AreEqual(
            "=IF(A4=\"A2\",A4,\"Data!A2\")",
            rewritten);
    }

    [TestMethod]
    public void DiscontiguousRangeImageRejectsMove()
    {
        var move = new WorksheetAxisMove(
            WorksheetAxis.Row,
            sourceIndex: 3,
            count: 2,
            destinationBoundary: 1);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            FormulaStructuralReferenceRewriter.Rewrite(
                "=SUM(A3:A4)",
                "Data",
                "Data",
                move));
    }

    [TestMethod]
    public void RangeCoveringEntireAffectedBandRemainsContiguous()
    {
        var move = new WorksheetAxisMove(
            WorksheetAxis.Row,
            sourceIndex: 3,
            count: 2,
            destinationBoundary: 1);

        var rewritten = FormulaStructuralReferenceRewriter.Rewrite(
            "=SUM(A2:A5)",
            "Data",
            "Data",
            move);

        Assert.AreEqual("=SUM(A2:A5)", rewritten);
    }
}
