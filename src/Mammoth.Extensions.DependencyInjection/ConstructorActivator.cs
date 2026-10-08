using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace Mammoth.Extensions.DependencyInjection;

internal static class ConstructorActivator
{
    // Cache type metadata only. Weak keys let collectible types and their metadata unload;
    // provider availability, dependency maps and requested keys remain resolution-specific.
    private static readonly ConditionalWeakTable<Type, TypeMetadata> Metadata = new();

    // Native DI's keyed probe reports implicit built-ins under every key. Its copied
    // descriptors distinguish explicit registrations without activating dependencies.
    // See docs/keyed-built-in-registration-probing.md for the compatibility rationale,
    // reflection guards and alternatives.
    private static readonly Type? NativeProbeType = typeof(ServiceProvider).Assembly.GetType(
        "Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteFactory");
    private static readonly FieldInfo? NativeDescriptors = NativeProbeType?.GetField("_descriptors", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly ConditionalWeakTable<IServiceProviderIsKeyedService, HashSet<ServiceIdentifier>> KeyedBuiltIns = new();

    private static bool IsBuiltIn(Type type) => type == typeof(IServiceProvider)
        || type == typeof(IServiceScopeFactory) || type == typeof(IServiceProviderIsService)
        || type == typeof(IServiceProviderIsKeyedService);

    private static bool IsKeyedBuiltInRegistered(IServiceProvider provider, IServiceProviderIsKeyedService probe, Type type, object key)
    {
        // Mammoth's immutable snapshot is the preferred source, including instrumented
        // providers. Custom probes retain their own availability contract.
        if (provider.GetService<ServiceProviderRegistrationSnapshot>() is { } snapshot)
            return snapshot.GetLifetime(type, key) != null || snapshot.GetLifetime(type, KeyedService.AnyKey) != null;
        if (probe.GetType() != NativeProbeType) return probe.IsKeyedService(type, key);
        var registrations = KeyedBuiltIns.GetValue(probe, static nativeProbe =>
        {
            // Guard native internals: changed or unavailable metadata must fail clearly
            // rather than guess availability or invoke a dependency factory.
            if (NativeDescriptors?.FieldType != typeof(ServiceDescriptor[])
                || NativeDescriptors.GetValue(nativeProbe) is not ServiceDescriptor[] descriptors)
                throw new NotSupportedException("Native keyed built-in constructor selection requires registration metadata. Use Mammoth's ServiceProviderFactory with this provider version.");
            var identities = new HashSet<ServiceIdentifier>();
            foreach (var descriptor in descriptors)
                if (descriptor.IsKeyedService && descriptor.ServiceKey != null && IsBuiltIn(descriptor.ServiceType))
                    identities.Add(ServiceIdentifier.FromDescriptor(descriptor));
            return identities;
        });
        return registrations.Contains(new ServiceIdentifier(key, type))
            || registrations.Contains(new ServiceIdentifier(KeyedService.AnyKey, type));
    }

    internal static object CreateKeyed(IServiceProvider provider, Type target, object? serviceKey, object? inner = null)
    {
        // Keep ActivatorUtilities behavior for constructors without contextual parameters.
        if (!Metadata.GetValue(target, static type => new TypeMetadata(type)).HasContextualParameters)
            return inner == null ? ActivatorUtilities.CreateInstance(provider, target)
                : ActivatorUtilities.CreateInstance(provider, target, inner);
        return CreateInstance(provider, target, [], serviceKey, inner);
    }

    internal static object CreateDependsOn(IServiceProvider provider, Type target, Dependency[] map, object? serviceKey = null)
        => CreateInstance(provider, target, map, serviceKey);

    private static object CreateInstance(IServiceProvider provider, Type target, Dependency[] map, object? serviceKey, object? inner = null)
    {
        if (provider is not IKeyedServiceProvider keyed)
            throw new NotSupportedException($"ServiceProvider must be an {nameof(IKeyedServiceProvider)}");
        var ordinaryProbe = provider.GetService<IServiceProviderIsService>()
            ?? throw new NotSupportedException("DependsOn constructor selection requires IServiceProviderIsService.");
        var keyedProbe = provider.GetService<IServiceProviderIsKeyedService>()
            ?? throw new NotSupportedException("DependsOn constructor selection requires IServiceProviderIsKeyedService.");
        var metadata = Metadata.GetValue(target, static type => new TypeMetadata(type));
        if (metadata.PreferredCount > 1)
            throw new InvalidOperationException($"Multiple preferred constructors on {target}.");
        ConstructorMetadata? selected = null;
        int selectedInnerIndex = -1;
        bool ambiguous = false;
        foreach (var candidate in metadata.Constructors)
        {
            if (metadata.PreferredCount == 1 && !candidate.IsPreferred) continue;
            var parameters = candidate.Parameters;
            // Only decorators supply an inner service. Attributed parameters retain their
            // key injection or dependency lookup rather than consuming that inner instance.
            var innerIndex = -1;
            if (inner != null)
                for (var i = 0; i < parameters.Length; i++)
                    if (!parameters[i].IsServiceKey && parameters[i].FromKey == null
                        && AcceptsValue(parameters[i].ParameterType, inner))
                    {
                        innerIndex = i;
                        break;
                    }
            if (inner != null && innerIndex < 0) continue;
            var satisfiable = true;
            for (var i = 0; i < parameters.Length; i++)
                if (i != innerIndex && !CanSupply(parameters[i]))
                {
                    satisfiable = false;
                    break;
                }
            if (!satisfiable) continue;
            if (selected == null || parameters.Length > selected.Parameters.Length)
            {
                selected = candidate;
                selectedInnerIndex = innerIndex;
                ambiguous = false;
            }
            else if (parameters.Length == selected.Parameters.Length)
                ambiguous = true;
        }
        if (selected == null)
            throw new InvalidOperationException($"No satisfiable public constructor on {target}.");
        if (ambiguous)
            throw new InvalidOperationException($"Multiple equally long satisfiable constructors on {target}.");
        // Resolve dependencies outside the catch: only unwrap the reflection invocation.
        var arguments = new object?[selected.Parameters.Length];
        for (var i = 0; i < arguments.Length; i++)
            arguments[i] = i == selectedInnerIndex ? inner : Resolve(selected.Parameters[i]);
        try
        {
            return selected.Constructor.Invoke(arguments);
        }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }

        bool CanSupply(ParameterMetadata parameter)
        {
            var dependency = FindDependency(map, parameter.Name);
            if (dependency != null)
                return dependency.T == Dependency.DependencyType.KeyedServices
                    ? IsKeyedRegistered(parameter.ParameterType, dependency.Value)
                    : AcceptsValue(parameter.ParameterType, dependency.Value);
            if (parameter.IsServiceKey)
            {
                if (serviceKey != null && parameter.ParameterType != typeof(object) && parameter.ParameterType != serviceKey.GetType())
                    throw new InvalidOperationException("The ServiceKey parameter type must match the service key type or be object.");
                return AcceptsValue(parameter.ParameterType, serviceKey);
            }
            return IsRegistered(parameter) || parameter.HasDefaultValue;
        }

        bool IsRegistered(ParameterMetadata parameter)
        {
            var key = EffectiveKey(parameter.FromKey, serviceKey);
            return key != null
                ? IsKeyedRegistered(parameter.ParameterType, key)
                : ordinaryProbe.IsService(parameter.ParameterType);
        }

        bool IsKeyedRegistered(Type type, object? key)
        {
            if (key != null && IsBuiltIn(type))
                return IsKeyedBuiltInRegistered(provider, keyedProbe, type, key);
            // DI 10's probe misses AnyKey open-generic fallback for a concrete key.
            // Probe the wildcard without activating a dependency; resolution still uses
            // the requested key, retaining native precedence, caching and constraint errors.
            return keyedProbe.IsKeyedService(type, key)
                || (key != null && type.IsConstructedGenericType && keyedProbe.IsKeyedService(type, KeyedService.AnyKey));
        }

        object? Resolve(ParameterMetadata parameter)
        {
            var dependency = FindDependency(map, parameter.Name);
            if (dependency != null)
                return dependency.T == Dependency.DependencyType.KeyedServices
                    ? keyed.GetRequiredKeyedService(parameter.ParameterType, dependency.Value)
                    : dependency.Value;
            if (parameter.IsServiceKey) return serviceKey;
            if (!IsRegistered(parameter) && parameter.HasDefaultValue)
                return parameter.DefaultValue;
            var key = EffectiveKey(parameter.FromKey, serviceKey);
            return key != null
                ? keyed.GetRequiredKeyedService(parameter.ParameterType, key)
                : provider.GetRequiredService(parameter.ParameterType);
        }
    }

    private static Dependency? FindDependency(Dependency[] map, string? name)
    {
        foreach (var dependency in map)
            if (dependency.ParameterName == name) return dependency;
        return null;
    }

    private sealed class TypeMetadata
    {
        internal ConstructorMetadata[] Constructors { get; }
        internal int PreferredCount { get; }
        internal bool HasContextualParameters { get; }

        internal TypeMetadata(Type target)
        {
            Constructors = target.GetConstructors().Select(c => new ConstructorMetadata(c)).ToArray();
            foreach (var constructor in Constructors)
            {
                if (constructor.IsPreferred) PreferredCount++;
                foreach (var parameter in constructor.Parameters)
                    if (parameter.IsServiceKey || parameter.FromKey?.LookupMode == ServiceKeyLookupMode.InheritKey)
                        HasContextualParameters = true;
            }
        }
    }

    private sealed class ConstructorMetadata
    {
        internal ConstructorInfo Constructor { get; }
        internal ParameterMetadata[] Parameters { get; }
        internal bool IsPreferred { get; }

        internal ConstructorMetadata(ConstructorInfo constructor)
        {
            Constructor = constructor;
            Parameters = constructor.GetParameters().Select(p => new ParameterMetadata(p)).ToArray();
            IsPreferred = constructor.IsDefined(typeof(ActivatorUtilitiesConstructorAttribute), false);
        }
    }

    private sealed class ParameterMetadata
    {
        internal string? Name { get; }
        internal Type ParameterType { get; }
        internal bool IsServiceKey { get; }
        internal FromKeyedServicesAttribute? FromKey { get; }
        internal bool HasDefaultValue { get; }
        internal object? DefaultValue { get; }

        internal ParameterMetadata(ParameterInfo parameter)
        {
            Name = parameter.Name;
            ParameterType = parameter.ParameterType;
            IsServiceKey = parameter.IsDefined(typeof(ServiceKeyAttribute), false);
            FromKey = parameter.GetCustomAttribute<FromKeyedServicesAttribute>();
            // Metadata covers unused constructors and overridden parameters too. Normalize
            // DateTime's framework-specific reflection failure before caching the default;
            // map/service availability and precedence remain resolution-specific.
            HasDefaultValue = ParameterDefaultValue.TryGetDefaultValue(parameter, out var value);
            DefaultValue = value;
        }
    }

    private static object? EffectiveKey(FromKeyedServicesAttribute? attribute, object? serviceKey) => attribute?.LookupMode switch
    {
        ServiceKeyLookupMode.InheritKey => serviceKey,
        ServiceKeyLookupMode.ExplicitKey => attribute.Key,
        _ => null
    };

    private static bool AcceptsValue(Type type, object? value) => value != null
        ? type.IsInstanceOfType(value)
        : !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
}
