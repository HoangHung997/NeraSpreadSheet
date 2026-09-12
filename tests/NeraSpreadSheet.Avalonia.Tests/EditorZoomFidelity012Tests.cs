using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class EditorZoomFidelity012Tests
{
    private const string VietnameseText = "Phiếu yêu cầu nghiệm thu hoàn thành";

    [TestMethod]
    public Task CellEditorChromeAndInsetsScaleWithZoomWithoutChangingCellGeometry() =>
        AvaloniaTestEnvironment.OnUiAsync(() =>
        {
            using var fixture = new Fixture();
            fixture.Session.SetValue(default, VietnameseText);
            fixture.Session.Styles.ApplyToSelection(style => style with
            {
                Font = style.Font with
                {
                    Family = "Times New Roman",
                    Size = 12d,
                    Color = new ColorRgba(23, 23, 23),
                },
                Fill = new CellFillStyle
                {
                    IsVisible = true,
                    Pattern = CellFillPattern.Solid,
                    Color = new ColorRgba(255, 255, 210),
                },
            }, "012 test style");

            Assert.IsTrue(fixture.Control.BeginEdit(focusEditor: false));
            var editor = (TextBox)fixture.Control.Children[0];
            var cellGeometry = fixture.Session.ActiveWorksheet.Dimensions.GetRowHeight(0);

            foreach (var zoom in new[] { 0.50d, 0.65d, 0.75d, 0.85d, 1.00d, 1.25d })
            {
                fixture.Control.Zoom = zoom;
                fixture.Window.UpdateLayout();

                Assert.AreEqual(12d * zoom, editor.FontSize, 0.000001, $"FontSize at {zoom:P0}");
                Assert.AreEqual(0d, editor.BorderThickness.Left, 0.000001, $"Border left at {zoom:P0}");
                Assert.AreEqual(0d, editor.BorderThickness.Top, 0.000001, $"Border top at {zoom:P0}");
                Assert.AreEqual(0d, editor.BorderThickness.Right, 0.000001, $"Border right at {zoom:P0}");
                Assert.AreEqual(0d, editor.BorderThickness.Bottom, 0.000001, $"Border bottom at {zoom:P0}");
                Assert.AreEqual(4d * zoom, editor.Padding.Left, 0.000001, $"Padding left at {zoom:P0}");
                Assert.AreEqual(4d * zoom, editor.Padding.Right, 0.000001, $"Padding right at {zoom:P0}");
                Assert.AreEqual(1d * zoom, editor.Padding.Top, 0.000001, $"Padding top at {zoom:P0}");
                Assert.AreEqual(1d * zoom, editor.Padding.Bottom, 0.000001, $"Padding bottom at {zoom:P0}");
                Assert.IsFalse(editor.IsInactiveSelectionHighlightEnabled, $"Inactive selection at {zoom:P0}");
                Assert.IsTrue(editor.Bounds.Height > 0d, $"Visible editor height at {zoom:P0}");
                Assert.AreEqual(cellGeometry * zoom, editor.Bounds.Height, 0.75d, $"Cell height parity at {zoom:P0}");
                Assert.IsTrue(
                    editor.Bounds.Height + 0.01d >= editor.FontSize + editor.Padding.Top + editor.Padding.Bottom,
                    $"Editor content must fit the physical cell at {zoom:P0}");
            }
        });

    [TestMethod]
    public Task FormulaBarSelectionEndpointsDoNotPaintAnInactiveSelectionBandInsideCell() =>
        AvaloniaTestEnvironment.OnUiAsync(() =>
        {
            using var fixture = new Fixture();
            fixture.Session.SetValue(default, VietnameseText);
            fixture.Control.Zoom = 0.65d;
            Assert.IsTrue(fixture.Control.BeginEdit(focusEditor: false));
            var editor = (TextBox)fixture.Control.Children[0];

            Assert.IsFalse(editor.IsFocused);
            Assert.IsTrue(fixture.Control.UpdateEditorDraft(VietnameseText, 0, VietnameseText.Length));
            fixture.Window.UpdateLayout();

            Assert.AreEqual(0, editor.SelectionStart);
            Assert.AreEqual(VietnameseText.Length, editor.SelectionEnd);
            Assert.IsFalse(editor.IsInactiveSelectionHighlightEnabled);
            Assert.AreEqual(0d, editor.BorderThickness.Top, 0.000001);
            Assert.AreEqual(2.6d, editor.Padding.Left, 0.000001);
        });

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Session = new SpreadsheetSession(new Workbook());
            Control = new NeraSpreadsheetControl { Session = Session };
            Window = new Window { Width = 900, Height = 600, Content = Control };
            Window.Show();
            Window.UpdateLayout();
            Control.Focus();
        }

        public SpreadsheetSession Session { get; }
        public NeraSpreadsheetControl Control { get; }
        public Window Window { get; }

        public void Dispose()
        {
            Window.Content = null;
            Window.Close();
            Control.Dispose();
        }
    }
}
