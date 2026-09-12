from pathlib import Path


def replace(path: str, old: str, new: str, count: int = 1) -> None:
    file = Path(path)
    text = file.read_text(encoding="utf-8")
    actual = text.count(old)
    if actual != count:
        raise SystemExit(
            f"{path}: expected {count} occurrence(s), found {actual}: {old[:120]!r}"
        )
    file.write_text(text.replace(old, new), encoding="utf-8")


# Date/time values participate in arithmetic and comparisons through Excel serials.
path = "src/NeraSpreadSheet.Formulas/NeraFormulaEngine.cs"
replace(path, "if (!TryNumber(value, out var number))", "if (!TryNumber(value, context, out var number))")
replace(path, "var comparison = Compare(left, right);", "var comparison = Compare(left, right, context);")
replace(
    path,
    "if (!TryNumber(left, out var leftNumber) ||\n            !TryNumber(right, out var rightNumber))",
    "if (!TryNumber(left, context, out var leftNumber) ||\n            !TryNumber(right, context, out var rightNumber))",
)
replace(
    path,
    """    private static bool TryNumber(
        CellValue value,
        out double number)
    {
        switch (value.Kind)
        {
            case CellValueKind.Number:
                number = (double)value.RawValue!;
                return true;
            case CellValueKind.Boolean:
                number = (bool)value.RawValue! ? 1d : 0d;
                return true;
            case CellValueKind.Blank:
                number = 0d;
                return true;
            default:
                number = 0d;
                return false;
        }
    }""",
    """    private static bool TryNumber(
        CellValue value,
        out double number) =>
        FormulaValueCoercion.TryNumber(value, out number);

    private static bool TryNumber(
        CellValue value,
        IFormulaEvaluationContext context,
        out double number) =>
        FormulaValueCoercion.TryNumber(value, context, out number);""",
)
replace(
    path,
    """    private static int Compare(CellValue left, CellValue right)
    {
        if (TryNumber(left, out var leftNumber) &&
            TryNumber(right, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.Compare(
            left.ToString(),
            right.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }""",
    """    private static int Compare(CellValue left, CellValue right) =>
        Compare(left, right, context: null);

    private static int Compare(
        CellValue left,
        CellValue right,
        IFormulaEvaluationContext? context)
    {
        double leftNumber;
        double rightNumber;
        var leftNumeric = context is null
            ? TryNumber(left, out leftNumber)
            : TryNumber(left, context, out leftNumber);
        var rightNumeric = context is null
            ? TryNumber(right, out rightNumber)
            : TryNumber(right, context, out rightNumber);
        if (leftNumeric && rightNumeric)
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.Compare(
            left.ToString(),
            right.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }""",
)

# Workbook calculations carry the workbook's 1900/1904 basis into formula functions.
path = "src/NeraSpreadSheet.Formulas/WorkbookCalculationEngine.cs"
replace(
    path,
    """          IFormulaSparseRangeContext,
          IFormulaWorkbookMetadataEvaluationContext""",
    """          IFormulaSparseRangeContext,
          IFormulaWorkbookMetadataEvaluationContext,
          IFormulaDateSystemEvaluationContext""",
)
replace(
    path,
    """        public CellAddress CurrentCellAddress => _currentAddress;

        public int WorksheetCount => _workbook.Worksheets.Count;""",
    """        public CellAddress CurrentCellAddress => _currentAddress;

        public ExcelDateSystem DateSystem => _workbook.DateSystem;

        public int WorksheetCount => _workbook.Worksheets.Count;""",
)

# Formatting controls presentation only. General/Number expose the serial.
path = "src/NeraSpreadSheet.Core/ExcelCellValueFormatter.cs"
replace(
    path,
    """            var numeric = value.Kind == CellValueKind.DateTime
                ? ToSerial((DateTime)value.RawValue!, dateSystem)
                : (double)value.RawValue!;""",
    """            var numeric = value.Kind == CellValueKind.DateTime
                ? ExcelDateSerial.ToSerial((DateTime)value.RawValue!, dateSystem)
                : (double)value.RawValue!;""",
)
replace(
    path,
    """                return value.Kind == CellValueKind.DateTime
                    ? ((DateTime)value.RawValue!).ToString(culture)
                    : numeric.ToString("G15", culture);""",
    """                return numeric.ToString("G15", culture);""",
)
replace(
    path,
    """    private static double ToSerial(DateTime dateTime, ExcelDateSystem dateSystem) =>
        dateSystem == ExcelDateSystem.Date1904
            ? (dateTime - new DateTime(1904, 1, 1)).TotalDays
            : dateTime.ToOADate();

    private static DateTime FromSerial(double serial, ExcelDateSystem dateSystem) =>
        dateSystem == ExcelDateSystem.Date1904
            ? new DateTime(1904, 1, 1).AddDays(serial)
            : DateTime.FromOADate(serial);""",
    """    private static DateTime FromSerial(double serial, ExcelDateSystem dateSystem) =>
        ExcelDateSerial.FromSerial(serial, dateSystem);""",
)

# Normalize ISO-date XLSX cells into numeric serials and write legacy DateTime values as numeric cells.
path = "src/NeraSpreadSheet.OpenXml/NeraOpenXmlWorkbookSerializer.cs"
replace(
    path,
    """                workbook.Styles,
                options,
                cancellationToken);""",
    """                workbook.Styles,
                workbook.DateSystem,
                options,
                cancellationToken);""",
    count=1,
)
replace(
    path,
    """        CellStyleCatalog catalog,
        OpenXmlImportOptions options,""",
    """        CellStyleCatalog catalog,
        ExcelDateSystem dateSystem,
        OpenXmlImportOptions options,""",
)
replace(path, ": ReadValue(cell, sharedStrings);", ": ReadValue(cell, sharedStrings, dateSystem);")
replace(
    path,
    """    private static NeraCellValue ReadValue(
        Cell cell,
        SharedStringTable? sharedStrings)""",
    """    private static NeraCellValue ReadValue(
        Cell cell,
        SharedStringTable? sharedStrings,
        ExcelDateSystem dateSystem)""",
)
replace(
    path,
    """            return DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var dateTime)
                ? NeraCellValue.FromDateTime(dateTime)
                : NeraCellValue.Blank;""",
    """            return DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var dateTime)
                ? NeraCellValue.FromNumber(ExcelDateSerial.ToSerial(dateTime, dateSystem))
                : NeraCellValue.Blank;""",
)
replace(
    path,
    """                styleTable,
                options,
                cancellationToken);""",
    """                styleTable,
                workbook.DateSystem,
                options,
                cancellationToken);""",
    count=1,
)
replace(
    path,
    """        OpenXmlStyleTable styleTable,
        OpenXmlExportOptions options,
        CancellationToken cancellationToken)""",
    """        OpenXmlStyleTable styleTable,
        ExcelDateSystem dateSystem,
        OpenXmlExportOptions options,
        CancellationToken cancellationToken)""",
    count=1,
)
replace(
    path,
    """                        styleTable,
                        options,
                        sharedFormulaPlan));""",
    """                        styleTable,
                        dateSystem,
                        options,
                        sharedFormulaPlan));""",
)
replace(
    path,
    """        OpenXmlStyleTable styleTable,
        OpenXmlExportOptions options,
        OpenXmlSharedFormulaExportPlan sharedFormulaPlan)""",
    """        OpenXmlStyleTable styleTable,
        ExcelDateSystem dateSystem,
        OpenXmlExportOptions options,
        OpenXmlSharedFormulaExportPlan sharedFormulaPlan)""",
)
replace(path, "ApplyValue(cell, data.Value, isFormulaResult: true);", "ApplyValue(cell, data.Value, dateSystem, isFormulaResult: true);")
replace(path, "ApplyValue(cell, data.Value, isFormulaResult: false);", "ApplyValue(cell, data.Value, dateSystem, isFormulaResult: false);")
replace(
    path,
    """    private static void ApplyValue(
        Cell cell,
        NeraCellValue value,
        bool isFormulaResult)""",
    """    private static void ApplyValue(
        Cell cell,
        NeraCellValue value,
        ExcelDateSystem dateSystem,
        bool isFormulaResult)""",
)
replace(
    path,
    """            case CellValueKind.DateTime:
                cell.DataType = CellValues.Date;
                cell.CellValue = new OpenXmlCellValue(
                    ((DateTime)value.RawValue!).ToString(
                        "O",
                        CultureInfo.InvariantCulture));
                return;""",
    """            case CellValueKind.DateTime:
                cell.CellValue = new OpenXmlCellValue(
                    ExcelDateSerial.ToSerial(
                        (DateTime)value.RawValue!,
                        dateSystem).ToString(
                            "R",
                            CultureInfo.InvariantCulture));
                if (!isFormulaResult)
                {
                    cell.DataType = CellValues.Number;
                }
                return;""",
)

# Formula bar and in-cell editor: keyboard/caret navigation without visible native scrollbars.
path = "samples/NeraSpreadSheet.Avalonia.Sample/FullShellWindow.cs"
replace(
    path,
    "    private readonly TextBox _formula = new() { AcceptsReturn = true, MinHeight = 30, MaxHeight = 100 };",
    """    private readonly TextBox _formula = new()
    {
        AcceptsReturn = true,
        MinHeight = 30,
        MaxHeight = 100,
        TextWrapping = global::Avalonia.Media.TextWrapping.NoWrap,
        HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
        VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
    };""",
)

path = "src/NeraSpreadSheet.Avalonia/NeraSpreadsheetControl.cs"
replace(
    path,
    """    private readonly TextBox _editor = new()
    {
        IsVisible = false,
        AcceptsReturn = true,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(0),
        MinWidth = 0,
        MinHeight = 0,
        UseLayoutRounding = false,
    };""",
    """    private readonly TextBox _editor = new()
    {
        IsVisible = false,
        AcceptsReturn = true,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(0),
        MinWidth = 0,
        MinHeight = 0,
        UseLayoutRounding = false,
        HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
        VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
        IsInactiveSelectionHighlightEnabled = false,
    };""",
)
