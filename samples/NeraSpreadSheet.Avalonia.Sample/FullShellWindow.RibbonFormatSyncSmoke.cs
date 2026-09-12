using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class FullShellWindow
{
    internal async void StartRibbonFormatSyncSmoke(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var checks = new List<string>();
        void Check(string id, bool condition)
        {
            if (!condition) throw new InvalidOperationException("Ribbon format sync check failed: " + id);
            checks.Add(id);
        }

        try
        {
            await SettleRibbonAsync();
            Check("native-window", IsVisible && Spreadsheet.ActiveSpreadsheet.RenderedFrameCount > 0);

            var systemFonts = FontManager.Current.SystemFonts
                .Select(static family => family.Name)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            var fontState = State("Ui.FontFamily");
            Check("system-fonts-enumerated", fontState.ItemsSource.Count == systemFonts.Length);
            Check("system-fonts-present", systemFonts.All(name => fontState.ItemsSource.Any(item => string.Equals(item.Value, name, StringComparison.OrdinalIgnoreCase))));

            var firstFont = systemFonts.FirstOrDefault() ?? FontManager.Current.DefaultFontFamily.Name;
            var secondFont = systemFonts.Skip(1).FirstOrDefault() ?? firstFont;
            var first = new CellAddress(5, 5);
            var second = new CellAddress(6, 5);

            StyleCell(first, firstFont, 13, 700, italic: true, underline: true,
                fontColor: new ColorRgba(18, 52, 86), fill: new ColorRgba(210, 220, 230),
                number: "#,##0.000", alignment: CellHorizontalAlignment.Center, wrap: true, border: "all");
            StyleCell(second, secondFont, 17, 400, italic: false, underline: false,
                fontColor: new ColorRgba(120, 40, 80), fill: null,
                number: "dd-mmm-yyyy", alignment: CellHorizontalAlignment.Right, wrap: false, border: "bottom");

            await AssertSelection(first, firstFont, "13", bold: true, italic: true, underline: true,
                "#123456", "#D2DCE6", "#,##0.000", CellHorizontalAlignment.Center, wrap: true, "all", "first");
            await AssertSelection(second, secondFont, "17", bold: false, italic: false, underline: false,
                "#782850", "none", "dd-mmm-yyyy", CellHorizontalAlignment.Right, wrap: false, "bottom", "second");
            await AssertSelection(first, firstFont, "13", bold: true, italic: true, underline: true,
                "#123456", "#D2DCE6", "#,##0.000", CellHorizontalAlignment.Center, wrap: true, "all", "first-return");

            Console.WriteLine("NERA_RIBBON_FORMAT_SYNC_SUCCESS checks=" + checks.Count + " fonts=" + systemFonts.Length);
            lifetime.Shutdown(0);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("NERA_RIBBON_FORMAT_SYNC_FAILURE " + exception);
            lifetime.Shutdown(1);
        }

        void StyleCell(
            CellAddress address,
            string family,
            double size,
            int weight,
            bool italic,
            bool underline,
            ColorRgba fontColor,
            ColorRgba? fill,
            string number,
            CellHorizontalAlignment alignment,
            bool wrap,
            string border)
        {
            Session.Selection.Select(new CellRange(address, address));
            ApplyStyle(style => style with
            {
                Font = style.Font with
                {
                    Family = family,
                    Size = size,
                    Weight = weight,
                    Italic = italic,
                    Underline = underline,
                    DoubleUnderline = false,
                    Color = fontColor,
                },
                Fill = fill is { } fillColor
                    ? new CellFillStyle { IsVisible = true, Pattern = CellFillPattern.Solid, Color = fillColor }
                    : new CellFillStyle(),
                NumberFormat = new CellNumberFormatStyle { FormatCode = number },
                Alignment = style.Alignment with { Horizontal = alignment, WrapText = wrap },
                Border = border == "all"
                    ? FourSides(new CellBorderSide { Style = CellBorderLineStyle.Thin, Width = 1, Color = new ColorRgba(20, 20, 20) })
                    : new CellBorderStyle { Bottom = new CellBorderSide { Style = CellBorderLineStyle.Thin, Width = 1, Color = new ColorRgba(20, 20, 20) } },
            });
        }

        async Task AssertSelection(
            CellAddress address,
            string family,
            string size,
            bool bold,
            bool italic,
            bool underline,
            string fontColor,
            string fill,
            string number,
            CellHorizontalAlignment alignment,
            bool wrap,
            string border,
            string suffix)
        {
            Session.Selection.Select(new CellRange(address, address));
            _runtime.Refresh();
            await SettleRibbonAsync();

            Check("font-family-state-" + suffix, State("Ui.FontFamily").SelectedValue == family);
            Check("font-size-state-" + suffix, State("Ui.FontSize").SelectedValue == size);
            Check("bold-state-" + suffix, State("Cell.Bold").IsChecked == bold);
            Check("italic-state-" + suffix, State("Cell.Italic").IsChecked == italic);
            Check("underline-state-" + suffix, State("Ui.Underline").IsChecked == underline);
            Check("font-color-state-" + suffix, State("Ui.FontColor").SelectedValue == fontColor);
            Check("fill-state-" + suffix, State("Ui.Fill").SelectedValue == fill);
            Check("number-state-" + suffix, State("Ui.Number").SelectedValue == number);
            Check("alignment-state-" + suffix, State("Ui.Align." + alignment).IsChecked == true);
            Check("wrap-state-" + suffix, State("Ui.Wrap").IsChecked == wrap);
            Check("border-state-" + suffix, State("Ui.Borders").SelectedValue == border);

            Check("font-family-native-" + suffix, SelectedTag("ribbon-command-Ui.FontFamily") == family);
            Check("font-size-native-" + suffix, SelectedTag("ribbon-command-Ui.FontSize") == size);
            Check("font-color-native-" + suffix, SelectedTag("ribbon-command-Ui.FontColor") == fontColor);
            Check("fill-native-" + suffix, SelectedTag("ribbon-command-Ui.Fill") == fill);
            Check("number-native-" + suffix, SelectedTag("ribbon-command-Ui.Number") == number);
            Check("border-native-" + suffix, SelectedTag("ribbon-command-Ui.Borders") == border);
            Check("bold-native-" + suffix, FindRibbonControl<ToggleButton>("ribbon-command-Cell.Bold").IsChecked == bold);
            Check("italic-native-" + suffix, FindRibbonControl<ToggleButton>("ribbon-command-Cell.Italic").IsChecked == italic);
            Check("underline-native-" + suffix, FindRibbonControl<ToggleButton>("ribbon-command-Ui.Underline").IsChecked == underline);
            Check("wrap-native-" + suffix, FindRibbonControl<ToggleButton>("ribbon-command-Ui.Wrap").IsChecked == wrap);
        }

        CommandState State(string id)
        {
            if (!_registry.TryResolve(id, out _, out var handler) || handler is not IStatefulCommandHandler stateful)
                throw new InvalidOperationException("Missing stateful command: " + id);
            return stateful.GetState(new CommandContext(Parameter: "format-sync-smoke"));
        }

        string? SelectedTag(string automationId)
        {
            var combo = _ribbon.GetVisualDescendants().OfType<ComboBox>()
                .Single(control => AutomationProperties.GetAutomationId(control) == automationId);
            return (combo.SelectedItem as ComboBoxItem)?.Tag as string;
        }

        static CellBorderStyle FourSides(CellBorderSide side) => new()
        {
            Left = side,
            Top = side,
            Right = side,
            Bottom = side,
        };
    }
}
