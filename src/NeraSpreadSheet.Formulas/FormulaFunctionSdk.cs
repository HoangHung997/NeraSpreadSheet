using System.Collections.ObjectModel;
using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public readonly record struct FormulaFunctionApiVersion :
    IComparable<FormulaFunctionApiVersion>
{
    public static FormulaFunctionApiVersion Current { get; } = new(1, 0);

    public FormulaFunctionApiVersion(int major, int minor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(major);
        ArgumentOutOfRangeException.ThrowIfNegative(minor);
        Major = major;
        Minor = minor;
    }

    public int Major { get; }

    public int Minor { get; }

    public bool IsSupportedBy(FormulaFunctionApiVersion hostVersion) =>
        Major == hostVersion.Major && Minor <= hostVersion.Minor;

    public int CompareTo(FormulaFunctionApiVersion other)
    {
        var major = Major.CompareTo(other.Major);
        return major != 0 ? major : Minor.CompareTo(other.Minor);
    }

    public static bool operator <(
        FormulaFunctionApiVersion left,
        FormulaFunctionApiVersion right) =>
        left.CompareTo(right) < 0;

    public static bool operator <=(
        FormulaFunctionApiVersion left,
        FormulaFunctionApiVersion right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >(
        FormulaFunctionApiVersion left,
        FormulaFunctionApiVersion right) =>
        left.CompareTo(right) > 0;

    public static bool operator >=(
        FormulaFunctionApiVersion left,
        FormulaFunctionApiVersion right) =>
        left.CompareTo(right) >= 0;

    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Major}.{Minor}");
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

    public static bool operator <(
        FormulaFunctionVersion left,
        FormulaFunctionVersion right) =>
        left.CompareTo(right) < 0;

    public static bool operator <=(
        FormulaFunctionVersion left,
        FormulaFunctionVersion right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >(
        FormulaFunctionVersion left,
        FormulaFunctionVersion right) =>
        left.CompareTo(right) > 0;

    public static bool operator >=(
        FormulaFunctionVersion left,
        FormulaFunctionVersion right) =>
        left.CompareTo(right) >= 0;

    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Major}.{Minor}.{Patch}");
}

public readonly record struct FormulaFunctionIdentity
{
    public FormulaFunctionIdentity(string @namespace, string name)
    {
        Namespace = FormulaFunctionName.NormalizeNamespace(@namespace);
        Name = FormulaFunctionName.Normalize(name);
    }

    public string Namespace { get; }

    public string Name { get; }

    public string QualifiedName => $"{Namespace}:{Name}";

    public override string ToString() => QualifiedName;
}

[Flags]
public enum FormulaFunctionCapabilities
{
    None = 0,
    ScalarArguments = 1,
    RangeArguments = 2,
    ArrayArguments = 4,
    ReturnsScalar = 8,
    ReturnsArray = 16,
}

public enum FormulaFunctionVolatility
{
    Deterministic = 0,
    Volatile,
    ExternalState,
}

public enum FormulaFunctionSecurityClassification
{
    Pure = 0,
    ContextReadOnly,
    ExternalState,
}

public enum FormulaFunctionDependencyPolicy
{
    EngineCapturedOnly = 0,
    FunctionMayDeclareAdditional,
}

public enum FormulaFunctionRegistrationConflictPolicy
{
    Reject = 0,
    AllowSideBySide,
    ReplaceExactVersion,
}

public enum FormulaFunctionArgumentKind
{
    Scalar = 0,
    Range,
    Array,
}

public enum FormulaFunctionArgumentCountPolicy
{
    LogicalArguments = 0,
    FlattenedValues,
}

public sealed record FormulaFunctionDescriptor
{
    public FormulaFunctionDescriptor(
        FormulaFunctionIdentity identity,
        FormulaFunctionVersion version,
        FormulaFunctionApiVersion minimumHostApiVersion,
        int minimumArguments,
        int maximumArguments,
        FormulaFunctionCapabilities capabilities,
        FormulaFunctionVolatility volatility =
            FormulaFunctionVolatility.Deterministic,
        FormulaFunctionSecurityClassification securityClassification =
            FormulaFunctionSecurityClassification.Pure,
        FormulaFunctionDependencyPolicy dependencyPolicy =
            FormulaFunctionDependencyPolicy.EngineCapturedOnly,
        bool propagateArgumentErrors = true,
        IEnumerable<string>? aliases = null,
        FormulaFunctionArgumentCountPolicy argumentCountPolicy =
            FormulaFunctionArgumentCountPolicy.LogicalArguments)
    {
        if (string.IsNullOrWhiteSpace(identity.Namespace) ||
            string.IsNullOrWhiteSpace(identity.Name))
        {
            throw new ArgumentException(
                "A formula-function identity must be initialized.",
                nameof(identity));
        }
        if (minimumHostApiVersion.Major <= 0)
        {
            throw new ArgumentException(
                "The minimum host API version must be initialized.",
                nameof(minimumHostApiVersion));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(minimumArguments);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            maximumArguments,
            minimumArguments);
        ValidateCapabilities(capabilities);
        if ((capabilities &
             (FormulaFunctionCapabilities.ReturnsScalar |
              FormulaFunctionCapabilities.ReturnsArray)) == 0)
        {
            throw new ArgumentException(
                "Function capabilities must declare at least one return kind.",
                nameof(capabilities));
        }
        if (!Enum.IsDefined(volatility))
        {
            throw new ArgumentOutOfRangeException(nameof(volatility));
        }
        if (!Enum.IsDefined(securityClassification))
        {
            throw new ArgumentOutOfRangeException(
                nameof(securityClassification));
        }
        if (!Enum.IsDefined(dependencyPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(dependencyPolicy));
        }
        if (!Enum.IsDefined(argumentCountPolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(argumentCountPolicy));
        }

        Identity = identity;
        Version = version;
        MinimumHostApiVersion = minimumHostApiVersion;
        MinimumArguments = minimumArguments;
        MaximumArguments = maximumArguments;
        Capabilities = capabilities;
        Volatility = volatility;
        SecurityClassification = securityClassification;
        DependencyPolicy = dependencyPolicy;
        PropagateArgumentErrors = propagateArgumentErrors;
        Aliases = NormalizeAliases(identity.Name, aliases);
        ArgumentCountPolicy = argumentCountPolicy;
    }

    public FormulaFunctionIdentity Identity { get; }

    public FormulaFunctionVersion Version { get; }

    public FormulaFunctionApiVersion MinimumHostApiVersion { get; }

    public int MinimumArguments { get; }

    public int MaximumArguments { get; }

    public FormulaFunctionCapabilities Capabilities { get; }

    public FormulaFunctionVolatility Volatility { get; }

    public FormulaFunctionSecurityClassification SecurityClassification
    {
        get;
    }

    public FormulaFunctionDependencyPolicy DependencyPolicy { get; }

    public bool PropagateArgumentErrors { get; }

    public IReadOnlyList<string> Aliases { get; }

    public FormulaFunctionArgumentCountPolicy ArgumentCountPolicy { get; }

    public IEnumerable<string> EnumerateFormulaNames()
    {
        yield return Identity.Name;
        foreach (var alias in Aliases)
        {
            yield return alias;
        }
    }

    internal static void ValidateCapabilities(
        FormulaFunctionCapabilities capabilities)
    {
        const FormulaFunctionCapabilities known =
            FormulaFunctionCapabilities.ScalarArguments |
            FormulaFunctionCapabilities.RangeArguments |
            FormulaFunctionCapabilities.ArrayArguments |
            FormulaFunctionCapabilities.ReturnsScalar |
            FormulaFunctionCapabilities.ReturnsArray;
        if (capabilities == FormulaFunctionCapabilities.None ||
            (capabilities & ~known) != FormulaFunctionCapabilities.None)
        {
            throw new ArgumentOutOfRangeException(nameof(capabilities));
        }
    }

    private static IReadOnlyList<string> NormalizeAliases(
        string primaryName,
        IEnumerable<string>? aliases)
    {
        if (aliases is null)
        {
            return System.Array.Empty<string>();
        }
        var result = aliases
            .Select(FormulaFunctionName.Normalize)
            .Where(alias => !string.Equals(
                alias,
                primaryName,
                StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static alias => alias, StringComparer.Ordinal)
            .ToArray();
        return System.Array.AsReadOnly(result);
    }
}

public sealed record FormulaFunctionRegistryPolicy
{
    public FormulaFunctionApiVersion HostApiVersion { get; init; } =
        FormulaFunctionApiVersion.Current;

    public FormulaFunctionCapabilities SupportedCapabilities { get; init; } =
        FormulaFunctionCapabilities.ScalarArguments |
        FormulaFunctionCapabilities.RangeArguments |
        FormulaFunctionCapabilities.ReturnsScalar;

    public bool AllowVolatileFunctions { get; init; } = true;

    public bool AllowExternalStateFunctions { get; init; }

    public FormulaFunctionSecurityClassification MaximumSecurityClassification
    {
        get;
        init;
    } = FormulaFunctionSecurityClassification.ContextReadOnly;

    public int MaximumVersionsPerIdentity { get; init; } = 8;
}

public sealed record FormulaFunctionRegistrationOptions
{
    public FormulaFunctionRegistrationConflictPolicy ConflictPolicy
    {
        get;
        init;
    } = FormulaFunctionRegistrationConflictPolicy.Reject;
}

public sealed class FormulaFunctionArgument
{
    private readonly CellValue[] _values;
    private readonly ReadOnlyCollection<CellValue> _readOnlyValues;

    private FormulaFunctionArgument(
        FormulaFunctionArgumentKind kind,
        CellValue[] values,
        FormulaDependency? sourceDependency,
        FormulaArrayValue? arrayValue)
    {
        Kind = kind;
        _values = values;
        _readOnlyValues = System.Array.AsReadOnly(_values);
        SourceDependency = sourceDependency;
        ArrayValue = arrayValue;
    }

    public FormulaFunctionArgumentKind Kind { get; }

    public IReadOnlyList<CellValue> Values => _readOnlyValues;

    public FormulaDependency? SourceDependency { get; }

    public FormulaArrayValue? ArrayValue { get; }

    public CellValue ScalarValue =>
        Kind == FormulaFunctionArgumentKind.Scalar && _values.Length == 1
            ? _values[0]
            : throw new InvalidOperationException(
                "The function argument is not a scalar value.");

    public static FormulaFunctionArgument Scalar(CellValue value) =>
        new(
            FormulaFunctionArgumentKind.Scalar,
            [value],
            null,
            null);

    public static FormulaFunctionArgument Range(
        FormulaDependency sourceDependency,
        IEnumerable<CellValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new FormulaFunctionArgument(
            FormulaFunctionArgumentKind.Range,
            values.ToArray(),
            sourceDependency,
            null);
    }

    public static FormulaFunctionArgument Array(FormulaArrayValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new FormulaFunctionArgument(
            FormulaFunctionArgumentKind.Array,
            value.ToArray(),
            null,
            value);
    }
}

public sealed class FormulaFunctionInvocation
{
    private readonly FormulaFunctionArgument[] _arguments;
    private readonly ReadOnlyCollection<FormulaFunctionArgument>
        _readOnlyArguments;

    public FormulaFunctionInvocation(
        IEnumerable<FormulaFunctionArgument> arguments,
        IFormulaEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        _arguments = arguments.ToArray();
        _readOnlyArguments = System.Array.AsReadOnly(_arguments);
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IReadOnlyList<FormulaFunctionArgument> Arguments =>
        _readOnlyArguments;

    public IFormulaEvaluationContext Context { get; }

    public CellValue[] FlattenValues() =>
        _arguments
            .SelectMany(static argument => argument.Values)
            .ToArray();

    internal long GetArgumentCount(
        FormulaFunctionArgumentCountPolicy policy) =>
        policy == FormulaFunctionArgumentCountPolicy.LogicalArguments
            ? _arguments.Length
            : _arguments.Sum(static argument =>
                (long)argument.Values.Count);
}

public interface IVersionedFormulaFunction : IFormulaFunction
{
    FormulaFunctionDescriptor Descriptor { get; }

    FormulaEvaluationResult Invoke(FormulaFunctionInvocation invocation);
}

public interface IVersionedFormulaFunctionRegistry :
    IFormulaFunctionRegistry
{
    FormulaFunctionApiVersion HostApiVersion { get; }

    int Count { get; }

    int VersionCount { get; }

    IReadOnlyList<FormulaFunctionDescriptor> Descriptors { get; }

    void Register(
        IVersionedFormulaFunction formulaFunction,
        FormulaFunctionRegistrationOptions? options = null);

    bool Unregister(
        FormulaFunctionIdentity identity,
        FormulaFunctionVersion version);

    bool TryGetDescriptor(
        string name,
        out FormulaFunctionDescriptor descriptor);

    bool TryResolve(
        FormulaFunctionIdentity identity,
        FormulaFunctionVersion version,
        out IVersionedFormulaFunction formulaFunction);
}

public sealed class FormulaFunctionDefinition : IVersionedFormulaFunction
{
    private readonly Func<
        FormulaFunctionInvocation,
        FormulaEvaluationResult> _evaluator;

    public FormulaFunctionDefinition(
        FormulaFunctionDescriptor descriptor,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator)
    {
        Descriptor = descriptor ??
            throw new ArgumentNullException(nameof(descriptor));
        if ((descriptor.Capabilities &
             FormulaFunctionCapabilities.ReturnsScalar) == 0)
        {
            throw new ArgumentException(
                "FormulaFunctionDefinition requires ReturnsScalar capability.",
                nameof(descriptor));
        }
        _evaluator = evaluator ??
            throw new ArgumentNullException(nameof(evaluator));
    }

    public FormulaFunctionDescriptor Descriptor { get; }

    public string Name => Descriptor.Identity.Name;

    public FormulaEvaluationResult Invoke(
        IReadOnlyList<CellValue> arguments,
        IFormulaEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(context);
        return Invoke(new FormulaFunctionInvocation(
            arguments.Select(FormulaFunctionArgument.Scalar),
            context));
    }

    public FormulaEvaluationResult Invoke(FormulaFunctionInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var argumentCount = invocation.GetArgumentCount(
            Descriptor.ArgumentCountPolicy);
        if (argumentCount < Descriptor.MinimumArguments ||
            argumentCount > Descriptor.MaximumArguments)
        {
            return FormulaEvaluationResult.Failure(
                FormulaErrorCode.InvalidValue);
        }
        if (!SupportsArguments(invocation.Arguments))
        {
            return FormulaEvaluationResult.Failure(
                FormulaErrorCode.InvalidValue);
        }
        if (Descriptor.PropagateArgumentErrors &&
            TryGetFirstError(invocation.Arguments, out var error))
        {
            return new FormulaEvaluationResult(
                error,
                FormulaErrorMapping.ToErrorCode(error),
                System.Array.Empty<FormulaDependency>());
        }

        var result = _evaluator(invocation) ??
            throw new InvalidOperationException(
                $"Formula function '{Name}' returned a null result.");
        if (Descriptor.DependencyPolicy ==
                FormulaFunctionDependencyPolicy.EngineCapturedOnly &&
            result.Dependencies.Count > 0)
        {
            throw new InvalidOperationException(
                $"Formula function '{Name}' declared dependencies while its " +
                "descriptor allows engine-captured dependencies only.");
        }
        return result;
    }

    private bool SupportsArguments(
        IReadOnlyList<FormulaFunctionArgument> arguments)
    {
        foreach (var argument in arguments)
        {
            var required = argument.Kind switch
            {
                FormulaFunctionArgumentKind.Scalar =>
                    FormulaFunctionCapabilities.ScalarArguments,
                FormulaFunctionArgumentKind.Range =>
                    FormulaFunctionCapabilities.RangeArguments,
                FormulaFunctionArgumentKind.Array =>
                    FormulaFunctionCapabilities.ArrayArguments,
                _ => FormulaFunctionCapabilities.None,
            };
            if ((Descriptor.Capabilities & required) == 0)
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryGetFirstError(
        IReadOnlyList<FormulaFunctionArgument> arguments,
        out CellValue error)
    {
        foreach (var argument in arguments)
        {
            foreach (var value in argument.Values)
            {
                if (value.Kind == CellValueKind.Error)
                {
                    error = value;
                    return true;
                }
            }
        }
        error = default;
        return false;
    }
}

public class VersionedFormulaFunctionRegistry :
    IVersionedFormulaFunctionRegistry
{
    private readonly object _gate = new();
    private readonly FormulaFunctionRegistryPolicy _policy;
    private readonly Dictionary<
        FormulaFunctionIdentity,
        SortedDictionary<FormulaFunctionVersion, IVersionedFormulaFunction>>
        _functions = [];
    private readonly Dictionary<string, FormulaFunctionIdentity> _nameOwners =
        new(StringComparer.OrdinalIgnoreCase);

    public VersionedFormulaFunctionRegistry(
        FormulaFunctionRegistryPolicy? policy = null)
    {
        _policy = policy ?? new FormulaFunctionRegistryPolicy();
        ValidatePolicy(_policy);
    }

    public FormulaFunctionApiVersion HostApiVersion =>
        _policy.HostApiVersion;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _functions.Count;
            }
        }
    }

    public int VersionCount
    {
        get
        {
            lock (_gate)
            {
                return _functions.Values.Sum(static versions =>
                    versions.Count);
            }
        }
    }

    public IReadOnlyList<FormulaFunctionDescriptor> Descriptors
    {
        get
        {
            lock (_gate)
            {
                return _functions.Values
                    .SelectMany(static versions => versions.Values)
                    .Select(static function => function.Descriptor)
                    .OrderBy(static descriptor =>
                        descriptor.Identity.QualifiedName,
                        StringComparer.Ordinal)
                    .ThenBy(static descriptor => descriptor.Version)
                    .ToArray();
            }
        }
    }

    public void Register(
        IVersionedFormulaFunction formulaFunction,
        FormulaFunctionRegistrationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(formulaFunction);
        options ??= new FormulaFunctionRegistrationOptions();
        if (!Enum.IsDefined(options.ConflictPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
        ValidateDescriptor(formulaFunction.Descriptor);
        lock (_gate)
        {
            RegisterCore(formulaFunction, options);
        }
    }

    public void RegisterLegacy(IFormulaFunction formulaFunction)
    {
        ArgumentNullException.ThrowIfNull(formulaFunction);
        if (formulaFunction is IVersionedFormulaFunction versioned)
        {
            Register(versioned);
            return;
        }
        Register(new LegacyFormulaFunctionAdapter(formulaFunction));
    }

    public bool Unregister(
        FormulaFunctionIdentity identity,
        FormulaFunctionVersion version)
    {
        lock (_gate)
        {
            if (!_functions.TryGetValue(identity, out var versions) ||
                !versions.Remove(version))
            {
                return false;
            }
            if (versions.Count > 0)
            {
                return true;
            }

            _functions.Remove(identity);
            foreach (var name in _nameOwners
                         .Where(pair => pair.Value == identity)
                         .Select(static pair => pair.Key)
                         .ToArray())
            {
                _nameOwners.Remove(name);
            }
            return true;
        }
    }

    public bool TryResolve(
        string name,
        out IFormulaFunction formulaFunction)
    {
        lock (_gate)
        {
            if (!TryResolveVersionedCore(name, out var versioned))
            {
                formulaFunction = null!;
                return false;
            }
            formulaFunction = versioned;
            return true;
        }
    }

    public bool TryGetDescriptor(
        string name,
        out FormulaFunctionDescriptor descriptor)
    {
        lock (_gate)
        {
            if (!TryResolveVersionedCore(name, out var function))
            {
                descriptor = null!;
                return false;
            }
            descriptor = function.Descriptor;
            return true;
        }
    }

    public bool TryResolve(
        FormulaFunctionIdentity identity,
        FormulaFunctionVersion version,
        out IVersionedFormulaFunction formulaFunction)
    {
        lock (_gate)
        {
            if (_functions.TryGetValue(identity, out var versions) &&
                versions.TryGetValue(version, out formulaFunction!))
            {
                return true;
            }
            formulaFunction = null!;
            return false;
        }
    }

    private void RegisterCore(
        IVersionedFormulaFunction formulaFunction,
        FormulaFunctionRegistrationOptions options)
    {
        var identity = formulaFunction.Descriptor.Identity;
        var names = formulaFunction.Descriptor
            .EnumerateFormulaNames()
            .ToArray();
        EnsureNameOwnership(identity, names);

        if (!_functions.TryGetValue(identity, out var versions))
        {
            versions = [];
            _functions.Add(identity, versions);
        }
        else
        {
            EnsureAliasStability(versions, names);
        }

        if (versions.TryGetValue(
                formulaFunction.Descriptor.Version,
                out _))
        {
            if (options.ConflictPolicy !=
                FormulaFunctionRegistrationConflictPolicy.ReplaceExactVersion)
            {
                throw new InvalidOperationException(
                    $"Formula function '{identity}' version " +
                    $"{formulaFunction.Descriptor.Version} is already registered.");
            }
            versions[formulaFunction.Descriptor.Version] = formulaFunction;
        }
        else
        {
            if (versions.Count > 0 &&
                options.ConflictPolicy !=
                FormulaFunctionRegistrationConflictPolicy.AllowSideBySide)
            {
                throw new InvalidOperationException(
                    $"Formula function '{identity}' already has a registered " +
                    "version; AllowSideBySide is required for a new version.");
            }
            if (versions.Count >= _policy.MaximumVersionsPerIdentity)
            {
                throw new InvalidOperationException(
                    $"Formula function '{identity}' exceeds the configured " +
                    $"version limit of {_policy.MaximumVersionsPerIdentity}.");
            }
            versions.Add(formulaFunction.Descriptor.Version, formulaFunction);
        }

        foreach (var name in names)
        {
            _nameOwners[name] = identity;
        }
    }

    private bool TryResolveVersionedCore(
        string name,
        out IVersionedFormulaFunction formulaFunction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = FormulaFunctionName.Normalize(name);
        if (!_nameOwners.TryGetValue(normalized, out var identity) ||
            !_functions.TryGetValue(identity, out var versions) ||
            versions.Count == 0)
        {
            formulaFunction = null!;
            return false;
        }
        formulaFunction = versions.Last().Value;
        return true;
    }

    private void ValidateDescriptor(FormulaFunctionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!descriptor.MinimumHostApiVersion.IsSupportedBy(
                _policy.HostApiVersion))
        {
            throw new InvalidOperationException(
                $"Formula function '{descriptor.Identity}' requires host API " +
                $"{descriptor.MinimumHostApiVersion}, but the registry provides " +
                $"{_policy.HostApiVersion}.");
        }
        var unsupported = descriptor.Capabilities &
                          ~_policy.SupportedCapabilities;
        if (unsupported != FormulaFunctionCapabilities.None)
        {
            throw new InvalidOperationException(
                $"Formula function '{descriptor.Identity}' requests unsupported " +
                $"capabilities: {unsupported}.");
        }
        if (!_policy.AllowVolatileFunctions &&
            descriptor.Volatility == FormulaFunctionVolatility.Volatile)
        {
            throw new InvalidOperationException(
                $"Volatile formula function '{descriptor.Identity}' is not allowed.");
        }
        if (!_policy.AllowExternalStateFunctions &&
            (descriptor.Volatility == FormulaFunctionVolatility.ExternalState ||
             descriptor.SecurityClassification ==
                 FormulaFunctionSecurityClassification.ExternalState))
        {
            throw new InvalidOperationException(
                $"External-state formula function '{descriptor.Identity}' is not allowed.");
        }
        if (descriptor.SecurityClassification >
            _policy.MaximumSecurityClassification)
        {
            throw new InvalidOperationException(
                $"Formula function '{descriptor.Identity}' exceeds the configured " +
                "security classification.");
        }
    }

    private void EnsureNameOwnership(
        FormulaFunctionIdentity identity,
        IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            if (_nameOwners.TryGetValue(name, out var owner) &&
                owner != identity)
            {
                throw new InvalidOperationException(
                    $"Formula name '{name}' is already owned by '{owner}'.");
            }
        }
    }

    private static void EnsureAliasStability(
        SortedDictionary<FormulaFunctionVersion, IVersionedFormulaFunction>
            versions,
        string[] names)
    {
        var existingNames = versions.Values
            .First()
            .Descriptor
            .EnumerateFormulaNames()
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        var requestedNames = names
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        if (!existingNames.SequenceEqual(
                requestedNames,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "All versions of one formula-function identity must expose " +
                "the same primary name and aliases.");
        }
    }

    private static void ValidatePolicy(FormulaFunctionRegistryPolicy policy)
    {
        if (policy.HostApiVersion.Major <= 0)
        {
            throw new ArgumentException(
                "The registry host API version must be initialized.",
                nameof(policy));
        }
        FormulaFunctionDescriptor.ValidateCapabilities(
            policy.SupportedCapabilities);
        if (!Enum.IsDefined(policy.MaximumSecurityClassification))
        {
            throw new ArgumentOutOfRangeException(nameof(policy));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            policy.MaximumVersionsPerIdentity);
    }

    private sealed class LegacyFormulaFunctionAdapter :
        IVersionedFormulaFunction
    {
        private readonly IFormulaFunction _inner;

        public LegacyFormulaFunctionAdapter(IFormulaFunction inner)
        {
            _inner = inner;
            Descriptor = new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("LEGACY", inner.Name),
                new FormulaFunctionVersion(0, 0, 0),
                FormulaFunctionApiVersion.Current,
                0,
                int.MaxValue,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.RangeArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                securityClassification:
                    FormulaFunctionSecurityClassification.ContextReadOnly,
                propagateArgumentErrors: false,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.FlattenedValues);
        }

        public FormulaFunctionDescriptor Descriptor { get; }

        public string Name => Descriptor.Identity.Name;

        public FormulaEvaluationResult Invoke(
            IReadOnlyList<CellValue> arguments,
            IFormulaEvaluationContext context) =>
            _inner.Invoke(arguments, context);

        public FormulaEvaluationResult Invoke(
            FormulaFunctionInvocation invocation) =>
            _inner.Invoke(
                invocation.FlattenValues(),
                invocation.Context);
    }
}

internal static class FormulaFunctionName
{
    public static string Normalize(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim().ToUpperInvariant();
        if (normalized.Length > 128 ||
            !IsFormulaName(normalized))
        {
            throw new ArgumentException(
                $"'{name}' is not a valid formula function name.",
                nameof(name));
        }
        return normalized;
    }

    public static string NormalizeNamespace(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length > 128 ||
            normalized.Any(static character =>
                !(char.IsAsciiLetterOrDigit(character) ||
                  character is '_' or '.' or '-')))
        {
            throw new ArgumentException(
                $"'{value}' is not a valid formula-function namespace.",
                nameof(value));
        }
        return normalized;
    }

    private static bool IsFormulaName(string value)
    {
        if (!(char.IsAsciiLetter(value[0]) || value[0] == '_'))
        {
            return false;
        }
        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if (!(char.IsAsciiLetterOrDigit(character) ||
                  character is '_' or '.'))
            {
                return false;
            }
        }
        return true;
    }
}

internal static class FormulaErrorMapping
{
    public static FormulaErrorCode ToErrorCode(CellValue value)
    {
        if (value.Kind != CellValueKind.Error)
        {
            return FormulaErrorCode.None;
        }
        return Convert.ToString(
            value.RawValue,
            CultureInfo.InvariantCulture) switch
        {
            "#DIV/0!" => FormulaErrorCode.DivisionByZero,
            "#REF!" => FormulaErrorCode.InvalidReference,
            "#NAME?" => FormulaErrorCode.InvalidName,
            "#CIRC!" => FormulaErrorCode.CircularReference,
            "#N/A" => FormulaErrorCode.NotAvailable,
            "#SPILL!" => FormulaErrorCode.Spill,
            _ => FormulaErrorCode.InvalidValue,
        };
    }
}
