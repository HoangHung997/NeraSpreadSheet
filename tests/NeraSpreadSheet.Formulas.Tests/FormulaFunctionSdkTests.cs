using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class FormulaFunctionSdkTests
{
    private static readonly string[] TwiceAliases = ["TWICE"];
    private static readonly string[] SharedAliases = ["SHARED"];

    [TestMethod]
    public void BuiltInsPublishVersionedMetadataWithoutBreakingLegacyLookup()
    {
        var registry = new BuiltInFormulaFunctionRegistry();

        Assert.AreEqual(191, registry.Count);
        Assert.AreEqual(191, registry.VersionCount);
        Assert.AreEqual(
            FormulaFunctionApiVersion.Current,
            registry.HostApiVersion);
        Assert.IsTrue(registry.TryResolve("sum", out var legacySum));
        Assert.AreEqual("SUM", legacySum.Name);
        Assert.IsTrue(registry.TryGetDescriptor(
            "SUM",
            out var sumDescriptor));
        Assert.AreEqual(
            new FormulaFunctionIdentity("NERA.BUILTIN", "SUM"),
            sumDescriptor.Identity);
        Assert.AreEqual(
            new FormulaFunctionVersion(1, 0, 0),
            sumDescriptor.Version);
        Assert.AreEqual(
            FormulaFunctionVolatility.Deterministic,
            sumDescriptor.Volatility);
        Assert.IsTrue(registry.TryGetDescriptor(
            "TODAY",
            out var todayDescriptor));
        Assert.AreEqual(
            FormulaFunctionVolatility.Volatile,
            todayDescriptor.Volatility);
        Assert.AreEqual(
            FormulaFunctionSecurityClassification.ContextReadOnly,
            todayDescriptor.SecurityClassification);
    }

    [TestMethod]
    public void VersionValueObjectsProvideOrderedComparison()
    {
        var apiOne = new FormulaFunctionApiVersion(1, 0);
        var apiTwo = new FormulaFunctionApiVersion(1, 1);
        Assert.IsTrue(apiOne < apiTwo);
        Assert.IsTrue(apiOne <= apiTwo);
        Assert.IsTrue(apiTwo > apiOne);
        Assert.IsTrue(apiTwo >= apiOne);
        Assert.IsTrue(apiOne.IsSupportedBy(apiTwo));

        var versionOne = new FormulaFunctionVersion(1, 2, 3);
        var versionTwo = new FormulaFunctionVersion(1, 3, 0);
        Assert.IsTrue(versionOne < versionTwo);
        Assert.AreEqual("1.2.3", versionOne.ToString());
        Assert.AreEqual("1.0", apiOne.ToString());
    }

    [TestMethod]
    public void SideBySideVersionsResolveHighestAndRemainExactlyAddressable()
    {
        var registry = new VersionedFormulaFunctionRegistry();
        var identity = new FormulaFunctionIdentity("ACME", "DOUBLE");
        var versionOne = CreateScalarFunction(
            identity,
            new FormulaFunctionVersion(1, 0, 0),
            2d,
            aliases: TwiceAliases);
        var versionTwo = CreateScalarFunction(
            identity,
            new FormulaFunctionVersion(2, 0, 0),
            4d,
            aliases: TwiceAliases);
        registry.Register(versionOne);
        registry.Register(
            versionTwo,
            new FormulaFunctionRegistrationOptions
            {
                ConflictPolicy =
                    FormulaFunctionRegistrationConflictPolicy.AllowSideBySide,
            });

        Assert.AreEqual(1, registry.Count);
        Assert.AreEqual(2, registry.VersionCount);
        Assert.IsTrue(registry.TryResolve(
            "TWICE",
            out var selected));
        var selectedResult = selected.Invoke(
            [],
            new FormulaSurfaceTestContext());
        Assert.AreEqual(4d, selectedResult.Value.RawValue);
        Assert.IsTrue(registry.TryResolve(
            identity,
            new FormulaFunctionVersion(1, 0, 0),
            out var exact));
        Assert.AreEqual(
            2d,
            exact.Invoke(
                new FormulaFunctionInvocation(
                    [],
                    new FormulaSurfaceTestContext()))
                .Value
                .RawValue);

        Assert.IsTrue(registry.Unregister(
            identity,
            new FormulaFunctionVersion(2, 0, 0)));
        Assert.IsTrue(registry.TryResolve("DOUBLE", out selected));
        Assert.AreEqual(
            2d,
            selected.Invoke(
                [],
                new FormulaSurfaceTestContext())
                .Value
                .RawValue);
    }

    [TestMethod]
    public void RegistrationRejectsIncompatibleApiCapabilitiesAndSecurity()
    {
        var registry = new VersionedFormulaFunctionRegistry();
        var future = new FormulaFunctionDefinition(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("ACME", "FUTURE"),
                new FormulaFunctionVersion(1, 0, 0),
                new FormulaFunctionApiVersion(2, 0),
                0,
                0,
                FormulaFunctionCapabilities.ReturnsScalar),
            static _ => FormulaEvaluationResult.Success(
                CellValue.FromNumber(1d)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            registry.Register(future));

        var arrayArgument = new FormulaFunctionDefinition(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("ACME", "ARRAYARG"),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                1,
                1,
                FormulaFunctionCapabilities.ArrayArguments |
                FormulaFunctionCapabilities.ReturnsScalar),
            static _ => FormulaEvaluationResult.Success(
                CellValue.FromNumber(1d)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            registry.Register(arrayArgument));

        var external = new FormulaFunctionDefinition(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("ACME", "EXTERNAL"),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                0,
                0,
                FormulaFunctionCapabilities.ReturnsScalar,
                FormulaFunctionVolatility.ExternalState,
                FormulaFunctionSecurityClassification.ExternalState),
            static _ => FormulaEvaluationResult.Success(
                CellValue.FromNumber(1d)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            registry.Register(external));
    }

    [TestMethod]
    public void NamesAliasesAndExactVersionsHaveDeterministicConflictRules()
    {
        var registry = new VersionedFormulaFunctionRegistry();
        var first = CreateScalarFunction(
            new FormulaFunctionIdentity("ACME.ONE", "PRIMARY"),
            new FormulaFunctionVersion(1, 0, 0),
            1d,
            aliases: SharedAliases);
        registry.Register(first);
        var aliasConflict = CreateScalarFunction(
            new FormulaFunctionIdentity("ACME.TWO", "OTHER"),
            new FormulaFunctionVersion(1, 0, 0),
            2d,
            aliases: SharedAliases);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            registry.Register(aliasConflict));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            registry.Register(first));

        var replacement = CreateScalarFunction(
            first.Descriptor.Identity,
            first.Descriptor.Version,
            3d,
            aliases: SharedAliases);
        registry.Register(
            replacement,
            new FormulaFunctionRegistrationOptions
            {
                ConflictPolicy =
                    FormulaFunctionRegistrationConflictPolicy.ReplaceExactVersion,
            });
        Assert.IsTrue(registry.TryResolve("PRIMARY", out var selected));
        Assert.AreEqual(
            3d,
            selected.Invoke(
                [],
                new FormulaSurfaceTestContext())
                .Value
                .RawValue);
    }

    [TestMethod]
    public void VersionedFunctionReceivesRangeIdentityShapeAndValues()
    {
        var registry = new VersionedFormulaFunctionRegistry();
        FormulaFunctionInvocation? observed = null;
        registry.Register(new FormulaFunctionDefinition(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("ACME", "RANGECOUNT"),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                1,
                1,
                FormulaFunctionCapabilities.RangeArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                propagateArgumentErrors: false),
            invocation =>
            {
                observed = invocation;
                return FormulaEvaluationResult.Success(
                    CellValue.FromNumber(
                        invocation.Arguments[0].Values.Count));
            }));
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(0, 1)] = CellValue.FromNumber(2d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(3d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(4d),
        };
        var engine = new NeraFormulaEngine(registry);

        var result = engine.Evaluate(
            "=RANGECOUNT(A1:B2)",
            new FormulaSurfaceTestContext(values));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(4d, result.Value.RawValue);
        var invocation = observed ?? throw new AssertFailedException(
            "The extension function was not invoked.");
        Assert.AreEqual(1, invocation.Arguments.Count);
        Assert.AreEqual(
            FormulaFunctionArgumentKind.Range,
            invocation.Arguments[0].Kind);
        Assert.AreEqual(4, invocation.Arguments[0].Values.Count);
        Assert.IsTrue(invocation.Arguments[0].SourceDependency.HasValue);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(1, 1)),
            invocation.Arguments[0].SourceDependency!.Value.Range);
        Assert.AreEqual(1, result.Dependencies.Count);
    }

    [TestMethod]
    public void FunctionMayDeclareAdditionalDependenciesWhenDescriptorAllowsIt()
    {
        var registry = new VersionedFormulaFunctionRegistry();
        var additional = new FormulaDependency(
            null,
            new CellRange(
                new CellAddress(2, 0),
                new CellAddress(2, 1)));
        registry.Register(new FormulaFunctionDefinition(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("ACME", "WATCH"),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                1,
                1,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                dependencyPolicy:
                    FormulaFunctionDependencyPolicy.FunctionMayDeclareAdditional),
            invocation => FormulaEvaluationResult.Success(
                invocation.Arguments[0].ScalarValue,
                [additional])));
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(7d),
        };

        var result = new NeraFormulaEngine(registry).Evaluate(
            "=WATCH(A1)",
            new FormulaSurfaceTestContext(values));

        Assert.AreEqual(7d, result.Value.RawValue);
        Assert.AreEqual(2, result.Dependencies.Count);
        Assert.IsTrue(result.Dependencies.Contains(additional));
    }

    [TestMethod]
    public void LegacyFunctionRegistrationRemainsSourceCompatible()
    {
        var registry = new BuiltInFormulaFunctionRegistry();
        registry.Register(new LegacyIncrementFunction());

        var result = new NeraFormulaEngine(registry).Evaluate(
            "=LEGACYINC(4)",
            new FormulaSurfaceTestContext());

        Assert.AreEqual(5d, result.Value.RawValue);
        Assert.IsTrue(registry.TryGetDescriptor(
            "LEGACYINC",
            out var descriptor));
        Assert.AreEqual("LEGACY", descriptor.Identity.Namespace);
    }

    private static FormulaFunctionDefinition CreateScalarFunction(
        FormulaFunctionIdentity identity,
        FormulaFunctionVersion version,
        double result,
        IEnumerable<string>? aliases = null) =>
        new(
            new FormulaFunctionDescriptor(
                identity,
                version,
                FormulaFunctionApiVersion.Current,
                0,
                0,
                FormulaFunctionCapabilities.ReturnsScalar,
                aliases: aliases),
            _ => FormulaEvaluationResult.Success(
                CellValue.FromNumber(result)));

    private sealed class LegacyIncrementFunction : IFormulaFunction
    {
        public string Name => "LEGACYINC";

        public FormulaEvaluationResult Invoke(
            IReadOnlyList<CellValue> arguments,
            IFormulaEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (arguments.Count != 1 ||
                !FormulaValueCoercion.TryNumber(
                    arguments[0],
                    out var number))
            {
                return FormulaEvaluationResult.Failure(
                    FormulaErrorCode.InvalidValue);
            }
            return FormulaEvaluationResult.Success(
                CellValue.FromNumber(number + 1d));
        }
    }
}
