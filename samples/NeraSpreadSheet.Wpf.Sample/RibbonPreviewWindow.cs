using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Wpf.Sample;

/// <summary>A runnable SDK Ribbon sample with real spreadsheet command bindings.</summary>
public sealed partial class RibbonPreviewWindow : Window, IDisposable
{
    private PresentationLocalization Localization => _runtime?.Localization ?? PresentationLocalization.Default;
    private readonly PreviewDockPanel _root = new() { Background = Brushes.White };
    private readonly TextBlock _status = new() { Margin = new Thickness(12, 5, 12, 5) };
    private readonly TextBlock _address = new() { Width = 84, Margin = new Thickness(12, 6, 8, 6) };
    private readonly TextBlock _formula = new() { Margin = new Thickness(10, 6, 8, 6) };
    private readonly NeraSpreadsheetControl _sheet = new() { UseAdaptiveNavigationExtent = true };
    private readonly SpreadsheetSession _session;
    private readonly CommandRegistry _commands = new();
    private readonly RibbonRuntimeController _runtime;
    private readonly NeraRibbonControl _ribbon;
    private readonly ListBox _worksheetTabs;
    private readonly IDisposable _shortcuts;
    private IReadOnlyList<SpreadsheetTableStyleGalleryItem>? _gallerySource;
    private readonly Dictionary<string, RibbonGalleryPreview> _galleryThumbnails = new(StringComparer.Ordinal);
    private NeraAutoFilterPagedPopupPresenter? _filterPopup;
    private bool _showGridlines = true;
    private bool _disposed;

    public RibbonPreviewWindow() : this(CreatePreviewSession())
    {
    }

    /// <summary>Creates the complete sample shell over an existing workbook session without replacing its state.</summary>
    public RibbonPreviewWindow(SpreadsheetSession session, string? workbookTitle = null)
    {
        Title = "NeraSpreadSheet · Ribbon SDK";
        Width = 1280;
        Height = 800;
        MinWidth = 640;
        MinHeight = 480;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 12;
        Background = Brushes.White;
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _sheet.Session = _session;
        RegisterPreviewCommands();
        _runtime = new RibbonRuntimeController(CreatePreviewDefinition(), _commands);
        RibbonCommandCatalogAudit.Validate(_commands, _runtime.Definition, RibbonProductionCommandCatalog.CommandIds);
        _runtime.ActivationContextProvider = CollectTableParametersAsync;
        _ribbon = new NeraRibbonControl(_runtime) { VerticalAlignment = VerticalAlignment.Top };
        _ribbon.BindTableDesign(_session);
        _shortcuts = _ribbon.BindShortcuts(this);
        _ribbon.CommandActivationFailed += OnCommandActivationFailed;
        _runtime.SnapshotChanged += OnPreviewStateChanged;
        _session.Selection.Changed += OnPreviewStateChanged;
        _session.ActiveWorksheetChanged += OnPreviewStateChanged;
        var title = new TextBlock
        {
            Text = workbookTitle ?? Localization.Get("NERA  /  Bảng tính bán hàng"),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(28, 91, 111)),
            Margin = new Thickness(14, 10, 14, 8),
        };
        DockPanel.SetDock(title, Dock.Top);
        _root.Children.Add(title);
        DockPanel.SetDock(_ribbon, Dock.Top);
        _root.Children.Add(_ribbon);
        var formulaRow = new DockPanel { Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(247, 249, 250)) };
        formulaRow.Children.Add(_address);
        formulaRow.Children.Add(new TextBlock { Text = Localization.Get("ƒx"), Margin = new Thickness(8, 5, 8, 5), FontStyle = FontStyles.Italic });
        formulaRow.Children.Add(_formula);
        DockPanel.SetDock(formulaRow, Dock.Top);
        _root.Children.Add(formulaRow);
        var footer = new DockPanel { Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 247)) };
        var tools = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        tools.Children.Add(ShellButton(Localization.Get("Tùy biến Ribbon"), () =>
            new NeraRibbonCustomizationDialog(_runtime) { Owner = this, IconTheme = _ribbon.IconTheme }.ShowDialog()));
        tools.Children.Add(ShellButton(Localization.Get("Thu gọn / Mở rộng"), () => _ribbon.IsMinimized = !_ribbon.IsMinimized));
        var theme = new ComboBox
        {
            ItemsSource = new[] { Localization.Get("Sáng"), Localization.Get("Tối"), Localization.Get("Tương phản sáng"), Localization.Get("Tương phản tối") },
            SelectedIndex = 0,
            Width = 150,
            Margin = new Thickness(6, 3, 8, 3),
        };
        theme.SelectionChanged += (_, _) => SetTheme((NeraIconTheme)theme.SelectedIndex);
        tools.Children.Add(theme);
        DockPanel.SetDock(tools, Dock.Right);
        footer.Children.Add(tools);
        footer.Children.Add(_status);
        DockPanel.SetDock(footer, Dock.Bottom);
        _root.Children.Add(footer);
        _worksheetTabs = CreateWorksheetTabs();
        DockPanel.SetDock(_worksheetTabs, Dock.Bottom);
        _root.Children.Add(_worksheetTabs);
        _root.Children.Add(CreateWorksheetNavigation());
        Content = _root;
        Closed += (_, _) => Dispose();
        UpdateSelectionText();
        SetStatus(Localization.Get("Sẵn sàng · Lệnh chỉnh sửa dùng lịch sử Hoàn tác của workbook"));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _runtime.ActivationContextProvider = null;
        _runtime.SnapshotChanged -= OnPreviewStateChanged;
        _session.Selection.Changed -= OnPreviewStateChanged;
        _session.ActiveWorksheetChanged -= OnPreviewStateChanged;
        _ribbon.CommandActivationFailed -= OnCommandActivationFailed;
        _worksheetTabs.SelectionChanged -= OnWorksheetTabSelectionChanged;
        _worksheetTabs.SizeChanged -= OnWorksheetTabsSizeChanged;
        DisposeWorksheetNavigation();
        _filterPopup?.Dispose();
        _shortcuts.Dispose();
        _ribbon.Dispose();
        _sheet.Dispose();
        GC.SuppressFinalize(this);
    }

    private static Button ShellButton(string text, Action action)
    {
        var button = new Button { Content = text, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(3) };
        button.Click += (_, _) => action();
        return button;
    }

    private void SetTheme(NeraIconTheme theme)
    {
        _ribbon.IconTheme = theme;
        if (_filterPopup is not null) _filterPopup.IconTheme = theme;
    }
    private void SetStatus(string text) => _status.Text = Localization.Get(text);

    private void OnCommandActivationFailed(object? sender, NeraWpfCommandActivationFailedEventArgs e) =>
        SetStatus(DescribeTableError(e.Exception));

    private void OnPreviewStateChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(UpdateSelectionText);

    private void UpdateSelectionText()
    {
        if (_disposed) return;
        var address = _session.Selection.ActiveCell;
        _address.Text = address.ToString();
        var cell = _session.ActiveWorksheet.GetCell(address);
        _formula.Text = cell.Formula ?? cell.Value.ToString();
        SynchronizeWorksheetTabs();
        _sheet.InvalidateVisual();
        QueueNavigationRefresh();
    }

    private static SpreadsheetSession CreatePreviewSession()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        workbook.RenameWorksheet(sheet, "Bán hàng");
        workbook.AddWorksheet("Trang trống");
        string[] headers = ["Sản phẩm", "Khu vực", "Số lượng", "Đơn giá", "Thành tiền"];
        for (var column = 0; column < headers.Length; column++)
        {
            sheet.SetValue(new CellAddress(0, column), headers[column]);
            sheet.Dimensions.SetColumnWidth(column, column == 0 ? 220 : 155);
        }
        string[] products = ["Sổ tay", "Bút mực", "Giấy in", "Bìa hồ sơ", "Kẹp giấy"];
        for (var row = 1; row < 32; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), products[(row - 1) % products.Length]);
            sheet.SetValue(new CellAddress(row, 1), row % 2 == 0 ? "Miền Bắc" : "Miền Nam");
            sheet.SetValue(new CellAddress(row, 2), 10d + row * 3);
            sheet.SetValue(new CellAddress(row, 3), 15000d + (row % 5) * 8000);
        }
        var session = new SpreadsheetSession(workbook);
        session.Tables.Add(new SpreadsheetTable(
            Guid.NewGuid(), "BanHang", new CellRange(default, new CellAddress(32, 4)),
            headers.Select(name => new SpreadsheetTableColumn(Guid.NewGuid(), name)),
            hasTotalsRow: true, styleName: "TableStyleMedium2", showRowStripes: true));
        var table = sheet.Tables.Single();
        session.Tables.SetCalculatedColumnFormula(table.Id, table.Columns[^1].Id, "=[@[Số lượng]]*[@[Đơn giá]]");
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        session.Recalculate();
        return session;
    }

    private RibbonGalleryPreview? CreateStylePreview(NeraSpreadSheet.Commands.CommandItem choice)
    {
        var source = _session.TableDesign.Snapshot.Styles;
        if (!ReferenceEquals(source, _gallerySource))
        {
            _gallerySource = source;
            _galleryThumbnails.Clear();
        }
        if (_galleryThumbnails.TryGetValue(choice.Value, out var cached)) return cached;
        var cells = source.FirstOrDefault(entry => entry.Name == choice.Value)?.Preview;
        if (cells is null || cells.Count == 0) return null;
        var preview = new RibbonGalleryPreview(cells.Max(cell => cell.RowIndex) + 1, cells.Max(cell => cell.ColumnIndex) + 1,
            cells.Select(cell => new RibbonGalleryPreviewCell(
            Argb(cell.Style.Fill.IsVisible ? cell.Style.Fill.Color : ColorRgba.White),
            Argb(cell.Style.Font.Color))));
        _galleryThumbnails.Add(choice.Value, preview);
        return preview;
    }

    private static uint Argb(ColorRgba color) =>
        ((uint)color.Alpha << 24) | ((uint)color.Red << 16) | ((uint)color.Green << 8) | color.Blue;

    private static double ParseNumber(string value) => double.Parse(value, CultureInfo.InvariantCulture);

    private sealed class PreviewDockPanel : DockPanel
    {
        // A loaded native Window may be capped by the monitor's maximum track
        // size. Capture the complete arranged logical surface without stretching
        // the capped layout clip; interactive windows retain normal clipping.
        public bool CaptureFullLayout { get; set; }
        protected override Geometry? GetLayoutClip(Size layoutSlotSize) =>
            CaptureFullLayout ? null : base.GetLayoutClip(layoutSlotSize);
    }
}
