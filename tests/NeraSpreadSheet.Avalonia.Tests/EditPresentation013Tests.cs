using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class EditPresentation013Tests
{
    [TestMethod]
    public Task InCellEditorUsesOpaqueSingleSurfaceWithoutVisibleScrollbars() =>
        AvaloniaTestEnvironment.OnUiAsync(() =>
        {
            var session = new SpreadsheetSession(new Workbook());
            session.SetValue(default, "Phiếu yêu cầu nghiệm thu hoàn thành với nội dung rất dài");
            using var control = new NeraSpreadsheetControl { Session = session, Width = 800, Height = 500 };
            var window = new Window { Width = 800, Height = 500, Content = control };
            try
            {
                window.Show();
                window.UpdateLayout();
                control.Zoom = 0.65d;
                Assert.IsTrue(control.BeginEdit(focusEditor: false));
                window.UpdateLayout();
                var editor = (TextBox)control.Children.Single();

                Assert.IsTrue(editor.IsVisible);
                Assert.IsNotNull(editor.Background, "Editor must cover the display-list text beneath it while editing.");
                Assert.AreEqual(0d, editor.BorderThickness.Left, 1e-9);
                Assert.AreEqual(ScrollBarVisibility.Hidden, editor.HorizontalScrollBarVisibility);
                Assert.AreEqual(ScrollBarVisibility.Hidden, editor.VerticalScrollBarVisibility);
                Assert.IsFalse(editor.IsInactiveSelectionHighlightEnabled);
                Assert.AreEqual(session.ActiveWorksheet.Dimensions.GetRowHeight(0) * 0.65d,
                    editor.Bounds.Height, 0.75d,
                    "Editing must not alter worksheet row geometry.");
            }
            finally
            {
                window.Content = null;
                window.Close();
            }
        });
}
