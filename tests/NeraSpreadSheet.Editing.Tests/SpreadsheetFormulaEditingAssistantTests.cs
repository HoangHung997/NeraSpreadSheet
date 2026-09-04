using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetFormulaEditingAssistantTests
{
    [TestMethod]
    public void SuggestionsShouldMatchFunctionFragmentIgnoringCase()
    {
        var assistant = new SpreadsheetFormulaEditingAssistant();

        var suggestions = assistant.GetSuggestions("=su", 3);

        Assert.IsTrue(suggestions.Any(static item => item.Name == "SUM"));
        Assert.IsTrue(suggestions.All(static item =>
            item.Name.StartsWith("SU", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void SuggestionsShouldIgnoreWorksheetReferenceFragments()
    {
        var assistant = new SpreadsheetFormulaEditingAssistant();

        var suggestions = assistant.GetSuggestions("=Sheet1!SU", 10);

        Assert.AreEqual(0, suggestions.Count);
    }

    [TestMethod]
    public void ApplySuggestionShouldAddOpeningParenthesis()
    {
        var assistant = new SpreadsheetFormulaEditingAssistant();
        var suggestion = assistant.GetSuggestions("=su", 3)
            .Single(static item => item.Name == "SUM");

        var result = SpreadsheetFormulaEditingAssistant.ApplySuggestion(
            "=su",
            3,
            suggestion);

        Assert.AreEqual("=SUM(", result.Text);
        Assert.AreEqual(5, result.CaretIndex);
    }

    [TestMethod]
    public void PointModeShouldReplaceProvisionalRangeDuringDrag()
    {
        var assistant = new SpreadsheetFormulaEditingAssistant();
        var first = SpreadsheetFormulaEditingAssistant.InsertReference(
            "=SUM(",
            5,
            new CellRange(
                new CellAddress(1, 1),
                new CellAddress(1, 1)));

        var dragged = SpreadsheetFormulaEditingAssistant.InsertReference(
            first.Text,
            first.CaretIndex,
            new CellRange(
                new CellAddress(1, 1),
                new CellAddress(3, 2)),
            provisionalSpan: first.InsertedSpan);

        Assert.AreEqual("=SUM(B2:C4", dragged.Text);
        Assert.AreEqual(10, dragged.CaretIndex);
    }

    [TestMethod]
    public void PointModeShouldQuoteCrossSheetNames()
    {
        var assistant = new SpreadsheetFormulaEditingAssistant();

        var result = SpreadsheetFormulaEditingAssistant.InsertReference(
            "=",
            1,
            new CellRange(default, default),
            "Sales Data");

        Assert.AreEqual("='Sales Data'!A1", result.Text);
    }
}
