using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace Mammoth.DependencyInjection.Regression;

// Test/repro instrumentation only. Never linked into the production library.
// A bounded observation of accessor replacement makes cold-provider passes impossible.
internal sealed class NativeResolverCompilation
{
    private readonly ServiceProvider _provider;
    private readonly Type _serviceType;
    private readonly object? _key;
    private readonly Delegate _keyedBefore;
    private readonly Delegate? _unkeyedBefore;
    private NativeResolverCompilation(ServiceProvider provider, Type serviceType, object? key)
    {
        _provider = provider;
        _serviceType = serviceType;
        _key = key;
        _keyedBefore = ReadAccessor(key) ?? throw new InvalidOperationException("Resolve the keyed service once before observing compilation.");
        _unkeyedBefore = ReadAccessor(null);
        if (_keyedBefore.Target?.GetType().FullName?.Contains("DynamicServiceProviderEngine") != true)
            throw new InvalidOperationException("Expected a runtime delegate awaiting native dynamic compilation, but found " + Describe(_keyedBefore));
    }
    internal static NativeResolverCompilation Observe<T>(ServiceProvider provider, object? key) =>
        new(provider, typeof(IEnumerable<T>), key);

    internal static NativeResolverCompilation ObserveService(ServiceProvider provider, Type type, object? key) =>
        new(provider, type, key);

    internal void WaitForReplacement()
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(30))
        {
            // Broken DI installs the keyed resolver under the null key. Observe
            // either identity changing, then assert the actual resolved membership.
            if (!ReferenceEquals(ReadAccessor(_key), _keyedBefore)
                || (!ReferenceEquals(ReadAccessor(null), _unkeyedBefore) && UnkeyedCacheHasRequestedKey()))
                return;
            Thread.Sleep(1);
        }
        throw new TimeoutException($"Native accessor replacement was not observed in 30 seconds. DI={typeof(ServiceProvider).Assembly.FullName}; type={_serviceType}; key={_key}; keyed={Describe(ReadAccessor(_key))}; unkeyed={Describe(ReadAccessor(null))}");
    }
    private bool UnkeyedCacheHasRequestedKey()
    {
        var field = typeof(ServiceProvider).GetField("_serviceAccessors", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var entry in (IEnumerable)field.GetValue(_provider)!)
        {
            var entryType = entry.GetType();
            var identity = entryType.GetProperty("Key")!.GetValue(entry)!;
            var identityType = identity.GetType();
            if ((Type)identityType.GetProperty("ServiceType")!.GetValue(identity)! != _serviceType
                || identityType.GetProperty("ServiceKey")!.GetValue(identity) != null) continue;
            var accessor = entryType.GetProperty("Value")!.GetValue(entry)!;
            var callSite = accessor.GetType().GetProperty("CallSite")!.GetValue(accessor)!;
            var cache = callSite.GetType().GetProperty("Cache")!.GetValue(callSite)!;
            var cacheKey = cache.GetType().GetProperty("Key")!.GetValue(cache)!;
            var cacheIdentity = cacheKey.GetType().GetProperty("ServiceIdentifier")!.GetValue(cacheKey)!;
            return Equals(cacheIdentity.GetType().GetProperty("ServiceKey")!.GetValue(cacheIdentity), _key);
        }
        return false;
    }
    private Delegate? ReadAccessor(object? key)
    {
        var field = typeof(ServiceProvider).GetField("_serviceAccessors", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException("Native test instrumentation needs ServiceProvider._serviceAccessors.");
        foreach (var entry in (IEnumerable)field.GetValue(_provider)!)
        {
            var entryType = entry.GetType();
            var identity = entryType.GetProperty("Key")!.GetValue(entry)!;
            var identityType = identity.GetType();
            if ((Type)identityType.GetProperty("ServiceType")!.GetValue(identity)! != _serviceType
                || !Equals(identityType.GetProperty("ServiceKey")!.GetValue(identity), key)) continue;
            var accessor = entryType.GetProperty("Value")!.GetValue(entry)!;
            return (Delegate?)accessor.GetType().GetProperty("RealizedService")!.GetValue(accessor);
        }
        return null;
    }
    private static string Describe(Delegate? resolver) => resolver == null ? "absent" :
        $"{resolver.Method}; target={resolver.Target?.GetType().FullName ?? "none"}";
}
