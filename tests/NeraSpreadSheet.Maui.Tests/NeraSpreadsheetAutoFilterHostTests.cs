using Microsoft.Maui.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using Windows.System;

namespace NeraSpreadSheet.Maui.Tests;

[TestClass]
public sealed class NeraSpreadsheetAutoFilterHostTests
{
    [TestMethod]
    public void HostTypeExposesTheSharedSpreadsheetContract()
    {
        var type = typeof(NeraSpreadsheetAutoFilterHost);

        Assert.IsTrue(typeof(Grid).IsAssignableFrom(type));
        Assert.IsNotNull(type.GetProperty(nameof(NeraSpreadsheetAutoFilterHost.Workbook)));
        Assert.IsNotNull(type.GetProperty(nameof(NeraSpreadsheetAutoFilterHost.Spreadsheet)));
        Assert.IsNotNull(type.GetProperty(nameof(NeraSpreadsheetAutoFilterHost.IsFilterSheetOpen)));
        Assert.IsNotNull(type.GetMethod(nameof(NeraSpreadsheetAutoFilterHost.TryOpenForActiveCell)));
        Assert.IsNotNull(type.GetMethod(nameof(NeraSpreadsheetAutoFilterHost.TryOpenFilter)));
        Assert.IsNotNull(type.GetMethod(nameof(NeraSpreadsheetAutoFilterHost.CloseFilterSheet)));
        Assert.IsNotNull(type.GetMethod(nameof(NeraSpreadsheetAutoFilterHost.GetDatePageAsync)));
        Assert.IsNotNull(type.GetMethod(nameof(NeraSpreadsheetAutoFilterHost.ApplyRichFilterAsync)));
        Assert.IsNotNull(type.GetMethod(nameof(NeraSpreadsheetAutoFilterHost.ApplyColumnSortAsync)));
        Assert.IsNotNull(type.GetMethod(nameof(NeraSpreadsheetAutoFilterHost.ReapplyAsync)));
        Assert.IsNotNull(type.GetMethod(nameof(NeraSpreadsheetAutoFilterHost.ClearSortAsync)));
        Assert.AreEqual(
            typeof(CollectionView),
            type.GetField("_dateValues", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType);
        Assert.AreEqual(
            typeof(Entry),
            type.GetField("_secondCriterionInput", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType);
        Assert.AreEqual(
            typeof(Picker),
            type.GetField("_conditionJoinPicker", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType);
    }

    [TestMethod]
    public void FilterTargetsResolveWithoutConstructingANativeVisualTree()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        worksheet.SetValue(new CellAddress(0, 0), "Status");
        worksheet.SetValue(new CellAddress(1, 0), "Open");
        worksheet.AddTable(new SpreadsheetTable(
            tableId,
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(1, 0)),
            [new SpreadsheetTableColumn(columnId, "Status")]));
        worksheet.SetValue(new CellAddress(0, 2), "Region");
        worksheet.SetValue(new CellAddress(1, 2), "North");
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(0, 2),
                new CellAddress(1, 2))));
        var session = new SpreadsheetSession(workbook);

        Assert.IsTrue(session.TryResolveAutoFilterTarget(
            new CellAddress(1, 0),
            out var tableTarget));
        Assert.AreEqual(
            SpreadsheetAutoFilterOwnerKind.Table,
            tableTarget.OwnerKind);
        Assert.IsTrue(session.TryResolveAutoFilterTarget(
            new CellAddress(1, 2),
            out var worksheetTarget));
        Assert.AreEqual(
            SpreadsheetAutoFilterOwnerKind.Worksheet,
            worksheetTarget.OwnerKind);
    }

    [TestMethod]
    public void WindowsKeyboardRoutingLeavesEditorPickerAndButtonKeysUnclaimed()
    {
        Assert.IsFalse(NeraSpreadsheetAutoFilterHost.ShouldHandlePlatformFilterKey(
            VirtualKey.Home,
            searchFocused: true,
            valuesFocused: false,
            dateValuesFocused: false));
        Assert.IsFalse(NeraSpreadsheetAutoFilterHost.ShouldHandlePlatformFilterKey(
            VirtualKey.Enter,
            searchFocused: false,
            valuesFocused: false,
            dateValuesFocused: false));
        Assert.IsFalse(NeraSpreadsheetAutoFilterHost.ShouldHandlePlatformFilterKey(
            VirtualKey.PageDown,
            searchFocused: false,
            valuesFocused: false,
            dateValuesFocused: false));

        Assert.IsTrue(NeraSpreadsheetAutoFilterHost.ShouldHandlePlatformFilterKey(
            VirtualKey.Down,
            searchFocused: true,
            valuesFocused: false,
            dateValuesFocused: false));
        Assert.IsTrue(NeraSpreadsheetAutoFilterHost.ShouldHandlePlatformFilterKey(
            VirtualKey.End,
            searchFocused: false,
            valuesFocused: true,
            dateValuesFocused: false));
        Assert.IsTrue(NeraSpreadsheetAutoFilterHost.ShouldHandlePlatformFilterKey(
            VirtualKey.PageUp,
            searchFocused: false,
            valuesFocused: false,
            dateValuesFocused: true));
        Assert.IsTrue(NeraSpreadsheetAutoFilterHost.ShouldHandlePlatformFilterKey(
            VirtualKey.Escape,
            searchFocused: false,
            valuesFocused: false,
            dateValuesFocused: false));
    }

    [TestMethod]
    public void ProductionTableHostExposesSortActions()
    {
        Assert.IsNotNull(typeof(NeraSpreadsheetTableHost).GetMethod(
            nameof(NeraSpreadsheetTableHost.ApplyColumnSortAsync)));
        Assert.IsNotNull(typeof(NeraSpreadsheetTableHost).GetMethod(
            nameof(NeraSpreadsheetTableHost.ReapplyAsync)));
        Assert.IsNotNull(typeof(NeraSpreadsheetTableHost).GetMethod(
            nameof(NeraSpreadsheetTableHost.ClearSortAsync)));
    }
}
