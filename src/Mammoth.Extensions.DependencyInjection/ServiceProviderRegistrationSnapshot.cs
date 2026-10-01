using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection;

// Authoritative metadata is never exposed through the legacy mutable public collections.
internal sealed class ServiceProviderRegistrationSnapshot
{
    private readonly HashSet<Type> _types = [];
    private readonly HashSet<Type> _unkeyedTypes = [];
    private readonly HashSet<object> _keys = [];
    private readonly Dictionary<Type, IReadOnlyCollection<object>> _keysByType = [];
    private readonly ServiceLifetimes _lifetimes = new();

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
            if (!descriptor.IsKeyedService && _types.Contains(descriptor.ServiceType))
                _unkeyedTypes.Add(descriptor.ServiceType);
        }
        foreach (var entry in keysByType)
            _keysByType.Add(entry.Key, Array.AsReadOnly(entry.Value.ToArray()));
    }

    internal bool ContainsType(Type type) => _types.Contains(type) ||
        (type.IsConstructedGenericType && _types.Contains(type.GetGenericTypeDefinition()));
    internal bool HasUnkeyed(Type type) => _unkeyedTypes.Contains(type) ||
        (type.IsConstructedGenericType && _unkeyedTypes.Contains(type.GetGenericTypeDefinition()));
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
