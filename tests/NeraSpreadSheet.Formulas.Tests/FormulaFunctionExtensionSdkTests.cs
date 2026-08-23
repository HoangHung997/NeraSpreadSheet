using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class FormulaFunctionExtensionSdkTests
{
    [TestMethod]
    public void RegistryResolvesHighestCompatibleVersion()
    {
        var registry = new VersionedFormulaFunctionRegistry();
        registry.Register(CreateConstantFunction(
            "Example",
            "SDKVALUE",
            new FormulaFunctionVersion(1, 0),
            10d));
        registry.Register(CreateConstantFunction(
            "Example",
            "SDKVALUE",
            new FormulaFunctionVersion(1, 2),
            12d));
        registry.Register(CreateConstantFunction(
            "Example",
            "SDKVALUE",
            new FormulaFunctionVersion(2, 0),
            20d));

        Assert.IsTrue(registry.TryResolve("sdkvalue", out var function));
        Assert.AreEqual(20d, function.Invoke(
            Array.Empty<CellValue>(),
            new FormulaSurfaceTestContext()).Value.RawValue);
        Assert.AreEqual(3, registry.RegistrationCount);
        Assert.AreEqual(
            new FormulaFunctionVersion(2, 0),
            ((IVersionedFormulaFunction)function)
                .Descriptor.Identity.Version);
    }

    [TestMethod]
    public void DuplicateIdentityAndCrossNamespaceNameConflictAreRejected()
    {
        var registry = new VersionedFormulaFunctionRegistry();
        var first = CreateConstantFunction(
            "VendorA",
            "RISK_SCORE",
            new FormulaFunctionVersion(1, 0),
            1d);
        registry.Register(first);

        Assert.ThrowsExactly<FormulaFunctionRegistrationException>(() =>
            registry.Register(first));
        Assert.ThrowsExactly<FormulaFunctionRegistrationException>(() =>
            registry.Register(CreateConstantFunction(
                "VendorB",
                "RISK_SCORE",
                new FormulaFunctionVersion(2, 0),
                2d)));
        Assert.AreEqual(1, registry.RegistrationCount);
    }

    [TestMethod]
    public void HostPolicyRejectsIncompatibleApiCapabilitiesAndExternalState()
    {
        var policy = new FormulaFunctionHostPolicy
        {
            ApiVersion = new FormulaFunctionApiVersion(1, 1),
            AllowedCapabilities = FormulaFunctionValueCapabilities.Scalar,
            AllowExternalStateFunctions = false,
            AllowIsolatedExtensions = false,
        };
        var registry = new VersionedFormulaFunctionRegistry(policy);

        Assert.ThrowsExactly<FormulaFunctionRegistrationException>(() =>
            registry.Register(CreateFunction(new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity(
                    "Example",
                    "NEW_API",
                    new FormulaFunctionVersion(1, 0)),
                new FormulaFunctionApiVersion(1, 2),
                0,
                0))));
        Assert.ThrowsExactly<FormulaFunctionRegistrationException>(() =>
            registry.Register(CreateFunction(new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity(
                    "Example",
                    "ARRAY_ONLY",
                    new FormulaFunctionVersion(1, 0)),
                FormulaFunctionApiVersion.Current,
                0,
                0,
                FormulaFunctionValueCapabilities.Array))));
        Assert.ThrowsExactly<FormulaFunctionRegistrationException>(() =>
            registry.Register(CreateFunction(new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity(
                    "Example",
                    "EXTERNAL_RATE",
                    new FormulaFunctionVersion(1, 0)),
                FormulaFunctionApiVersion.Current,
                0,
                0,
                volatility: FormulaFunctionVolatility.ExternalState,
                dependencyPolicy:
                    FormulaFunctionDependencyPolicy.ExternalState))));
        Assert.AreEqual(0, registry.RegistrationCount);
    }

    [TestMethod]
    public void RegistrationCanBeRemovedByStableIdentity()
    {
        var registry = new VersionedFormulaFunctionRegistry();
        var function = CreateConstantFunction(
            "Example",
            "REMOVABLE",
            new FormulaFunctionVersion(1, 3, 2),
            5d);
        registry.Register(function);

        Assert.IsTrue(registry.Unregister(function.Descriptor.Identity));
        Assert.IsFalse(registry.TryResolve("REMOVABLE", out _));
        Assert.IsFalse(registry.Unregister(function.Descriptor.Identity));
    }

    [TestMethod]
    public void NeraFormulaEngineInvokesVersionedExtensionThroughLegacyContract()
    {
        var registry = new VersionedFormulaFunctionRegistry();
        registry.Register(new DelegateVersionedFormulaFunction(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity(
                    "Example",
                    "DOUBLE_VALUE",
                    new FormulaFunctionVersion(1, 0)),
                FormulaFunctionApiVersion.Current,
                1,
                1),
            static (arguments, _) =>
                arguments[0].Kind == CellValueKind.Number
                    ? FormulaEvaluationResult.Success(
                        CellValue.FromNumber(
                            (double)arguments[0].RawValue! * 2d))
                    : FormulaEvaluationResult.Failure(
                        FormulaErrorCode.InvalidValue)));
        var engine = new NeraFormulaEngine(registry);

        var result = engine.Evaluate(
            "=DOUBLE_VALUE(21)",
            new FormulaSurfaceTestContext());

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(42d, result.Value.RawValue);
    }

    [TestMethod]
    public void DelegateFunctionEnforcesDescriptorArgumentBounds()
    {
        var function = CreateConstantFunction(
            "Example",
            "ONE_ARGUMENT",
            new FormulaFunctionVersion(1, 0),
            1d,
            minimumArguments: 1,
            maximumArguments: 1);

        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            function.Invoke(
                Array.Empty<CellValue>(),
                new FormulaSurfaceTestContext()).ErrorCode);
        Assert.IsTrue(function.Invoke(
            [CellValue.FromNumber(1d)],
            new FormulaSurfaceTestContext()).IsSuccess);
    }

    private static DelegateVersionedFormulaFunction CreateConstantFunction(
        string namespaceName,
        string name,
        FormulaFunctionVersion version,
        double value,
        int minimumArguments = 0,
        int maximumArguments = 0) =>
        new(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity(
                    namespaceName,
                    name,
                    version),
                FormulaFunctionApiVersion.Current,
                minimumArguments,
                maximumArguments),
            (_, _) => FormulaEvaluationResult.Success(
                CellValue.FromNumber(value)));

    private static DelegateVersionedFormulaFunction CreateFunction(
        FormulaFunctionDescriptor descriptor) =>
        new(
            descriptor,
            static (_, _) => FormulaEvaluationResult.Success(
                CellValue.Blank));
}
