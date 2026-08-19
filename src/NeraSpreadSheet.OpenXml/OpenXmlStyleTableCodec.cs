using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraBorderStyle = NeraSpreadSheet.Core.CellBorderStyle;
using NeraBorderSide = NeraSpreadSheet.Core.CellBorderSide;
using NeraCellStyle = NeraSpreadSheet.Core.CellStyle;
using OpenXmlBorder = DocumentFormat.OpenXml.Spreadsheet.Border;
using OpenXmlCellStyle = DocumentFormat.OpenXml.Spreadsheet.CellStyle;
using OpenXmlFill = DocumentFormat.OpenXml.Spreadsheet.Fill;
using OpenXmlFont = DocumentFormat.OpenXml.Spreadsheet.Font;

namespace NeraSpreadSheet.OpenXml;

internal sealed class OpenXmlStyleTable
{
    private const uint FirstCustomNumberFormatId = 164U;
    private readonly List<NeraCellStyle> _styles;
    private readonly Dictionary<NeraCellStyle, uint> _styleIndexes;

    private OpenXmlStyleTable(IEnumerable<NeraCellStyle> styles)
    {
        ArgumentNullException.ThrowIfNull(styles);
        _styles = styles.ToList();
        if (_styles.Count == 0)
        {
            throw new InvalidDataException("An OpenXml style table must contain at least one cell format.");
        }
        _styleIndexes = new Dictionary<NeraCellStyle, uint>();
        for (var index = 0; index < _styles.Count; index++)
        {
            _styleIndexes.TryAdd(_styles[index], (uint)index);
        }
    }

    public int Count => _styles.Count;

    public NeraCellStyle GetStyle(uint styleIndex) =>
        styleIndex < _styles.Count
            ? _styles[(int)styleIndex]
            : NeraCellStyle.Default;

    public uint GetOrAddStyle(NeraCellStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        if (_styleIndexes.TryGetValue(style, out var existing))
        {
            return existing;
        }
        var index = checked((uint)_styles.Count);
        _styles.Add(style);
        _styleIndexes.Add(style, index);
        return index;
    }

    public static OpenXmlStyleTable CreateForExport(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var styles = workbook.Styles.Snapshot();
        if (styles.Count == 0 || styles[0] != NeraCellStyle.Default)
        {
            throw new InvalidDataException("The first exported Nera style must be the default style.");
        }

        var table = new OpenXmlStyleTable(styles);
        foreach (var worksheet in workbook.Worksheets)
        {
            var state = worksheet.CaptureAxisStyleState();
            foreach (var span in state.RowSpans)
            {
                table.GetOrAddStyle(ComposeAxisOperations(span.Operations));
            }
            foreach (var span in state.ColumnSpans)
            {
                table.GetOrAddStyle(ComposeAxisOperations(span.Operations));
            }
        }
        return table;
    }

    public static OpenXmlStyleTable Read(
        WorkbookPart workbookPart,
        CellStyleCatalog catalog,
        IReadOnlyList<NeraCellStyle>? exactCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        ArgumentNullException.ThrowIfNull(catalog);
        var stylesheet = workbookPart.WorkbookStylesPart?.Stylesheet;
        var cellFormatElements = stylesheet?.CellFormats?
            .Elements<CellFormat>()
            .ToArray() ?? [];
        if (cellFormatElements.Length == 0)
        {
            if (exactCatalog is { Count: > 0 })
            {
                return new OpenXmlStyleTable(exactCatalog);
            }
            return new OpenXmlStyleTable([NeraCellStyle.Default]);
        }

        if (exactCatalog is not null && exactCatalog.Count > cellFormatElements.Length)
        {
            throw new InvalidDataException(
                "The exact Nera style catalog contains more entries than the XLSX cell-format table.");
        }

        var numberFormats = ReadNumberFormats(stylesheet!);
        var styles = new List<NeraCellStyle>(cellFormatElements.Length);
        for (var index = 0; index < cellFormatElements.Length; index++)
        {
            var style = exactCatalog is not null && index < exactCatalog.Count
                ? exactCatalog[index]
                : ReadCellStyle(stylesheet!, cellFormatElements[index], numberFormats);
            styles.Add(style);
            catalog.Intern(style);
        }
        return new OpenXmlStyleTable(styles);
    }

    public void Write(WorkbookPart workbookPart)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        var stylesPart = workbookPart.WorkbookStylesPart ??
            workbookPart.AddNewPart<WorkbookStylesPart>();

        var fonts = new Fonts();
        var fills = new Fills();
        var borders = new Borders();
        var cellFormats = new CellFormats();
        var numberFormats = new NumberingFormats();
        var fontIds = new Dictionary<CellFontStyle, uint>();
        var fillIds = new Dictionary<CellFillStyle, uint>();
        var borderIds = new Dictionary<NeraBorderStyle, uint>();
        var numberFormatIds = new Dictionary<string, uint>(StringComparer.Ordinal);

        AddFont(fonts, fontIds, NeraCellStyle.Default.Font);
        AddDefaultFills(fills, fillIds);
        AddBorder(borders, borderIds, NeraCellStyle.Default.Border);
        numberFormatIds["General"] = 0U;
        uint nextNumberFormatId = FirstCustomNumberFormatId;

        foreach (var style in _styles)
        {
            var fontId = AddFont(fonts, fontIds, style.Font);
            var fillId = AddFill(fills, fillIds, style.Fill);
            var borderId = AddBorder(borders, borderIds, style.Border);
            var numberFormatId = ResolveNumberFormatId(
                style.NumberFormat.FormatCode,
                numberFormatIds,
                numberFormats,
                ref nextNumberFormatId);
            var alignment = BuildAlignment(style.Alignment);
            cellFormats.Append(new CellFormat
            {
                FontId = fontId,
                FillId = fillId,
                BorderId = borderId,
                NumberFormatId = numberFormatId,
                FormatId = 0U,
                ApplyFont = true,
                ApplyFill = true,
                ApplyBorder = true,
                ApplyNumberFormat = numberFormatId != 0U,
                ApplyAlignment = alignment is not null,
                Alignment = alignment,
            });
        }

        fonts.Count = checked((uint)fonts.ChildElements.Count);
        fills.Count = checked((uint)fills.ChildElements.Count);
        borders.Count = checked((uint)borders.ChildElements.Count);
        cellFormats.Count = checked((uint)cellFormats.ChildElements.Count);
        numberFormats.Count = checked((uint)numberFormats.ChildElements.Count);

        var cellStyleFormats = new CellStyleFormats(
            new CellFormat
            {
                NumberFormatId = 0U,
                FontId = 0U,
                FillId = 0U,
                BorderId = 0U,
            })
        {
            Count = 1U,
        };
        var cellStyles = new CellStyles(
            new OpenXmlCellStyle
            {
                Name = "Normal",
                FormatId = 0U,
                BuiltinId = 0U,
            })
        {
            Count = 1U,
        };

        var stylesheet = new Stylesheet();
        if (numberFormats.ChildElements.Count > 0)
        {
            stylesheet.Append(numberFormats);
        }
        stylesheet.Append(fonts);
        stylesheet.Append(fills);
        stylesheet.Append(borders);
        stylesheet.Append(cellStyleFormats);
        stylesheet.Append(cellFormats);
        stylesheet.Append(cellStyles);
        stylesPart.Stylesheet = stylesheet;
        stylesheet.Save();
    }

    private static NeraCellStyle ComposeAxisOperations(
        WorksheetAxisStyleOperation[] operations)
    {
        var style = NeraCellStyle.Default;
        foreach (var operation in operations)
        {
            style = operation.Patch.Apply(style);
        }
        return style;
    }

    private static Dictionary<uint, string> ReadNumberFormats(Stylesheet stylesheet)
    {
        var formats = new Dictionary<uint, string>();
        if (stylesheet.NumberingFormats is null)
        {
            return formats;
        }
        foreach (var format in stylesheet.NumberingFormats.Elements<NumberingFormat>())
        {
            if (format.NumberFormatId?.Value is uint id &&
                !string.IsNullOrWhiteSpace(format.FormatCode?.Value))
            {
                formats[id] = format.FormatCode!.Value!;
            }
        }
        return formats;
    }

    private static NeraCellStyle ReadCellStyle(
        Stylesheet stylesheet,
        CellFormat cellFormat,
        IReadOnlyDictionary<uint, string> numberFormats)
    {
        var font = GetElement(stylesheet.Fonts, cellFormat.FontId?.Value)
            as OpenXmlFont;
        var fill = GetElement(stylesheet.Fills, cellFormat.FillId?.Value)
            as OpenXmlFill;
        var border = GetElement(stylesheet.Borders, cellFormat.BorderId?.Value)
            as OpenXmlBorder;
        var alignment = cellFormat.Alignment;
        var numberFormatId = cellFormat.NumberFormatId?.Value ?? 0U;
        return new NeraCellStyle
        {
            Font = ReadFont(font),
            Fill = ReadFill(fill),
            Border = ReadBorder(border),
            Alignment = ReadAlignment(alignment),
            NumberFormat = new CellNumberFormatStyle
            {
                FormatCode = ResolveNumberFormatCode(numberFormatId, numberFormats),
            },
        };
    }

    private static OpenXmlElement? GetElement(
        OpenXmlCompositeElement? parent,
        uint? index)
    {
        if (parent is null || index is null || index.Value >= parent.ChildElements.Count)
        {
            return null;
        }
        return parent.ChildElements[(int)index.Value];
    }

    private static CellFontStyle ReadFont(OpenXmlFont? font)
    {
        if (font is null)
        {
            return NeraCellStyle.Default.Font;
        }
        return new CellFontStyle
        {
            Family = font.FontName?.Val?.Value ?? NeraCellStyle.Default.Font.Family,
            Size = font.FontSize?.Val?.Value is double size && double.IsFinite(size) && size > 0d
                ? size
                : NeraCellStyle.Default.Font.Size,
            Weight = font.Bold is null ? 400 : 700,
            Italic = font.Italic is not null,
            Underline = font.Underline is not null,
            Color = ReadColor(font.Color, NeraCellStyle.Default.Font.Color),
        };
    }

    private static CellFillStyle ReadFill(OpenXmlFill? fill)
    {
        var pattern = fill?.PatternFill;
        if (pattern?.PatternType?.Value != PatternValues.Solid)
        {
            return new CellFillStyle();
        }
        return new CellFillStyle
        {
            IsVisible = true,
            Color = ReadColor(pattern.ForegroundColor, ColorRgba.Transparent),
        };
    }

    private static NeraBorderStyle ReadBorder(OpenXmlBorder? border) => new()
    {
        Left = ReadBorderSide(border?.LeftBorder),
        Top = ReadBorderSide(border?.TopBorder),
        Right = ReadBorderSide(border?.RightBorder),
        Bottom = ReadBorderSide(border?.BottomBorder),
    };

    private static NeraBorderSide ReadBorderSide(BorderPropertiesType? side)
    {
        if (side?.Style?.Value is not BorderStyleValues styleValue)
        {
            return new NeraBorderSide();
        }

        var style = CellBorderLineStyle.Thin;
        var width = 1d;
        if (styleValue.Equals(BorderStyleValues.Medium))
        {
            style = CellBorderLineStyle.Medium;
            width = 2d;
        }
        else if (styleValue.Equals(BorderStyleValues.Thick))
        {
            style = CellBorderLineStyle.Thick;
            width = 3d;
        }
        else if (styleValue.Equals(BorderStyleValues.Dashed))
        {
            style = CellBorderLineStyle.Dashed;
        }
        else if (styleValue.Equals(BorderStyleValues.Dotted))
        {
            style = CellBorderLineStyle.Dotted;
        }
        else if (styleValue.Equals(BorderStyleValues.Double))
        {
            style = CellBorderLineStyle.DoubleLine;
            width = 2d;
        }

        return new NeraBorderSide
        {
            Style = style,
            Width = width,
            Color = ReadColor(side.Color, ColorRgba.Black),
        };
    }

    private static CellAlignmentStyle ReadAlignment(Alignment? alignment)
    {
        if (alignment is null)
        {
            return NeraCellStyle.Default.Alignment;
        }
        var rotation = alignment.TextRotation?.Value is uint rawRotation
            ? rawRotation <= 90U
                ? (int)rawRotation
                : rawRotation <= 180U
                    ? 90 - (int)rawRotation
                    : 0
            : 0;
        var horizontal = alignment.Horizontal?.Value;
        var vertical = alignment.Vertical?.Value;
        return new CellAlignmentStyle
        {
            Horizontal = horizontal?.Equals(HorizontalAlignmentValues.Left) == true
                ? CellHorizontalAlignment.Left
                : horizontal?.Equals(HorizontalAlignmentValues.Center) == true
                    ? CellHorizontalAlignment.Center
                    : horizontal?.Equals(HorizontalAlignmentValues.Right) == true
                        ? CellHorizontalAlignment.Right
                        : CellHorizontalAlignment.General,
            Vertical = vertical?.Equals(VerticalAlignmentValues.Top) == true
                ? CellVerticalAlignment.Top
                : vertical?.Equals(VerticalAlignmentValues.Center) == true
                    ? CellVerticalAlignment.Center
                    : CellVerticalAlignment.Bottom,
            WrapText = alignment.WrapText?.Value ?? false,
            TextRotationDegrees = rotation,
        };
    }

    private static ColorRgba ReadColor(ColorType? color, ColorRgba fallback)
    {
        var rgb = color?.Rgb?.Value;
        if (string.IsNullOrWhiteSpace(rgb))
        {
            return fallback;
        }
        var normalized = rgb.Length == 8 ? rgb : rgb.Length == 6 ? $"FF{rgb}" : null;
        if (normalized is null ||
            !uint.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
        {
            return fallback;
        }
        return new ColorRgba(
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF),
            (byte)((argb >> 24) & 0xFF));
    }

    private static uint AddFont(
        Fonts fonts,
        Dictionary<CellFontStyle, uint> ids,
        CellFontStyle font)
    {
        if (ids.TryGetValue(font, out var existing))
        {
            return existing;
        }
        var id = checked((uint)ids.Count);
        var element = new OpenXmlFont(
            new FontName { Val = font.Family },
            new FontSize { Val = font.Size });
        if (font.Weight >= 600)
        {
            element.Append(new Bold());
        }
        if (font.Italic)
        {
            element.Append(new Italic());
        }
        if (font.Underline)
        {
            element.Append(new Underline());
        }
        element.Append(new Color { Rgb = ToArgb(font.Color) });
        fonts.Append(element);
        ids.Add(font, id);
        return id;
    }

    private static void AddDefaultFills(
        Fills fills,
        Dictionary<CellFillStyle, uint> ids)
    {
        var none = new CellFillStyle();
        fills.Append(new OpenXmlFill(new PatternFill { PatternType = PatternValues.None }));
        ids[none] = 0U;
        fills.Append(new OpenXmlFill(new PatternFill { PatternType = PatternValues.Gray125 }));
    }

    private static uint AddFill(
        Fills fills,
        Dictionary<CellFillStyle, uint> ids,
        CellFillStyle fill)
    {
        if (ids.TryGetValue(fill, out var existing))
        {
            return existing;
        }
        var id = checked((uint)fills.ChildElements.Count);
        OpenXmlFill element;
        if (!fill.IsVisible)
        {
            element = new OpenXmlFill(new PatternFill { PatternType = PatternValues.None });
        }
        else
        {
            element = new OpenXmlFill(new PatternFill(
                new ForegroundColor { Rgb = ToArgb(fill.Color) },
                new BackgroundColor { Indexed = 64U })
            {
                PatternType = PatternValues.Solid,
            });
        }
        fills.Append(element);
        ids.Add(fill, id);
        return id;
    }

    private static uint AddBorder(
        Borders borders,
        Dictionary<NeraBorderStyle, uint> ids,
        NeraBorderStyle border)
    {
        if (ids.TryGetValue(border, out var existing))
        {
            return existing;
        }
        var id = checked((uint)ids.Count);
        borders.Append(new OpenXmlBorder(
            BuildLeftBorder(border.Left),
            BuildRightBorder(border.Right),
            BuildTopBorder(border.Top),
            BuildBottomBorder(border.Bottom),
            new DiagonalBorder()));
        ids.Add(border, id);
        return id;
    }

    private static LeftBorder BuildLeftBorder(NeraBorderSide side) =>
        ApplyBorder(new LeftBorder(), side);

    private static RightBorder BuildRightBorder(NeraBorderSide side) =>
        ApplyBorder(new RightBorder(), side);

    private static TopBorder BuildTopBorder(NeraBorderSide side) =>
        ApplyBorder(new TopBorder(), side);

    private static BottomBorder BuildBottomBorder(NeraBorderSide side) =>
        ApplyBorder(new BottomBorder(), side);

    private static T ApplyBorder<T>(T element, NeraBorderSide side)
        where T : BorderPropertiesType
    {
        element.Style = side.Style switch
        {
            CellBorderLineStyle.None => null,
            CellBorderLineStyle.Medium => BorderStyleValues.Medium,
            CellBorderLineStyle.Thick => BorderStyleValues.Thick,
            CellBorderLineStyle.Dashed => BorderStyleValues.Dashed,
            CellBorderLineStyle.Dotted => BorderStyleValues.Dotted,
            CellBorderLineStyle.DoubleLine => BorderStyleValues.Double,
            _ => BorderStyleValues.Thin,
        };
        if (side.Style != CellBorderLineStyle.None)
        {
            element.Append(new Color { Rgb = ToArgb(side.Color) });
        }
        return element;
    }

    private static Alignment? BuildAlignment(CellAlignmentStyle alignment)
    {
        if (alignment == NeraCellStyle.Default.Alignment)
        {
            return null;
        }
        uint? rotation = alignment.TextRotationDegrees switch
        {
            > 0 => checked((uint)alignment.TextRotationDegrees),
            < 0 => checked((uint)(90 - alignment.TextRotationDegrees)),
            _ => null,
        };
        return new Alignment
        {
            Horizontal = alignment.Horizontal switch
            {
                CellHorizontalAlignment.Left => HorizontalAlignmentValues.Left,
                CellHorizontalAlignment.Center => HorizontalAlignmentValues.Center,
                CellHorizontalAlignment.Right => HorizontalAlignmentValues.Right,
                _ => null,
            },
            Vertical = alignment.Vertical switch
            {
                CellVerticalAlignment.Top => VerticalAlignmentValues.Top,
                CellVerticalAlignment.Center => VerticalAlignmentValues.Center,
                CellVerticalAlignment.Bottom => VerticalAlignmentValues.Bottom,
                _ => null,
            },
            WrapText = alignment.WrapText,
            TextRotation = rotation,
        };
    }

    private static uint ResolveNumberFormatId(
        string formatCode,
        Dictionary<string, uint> ids,
        NumberingFormats formats,
        ref uint nextId)
    {
        if (ids.TryGetValue(formatCode, out var existing))
        {
            return existing;
        }
        var id = nextId++;
        formats.Append(new NumberingFormat
        {
            NumberFormatId = id,
            FormatCode = formatCode,
        });
        ids.Add(formatCode, id);
        return id;
    }

    private static string ResolveNumberFormatCode(
        uint id,
        IReadOnlyDictionary<uint, string> customFormats)
    {
        if (customFormats.TryGetValue(id, out var custom))
        {
            return custom;
        }
        return id switch
        {
            0U => "General",
            1U => "0",
            2U => "0.00",
            3U => "#,##0",
            4U => "#,##0.00",
            5U => "$#,##0_);($#,##0)",
            6U => "$#,##0_);[Red]($#,##0)",
            7U => "$#,##0.00_);($#,##0.00)",
            8U => "$#,##0.00_);[Red]($#,##0.00)",
            9U => "0%",
            10U => "0.00%",
            11U => "0.00E+00",
            12U => "# ?/?",
            13U => "# ??/??",
            14U => "m/d/yyyy",
            15U => "d-mmm-yy",
            16U => "d-mmm",
            17U => "mmm-yy",
            18U => "h:mm AM/PM",
            19U => "h:mm:ss AM/PM",
            20U => "h:mm",
            21U => "h:mm:ss",
            22U => "m/d/yy h:mm",
            37U => "#,##0 ;(#,##0)",
            38U => "#,##0 ;[Red](#,##0)",
            39U => "#,##0.00;(#,##0.00)",
            40U => "#,##0.00;[Red](#,##0.00)",
            45U => "mm:ss",
            46U => "[h]:mm:ss",
            47U => "mmss.0",
            48U => "##0.0E+0",
            49U => "@",
            _ => "General",
        };
    }

    private static string ToArgb(ColorRgba color) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}");
}
