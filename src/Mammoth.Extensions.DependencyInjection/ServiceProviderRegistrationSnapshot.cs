using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection;

// Authoritative metadata is never exposed through the legacy mutable public collections.
internal sealed class ServiceProviderRegistrationSnapshot
{
    private readonly HashSet<Type> _types = [];
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
            _lifetimes.Add(descriptor.ServiceType, descriptor.Lifetime, descriptor.ServiceKey);
        foreach (var entry in keysByType)
            _keysByType.Add(entry.Key, Array.AsReadOnly(entry.Value.ToArray()));
    }

    internal bool ContainsType(Type type) => _types.Contains(type);
    internal bool ContainsKey(object key) => _keys.Contains(key);
    internal ServiceLifetime? GetLifetime(Type type, object? key = null) => _lifetimes.GetLifetime(type, key);
    internal IEnumerable<object> GetKeys(Type type) => _keysByType.TryGetValue(type, out var keys) ? keys : [];
}
