using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class DifferentialAndFuzzHardeningTests
{
    private const int ArithmeticSeed = 0x4E455241;
    private const int DependencySeed = 0x4450465A;
    private const int MalformedSeed = 0x46555A5A;
    private const int ArithmeticCaseCount = 1_000;
    private const int DependencyCaseCount = 250;
    private const int MalformedCaseCount = 2_000;
    private static readonly JsonSerializerOptions CorpusJsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    [TestMethod]
    public void DifferentialCorpus_LockedScalarCasesMatchExpectedOutcomes()
    {
        var engine = new NeraFormulaEngine();
        var cases = LoadCorpus();

        foreach (var testCase in cases)
        {
            var result = engine.Evaluate(testCase.Formula, EmptyContext.Instance);
            switch (testCase.Kind)
            {
                case "number":
                    Assert.IsTrue(result.IsSuccess, Message(testCase, result));
                    Assert.AreEqual(
                        double.Parse(testCase.Expected, CultureInfo.InvariantCulture),
                        GetNumber(result.Value),
                        1e-10,
                        Message(testCase, result));
                    break;
                case "boolean":
                    Assert.IsTrue(result.IsSuccess, Message(testCase, result));
                    Assert.AreEqual(
                        bool.Parse(testCase.Expected),
                        result.Value.RawValue,
                        Message(testCase, result));
                    break;
                case "text":
                    Assert.IsTrue(result.IsSuccess, Message(testCase, result));
                    Assert.AreEqual(testCase.Expected, result.Value.RawValue, Message(testCase, result));
                    break;
                case "error":
                    Assert.IsFalse(result.IsSuccess, Message(testCase, result));
                    Assert.AreEqual(testCase.Expected, result.Value.RawValue, Message(testCase, result));
                    break;
                default:
                    Assert.Fail($"Unknown corpus kind '{testCase.Kind}' for '{testCase.Id}'.");
                    break;
            }
        }
    }

    [TestMethod]
    public void SeededArithmeticDifferentialFuzz_OneThousandExpressionsMatchIndependentOracle()
    {
        var random = new Random(ArithmeticSeed);
        var engine = new NeraFormulaEngine();

        for (var index = 0; index < ArithmeticCaseCount; index++)
        {
            var expression = GenerateExpression(random, depth: 4);
            var formula = "=" + expression.Text;
            var result = engine.Evaluate(formula, EmptyContext.Instance);

            Assert.IsTrue(
                result.IsSuccess,
                $"seed={ArithmeticSeed}; case={index}; formula={formula}; actual={result.Value.RawValue}");
            Assert.AreEqual(
                expression.Value,
                GetNumber(result.Value),
                1e-9,
                $"seed={ArithmeticSeed}; case={index}; formula={formula}");
        }
    }

    [TestMethod]
    public void SeededDependencyFuzz_RandomCellExpressionsMatchReferenceValuesAndDependencies()
    {
        var random = new Random(DependencySeed);
        var engine = new NeraFormulaEngine();

        for (var index = 0; index < DependencyCaseCount; index++)
        {
            var selected = Enumerable.Range(0, 16)
                .OrderBy(_ => random.Next())
                .Take(random.Next(1, 9))
                .ToArray();
            var values = new Dictionary<CellAddress, CellValue>();
            var terms = new List<string>(selected.Length);
            var expected = 0d;

            foreach (var ordinal in selected)
            {
                var row = ordinal / 4;
                var column = ordinal % 4;
                var address = new CellAddress(row, column);
                var value = random.Next(-5000, 5001) / 10d;
                values[address] = CellValue.FromNumber(value);
                terms.Add(ToA1(address));
                expected += value;
            }

            var formula = "=" + string.Join("+", terms);
            var result = engine.Evaluate(formula, new DictionaryContext(values));
            Assert.IsTrue(
                result.IsSuccess,
                $"seed={DependencySeed}; case={index}; formula={formula}; actual={result.Value.RawValue}");
            Assert.AreEqual(expected, GetNumber(result.Value), 1e-9, formula);

            var actualDependencies = result.Dependencies
                .Select(static dependency => dependency.Range.TopLeft)
                .ToHashSet();
            CollectionAssert.AreEquivalent(
                selected.Select(static ordinal => new CellAddress(ordinal / 4, ordinal % 4)).ToArray(),
                actualDependencies.ToArray(),
                $"seed={DependencySeed}; case={index}; formula={formula}");
        }
    }

    [TestMethod]
    public void MalformedFormulaFuzz_TwoThousandInputsNeverEscapeAsUnhandledExceptions()
    {
        var random = new Random(MalformedSeed);
        var engine = new NeraFormulaEngine();
        const string alphabet = "ABCXYZ0123456789+-*/^&=<>(),.:!$#?\" ";

        for (var index = 0; index < MalformedCaseCount; index++)
        {
            var length = random.Next(1, 97);
            var builder = new StringBuilder(length + 1).Append('=');
            for (var character = 0; character < length; character++)
            {
                builder.Append(alphabet[random.Next(alphabet.Length)]);
            }

            var formula = builder.ToString();
            try
            {
                _ = engine.Evaluate(formula, EmptyContext.Instance);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                Assert.Fail(
                    $"seed={MalformedSeed}; case={index}; formula={formula}; " +
                    $"exception={exception.GetType().FullName}: {exception.Message}");
            }
        }
    }

    private static List<CorpusCase> LoadCorpus()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "differential-scalar-v1.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<CorpusCase>>(json, CorpusJsonOptions) ??
            throw new InvalidOperationException("Differential scalar corpus could not be loaded.");
    }

    private static GeneratedExpression GenerateExpression(Random random, int depth)
    {
        if (depth <= 0 || random.NextDouble() < 0.28)
        {
            var value = random.Next(-200, 201);
            return new GeneratedExpression(value.ToString(CultureInfo.InvariantCulture), value);
        }

        var left = GenerateExpression(random, depth - 1);
        var operation = random.Next(5);
        if (operation == 3)
        {
            var divisor = random.Next(1, 21) * (random.Next(2) == 0 ? -1 : 1);
            return new GeneratedExpression(
                $"({left.Text}/{divisor.ToString(CultureInfo.InvariantCulture)})",
                left.Value / divisor);
        }
        if (operation == 4)
        {
            var basis = random.Next(-10, 11);
            var exponent = random.Next(0, 5);
            return new GeneratedExpression(
                $"({basis.ToString(CultureInfo.InvariantCulture)}^{exponent.ToString(CultureInfo.InvariantCulture)})",
                Math.Pow(basis, exponent));
        }

        var right = GenerateExpression(random, depth - 1);
        return operation switch
        {
            0 => new GeneratedExpression($"({left.Text}+{right.Text})", left.Value + right.Value),
            1 => new GeneratedExpression($"({left.Text}-{right.Text})", left.Value - right.Value),
            2 => new GeneratedExpression($"({left.Text}*{right.Text})", left.Value * right.Value),
            _ => throw new InvalidOperationException("Unknown generated arithmetic operation."),
        };
    }

    private static double GetNumber(CellValue value)
    {
        Assert.AreEqual(CellValueKind.Number, value.Kind);
        return (double)value.RawValue!;
    }

    private static string ToA1(CellAddress address) => address.ToA1();

    private static string Message(CorpusCase testCase, FormulaEvaluationResult result) =>
        $"case={testCase.Id}; formula={testCase.Formula}; expected={testCase.Expected}; actual={result.Value.RawValue}";

    private sealed record CorpusCase(string Id, string Formula, string Kind, string Expected);

    private readonly record struct GeneratedExpression(string Text, double Value);

    private sealed class EmptyContext : IFormulaEvaluationContext
    {
        public static EmptyContext Instance { get; } = new();

        public CellValue GetCellValue(string? worksheetName, CellAddress address) => CellValue.Blank;
    }

    private sealed class DictionaryContext(IReadOnlyDictionary<CellAddress, CellValue> values) :
        IFormulaEvaluationContext
    {
        public CellValue GetCellValue(string? worksheetName, CellAddress address) =>
            values.GetValueOrDefault(address, CellValue.Blank);
    }
}
