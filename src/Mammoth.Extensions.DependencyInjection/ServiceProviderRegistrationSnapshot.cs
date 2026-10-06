using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection;

// Authoritative metadata is never exposed through the legacy mutable public collections.
internal sealed class ServiceProviderRegistrationSnapshot
{
    private readonly HashSet<Type> _types = [];
    private readonly HashSet<object> _keys = [];
    private readonly Dictionary<Type, IReadOnlyCollection<object>> _keysByType = [];
    private readonly ServiceLifetimes _lifetimes = new();
    private readonly Dictionary<ServiceIdentifier, ServiceDescriptor> _descriptors = [];
    private ServiceDescriptor[] _orderedDescriptors;

    internal ServiceProviderRegistrationSnapshot(IEnumerable<ServiceDescriptor> descriptors,
        IEnumerable<Type> types, IEnumerable<object> keys, Dictionary<Type, HashSet<object>> keysByType)
    {
        // Copy the factory's discovery metadata so its filtering is preserved. The public
        // collections remain independent, and no mutable discovery storage escapes here.
        _types.UnionWith(types);
        _keys.UnionWith(keys);
        _orderedDescriptors = descriptors.ToArray();
        foreach (var descriptor in _orderedDescriptors)
        {
            _lifetimes.Add(descriptor.ServiceType, descriptor.Lifetime, descriptor.ServiceKey);
            _descriptors[ServiceIdentifier.FromDescriptor(descriptor)] = descriptor;
        }
        foreach (var entry in keysByType)
            _keysByType.Add(entry.Key, Array.AsReadOnly(entry.Value.ToArray()));
    }

    // Called once, after enrichment and before any provider is built. Public query
    // metadata remains the caller snapshot; activation uses the final native graph.
    internal void CaptureActivationRegistrations(IEnumerable<ServiceDescriptor> descriptors)
    {
        _orderedDescriptors = descriptors.ToArray();
        _descriptors.Clear();
        foreach (var descriptor in _orderedDescriptors)
            _descriptors[ServiceIdentifier.FromDescriptor(descriptor)] = descriptor;
    }

    internal void ValidateGenericConstraints(Type type, object? key)
    {
        if (!type.IsConstructedGenericType) return;
        // Exact registrations (including wildcard keys) win over open generics.
        if (Find(type, key) != null) return;
        var descriptor = Find(type.GetGenericTypeDefinition(), key);
        var implementation = descriptor?.IsKeyedService == true
            ? descriptor.KeyedImplementationType : descriptor?.ImplementationType;
        // Closing a candidate binding can throw even when another constructor is
        // selected. Native DI checks this before activating any dependencies.
        implementation?.MakeGenericType(type.GenericTypeArguments);
    }

    internal ConstructorRegistration? GetConstructorRegistration(int index, object? key) =>
        index < 0 || index >= _orderedDescriptors.Length ? null
            : Bind(_orderedDescriptors[index], index, _orderedDescriptors[index].ServiceType, key, false);

    internal IEnumerable<ConstructorRegistration> GetConstructorRegistrations(Type type, object? key)
    {
        // Native's built-in unkeyed call sites take precedence over registrations.
        if (key == null && (type == typeof(IServiceProvider) || type == typeof(IServiceScopeFactory)
            || type == typeof(IServiceProviderIsService) || type == typeof(IServiceProviderIsKeyedService))) yield break;
        var descriptor = Find(type, key);
        if (descriptor == null && type.IsConstructedGenericType)
            descriptor = Find(type.GetGenericTypeDefinition(), key);
        if (descriptor != null)
        {
            var binding = Bind(descriptor, Array.FindLastIndex(_orderedDescriptors, candidate => ReferenceEquals(candidate, descriptor)), type, key, false);
            if (binding != null) yield return binding;
            yield break;
        }
        if (!type.IsConstructedGenericType || type.GetGenericTypeDefinition() != typeof(IEnumerable<>)) yield break;
        var itemType = type.GenericTypeArguments[0];
        for (var index = 0; index < _orderedDescriptors.Length; index++)
        {
            descriptor = _orderedDescriptors[index];
            if (!KeysMatch(key, descriptor.ServiceKey)) continue;
            if (descriptor.ServiceType != itemType && (!itemType.IsConstructedGenericType
                || descriptor.ServiceType != itemType.GetGenericTypeDefinition())) continue;
            var actualKey = key == KeyedService.AnyKey ? descriptor.ServiceKey : key;
            var binding = Bind(descriptor, index, itemType, actualKey, skipInvalidGeneric: true);
            if (binding != null) yield return binding;
        }
    }

    private static ConstructorRegistration? Bind(ServiceDescriptor descriptor, int index, Type serviceType,
        object? key, bool skipInvalidGeneric)
    {
        var implementation = descriptor.IsKeyedService ? descriptor.KeyedImplementationType : descriptor.ImplementationType;
        // User factories and supplied instances are opaque leaves, never executed by validation.
        if (implementation == null) return null;
        if (implementation.IsGenericTypeDefinition)
        {
            try { implementation = implementation.MakeGenericType(serviceType.GenericTypeArguments); }
            catch (ArgumentException) when (skipInvalidGeneric) { return null; }
        }
        return new ConstructorRegistration(index, new ServiceIdentifier(key, serviceType), implementation);
    }

    private static bool KeysMatch(object? lookup, object? registered) => lookup == null ? registered == null
        : registered != null && registered != KeyedService.AnyKey
            && (lookup == KeyedService.AnyKey || Equals(lookup, registered));

    internal sealed class ConstructorRegistration(int index, ServiceIdentifier service, Type implementationType)
    {
        internal int Index { get; } = index;
        internal ServiceIdentifier Service { get; } = service;
        internal Type ImplementationType { get; } = implementationType;
    }

    private ServiceDescriptor? Find(Type type, object? key)
    {
        if (_descriptors.TryGetValue(new ServiceIdentifier(key, type), out var descriptor)) return descriptor;
        return key != null && _descriptors.TryGetValue(new ServiceIdentifier(KeyedService.AnyKey, type), out descriptor)
            ? descriptor : null;
    }

    internal bool ContainsType(Type type) => _types.Contains(type) ||
        (type.IsConstructedGenericType && _types.Contains(type.GetGenericTypeDefinition()));
    internal bool ContainsKey(object key) => _keys.Contains(key);
    internal ServiceLifetime? GetLifetime(Type type, object? key = null) => _lifetimes.GetLifetime(type, key);
    internal IEnumerable<object> GetKeys(Type type)
    {
        // Merge into a fresh set so neither repeated enumeration nor public metadata
        // mutation can alter the snapshot's exact or generic-definition keys.
        var keys = new HashSet<object>();
        if (_keysByType.TryGetValue(type, out var exact))
            keys.UnionWith(exact);
        if (type.IsConstructedGenericType &&
            _keysByType.TryGetValue(type.GetGenericTypeDefinition(), out var generic))
            keys.UnionWith(generic);
        return keys;
    }
}
