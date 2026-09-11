using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia;

public sealed partial class NeraFormatCellsDialog
{
    private StackPanel BuildBorderFidelity007()
    {
        var panel = Panel();
        Note(panel, "Chọn kiểu nét và màu, sau đó dùng preset hoặc bấm từng cạnh. Outline/Inside được áp theo hình học vùng chọn, không nhân viền ngoài vào mọi ô.");

        _borderLine = ChoiceField(
            panel,
            "format-border-line",
            "Kiểu nét",
            Enum.GetValues<CellBorderLineStyle>()
                .Where(static value => value != CellBorderLineStyle.None)
                .Select(value => (value.ToString(), BorderLineCaption(value))),
            CellBorderLineStyle.Thin.ToString());
        _borderColor = TextField(panel, "format-border-color", "Màu viền (#RRGGBB)", "#000000");

        panel.Children.Add(new TextBlock
        {
            Text = L("Preset"),
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 8, 0, 4),
        });
        var presets = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 8 };
        panel.Children.Add(presets);

        var previewText = new TextBlock
        {
            Text = L("Xem trước"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        var preview = new Border
        {
            Width = 260,
            Height = 112,
            Margin = new Thickness(0, 10, 0, 8),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1),
            Child = previewText,
        };
        AutomationProperties.SetAutomationId(preview, "format-border-preview");
        AutomationProperties.SetName(preview, L("Xem trước đường viền"));
        panel.Children.Add(preview);

        panel.Children.Add(new TextBlock
        {
            Text = L("Cạnh"),
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 4, 0, 4),
        });
        var edges = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 12, LineSpacing = 6 };
        panel.Children.Add(edges);
        var top = BorderEdge("format-border-top", "Trên");
        var bottom = BorderEdge("format-border-bottom", "Dưới");
        var left = BorderEdge("format-border-left", "Trái");
        var right = BorderEdge("format-border-right", "Phải");
        var insideH = BorderEdge("format-border-inside-horizontal", "Trong ngang");
        var insideV = BorderEdge("format-border-inside-vertical", "Trong dọc");
        var diagonalUp = BorderEdge("format-border-diagonal-up", "Chéo lên");
        var diagonalDown = BorderEdge("format-border-diagonal-down", "Chéo xuống");
        foreach (var edge in new[] { top, bottom, left, right, insideH, insideV, diagonalUp, diagonalDown })
            edges.Children.Add(edge);

        void RefreshPreview()
        {
            var selection = _borderSelection;
            if (selection?.ClearAll == true)
            {
                preview.BorderThickness = new Thickness(1);
                preview.BorderBrush = Brushes.LightGray;
                previewText.Text = L("Không có đường viền");
                return;
            }
            preview.BorderBrush = Brushes.Black;
            preview.BorderThickness = new Thickness(
                selection?.Left == true ? 3 : 1,
                selection?.Top == true ? 3 : 1,
                selection?.Right == true ? 3 : 1,
                selection?.Bottom == true ? 3 : 1);
            var details = new List<string>();
            if (selection?.InsideHorizontal == true) details.Add(L("trong ngang"));
            if (selection?.InsideVertical == true) details.Add(L("trong dọc"));
            if (selection?.DiagonalUp == true) details.Add(L("chéo lên"));
            if (selection?.DiagonalDown == true) details.Add(L("chéo xuống"));
            previewText.Text = details.Count == 0 ? L("Xem trước") : string.Join(" · ", details);
        }

        void SetChecks(SpreadsheetBorderSelection selection)
        {
            top.IsChecked = selection.Top;
            bottom.IsChecked = selection.Bottom;
            left.IsChecked = selection.Left;
            right.IsChecked = selection.Right;
            insideH.IsChecked = selection.InsideHorizontal;
            insideV.IsChecked = selection.InsideVertical;
            diagonalUp.IsChecked = selection.DiagonalUp;
            diagonalDown.IsChecked = selection.DiagonalDown;
            _borderSelection = selection;
            RefreshPreview();
        }

        void ReadChecks()
        {
            _borderSelection = new SpreadsheetBorderSelection
            {
                Top = top.IsChecked == true,
                Bottom = bottom.IsChecked == true,
                Left = left.IsChecked == true,
                Right = right.IsChecked == true,
                InsideHorizontal = insideH.IsChecked == true,
                InsideVertical = insideV.IsChecked == true,
                DiagonalUp = diagonalUp.IsChecked == true,
                DiagonalDown = diagonalDown.IsChecked == true,
            };
            RefreshPreview();
        }

        foreach (var edge in new[] { top, bottom, left, right, insideH, insideV, diagonalUp, diagonalDown })
            edge.IsCheckedChanged += (_, _) => ReadChecks();

        presets.Children.Add(BorderPreset("format-border-preset-none", "Không", () => SetChecks(SpreadsheetBorderSelection.None())));
        presets.Children.Add(BorderPreset("format-border-preset-outline", "Viền ngoài", () => SetChecks(SpreadsheetBorderSelection.Outline(new CellBorderSide()))));
        presets.Children.Add(BorderPreset("format-border-preset-inside", "Bên trong", () => SetChecks(SpreadsheetBorderSelection.Inside(new CellBorderSide()))));
        presets.Children.Add(BorderPreset("format-border-preset-all", "Tất cả", () => SetChecks(SpreadsheetBorderSelection.All(new CellBorderSide()))));
        presets.Children.Add(BorderPreset("format-border-preset-bottom", "Viền dưới", () => SetChecks(SpreadsheetBorderSelection.BottomEdge(new CellBorderSide()))));
        return panel;
    }

    private StackPanel BuildProtectionFidelity007()
    {
        var panel = Panel();
        var locked = CheckField(panel, "format-protection-locked", "Khóa ô", CommonFlag(style => style.Protection.Locked));
        Bind("protectionLocked", locked, patch => patch with { ProtectionLocked = locked.IsChecked });
        var hidden = CheckField(panel, "format-protection-hidden", "Ẩn công thức", CommonFlag(style => style.Protection.FormulaHidden));
        Bind("protectionHidden", hidden, patch => patch with { ProtectionFormulaHidden = hidden.IsChecked });
        Note(panel, "Các thuộc tính này có hiệu lực khi trang tính được bảo vệ. Chúng được lưu trong style và round-trip qua XLSX.");
        return panel;
    }

    private SpreadsheetBorderSelection? CurrentBorderSelectionFidelity007()
    {
        if (_borderSelection is null || !_borderSelection.HasChanges || _borderSelection.ClearAll)
            return _borderSelection;
        var style = Enum.Parse<CellBorderLineStyle>(Selected(_borderLine!) ?? CellBorderLineStyle.Thin.ToString());
        var side = new CellBorderSide
        {
            Style = style,
            Color = ParseColor(_borderColor!.Text),
            Width = BorderLineWidth(style),
        };
        return _borderSelection with { Side = side };
    }

    private static double BorderLineWidth(CellBorderLineStyle style) => style switch
    {
        CellBorderLineStyle.Hair => 0.5d,
        CellBorderLineStyle.Medium or CellBorderLineStyle.MediumDashed or
        CellBorderLineStyle.MediumDashDot or CellBorderLineStyle.MediumDashDotDot => 2d,
        CellBorderLineStyle.Thick => 3d,
        _ => 1d,
    };

    private string BorderLineCaption(CellBorderLineStyle style) => style switch
    {
        CellBorderLineStyle.Thin => L("Mảnh"),
        CellBorderLineStyle.Medium => L("Vừa"),
        CellBorderLineStyle.Thick => L("Dày"),
        CellBorderLineStyle.Dashed => L("Nét đứt"),
        CellBorderLineStyle.Dotted => L("Nét chấm"),
        CellBorderLineStyle.DoubleLine => L("Nét đôi"),
        CellBorderLineStyle.Hair => L("Nét tóc"),
        CellBorderLineStyle.MediumDashed => L("Nét đứt vừa"),
        CellBorderLineStyle.DashDot => L("Gạch chấm"),
        CellBorderLineStyle.MediumDashDot => L("Gạch chấm vừa"),
        CellBorderLineStyle.DashDotDot => L("Gạch hai chấm"),
        CellBorderLineStyle.MediumDashDotDot => L("Gạch hai chấm vừa"),
        CellBorderLineStyle.SlantDashDot => L("Gạch chấm xiên"),
        _ => style.ToString(),
    };

    private CheckBox BorderEdge(string id, string caption)
    {
        var check = new CheckBox { Content = L(caption), IsThreeState = false };
        AutomationProperties.SetAutomationId(check, id);
        AutomationProperties.SetName(check, L(caption));
        return check;
    }

    private Button BorderPreset(string id, string caption, Action apply)
    {
        var button = new Button { Content = L(caption), MinWidth = 88, Padding = new Thickness(10, 5) };
        AutomationProperties.SetAutomationId(button, id);
        AutomationProperties.SetName(button, L(caption));
        button.Click += (_, _) => apply();
        return button;
    }
}
