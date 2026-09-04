using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

public sealed partial class NeraAutoFilterPagedDropDownPresenter
{
    private void UpdateButtons()
    {
        if (_disposed || _control.IsDisposed)
        {
            return;
        }

        var hits = GetVisibleButtons();
        var visibleKeys = new HashSet<FilterButtonKey>();
        foreach (var hit in hits)
        {
            var key = CreateButtonKey(hit);
            visibleKeys.Add(key);
            if (!_buttons.TryGetValue(key, out var button))
            {
                button = CreateFilterButton();
                _buttons.Add(key, button);
                _control.Controls.Add(button);
            }

            button.Tag = hit;
            button.Bounds = ToRectangle(hit.Bounds);
            button.BackColor = ToColor(
                hit.IsFiltered
                    ? _control.RenderTheme.TableFilterButtonActiveBackground
                    : _control.RenderTheme.TableFilterButtonBackground);
            button.ForeColor = ToColor(
                _control.RenderTheme.TableFilterButtonGlyph);
            button.AccessibleName = GetFilterButtonAccessibleName(hit);
            button.AccessibleDescription =
                "Mở menu lọc phân trang bằng Enter, Space hoặc Alt+mũi tên xuống.";
            button.Visible = true;
            button.BringToFront();
        }

        foreach (var key in _buttons.Keys
                     .Where(key => !visibleKeys.Contains(key))
                     .ToArray())
        {
            var button = _buttons[key];
            button.Click -= OnFilterButtonClick;
            _control.Controls.Remove(button);
            button.Dispose();
            _buttons.Remove(key);
        }
    }

    private Button CreateFilterButton()
    {
        var button = new Button
        {
            Text = "▼",
            FlatStyle = FlatStyle.Flat,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            TabStop = true,
            AccessibleRole = AccessibleRole.PushButton,
            Font = new Font(
                _control.Font.FontFamily,
                Math.Max(6f, _control.Font.Size - 2f),
                FontStyle.Regular,
                GraphicsUnit.Point),
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = ToColor(
            _control.RenderTheme.TableFilterButtonBorder);
        button.Click += OnFilterButtonClick;
        return button;
    }

    private string GetFilterButtonAccessibleName(
        SpreadsheetAutoFilterButtonHit hit)
    {
        var session = _control.Session;
        if (session is not null &&
            session.TryResolveAutoFilterTarget(hit.HeaderCell, out var target))
        {
            return $"Lọc cột {target.ColumnName} trong {target.OwnerName}";
        }
        return "Mở bộ lọc bảng tính";
    }

    private void OnFilterButtonClick(object? sender, EventArgs e)
    {
        if (sender is not Button
            {
                Tag: SpreadsheetAutoFilterButtonHit hit,
            } button ||
            _control.Session is not { } session ||
            !session.TryResolveAutoFilterTarget(hit.HeaderCell, out var target))
        {
            return;
        }
        Open(button, hit, target);
    }

    private void OnControlPaint(object? sender, PaintEventArgs e) =>
        UpdateButtons();

    private void OnControlLayoutChanged(object? sender, EventArgs e) =>
        UpdateButtons();

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) =>
        UpdateButtons();

    private void OnControlPreviewKeyDown(
        object? sender,
        PreviewKeyDownEventArgs e)
    {
        if (!IsOpen &&
            e.Alt &&
            e.KeyCode == Keys.Down)
        {
            e.IsInputKey = true;
        }
    }

    private void OnControlKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsOpen &&
            e.Alt &&
            e.KeyCode == Keys.Down &&
            TryOpenForActiveCell())
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void OnControlDisposed(object? sender, EventArgs e) =>
        Dispose();

    private void CloseAndRefresh()
    {
        Close();
        _viewport?.InvalidateMetrics();
        _control.Invalidate();
        UpdateButtons();
    }

    private static FilterButtonKey CreateButtonKey(
        SpreadsheetAutoFilterButtonHit hit) =>
        new(
            hit.OwnerKind,
            hit.TableId,
            hit.TableColumnId,
            hit.WorksheetColumnIndex);

    private static SpreadsheetAutoFilterButtonOwnerKind ToGeometryOwner(
        SpreadsheetAutoFilterOwnerKind ownerKind) =>
        ownerKind switch
        {
            SpreadsheetAutoFilterOwnerKind.Table =>
                SpreadsheetAutoFilterButtonOwnerKind.Table,
            SpreadsheetAutoFilterOwnerKind.Worksheet =>
                SpreadsheetAutoFilterButtonOwnerKind.Worksheet,
            _ => throw new ArgumentOutOfRangeException(nameof(ownerKind)),
        };

    private static Rectangle ToRectangle(RectD bounds) =>
        Rectangle.FromLTRB(
            checked((int)Math.Floor(bounds.Left)),
            checked((int)Math.Floor(bounds.Top)),
            checked((int)Math.Ceiling(bounds.Right)),
            checked((int)Math.Ceiling(bounds.Bottom)));

    private static Color ToColor(ColorRgba color) =>
        Color.FromArgb(
            color.Alpha,
            color.Red,
            color.Green,
            color.Blue);
}
