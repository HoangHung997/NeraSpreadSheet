using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Wpf.Sample;

public sealed partial class RibbonPreviewWindow
{
    private ValueTask<CommandContext?> CollectTableParametersAsync(
        CommandId commandId, string? selectedValue, CommandContext context)
    {
        var id = commandId.Value;
        if (context.Parameter is not null ||
            id is not ("Table.Create" or "Table.Rename" or "Table.Resize" or
                "Table.CalculatedColumn" or "Table.RemoveDuplicates" or "Table.ConvertToRange" or "Table.Column.Insert") &&
            !(id == "Table.TotalsFunction" && selectedValue is null or "Custom") &&
            !(id == "Table.Style" && selectedValue is null))
            return ValueTask.FromResult<CommandContext?>(context);

        var state = _session.TableDesign.Refresh();
        var worksheet = _session.ActiveWorksheet;
        var selection = _session.Selection.Ranges.ToArray();
        var activeCell = _session.Selection.ActiveCell;
        var table = CurrentTable;
        var column = table?.Columns.FirstOrDefault(item => item.Id == state.ColumnId);
        var title = id switch
        {
            "Table.Create" => "Tạo Bảng", "Table.Rename" => "Đổi tên Bảng",
            "Table.Resize" => "Đổi kích thước Bảng", "Table.CalculatedColumn" => "Công thức cột",
            "Table.TotalsFunction" => "Công thức hàng tổng", "Table.Column.Insert" => "Chèn cột Bảng",
            "Table.RemoveDuplicates" => "Loại bỏ trùng lặp", "Table.Style" => "Kiểu Bảng", _ => "Chuyển thành phạm vi",
        };
        var help = id switch
        {
            "Table.Create" => $"Vùng đã chọn: {selection[0]}. Hàng đầu là tiêu đề. Để trống tên để tự đặt.",
            "Table.Rename" => "Nhập tên Bảng. Các tham chiếu có cấu trúc sẽ theo tên mới.",
            "Table.Resize" => $"Nhập phạm vi A1, giữ ô đầu {table?.Range.TopLeft}. Ví dụ: A1:E40.",
            "Table.CalculatedColumn" => $"Cột: {column?.Name}. Nhập công thức; để trống để bỏ công thức cột.",
            "Table.TotalsFunction" => $"Cột: {column?.Name}. Chọn hàm tổng; công thức bên dưới chỉ dùng cho Tùy chỉnh.",
            "Table.Column.Insert" => "Nhập tên cột mới, hoặc để trống để tự đặt tên.",
            "Table.RemoveDuplicates" => "Chọn cột dùng để so sánh. Giữ hàng đầu tiên của mỗi nhóm trùng.",
            "Table.Style" => "Chọn kiểu cho Bảng. Có thể Hoàn tác.",
            _ => $"Chuyển Bảng {table?.Name} thành ô thường. Nội dung ô được giữ. Có thể Hoàn tác.",
        };
        var input = new TextBox
        {
            Text = id switch
            {
                "Table.Rename" => table?.Name ?? "", "Table.Resize" => table?.Range.ToString() ?? "",
                "Table.CalculatedColumn" => column?.CalculatedColumnFormula ?? "",
                "Table.TotalsFunction" => column?.TotalsRowFormula ?? "=SUM(1,2)", _ => "",
            },
            Margin = new Thickness(0, 12, 0, 8), MinHeight = 28,
        };
        AutomationProperties.SetAutomationId(input, "table-parameter-input");
        AutomationProperties.SetName(input, title);
        var choices = new List<CheckBox>();
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap });
        ComboBox? values = null;
        if (selectedValue is null && id is "Table.Style" or "Table.TotalsFunction")
        {
            var command = new CommandDispatcher(_commands).QueryState(commandId);
            values = new ComboBox { ItemsSource = command.ItemsSource, DisplayMemberPath = "Caption", SelectedValuePath = "Value",
                SelectedValue = command.SelectedValue, Margin = new Thickness(0, 12, 0, 8), MinHeight = 28 };
            AutomationProperties.SetAutomationId(values, "table-parameter-choice");
            panel.Children.Add(values);
        }
        if (id == "Table.RemoveDuplicates")
        {
            var columns = new StackPanel();
            foreach (var entry in table!.Columns)
            {
                var choice = new CheckBox { Content = entry.Name, Tag = entry.Id, IsChecked = true, Margin = new Thickness(0, 6, 0, 6) };
                AutomationProperties.SetAutomationId(choice, $"table-column-{entry.Id:N}");
                choices.Add(choice);
                columns.Children.Add(choice);
            }
            panel.Children.Add(new ScrollViewer { Content = columns, MaxHeight = 240, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        }
        else if (id is not ("Table.ConvertToRange" or "Table.Style")) panel.Children.Add(input);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) };
        AutomationProperties.SetAutomationId(error, "table-parameter-error");
        panel.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var dialog = new Window
        {
            Owner = this, Title = title, Width = 460, SizeToContent = SizeToContent.Height,
            MaxHeight = 600, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel,
        };
        AutomationProperties.SetAutomationId(dialog, "table-parameter-dialog");
        var dark = _ribbon.IconTheme is NeraIconTheme.Dark or NeraIconTheme.HighContrastDark;
        dialog.Background = dark ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 34, 38)) : Brushes.White;
        dialog.Foreground = dark ? Brushes.White : Brushes.Black;
        input.Background = dark ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 51, 57)) : Brushes.White;
        input.Foreground = dialog.Foreground;
        error.Foreground = dark ? Brushes.LightSalmon : Brushes.DarkRed;
        object? parameter = null;
        var apply = ShellButton("Áp dụng", () =>
        {
            if (id == "Table.Resize")
            {
                var parts = input.Text.Trim().Split(':');
                if (parts.Length is < 1 or > 2 || !CellAddress.TryParseA1(parts[0], out var first) ||
                    !CellAddress.TryParseA1(parts[^1], out var last))
                { error.Text = "Phạm vi chưa hợp lệ. Nhập địa chỉ như A1:E40."; input.Focus(); return; }
                parameter = new CellRange(first, last);
            }
            else if (id == "Table.RemoveDuplicates")
            {
                var ids = choices.Where(choice => choice.IsChecked == true).Select(choice => (Guid)choice.Tag).ToArray();
                if (ids.Length == 0) { error.Text = "Chọn ít nhất một cột để so sánh."; return; }
                parameter = ids;
            }
            else
            {
                var text = input.Text.Trim();
                if (text.Length == 0 && (id == "Table.Rename" || id == "Table.TotalsFunction" &&
                    (selectedValue ?? values?.SelectedValue as string) == "Custom"))
                { error.Text = "Vui lòng nhập giá trị trước khi áp dụng."; input.Focus(); return; }
                parameter = text.Length == 0 ? null : text;
            }
            if (values is not null)
            {
                if (values.SelectedValue is not string choice) { error.Text = "Chọn một giá trị trước khi áp dụng."; return; }
                parameter = new RibbonItemActivation(choice, parameter);
            }
            dialog.DialogResult = true;
        });
        apply.IsDefault = true;
        AutomationProperties.SetAutomationId(apply, "table-parameter-apply");
        var cancel = new Button { Content = "Hủy", IsCancel = true, Margin = new Thickness(3), Padding = new Thickness(10, 3, 10, 3) };
        cancel.Click += (_, _) => dialog.DialogResult = false;
        AutomationProperties.SetAutomationId(cancel, "table-parameter-cancel");
        buttons.Children.Add(apply);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        var focus = Keyboard.FocusedElement as FrameworkElement;
        var focusId = focus is null ? "" : AutomationProperties.GetAutomationId(focus);
        dialog.Loaded += (_, _) => { if (input.Parent is not null) { input.Focus(); input.SelectAll(); } else apply.Focus(); };
        var accepted = dialog.ShowDialog() == true;
        Dispatcher.BeginInvoke(() =>
        {
            if (_disposed) return;
            var target = focus?.IsVisible == true ? focus : CaptureDescendants<FrameworkElement>(_ribbon)
                .FirstOrDefault(element => focusId.Length > 0 && AutomationProperties.GetAutomationId(element) == focusId);
            if (target is null || !target.Focus()) _sheet.Focus();
        });
        if (!accepted) return ValueTask.FromResult<CommandContext?>(null);
        var current = _session.TableDesign.Refresh();
        if (_disposed || !ReferenceEquals(worksheet, _session.ActiveWorksheet) || current.TableId != state.TableId ||
            current.ColumnId != state.ColumnId || activeCell != _session.Selection.ActiveCell ||
            !selection.SequenceEqual(_session.Selection.Ranges))
        {
            SetStatus("Vùng chọn đã thay đổi. Mở lại lệnh cho vùng hiện tại.");
            return ValueTask.FromResult<CommandContext?>(null);
        }
        return ValueTask.FromResult<CommandContext?>(context with { Parameter = parameter });
    }

    private static string DescribeTableError(Exception exception)
    {
        var message = exception.Message;
        if (message.Contains("top-left", StringComparison.Ordinal)) return "Phạm vi mới phải giữ nguyên ô đầu của Bảng.";
        if (message.Contains("destination cells", StringComparison.Ordinal)) return "Không thể mở rộng Bảng vì các ô đích đã có dữ liệu.";
        if (message.Contains("overlap", StringComparison.Ordinal) || message.Contains("spill", StringComparison.Ordinal))
            return "Phạm vi chồng Bảng khác, ô gộp hoặc vùng kết quả mảng. Chọn phạm vi trống phù hợp.";
        if (message.Contains("reference", StringComparison.OrdinalIgnoreCase) || message.Contains("A1", StringComparison.Ordinal))
            return "Thao tác bị từ chối vì sẽ thay đổi tham chiếu công thức. Kiểm tra công thức liên quan rồi thử lại.";
        if (exception is ArgumentException) return "Giá trị chưa hợp lệ. Kiểm tra tên Bảng/cột, phạm vi hoặc cú pháp công thức; tên phải duy nhất và không trùng địa chỉ ô.";
        if (exception is InvalidOperationException) return "Không thể áp dụng cho Bảng hiện tại. Kiểm tra phạm vi, hàng tổng/tiêu đề, dữ liệu đích và tham chiếu công thức.";
        return $"Không thực hiện được lệnh: {message}";
    }
}
