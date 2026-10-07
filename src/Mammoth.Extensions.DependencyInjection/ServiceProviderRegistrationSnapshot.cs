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

    internal ServiceProviderRegistrationSnapshot(IEnumerable<ServiceDescriptor> descriptors,
        IEnumerable<Type> types, IEnumerable<object> keys, Dictionary<Type, HashSet<object>> keysByType)
    {
        // Copy the factory's discovery metadata so its filtering is preserved. The public
        // collections remain independent, and no mutable discovery storage escapes here.
        _types.UnionWith(types);
        _keys.UnionWith(keys);
        foreach (var descriptor in descriptors)
        {
            _lifetimes.Add(descriptor.ServiceType, descriptor.Lifetime, descriptor.ServiceKey);
            _descriptors[ServiceIdentifier.FromDescriptor(descriptor)] = descriptor;
        }
        foreach (var entry in keysByType)
            _keysByType.Add(entry.Key, Array.AsReadOnly(entry.Value.ToArray()));
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
        _keysByType.TryGetValue(type, out var exact);
        IReadOnlyCollection<object>? generic = null;
        if (type.IsConstructedGenericType)
            _keysByType.TryGetValue(type.GetGenericTypeDefinition(), out generic);
        // The stored collections are copied and read-only, so a single group can
        // be reused safely. Allocate a union only when both groups contain keys.
        if (exact == null || exact.Count == 0)
            return generic is { Count: > 0 } ? generic : Array.Empty<object>();
        if (generic == null || generic.Count == 0)
            return exact;
        var keys = new HashSet<object>(exact);
        keys.UnionWith(generic);
        return keys;
    }
}
