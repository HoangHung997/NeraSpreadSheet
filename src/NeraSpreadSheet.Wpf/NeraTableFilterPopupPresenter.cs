using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Wpf;

/// <summary>
/// Attachable native WPF presenter for Table AutoFilter buttons and value menus.
/// All filter semantics remain in <see cref="SpreadsheetTablePresenterController"/>.
/// </summary>
public sealed class NeraTableFilterPopupPresenter : IDisposable
{
    private const double PopupWidth = 320d;
    private const double PopupMaximumHeight = 440d;

    private readonly NeraSpreadsheetControl _control;
    private readonly List<CheckBox> _valueCheckBoxes = [];
    private SpreadsheetSession? _viewportSession;
    private SpreadsheetViewportEngine? _viewport;
    private SpreadsheetTableFilterNavigator? _navigator;
    private FilterButtonAdorner? _adorner;
    private Popup? _popup;
    private TextBox? _searchBox;
    private Button? _applyButton;
    private bool _disposed;

    public NeraTableFilterPopupPresenter(NeraSpreadsheetControl control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _control.Loaded += OnLoaded;
        _control.Unloaded += OnUnloaded;
        _control.LayoutUpdated += OnLayoutUpdated;
        _control.SizeChanged += OnSizeChanged;
        _control.ScrollChanged += OnScrollChanged;
        _control.PreviewKeyDown += OnControlPreviewKeyDown;
        _control.PreviewMouseMove += OnPreviewMouseMove;
        _control.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        if (_control.IsLoaded)
        {
            AttachAdorner();
        }
    }

    public bool IsOpen => _popup?.IsOpen == true;

    public void Close()
    {
        if (_popup is not null)
        {
            _popup.IsOpen = false;
        }
    }

    public bool TryOpenAt(double x, double y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!TryHitTest(x, y, out var hit))
        {
            return false;
        }

        Open(hit);
        return true;
    }

    public bool TryOpenForActiveCell()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var session = _control.Session;
        if (session is null ||
            !session.TryResolveActiveTableFilterTarget(out var target))
        {
            return false;
        }

        foreach (var hit in GetVisibleButtons())
        {
            if (hit.TableId == target.TableId &&
                hit.ColumnId == target.ColumnId)
            {
                Open(hit);
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Close();
        DetachAdorner();
        _control.Loaded -= OnLoaded;
        _control.Unloaded -= OnUnloaded;
        _control.LayoutUpdated -= OnLayoutUpdated;
        _control.SizeChanged -= OnSizeChanged;
        _control.ScrollChanged -= OnScrollChanged;
        _control.PreviewKeyDown -= OnControlPreviewKeyDown;
        _control.PreviewMouseMove -= OnPreviewMouseMove;
        _control.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        _navigator?.Dispose();
        _navigator = null;
        _disposed = true;
    }

    internal SpreadsheetTableFilterButtonHit[] GetVisibleButtons()
    {
        var session = _control.Session;
        if (session is null ||
            _control.ActualWidth <= 0d ||
            _control.ActualHeight <= 0d ||
            !_control.RenderTheme.ShowTableFilterButtons)
        {
            return [];
        }

        var chrome = SpreadsheetChromeGeometry.Calculate(
            _control.ActualWidth,
            _control.ActualHeight,
            _control.RenderTheme);
        if (chrome.BodyWidth <= 0d || chrome.BodyHeight <= 0d)
        {
            return [];
        }

        if (!ReferenceEquals(_viewportSession, session))
        {
            _viewportSession = session;
            _viewport = new SpreadsheetViewportEngine(session);
        }

        var scroll = _control.ScrollSnapshot;
        var frame = _viewport!.Compose(
            scroll.OffsetX,
            scroll.OffsetY,
            chrome.BodyWidth,
            chrome.BodyHeight,
            overscan: 0d,
            _control.RenderTheme);
        return SpreadsheetTableFilterButtonGeometry.GetVisibleButtons(
                WorksheetSnapshot.Capture(session.ActiveWorksheet),
                frame.Layout,
                _control.RenderTheme)
            .Select(button => button with
            {
                Bounds = button.Bounds.Translate(
                    chrome.RowHeaderWidth,
                    chrome.ColumnHeaderHeight),
            })
            .ToArray();
    }

    private bool TryHitTest(
        double x,
        double y,
        out SpreadsheetTableFilterButtonHit hit)
    {
        var point = new PointD(x, y);
        foreach (var candidate in GetVisibleButtons())
        {
            if (candidate.Bounds.Contains(point))
            {
                hit = candidate;
                return true;
            }
        }

        hit = default;
        return false;
    }

    private void Open(SpreadsheetTableFilterButtonHit hit)
    {
        var session = _control.Session
            ?? throw new InvalidOperationException(
                "A spreadsheet session is required before opening a Table filter menu.");
        Close();
        var focusBeforeOpen = Keyboard.FocusedElement;
        var menu = new SpreadsheetTablePresenterController(session)
            .OpenFilterMenu(hit.TableId, hit.ColumnId);
        var navigator = new SpreadsheetTableFilterNavigator(menu);
        _navigator = navigator;

        var popup = new Popup
        {
            PlacementTarget = _control,
            Placement = PlacementMode.RelativePoint,
            HorizontalOffset = Math.Max(0d, hit.Bounds.Left),
            VerticalOffset = Math.Max(0d, hit.Bounds.Bottom),
            StaysOpen = false,
            AllowsTransparency = true,
            Child = BuildPopupContent(menu, navigator),
        };
        popup.Opened += (_, _) => FocusSearchBox(popup);
        popup.Closed += (_, _) =>
        {
            navigator.Dispose();
            if (ReferenceEquals(_popup, popup))
            {
                _popup = null;
                _navigator = null;
                _searchBox = null;
                _applyButton = null;
                _valueCheckBoxes.Clear();
            }
            RestoreFocus(focusBeforeOpen);
        };
        _popup = popup;
        popup.IsOpen = true;
    }

    private Border BuildPopupContent(
        SpreadsheetTableFilterMenu menu,
        SpreadsheetTableFilterNavigator navigator)
    {
        var root = new Border
        {
            Width = PopupWidth,
            MaxHeight = PopupMaximumHeight,
            Background = Brushes.White,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1d),
            Padding = new Thickness(10d),
            CornerRadius = new CornerRadius(4d),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 12d,
                Opacity = 0.25d,
                ShadowDepth = 2d,
            },
        };
        AutomationProperties.SetAutomationId(root, "NeraTableFilterPopup");
        AutomationProperties.SetName(
            root,
            $"Lọc {menu.ColumnName} trong Table {menu.TableName}");
        KeyboardNavigation.SetTabNavigation(
            root,
            KeyboardNavigationMode.Cycle);
        KeyboardNavigation.SetControlTabNavigation(
            root,
            KeyboardNavigationMode.Cycle);

        var panel = new DockPanel
        {
            LastChildFill = true,
        };
        root.Child = panel;

        var title = new TextBlock
        {
            Text = $"{menu.TableName} — {menu.ColumnName}",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0d, 0d, 0d, 8d),
        };
        AutomationProperties.SetName(title, title.Text);
        DockPanel.SetDock(title, Dock.Top);
        panel.Children.Add(title);

        var search = new TextBox
        {
            Margin = new Thickness(0d, 0d, 0d, 8d),
            ToolTip = "Tìm giá trị; nhấn mũi tên xuống để vào danh sách",
        };
        AutomationProperties.SetAutomationId(search, "NeraTableFilterSearch");
        AutomationProperties.SetName(
            search,
            $"Tìm giá trị trong cột {menu.ColumnName}");
        AutomationProperties.SetHelpText(
            search,
            "Nhấn Enter để áp dụng, Escape để đóng, hoặc mũi tên xuống để duyệt giá trị.");
        _searchBox = search;
        DockPanel.SetDock(search, Dock.Top);
        panel.Children.Add(search);

        var commands = new WrapPanel
        {
            Margin = new Thickness(0d, 0d, 0d, 8d),
        };
        var selectAll = CreateCommandButton(
            "Chọn tất cả",
            "NeraTableFilterSelectAll",
            "Chọn mọi giá trị đang hiển thị; phím tắt Ctrl+A khi con trỏ ở danh sách.");
        var selectNone = CreateCommandButton(
            "Bỏ chọn",
            "NeraTableFilterSelectNone",
            "Bỏ chọn mọi giá trị đang hiển thị; phím tắt Ctrl+Shift+A khi con trỏ ở danh sách.");
        commands.Children.Add(selectAll);
        commands.Children.Add(selectNone);
        DockPanel.SetDock(commands, Dock.Top);
        panel.Children.Add(commands);

        var status = new TextBlock
        {
            Foreground = Brushes.DimGray,
            FontSize = 11d,
            Margin = new Thickness(0d, 0d, 0d, 6d),
        };
        AutomationProperties.SetAutomationId(status, "NeraTableFilterStatus");
        DockPanel.SetDock(status, Dock.Top);
        panel.Children.Add(status);

        var itemsPanel = new StackPanel();
        var scroller = new ScrollViewer
        {
            Content = itemsPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 270d,
            CanContentScroll = true,
        };
        AutomationProperties.SetAutomationId(
            scroller,
            "NeraTableFilterValues");
        panel.Children.Add(scroller);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0d, 8d, 0d, 0d),
        };
        var clear = CreateCommandButton(
            "Xóa lọc",
            "NeraTableFilterClear",
            "Xóa bộ lọc hiện tại của cột này.");
        var cancel = CreateCommandButton(
            "Hủy",
            "NeraTableFilterCancel",
            "Đóng mà không áp dụng thay đổi; phím tắt Escape.");
        var apply = CreateCommandButton(
            "Áp dụng",
            "NeraTableFilterApply",
            "Áp dụng các giá trị đã chọn; phím tắt Enter trong ô tìm kiếm.");
        _applyButton = apply;
        footer.Children.Add(clear);
        footer.Children.Add(cancel);
        footer.Children.Add(apply);
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(footer);

        void RebuildItems(bool restoreValueFocus)
        {
            _valueCheckBoxes.Clear();
            itemsPanel.Children.Clear();
            foreach (var item in menu.GetVisibleItems())
            {
                var value = item.Value;
                var displayText = DisplayValue(value);
                var checkBox = new CheckBox
                {
                    IsChecked = item.IsSelected,
                    Content = $"{displayText}  ({item.Count})",
                    Margin = new Thickness(2d),
                    ToolTip = "Space hoặc Enter để chọn hay bỏ chọn",
                };
                AutomationProperties.SetName(
                    checkBox,
                    $"{displayText}; {item.Count:N0} dòng");
                AutomationProperties.SetHelpText(
                    checkBox,
                    "Dùng mũi tên, Home, End, Page Up, Page Down để di chuyển; Space hoặc Enter để thay đổi lựa chọn.");
                checkBox.GotKeyboardFocus += (_, _) =>
                    navigator.SetActiveValue(value);
                checkBox.Checked += (_, _) =>
                    menu.SelectValue(value, true);
                checkBox.Unchecked += (_, _) =>
                    menu.SelectValue(value, false);
                _valueCheckBoxes.Add(checkBox);
                itemsPanel.Children.Add(checkBox);
            }

            status.Text = menu.ValuesTruncated
                ? $"Đã quét {menu.ScannedRowCount:N0} hàng; danh sách giá trị đã bị giới hạn."
                : $"{menu.DistinctValueCount:N0} giá trị khác nhau trong {menu.ScannedRowCount:N0} hàng.";
            AutomationProperties.SetName(status, status.Text);
            apply.IsEnabled = menu.CanApplyValueSelection;
            if (restoreValueFocus)
            {
                _control.Dispatcher.BeginInvoke(
                    DispatcherPriority.Input,
                    new Action(() => FocusActiveValue(navigator)));
            }
        }

        search.TextChanged += (_, _) =>
        {
            menu.SetSearchText(search.Text);
            RebuildItems(restoreValueFocus: false);
        };
        selectAll.Click += (_, _) =>
        {
            navigator.Handle(
                SpreadsheetTableFilterNavigationCommand.SelectAllVisible);
            RebuildItems(restoreValueFocus: false);
        };
        selectNone.Click += (_, _) =>
        {
            navigator.Handle(
                SpreadsheetTableFilterNavigationCommand.ClearVisibleSelection);
            RebuildItems(restoreValueFocus: false);
        };
        apply.Click += (_, _) =>
        {
            menu.ApplyValueSelection();
            CloseAndRefresh();
        };
        clear.Click += (_, _) =>
        {
            menu.ClearColumnFilter();
            CloseAndRefresh();
        };
        cancel.Click += (_, _) => Close();
        root.PreviewKeyDown += (_, args) =>
            OnPopupPreviewKeyDown(
                args,
                menu,
                navigator,
                RebuildItems);

        RebuildItems(restoreValueFocus: false);
        return root;
    }

    private void OnPopupPreviewKeyDown(
        KeyEventArgs e,
        SpreadsheetTableFilterMenu menu,
        SpreadsheetTableFilterNavigator navigator,
        Action<bool> rebuildItems)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var searchFocused = _searchBox?.IsKeyboardFocusWithin == true;
        var valueFocused = _valueCheckBoxes.Any(static checkBox =>
            checkBox.IsKeyboardFocusWithin);
        var modifiers = e.KeyboardDevice.Modifiers;

        if (key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if ((modifiers & ModifierKeys.Control) != 0 &&
            key == Key.A &&
            !searchFocused)
        {
            navigator.Handle(
                (modifiers & ModifierKeys.Shift) != 0
                    ? SpreadsheetTableFilterNavigationCommand.ClearVisibleSelection
                    : SpreadsheetTableFilterNavigationCommand.SelectAllVisible);
            rebuildItems(valueFocused);
            e.Handled = true;
            return;
        }

        SpreadsheetTableFilterNavigationCommand command;
        switch (key)
        {
            case Key.Down when searchFocused:
                command = SpreadsheetTableFilterNavigationCommand.MoveFirst;
                break;
            case Key.Up when searchFocused:
                command = SpreadsheetTableFilterNavigationCommand.MoveLast;
                break;
            case Key.Down when valueFocused:
                command = SpreadsheetTableFilterNavigationCommand.MoveNext;
                break;
            case Key.Up when valueFocused:
                command = SpreadsheetTableFilterNavigationCommand.MovePrevious;
                break;
            case Key.Home when valueFocused:
                command = SpreadsheetTableFilterNavigationCommand.MoveFirst;
                break;
            case Key.End when valueFocused:
                command = SpreadsheetTableFilterNavigationCommand.MoveLast;
                break;
            case Key.PageUp when valueFocused:
                command = SpreadsheetTableFilterNavigationCommand.PagePrevious;
                break;
            case Key.PageDown when valueFocused:
                command = SpreadsheetTableFilterNavigationCommand.PageNext;
                break;
            case Key.Space when valueFocused:
            case Key.Enter when valueFocused:
                navigator.Handle(
                    SpreadsheetTableFilterNavigationCommand.ToggleCurrent);
                rebuildItems(true);
                e.Handled = true;
                return;
            case Key.Enter when searchFocused:
                if (menu.CanApplyValueSelection)
                {
                    menu.ApplyValueSelection();
                    CloseAndRefresh();
                }
                e.Handled = true;
                return;
            default:
                return;
        }

        navigator.Handle(command);
        FocusActiveValue(navigator);
        e.Handled = true;
    }

    private bool FocusActiveValue(
        SpreadsheetTableFilterNavigator navigator)
    {
        var index = navigator.Capture().ActiveIndex;
        if (index < 0 || index >= _valueCheckBoxes.Count)
        {
            return false;
        }

        var checkBox = _valueCheckBoxes[index];
        checkBox.BringIntoView();
        return checkBox.Focus();
    }

    private void FocusSearchBox(Popup popup)
    {
        _control.Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                if (!ReferenceEquals(_popup, popup) ||
                    !popup.IsOpen ||
                    _searchBox is null)
                {
                    return;
                }

                _searchBox.Focus();
                _searchBox.SelectAll();
            }));
    }

    private void RestoreFocus(IInputElement? focusTarget)
    {
        _control.Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                if (_disposed)
                {
                    return;
                }

                if (focusTarget is not null &&
                    Keyboard.Focus(focusTarget) is not null)
                {
                    return;
                }

                _control.Focus();
            }));
    }

    private static Button CreateCommandButton(
        string text,
        string automationId,
        string helpText)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 74d,
            Margin = new Thickness(2d),
            Padding = new Thickness(8d, 3d, 8d, 3d),
            ToolTip = helpText,
        };
        AutomationProperties.SetAutomationId(button, automationId);
        AutomationProperties.SetName(button, text);
        AutomationProperties.SetHelpText(button, helpText);
        return button;
    }

    private void CloseAndRefresh()
    {
        Close();
        _viewport?.InvalidateMetrics();
        _control.InvalidateVisual();
        _adorner?.InvalidateVisual();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => AttachAdorner();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Close();
        DetachAdorner();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) =>
        _adorner?.InvalidateVisual();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        _adorner?.InvalidateVisual();

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) =>
        _adorner?.InvalidateVisual();

    private void OnControlPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (IsOpen)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if ((e.KeyboardDevice.Modifiers & ModifierKeys.Alt) != 0 &&
            key == Key.Down &&
            TryOpenForActiveCell())
        {
            e.Handled = true;
        }
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        var point = e.GetPosition(_control);
        if (TryHitTest(point.X, point.Y, out _))
        {
            _control.Cursor = Cursors.Hand;
        }
        else if (_control.Cursor == Cursors.Hand)
        {
            _control.Cursor = null;
        }
    }

    private void OnPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var point = e.GetPosition(_control);
        if (!TryOpenAt(point.X, point.Y))
        {
            return;
        }

        e.Handled = true;
    }

    private void AttachAdorner()
    {
        if (_adorner is not null)
        {
            return;
        }

        var layer = AdornerLayer.GetAdornerLayer(_control);
        if (layer is null)
        {
            return;
        }

        _adorner = new FilterButtonAdorner(_control, this);
        layer.Add(_adorner);
    }

    private void DetachAdorner()
    {
        if (_adorner is null)
        {
            return;
        }

        AdornerLayer.GetAdornerLayer(_control)?.Remove(_adorner);
        _adorner = null;
    }

    private static string DisplayValue(CellValue value) =>
        value.IsBlank ? "(Trống)" : value.ToString();

    private static System.Windows.Media.Color ToColor(ColorRgba color) =>
        System.Windows.Media.Color.FromArgb(
            color.Alpha,
            color.Red,
            color.Green,
            color.Blue);

    private sealed class FilterButtonAdorner : Adorner
    {
        private readonly NeraTableFilterPopupPresenter _presenter;

        public FilterButtonAdorner(
            UIElement adornedElement,
            NeraTableFilterPopupPresenter presenter)
            : base(adornedElement)
        {
            _presenter = presenter;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var theme = _presenter._control.RenderTheme;
            foreach (var button in _presenter.GetVisibleButtons())
            {
                var bounds = new Rect(
                    button.Bounds.X,
                    button.Bounds.Y,
                    button.Bounds.Width,
                    button.Bounds.Height);
                var fill = new SolidColorBrush(ToColor(
                    button.IsFiltered
                        ? theme.TableFilterButtonActiveBackground
                        : theme.TableFilterButtonBackground));
                var border = new Pen(
                    new SolidColorBrush(ToColor(theme.TableFilterButtonBorder)),
                    1d);
                drawingContext.DrawRoundedRectangle(
                    fill,
                    border,
                    bounds,
                    2d,
                    2d);

                var centerX = bounds.Left + (bounds.Width / 2d);
                var centerY = bounds.Top + (bounds.Height / 2d) + 1d;
                var glyph = new StreamGeometry();
                using (var context = glyph.Open())
                {
                    var pointsUp = button.IsSorted && button.SortDescending != true;
                    context.BeginFigure(
                        pointsUp
                            ? new Point(centerX, centerY - 3d)
                            : new Point(centerX - 3.5d, centerY - 2d),
                        isFilled: true,
                        isClosed: true);
                    context.LineTo(
                        pointsUp
                            ? new Point(centerX + 3.5d, centerY + 2.5d)
                            : new Point(centerX + 3.5d, centerY - 2d),
                        isStroked: true,
                        isSmoothJoin: false);
                    context.LineTo(
                        pointsUp
                            ? new Point(centerX - 3.5d, centerY + 2.5d)
                            : new Point(centerX, centerY + 2.5d),
                        isStroked: true,
                        isSmoothJoin: false);
                }
                drawingContext.DrawGeometry(
                    new SolidColorBrush(ToColor(theme.TableFilterButtonGlyph)),
                    pen: null,
                    glyph);
            }
        }
    }
}
