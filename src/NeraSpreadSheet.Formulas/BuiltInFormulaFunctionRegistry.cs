namespace NeraSpreadSheet.Formulas;

public sealed class BuiltInFormulaFunctionRegistry :
    IVersionedFormulaFunctionRegistry
{
    private readonly VersionedFormulaFunctionRegistry _registry = new(
        new FormulaFunctionRegistryPolicy
        {
            AllowExternalStateFunctions = true,
            MaximumSecurityClassification =
                FormulaFunctionSecurityClassification.ExternalState,
        });

    public BuiltInFormulaFunctionRegistry()
    {
        foreach (var function in StandardFormulaFunctions.CreateAll())
        {
            Register(function);
        }
    }

    public FormulaFunctionApiVersion HostApiVersion =>
        _registry.HostApiVersion;

    public int Count => _registry.Count;

    public int VersionCount => _registry.VersionCount;

    public IReadOnlyList<FormulaFunctionDescriptor> Descriptors =>
        _registry.Descriptors;

    public void Register(IFormulaFunction formulaFunction)
    {
        ArgumentNullException.ThrowIfNull(formulaFunction);
        _registry.RegisterLegacy(formulaFunction);
    }

    public void Register(
        IVersionedFormulaFunction formulaFunction,
        FormulaFunctionRegistrationOptions? options = null) =>
        _registry.Register(formulaFunction, options);

    public bool Unregister(
        FormulaFunctionIdentity identity,
        FormulaFunctionVersion version) =>
        _registry.Unregister(identity, version);

    public bool TryResolve(
        string name,
        out IFormulaFunction formulaFunction) =>
        _registry.TryResolve(name, out formulaFunction);

    public bool TryGetDescriptor(
        string name,
        out FormulaFunctionDescriptor descriptor) =>
        _registry.TryGetDescriptor(name, out descriptor);

    public bool TryResolve(
        FormulaFunctionIdentity identity,
        FormulaFunctionVersion version,
        out IVersionedFormulaFunction formulaFunction) =>
        _registry.TryResolve(identity, version, out formulaFunction);
}
