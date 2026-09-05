using System.Globalization;
using System.Resources;

namespace NeraSpreadSheet.Commands;

/// <summary>
/// Immutable, host-scoped presentation resources. The neutral resource language is
/// Vietnamese; an unavailable culture or key falls back to Vietnamese or the key.
/// This service never changes process/thread culture, command identity or user data.
/// </summary>
public sealed class PresentationLocalization
{
    private static readonly ResourceManager Resources = new(
        "NeraSpreadSheet.Commands.PresentationStrings", typeof(PresentationLocalization).Assembly);
    private readonly Func<string, CultureInfo, string?>? _override;

    /// <summary>Creates a localizer with optional host resource overrides.</summary>
    public PresentationLocalization(CultureInfo culture, Func<string, CultureInfo, string?>? resourceOverride = null)
    {
        ArgumentNullException.ThrowIfNull(culture);
        Culture = CultureInfo.ReadOnly((CultureInfo)culture.Clone());
        _override = resourceOverride;
    }

    /// <summary>Gets the default Vietnamese presentation culture, independent of the embedding application.</summary>
    public static PresentationLocalization Default { get; } = new(CultureInfo.GetCultureInfo("vi-VN"));

    /// <summary>Gets this host's resource and display-formatting culture.</summary>
    public CultureInfo Culture { get; }

    /// <summary>Resolves an SDK resource key. A null override delegates to framework resource fallback.</summary>
    public string Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _override?.Invoke(key, Culture) ?? Resources.GetString(key, Culture) ?? key;
    }

    /// <summary>Formats a localized SDK message; data arguments are never translated.</summary>
    public string Format(string key, params object?[] arguments) => string.Format(Culture, Get(key), arguments);

    /// <summary>Reports whether a key exists in the Vietnamese fallback catalog.</summary>
    public static bool ContainsKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Resources.GetString(key, CultureInfo.InvariantCulture) is not null;
    }

    /// <summary>
    /// Localizes an unchanged built-in descriptor caption. Host descriptor text and
    /// dynamic workbook/formula text retain precedence over SDK defaults.
    /// </summary>
    public string CommandCaption(CommandId commandId, string caption) =>
        DefaultCommandCaptions.TryGetValue(commandId.Value, out var source) &&
        string.Equals(source, caption, StringComparison.Ordinal) ? Get(source) : caption;

    internal static bool IsDefaultCommand(CommandDescriptor descriptor) =>
        DefaultCommandCaptions.TryGetValue(descriptor.Id.Value, out var source) &&
        string.Equals(source, descriptor.Caption, StringComparison.Ordinal);

    private static readonly (string Prefix, int Count, string Key)[] StyleFamilies =
        [
            ("TableStyleLight", 21, "Kiểu sáng {0}"),
            ("TableStyleMedium", 28, "Kiểu trung bình {0}"),
            ("TableStyleDark", 11, "Kiểu tối {0}"),
        ];

    internal string TableStyleCaption(string name)
    {
        foreach (var (prefix, count, key) in StyleFamilies)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal) &&
                int.TryParse(name.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var number) &&
                number >= 1 && number <= count)
            {
                return Format(key, number);
            }
        }
        return name;
    }

    private static readonly Dictionary<string, string> DefaultCommandCaptions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Edit.Undo"] = "Undo",
            ["Edit.Redo"] = "Redo",
            ["Edit.Copy"] = "Copy",
            ["Edit.Cut"] = "Cut",
            ["Edit.Paste"] = "Paste",
            ["Cell.ClearContents"] = "Clear contents",
            ["Formula.RecalculateWorkbook"] = "Recalculate workbook",
            ["Cell.Format.Bold"] = "Bold",
            ["Cell.Format.Italic"] = "Italic",
            ["Cell.Merge"] = "Merge cells",
            ["Cell.Unmerge"] = "Unmerge cells",
            ["Data.SortAscending"] = "Sort ascending",
            ["Data.SortDescending"] = "Sort descending",
            ["Structure.Row.Insert"] = "Insert rows",
            ["Structure.Row.Delete"] = "Delete rows",
            ["Structure.Column.Insert"] = "Insert columns",
            ["Structure.Column.Delete"] = "Delete columns",
            ["Structure.Row.Hide"] = "Ẩn hàng",
            ["Structure.Row.Unhide"] = "Hiện hàng",
            ["Structure.Column.Hide"] = "Ẩn cột",
            ["Structure.Column.Unhide"] = "Hiện cột",
            ["View.FreezePanes"] = "Freeze panes",
            ["View.UnfreezePanes"] = "Unfreeze panes",
            ["View.Split.Undo"] = "Undo split view change",
            ["View.Split.Redo"] = "Redo split view change",
            ["Insert.Chart.Column"] = "Insert column chart",
            ["Insert.Chart.Bar"] = "Insert bar chart",
            ["Insert.Chart.Line"] = "Insert line chart",
            ["Insert.Chart.Pie"] = "Insert pie chart",
            ["Insert.Pivot.Sum"] = "Insert pivot summary",
            ["Table.Create"] = "Tạo Bảng",
            ["Table.Rename"] = "Đổi tên Bảng",
            ["Table.Resize"] = "Đổi kích thước Bảng",
            ["Table.HeaderRow"] = "Hàng tiêu đề",
            ["Table.TotalsRow"] = "Hàng tổng",
            ["Table.FirstColumn"] = "Cột đầu tiên",
            ["Table.LastColumn"] = "Cột cuối cùng",
            ["Table.BandedRows"] = "Hàng xen kẽ",
            ["Table.BandedColumns"] = "Cột xen kẽ",
            ["Table.FilterButtons"] = "Nút lọc",
            ["Table.Style"] = "Kiểu Bảng",
            ["Table.CalculatedColumn"] = "Cột được tính",
            ["Table.TotalsFunction"] = "Hàm tổng",
            ["Table.Row.Insert"] = "Chèn hàng Bảng",
            ["Table.Row.Delete"] = "Xóa hàng Bảng",
            ["Table.Column.Insert"] = "Chèn cột Bảng",
            ["Table.Column.Delete"] = "Xóa cột Bảng",
            ["Table.RemoveDuplicates"] = "Loại bỏ trùng lặp",
            ["Table.ConvertToRange"] = "Chuyển thành phạm vi",
        };
}
