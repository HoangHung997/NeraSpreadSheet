using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Formulas;

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

    [TestMethod]
    public void HelpCatalogShouldCoverEveryRegisteredNameAndAlias()
    {
        var registry = new BuiltInFormulaFunctionRegistry();
        var expected = registry.Descriptors
            .SelectMany(static descriptor => descriptor.EnumerateFormulaNames())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var assistant = new SpreadsheetFormulaEditingAssistant(
            registry.Descriptors);

        Assert.AreEqual(expected, assistant.FunctionHelp.Count);
        Assert.IsTrue(assistant.FunctionHelp.All(static help =>
            !string.IsNullOrWhiteSpace(help.Signature) &&
            !string.IsNullOrWhiteSpace(help.Description)));
    }

    [TestMethod]
    public void DefaultHelpCatalogShouldIncludeEngineOwnedFunctions()
    {
        var assistant = new SpreadsheetFormulaEditingAssistant();

        Assert.IsTrue(assistant.FunctionHelp.Count >
            new BuiltInFormulaFunctionRegistry().Descriptors.Count);
        Assert.IsTrue(assistant.FunctionHelp.Any(static help => help.Name == "IF"));
        Assert.IsTrue(assistant.FunctionHelp.Any(static help => help.Name == "SEQUENCE"));
        Assert.IsTrue(assistant.FunctionHelp.Any(static help => help.Name == "BYROW"));
    }

    [TestMethod]
    public void FunctionHelpShouldTrackNestedActiveArgument()
    {
        var assistant = new SpreadsheetFormulaEditingAssistant();
        const string formula = "=IF(A1,SUM(B1,B2),\"x,y\")";
        var insideSum = formula.IndexOf("B2", StringComparison.Ordinal) + 1;

        var nested = assistant.GetFunctionHelp(formula, insideSum);
        var outer = assistant.GetFunctionHelp(
            formula,
            formula.IndexOf("x,y", StringComparison.Ordinal) + 2);

        Assert.AreEqual("SUM", nested?.Function.Name);
        Assert.AreEqual(1, nested?.ActiveArgumentIndex);
        Assert.AreEqual("IF", outer?.Function.Name);
        Assert.AreEqual(2, outer?.ActiveArgumentIndex);
        Assert.AreEqual("value_if_false", outer?.ActiveArgument?.Name);
    }
}
