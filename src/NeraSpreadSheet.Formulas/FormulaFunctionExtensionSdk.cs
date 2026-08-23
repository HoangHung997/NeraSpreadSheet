using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public readonly record struct FormulaFunctionApiVersion :
    IComparable<FormulaFunctionApiVersion>
{
    public static FormulaFunctionApiVersion Current { get; } = new(1, 0);

    public FormulaFunctionApiVersion(int major, int minor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(major);
        ArgumentOutOfRangeException.ThrowIfNegative(minor);
        Major = major;
        Minor = minor;
    }

    public int Major { get; }

    public int Minor { get; }

    public bool Supports(FormulaFunctionApiVersion required) =>
        Major == required.Major && Minor >= required.Minor;

    public int CompareTo(FormulaFunctionApiVersion other)
    {
        var major = Major.CompareTo(other.Major);
        return major != 0 ? major : Minor.CompareTo(other.Minor);
    }

    public override string ToString() => $"{Major}.{Minor}";
}

public readonly record struct FormulaFunctionVersion :
    IComparable<FormulaFunctionVersion>
{
    public FormulaFunctionVersion(int major, int minor, int patch = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(major);
        ArgumentOutOfRangeException.ThrowIfNegative(minor);
        ArgumentOutOfRangeException.ThrowIfNegative(patch);
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    public int CompareTo(FormulaFunctionVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }
        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

[Flags]
public enum FormulaFunctionValueCapabilities
{
    None = 0,
    Scalar = 1,
    Range = 2,
    Array = 4,
}

public enum FormulaFunctionVolatility
{
    Deterministic,
    Volatile,
    ExternalState,
}

public enum FormulaFunctionDependencyPolicy
{
    ArgumentDependencies,
    DeclaredByFunction,
    ExternalState,
}

public enum FormulaFunctionExecutionTrust
{
    BuiltIn,
    TrustedInProcessExtension,
    IsolatedExtensionRequired,
}

public readonly record struct FormulaFunctionIdentity
{
    public FormulaFunctionIdentity(
        string namespaceName,
        string name,
        FormulaFunctionVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        NamespaceName = namespaceName.Trim();
        Name = NormalizeFunctionName(name);
        Version = version;
    }

    public string NamespaceName { get; }

    public string Name { get; }

    public FormulaFunctionVersion Version { get; }

    public override string ToString() =>
        $"{NamespaceName}:{Name}@{Version}";

    private static string NormalizeFunctionName(string name)
    {
        var normalized = name.Trim().ToUpperInvariant();
        if (normalized.Length == 0 ||
            normalized.Any(character =>
                !char.IsLetterOrDigit(character) &&
                character is not '_' and not '.'))
        {
            throw new ArgumentException(
                "A formula function name may contain only letters, digits, underscores, and periods.",
                nameof(name));
        }
        return normalized;
    }
}

public sealed record FormulaFunctionDescriptor
{
    public FormulaFunctionDescriptor(
        FormulaFunctionIdentity identity,
        FormulaFunctionApiVersion requiredHostApiVersion,
        int minimumArgumentCount,
        int maximumArgumentCount,
        FormulaFunctionValueCapabilities capabilities =
            FormulaFunctionValueCapabilities.Scalar,
        FormulaFunctionVolatility volatility =
            FormulaFunctionVolatility.Deterministic,
        FormulaFunctionDependencyPolicy dependencyPolicy =
            FormulaFunctionDependencyPolicy.ArgumentDependencies,
        FormulaFunctionExecutionTrust executionTrust =
            FormulaFunctionExecutionTrust.TrustedInProcessExtension)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumArgumentCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            maximumArgumentCount,
            minimumArgumentCount);
        if (capabilities == FormulaFunctionValueCapabilities.None ||
            (capabilities & ~AllCapabilities) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capabilities));
        }
        if (!Enum.IsDefined(volatility))
        {
            throw new ArgumentOutOfRangeException(nameof(volatility));
        }
        if (!Enum.IsDefined(dependencyPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(dependencyPolicy));
        }
        if (!Enum.IsDefined(executionTrust))
        {
            throw new ArgumentOutOfRangeException(nameof(executionTrust));
        }

        Identity = identity;
        RequiredHostApiVersion = requiredHostApiVersion;
        MinimumArgumentCount = minimumArgumentCount;
        MaximumArgumentCount = maximumArgumentCount;
        Capabilities = capabilities;
        Volatility = volatility;
        DependencyPolicy = dependencyPolicy;
        ExecutionTrust = executionTrust;
    }

    private const FormulaFunctionValueCapabilities AllCapabilities =
        FormulaFunctionValueCapabilities.Scalar |
        FormulaFunctionValueCapabilities.Range |
        FormulaFunctionValueCapabilities.Array;

    public FormulaFunctionIdentity Identity { get; }

    public FormulaFunctionApiVersion RequiredHostApiVersion { get; }

    public int MinimumArgumentCount { get; }

    public int MaximumArgumentCount { get; }

    public FormulaFunctionValueCapabilities Capabilities { get; }

    public FormulaFunctionVolatility Volatility { get; }

    public FormulaFunctionDependencyPolicy DependencyPolicy { get; }

    public FormulaFunctionExecutionTrust ExecutionTrust { get; }
}

public sealed record FormulaFunctionHostPolicy
{
    public FormulaFunctionApiVersion ApiVersion { get; init; } =
        FormulaFunctionApiVersion.Current;

    public FormulaFunctionValueCapabilities AllowedCapabilities { get; init; } =
        FormulaFunctionValueCapabilities.Scalar |
        FormulaFunctionValueCapabilities.Range |
        FormulaFunctionValueCapabilities.Array;

    public bool AllowVolatileFunctions { get; init; } = true;

    public bool AllowExternalStateFunctions { get; init; }

    public bool AllowTrustedInProcessExtensions { get; init; } = true;

    public bool AllowIsolatedExtensions { get; init; }

    public bool IsCompatible(
        FormulaFunctionDescriptor descriptor,
        out string? rejectionReason)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!ApiVersion.Supports(descriptor.RequiredHostApiVersion))
        {
            rejectionReason =
                $"Function requires host API {descriptor.RequiredHostApiVersion}, but the host provides {ApiVersion}.";
            return false;
        }
        if ((descriptor.Capabilities & ~AllowedCapabilities) != 0)
        {
            rejectionReason =
                "Function requests value capabilities disabled by the host policy.";
            return false;
        }
        if (descriptor.Volatility == FormulaFunctionVolatility.Volatile &&
            !AllowVolatileFunctions)
        {
            rejectionReason = "Volatile functions are disabled by the host policy.";
            return false;
        }
        if ((descriptor.Volatility == FormulaFunctionVolatility.ExternalState ||
             descriptor.DependencyPolicy == FormulaFunctionDependencyPolicy.ExternalState) &&
            !AllowExternalStateFunctions)
        {
            rejectionReason =
                "External-state functions are disabled by the host policy.";
            return false;
        }
        if (descriptor.ExecutionTrust ==
                FormulaFunctionExecutionTrust.TrustedInProcessExtension &&
            !AllowTrustedInProcessExtensions)
        {
            rejectionReason =
                "Trusted in-process extensions are disabled by the host policy.";
            return false;
        }
        if (descriptor.ExecutionTrust ==
                FormulaFunctionExecutionTrust.IsolatedExtensionRequired &&
            !AllowIsolatedExtensions)
        {
            rejectionReason =
                "The function requires an isolated host that is not enabled.";
            return false;
        }

        rejectionReason = null;
        return true;
    }
}

public interface IVersionedFormulaFunction : IFormulaFunction
{
    FormulaFunctionDescriptor Descriptor { get; }
}

public sealed class DelegateVersionedFormulaFunction :
    IVersionedFormulaFunction
{
    private readonly Func<
        IReadOnlyList<CellValue>,
        IFormulaEvaluationContext,
        FormulaEvaluationResult> _evaluate;

    public DelegateVersionedFormulaFunction(
        FormulaFunctionDescriptor descriptor,
        Func<
            IReadOnlyList<CellValue>,
            IFormulaEvaluationContext,
            FormulaEvaluationResult> evaluate)
    {
        Descriptor = descriptor ??
            throw new ArgumentNullException(nameof(descriptor));
        _evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
    }

    public FormulaFunctionDescriptor Descriptor { get; }

    public string Name => Descriptor.Identity.Name;

    public FormulaEvaluationResult Invoke(
        IReadOnlyList<CellValue> arguments,
        IFormulaEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(context);
        if (arguments.Count < Descriptor.MinimumArgumentCount ||
            arguments.Count > Descriptor.MaximumArgumentCount)
        {
            return FormulaEvaluationResult.Failure(
                FormulaErrorCode.InvalidValue);
        }
        return _evaluate(arguments, context);
    }
}

public sealed class FormulaFunctionRegistrationException : Exception
{
    public FormulaFunctionRegistrationException(string message)
        : base(message)
    {
    }
}

public sealed class VersionedFormulaFunctionRegistry :
    IFormulaFunctionRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<
        string,
        List<IVersionedFormulaFunction>> _functions =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly IFormulaFunctionRegistry? _fallback;

    public VersionedFormulaFunctionRegistry(
        FormulaFunctionHostPolicy? hostPolicy = null,
        IFormulaFunctionRegistry? fallback = null)
    {
        HostPolicy = hostPolicy ?? new FormulaFunctionHostPolicy();
        _fallback = fallback;
    }

    public FormulaFunctionHostPolicy HostPolicy { get; }

    public int RegistrationCount
    {
        get
        {
            lock (_gate)
            {
                return _functions.Values.Sum(static versions => versions.Count);
            }
        }
    }

    public void Register(IVersionedFormulaFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);
        var descriptor = function.Descriptor;
        if (!HostPolicy.IsCompatible(descriptor, out var reason))
        {
            throw new FormulaFunctionRegistrationException(
                $"Function {descriptor.Identity} is incompatible: {reason}");
        }
        if (!string.Equals(
                function.Name,
                descriptor.Identity.Name,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new FormulaFunctionRegistrationException(
                "IFormulaFunction.Name must match the descriptor identity name.");
        }

        lock (_gate)
        {
            if (!_functions.TryGetValue(function.Name, out var versions))
            {
                versions = [];
                _functions.Add(function.Name, versions);
            }
            else if (versions.Any(existing =>
                         !string.Equals(
                             existing.Descriptor.Identity.NamespaceName,
                             descriptor.Identity.NamespaceName,
                             StringComparison.OrdinalIgnoreCase)))
            {
                throw new FormulaFunctionRegistrationException(
                    $"Public function name '{function.Name}' is already owned by another namespace.");
            }

            if (versions.Any(existing =>
                    existing.Descriptor.Identity.Equals(descriptor.Identity)))
            {
                throw new FormulaFunctionRegistrationException(
                    $"Function identity {descriptor.Identity} is already registered.");
            }
            versions.Add(function);
            versions.Sort(static (left, right) =>
                right.Descriptor.Identity.Version.CompareTo(
                    left.Descriptor.Identity.Version));
        }
    }

    public bool Unregister(FormulaFunctionIdentity identity)
    {
        lock (_gate)
        {
            if (!_functions.TryGetValue(identity.Name, out var versions))
            {
                return false;
            }
            var removed = versions.RemoveAll(function =>
                function.Descriptor.Identity.Equals(identity)) > 0;
            if (versions.Count == 0)
            {
                _functions.Remove(identity.Name);
            }
            return removed;
        }
    }

    public bool TryResolve(
        string name,
        out IFormulaFunction formulaFunction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_gate)
        {
            if (_functions.TryGetValue(name.Trim(), out var versions) &&
                versions.Count > 0)
            {
                formulaFunction = versions[0];
                return true;
            }
        }
        if (_fallback is not null &&
            _fallback.TryResolve(name, out formulaFunction))
        {
            return true;
        }
        formulaFunction = null!;
        return false;
    }

    public IReadOnlyList<FormulaFunctionDescriptor> GetRegistrations(
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_gate)
        {
            return _functions.TryGetValue(name.Trim(), out var versions)
                ? versions.Select(static function => function.Descriptor).ToArray()
                : Array.Empty<FormulaFunctionDescriptor>();
        }
    }
}

public sealed class CompositeFormulaFunctionRegistry :
    IFormulaFunctionRegistry
{
    private readonly IFormulaFunctionRegistry[] _registries;

    public CompositeFormulaFunctionRegistry(
        params IFormulaFunctionRegistry[] registries)
    {
        ArgumentNullException.ThrowIfNull(registries);
        if (registries.Length == 0 || registries.Any(static registry => registry is null))
        {
            throw new ArgumentException(
                "At least one non-null formula-function registry is required.",
                nameof(registries));
        }
        _registries = [.. registries];
    }

    public bool TryResolve(
        string name,
        out IFormulaFunction formulaFunction)
    {
        foreach (var registry in _registries)
        {
            if (registry.TryResolve(name, out formulaFunction))
            {
                return true;
            }
        }
        formulaFunction = null!;
        return false;
    }
}
