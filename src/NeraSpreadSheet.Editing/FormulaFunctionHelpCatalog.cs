using System.Globalization;
using NeraSpreadSheet.Formulas;

namespace NeraSpreadSheet.Editing;

internal static class FormulaFunctionHelpCatalog
{
    private static readonly Dictionary<string, HelpDefinition> Curated =
        CreateCurated();

    public static IReadOnlyList<SpreadsheetFormulaEditingAssistant.CatalogEntry>
        EngineOwnedFunctions { get; } = CreateEngineOwnedFunctions();

    public static FormulaFunctionHelp Create(
        string name,
        int minimumArguments,
        int maximumArguments)
    {
        if (Curated.TryGetValue(name, out var curated))
        {
            return Build(name, curated.Description, curated.Arguments);
        }

        var arguments = CreateGenericArguments(
            minimumArguments,
            maximumArguments);
        var description = maximumArguments == 0
            ? $"Tính hàm {name} mà không cần đối số."
            : minimumArguments == maximumArguments
                ? $"Tính hàm {name} với {minimumArguments} đối số."
                : $"Tính hàm {name} với từ {minimumArguments} đến " +
                  $"{FormatMaximum(maximumArguments)} đối số.";
        return Build(name, description, arguments);
    }

    private static FormulaFunctionHelp Build(
        string name,
        string description,
        IReadOnlyList<FormulaFunctionArgumentHelp> arguments)
    {
        var signature = arguments.Count == 0
            ? $"{name}()"
            : $"{name}({string.Join(", ", arguments.Select(FormatArgument))})";
        return new FormulaFunctionHelp(
            name,
            signature,
            description,
            arguments);
    }

    private static string FormatArgument(FormulaFunctionArgumentHelp argument)
    {
        var name = argument.IsRepeating ? $"{argument.Name}, …" : argument.Name;
        return argument.IsOptional ? $"[{name}]" : name;
    }

    private static IReadOnlyList<FormulaFunctionArgumentHelp>
        CreateGenericArguments(
            int minimumArguments,
            int maximumArguments)
    {
        if (maximumArguments == 0)
        {
            return Array.Empty<FormulaFunctionArgumentHelp>();
        }

        const int maximumDisplayedArguments = 4;
        var fixedCount = Math.Min(
            Math.Max(minimumArguments, 1),
            maximumDisplayedArguments);
        var result = new List<FormulaFunctionArgumentHelp>(
            fixedCount + 1);
        for (var index = 0; index < fixedCount; index++)
        {
            result.Add(new FormulaFunctionArgumentHelp(
                $"value{index + 1}",
                $"Đối số {index + 1} của hàm.",
                IsOptional: index >= minimumArguments));
        }
        if (maximumArguments > fixedCount)
        {
            result.Add(new FormulaFunctionArgumentHelp(
                $"value{fixedCount + 1}",
                "Các đối số bổ sung của hàm.",
                IsOptional: true,
                IsRepeating: true));
        }
        return result;
    }

    private static IReadOnlyList<SpreadsheetFormulaEditingAssistant.CatalogEntry>
        CreateEngineOwnedFunctions()
    {
        static SpreadsheetFormulaEditingAssistant.CatalogEntry E(
            string name,
            int minimum,
            int maximum) => new(name, minimum, maximum);

        return
        [
            E("AREAS", 1, 1),
            E("AVERAGEIF", 2, 3),
            E("AVERAGEIFS", 3, 255),
            E("BYCOL", 2, 2),
            E("BYROW", 2, 2),
            E("CELL", 1, 2),
            E("CHOOSE", 2, 255),
            E("CHOOSECOLS", 2, 255),
            E("CHOOSEROWS", 2, 255),
            E("COLUMN", 0, 1),
            E("COLUMNS", 1, 1),
            E("COUNTIF", 2, 2),
            E("COUNTIFS", 2, 255),
            E("DROP", 2, 3),
            E("EXPAND", 2, 5),
            E("FILTER", 2, 3),
            E("FORMULA", 1, 1),
            E("FORMULATEXT", 1, 1),
            E("FREQUENCY", 2, 2),
            E("GETPIVOTDATA", 2, 255),
            E("GROUPBY", 3, 255),
            E("GROWTH", 1, 4),
            E("HSTACK", 1, 255),
            E("IF", 2, 3),
            E("IFERROR", 2, 2),
            E("IFNA", 2, 2),
            E("IFS", 2, 255),
            E("INDIRECT", 1, 2),
            E("ISFORMULA", 1, 1),
            E("ISOMITTED", 1, 1),
            E("ISREF", 1, 1),
            E("LAMBDA", 1, 255),
            E("LET", 3, 255),
            E("LINEST", 1, 4),
            E("LOGEST", 1, 4),
            E("MAKEARRAY", 3, 3),
            E("MAP", 2, 255),
            E("MINVERSE", 1, 1),
            E("MMULT", 2, 2),
            E("MODE.MULT", 1, 255),
            E("MUNIT", 1, 1),
            E("OFFSET", 3, 5),
            E("PIVOTBY", 3, 255),
            E("RANDARRAY", 0, 5),
            E("REDUCE", 3, 3),
            E("ROW", 0, 1),
            E("ROWS", 1, 1),
            E("SCAN", 3, 3),
            E("SEQUENCE", 1, 4),
            E("SHEET", 0, 1),
            E("SHEETS", 0, 1),
            E("SORT", 1, 4),
            E("SORTBY", 3, 255),
            E("STOCKHISTORY", 2, 11),
            E("SUBTOTAL", 2, 255),
            E("SUMIF", 2, 3),
            E("SUMIFS", 3, 255),
            E("SWITCH", 3, 255),
            E("TAKE", 2, 3),
            E("TEXTSPLIT", 2, 6),
            E("TOCOL", 1, 3),
            E("TOROW", 1, 3),
            E("TRANSPOSE", 1, 1),
            E("TREND", 1, 4),
            E("TRIMRANGE", 1, 1),
            E("UNIQUE", 1, 3),
            E("VSTACK", 1, 255),
            E("WRAPCOLS", 2, 3),
            E("WRAPROWS", 2, 3),
            E("XMATCH", 2, 4),
        ];
    }

    private static string FormatMaximum(int maximum) =>
        maximum == int.MaxValue
            ? "không giới hạn"
            : maximum.ToString(CultureInfo.InvariantCulture);

    private static Dictionary<string, HelpDefinition> CreateCurated()
    {
        var result = new Dictionary<string, HelpDefinition>(
            StringComparer.OrdinalIgnoreCase);
        Add(result, "SUM", "Cộng các số hoặc vùng ô.", Req("number1", "Số hoặc vùng thứ nhất."), Rep("number2", "Các số hoặc vùng bổ sung."));
        Add(result, "AVERAGE", "Trả về trung bình cộng của các số.", Req("number1", "Số hoặc vùng thứ nhất."), Rep("number2", "Các số hoặc vùng bổ sung."));
        Add(result, "COUNT", "Đếm các ô chứa số.", Req("value1", "Giá trị hoặc vùng thứ nhất."), Rep("value2", "Các giá trị hoặc vùng bổ sung."));
        Add(result, "COUNTA", "Đếm các ô không trống.", Req("value1", "Giá trị hoặc vùng thứ nhất."), Rep("value2", "Các giá trị hoặc vùng bổ sung."));
        Add(result, "IF", "Chọn kết quả theo một điều kiện.", Req("logical_test", "Điều kiện cần kiểm tra."), Req("value_if_true", "Kết quả khi điều kiện đúng."), Opt("value_if_false", "Kết quả khi điều kiện sai."));
        Add(result, "IFS", "Kiểm tra nhiều cặp điều kiện và kết quả theo thứ tự.", Req("logical_test1", "Điều kiện thứ nhất."), Req("value_if_true1", "Kết quả thứ nhất."), Rep("logical_test2, value_if_true2", "Các cặp điều kiện và kết quả bổ sung."));
        Add(result, "AND", "Trả về TRUE khi mọi điều kiện đều đúng.", Req("logical1", "Điều kiện thứ nhất."), Rep("logical2", "Các điều kiện bổ sung."));
        Add(result, "OR", "Trả về TRUE khi có ít nhất một điều kiện đúng.", Req("logical1", "Điều kiện thứ nhất."), Rep("logical2", "Các điều kiện bổ sung."));
        Add(result, "NOT", "Đảo giá trị logic.", Req("logical", "Giá trị logic cần đảo."));
        Add(result, "IFERROR", "Trả về giá trị thay thế nếu biểu thức có lỗi.", Req("value", "Biểu thức cần tính."), Req("value_if_error", "Kết quả dùng khi có lỗi."));
        Add(result, "IFNA", "Trả về giá trị thay thế riêng cho lỗi #N/A.", Req("value", "Biểu thức cần tính."), Req("value_if_na", "Kết quả dùng khi có #N/A."));
        Add(result, "VLOOKUP", "Tìm theo cột đầu và trả về giá trị ở cột chỉ định.", Req("lookup_value", "Giá trị cần tìm."), Req("table_array", "Bảng tìm kiếm."), Req("col_index_num", "Số thứ tự cột kết quả."), Opt("range_lookup", "TRUE cho gần đúng, FALSE cho chính xác."));
        Add(result, "HLOOKUP", "Tìm theo hàng đầu và trả về giá trị ở hàng chỉ định.", Req("lookup_value", "Giá trị cần tìm."), Req("table_array", "Bảng tìm kiếm."), Req("row_index_num", "Số thứ tự hàng kết quả."), Opt("range_lookup", "TRUE cho gần đúng, FALSE cho chính xác."));
        Add(result, "XLOOKUP", "Tìm một giá trị và trả về phần tử tương ứng.", Req("lookup_value", "Giá trị cần tìm."), Req("lookup_array", "Vùng dùng để tìm."), Req("return_array", "Vùng chứa kết quả."), Opt("if_not_found", "Giá trị khi không tìm thấy."), Opt("match_mode", "Chế độ khớp."), Opt("search_mode", "Hướng tìm kiếm."));
        Add(result, "INDEX", "Trả về phần tử tại hàng và cột trong một vùng.", Req("array", "Vùng hoặc mảng nguồn."), Req("row_num", "Số thứ tự hàng."), Opt("column_num", "Số thứ tự cột."));
        Add(result, "MATCH", "Trả về vị trí tương đối của giá trị trong vùng.", Req("lookup_value", "Giá trị cần tìm."), Req("lookup_array", "Vùng tìm kiếm."), Opt("match_type", "Kiểu khớp."));
        Add(result, "XMATCH", "Tìm vị trí với chế độ khớp và hướng tìm mở rộng.", Req("lookup_value", "Giá trị cần tìm."), Req("lookup_array", "Vùng tìm kiếm."), Opt("match_mode", "Chế độ khớp."), Opt("search_mode", "Hướng tìm kiếm."));
        Add(result, "SUMIF", "Cộng các ô thỏa một điều kiện.", Req("range", "Vùng kiểm tra."), Req("criteria", "Điều kiện."), Opt("sum_range", "Vùng cần cộng."));
        Add(result, "SUMIFS", "Cộng các ô thỏa nhiều điều kiện.", Req("sum_range", "Vùng cần cộng."), Req("criteria_range1", "Vùng điều kiện thứ nhất."), Req("criteria1", "Điều kiện thứ nhất."), Rep("criteria_range2, criteria2", "Các cặp vùng và điều kiện bổ sung."));
        Add(result, "COUNTIF", "Đếm các ô thỏa một điều kiện.", Req("range", "Vùng kiểm tra."), Req("criteria", "Điều kiện."));
        Add(result, "COUNTIFS", "Đếm các ô thỏa nhiều điều kiện.", Req("criteria_range1", "Vùng điều kiện thứ nhất."), Req("criteria1", "Điều kiện thứ nhất."), Rep("criteria_range2, criteria2", "Các cặp vùng và điều kiện bổ sung."));
        Add(result, "AVERAGEIF", "Tính trung bình các ô thỏa một điều kiện.", Req("range", "Vùng kiểm tra."), Req("criteria", "Điều kiện."), Opt("average_range", "Vùng cần tính trung bình."));
        Add(result, "AVERAGEIFS", "Tính trung bình các ô thỏa nhiều điều kiện.", Req("average_range", "Vùng cần tính trung bình."), Req("criteria_range1", "Vùng điều kiện thứ nhất."), Req("criteria1", "Điều kiện thứ nhất."), Rep("criteria_range2, criteria2", "Các cặp vùng và điều kiện bổ sung."));
        Add(result, "FILTER", "Lọc một mảng theo điều kiện.", Req("array", "Mảng cần lọc."), Req("include", "Mảng điều kiện."), Opt("if_empty", "Giá trị khi không có kết quả."));
        Add(result, "SORT", "Sắp xếp các hàng hoặc cột của một mảng.", Req("array", "Mảng cần sắp xếp."), Opt("sort_index", "Hàng hoặc cột làm khóa."), Opt("sort_order", "1 tăng dần, -1 giảm dần."), Opt("by_col", "TRUE để sắp theo cột."));
        Add(result, "SORTBY", "Sắp xếp một mảng theo một hoặc nhiều vùng khóa.", Req("array", "Mảng cần sắp xếp."), Req("by_array1", "Vùng khóa thứ nhất."), Opt("sort_order1", "Thứ tự của khóa thứ nhất."), Rep("by_array2, sort_order2", "Các cặp khóa và thứ tự bổ sung."));
        Add(result, "UNIQUE", "Trả về danh sách hàng hoặc cột duy nhất.", Req("array", "Mảng nguồn."), Opt("by_col", "So sánh theo cột."), Opt("exactly_once", "Chỉ lấy phần tử xuất hiện đúng một lần."));
        Add(result, "SEQUENCE", "Tạo một mảng số tuần tự.", Req("rows", "Số hàng."), Opt("columns", "Số cột."), Opt("start", "Giá trị bắt đầu."), Opt("step", "Bước tăng."));
        Add(result, "TEXT", "Định dạng một giá trị bằng mã định dạng Excel.", Req("value", "Giá trị cần định dạng."), Req("format_text", "Mã định dạng."));
        Add(result, "DATE", "Tạo ngày từ năm, tháng và ngày.", Req("year", "Năm."), Req("month", "Tháng."), Req("day", "Ngày."));
        Add(result, "TIME", "Tạo thời gian từ giờ, phút và giây.", Req("hour", "Giờ."), Req("minute", "Phút."), Req("second", "Giây."));
        Add(result, "ROUND", "Làm tròn số đến số chữ số chỉ định.", Req("number", "Số cần làm tròn."), Req("num_digits", "Số chữ số."));
        Add(result, "LEFT", "Lấy ký tự từ đầu chuỗi.", Req("text", "Chuỗi nguồn."), Opt("num_chars", "Số ký tự cần lấy."));
        Add(result, "RIGHT", "Lấy ký tự từ cuối chuỗi.", Req("text", "Chuỗi nguồn."), Opt("num_chars", "Số ký tự cần lấy."));
        Add(result, "MID", "Lấy một đoạn ký tự ở giữa chuỗi.", Req("text", "Chuỗi nguồn."), Req("start_num", "Vị trí bắt đầu."), Req("num_chars", "Số ký tự cần lấy."));
        Add(result, "SEARCH", "Tìm chuỗi con, không phân biệt hoa thường.", Req("find_text", "Chuỗi cần tìm."), Req("within_text", "Chuỗi nguồn."), Opt("start_num", "Vị trí bắt đầu."));
        Add(result, "FIND", "Tìm chuỗi con, có phân biệt hoa thường.", Req("find_text", "Chuỗi cần tìm."), Req("within_text", "Chuỗi nguồn."), Opt("start_num", "Vị trí bắt đầu."));
        Add(result, "SUBSTITUTE", "Thay văn bản khớp trong chuỗi.", Req("text", "Chuỗi nguồn."), Req("old_text", "Văn bản cần thay."), Req("new_text", "Văn bản mới."), Opt("instance_num", "Lần xuất hiện cần thay."));
        Add(result, "REPLACE", "Thay một đoạn ký tự theo vị trí.", Req("old_text", "Chuỗi nguồn."), Req("start_num", "Vị trí bắt đầu."), Req("num_chars", "Số ký tự thay."), Req("new_text", "Văn bản mới."));
        Add(result, "CONCAT", "Nối các chuỗi hoặc vùng văn bản.", Req("text1", "Văn bản thứ nhất."), Rep("text2", "Các văn bản bổ sung."));
        Add(result, "TEXTJOIN", "Nối văn bản bằng dấu phân cách.", Req("delimiter", "Dấu phân cách."), Req("ignore_empty", "Có bỏ qua ô trống hay không."), Req("text1", "Văn bản thứ nhất."), Rep("text2", "Các văn bản bổ sung."));
        Add(result, "CHOOSE", "Chọn một giá trị theo chỉ số.", Req("index_num", "Chỉ số giá trị."), Req("value1", "Giá trị thứ nhất."), Rep("value2", "Các giá trị bổ sung."));
        Add(result, "SWITCH", "So sánh biểu thức với nhiều giá trị theo thứ tự.", Req("expression", "Biểu thức cần so sánh."), Req("value1", "Giá trị so sánh thứ nhất."), Req("result1", "Kết quả thứ nhất."), Rep("value2, result2", "Các cặp giá trị và kết quả bổ sung."));
        Add(result, "OFFSET", "Trả về tham chiếu lệch từ một tham chiếu gốc.", Req("reference", "Tham chiếu gốc."), Req("rows", "Số hàng dịch."), Req("cols", "Số cột dịch."), Opt("height", "Chiều cao kết quả."), Opt("width", "Chiều rộng kết quả."));
        Add(result, "INDIRECT", "Chuyển văn bản thành tham chiếu ô hoặc vùng.", Req("ref_text", "Văn bản tham chiếu."), Opt("a1", "TRUE cho kiểu A1, FALSE cho R1C1."));
        Add(result, "LET", "Đặt tên cho kết quả trung gian trong công thức.", Req("name1", "Tên biến thứ nhất."), Req("name_value1", "Giá trị biến thứ nhất."), Rep("calculation_or_name2", "Tên, giá trị bổ sung và biểu thức cuối."));
        Add(result, "LAMBDA", "Tạo một hàm có thể tái sử dụng trong công thức.", Req("parameter_or_calculation", "Các tham số và biểu thức tính cuối."), Rep("parameter_or_calculation2", "Các tham số hoặc biểu thức bổ sung."));
        return result;
    }

    private static FormulaFunctionArgumentHelp Req(string name, string text) =>
        new(name, text);

    private static FormulaFunctionArgumentHelp Opt(string name, string text) =>
        new(name, text, IsOptional: true);

    private static FormulaFunctionArgumentHelp Rep(string name, string text) =>
        new(name, text, IsOptional: true, IsRepeating: true);

    private static void Add(
        IDictionary<string, HelpDefinition> target,
        string name,
        string description,
        params FormulaFunctionArgumentHelp[] arguments) =>
        target.Add(name, new HelpDefinition(description, arguments));

    private sealed record HelpDefinition(
        string Description,
        IReadOnlyList<FormulaFunctionArgumentHelp> Arguments);
}
