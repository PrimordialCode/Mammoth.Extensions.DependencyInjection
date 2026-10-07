using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class CollectionLifetimeRegressionTests
{
    public static IEnumerable<object[]> LifetimePairs()
    {
        foreach (var ordinary in new[] { ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient })
        foreach (var keyed in new[] { ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient })
            yield return new object[] { ordinary, keyed };
    }

    [TestMethod]
    [DynamicData(nameof(LifetimePairs))]
    public void LastAssignableRegistrationWinsIndependentlyForEachKey(ServiceLifetime ordinary, ServiceLifetime keyed)
    {
        IServiceCollection services = new ServiceCollection();
        var registeredKey = new EqualKey(42);
        var queryKey = new EqualKey(42);
        Assert.AreNotSame(registeredKey, queryKey);
        services.AddSingleton<IMarker, Marker>();
        services.AddKeyedTransient<IMarker, Marker>(registeredKey);
        services.Add(ServiceDescriptor.Describe(typeof(Marker), typeof(Marker), ordinary));
        services.Add(ServiceDescriptor.DescribeKeyed(typeof(Marker), registeredKey, typeof(Marker), keyed));
        // Later registrations with the wrong key/type must not affect either result.
        services.AddKeyedScoped<IMarker, Marker>(new EqualKey(43));
        services.AddSingleton<Unrelated>();
        AssertLifetimes(services, ordinary, keyed, queryKey);
        AssertLifetimes(services, ordinary, ServiceLifetime.Scoped, new EqualKey(43));
    }

    [TestMethod]
    public void MissingRegistrationsReturnFalseForEmptyAndNonemptyCollections()
    {
        var services = new ServiceCollection();
        AssertLifetimes(services, null, null, "missing");
        services.AddSingleton<Unrelated>();
        services.AddKeyedSingleton<IMarker, Marker>("other");
        services.AddKeyedTransient<IMarker, Marker>(KeyedService.AnyKey);
        AssertLifetimes(services, null, null, "missing");
        AssertLifetimes(services, null, ServiceLifetime.Transient, KeyedService.AnyKey);
    }

    [TestMethod]
    public void QueriesObserveCollectionChangesAndNullKeyRegistrationsRemainUnkeyed()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IMarker, Marker>();
        services.AddKeyedSingleton<IMarker, Marker>("blue");
        AssertLifetimes(services, ServiceLifetime.Singleton, ServiceLifetime.Singleton, "blue");
        services.AddKeyedScoped<Marker>(null);
        services.AddKeyedTransient<Marker>("blue");
        AssertLifetimes(services, ServiceLifetime.Scoped, ServiceLifetime.Transient, "blue");
        services.RemoveAt(services.Count - 1);
        services.RemoveAt(services.Count - 1);
        AssertLifetimes(services, ServiceLifetime.Singleton, ServiceLifetime.Singleton, "blue");
    }

    [TestMethod]
    [DataRow(ServiceLifetime.Singleton)]
    [DataRow(ServiceLifetime.Scoped)]
    [DataRow(ServiceLifetime.Transient)]
    public void ArgumentValidationAndPrecedenceArePreserved(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        string Parameter(Action query) => Assert.ThrowsExactly<ArgumentNullException>(query).ParamName!;
        Assert.AreEqual("serviceType", Parameter(() => Query(services, lifetime, null!, false, null!)));
        Assert.AreEqual("serviceType", Parameter(() => Query(null!, lifetime, null!, true, null!)));
        Assert.AreEqual("serviceKey", Parameter(() => Query(services, lifetime, typeof(IMarker), true, null!)));
        Assert.AreEqual("serviceKey", Parameter(() => Query(null!, lifetime, typeof(IMarker), true, null!)));
        Assert.AreEqual("source", Parameter(() => Query(null!, lifetime, typeof(IMarker), false, "blue")));
        Assert.AreEqual("source", Parameter(() => Query(null!, lifetime, typeof(IMarker), true, "blue")));
    }

    [TestMethod]
    public void DescriptorArraysRetainAssignableFilteringAndRegistrationOrder()
    {
        IServiceCollection services = new ServiceCollection();
        var ordinary = ServiceDescriptor.Singleton(typeof(IMarker), typeof(Marker));
        var keyed = ServiceDescriptor.KeyedScoped(typeof(Marker), "blue", typeof(Marker));
        var derived = ServiceDescriptor.Transient(typeof(Marker), typeof(Marker));
        services.Add(ordinary); services.Add(keyed); services.Add(derived); services.AddSingleton<Unrelated>();
        CollectionAssert.AreEqual(new[] { ordinary, keyed, derived }, services.GetServiceDescriptors(typeof(IMarker)));
        CollectionAssert.AreEqual(new[] { ordinary, derived }, services.GetServiceDescriptors(typeof(IMarker), false));
        CollectionAssert.AreEqual(new[] { keyed }, services.GetServiceDescriptors(typeof(IMarker), true));
    }

    private static bool Query(IServiceCollection services, ServiceLifetime lifetime, Type type, bool keyed, object key)
        => (lifetime, keyed) switch
        {
            (ServiceLifetime.Singleton, false) => services.IsSingletonServiceRegistered(type),
            (ServiceLifetime.Scoped, false) => services.IsScopedServiceRegistered(type),
            (ServiceLifetime.Transient, false) => services.IsTransientServiceRegistered(type),
            (ServiceLifetime.Singleton, true) => services.IsKeyedSingletonServiceRegistered(type, key),
            (ServiceLifetime.Scoped, true) => services.IsKeyedScopedServiceRegistered(type, key),
            _ => services.IsKeyedTransientServiceRegistered(type, key)
        };

    private static void AssertLifetimes(IServiceCollection services, ServiceLifetime? ordinary, ServiceLifetime? keyed, object key)
    {
        foreach (var lifetime in new[] { ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient })
        {
            Assert.AreEqual(ordinary == lifetime, Query(services, lifetime, typeof(IMarker), false, key));
            Assert.AreEqual(keyed == lifetime, Query(services, lifetime, typeof(IMarker), true, key));
        }
        Assert.AreEqual(ordinary == ServiceLifetime.Singleton, services.IsSingletonServiceRegistered<IMarker>());
        Assert.AreEqual(ordinary == ServiceLifetime.Scoped, services.IsScopedServiceRegistered<IMarker>());
        Assert.AreEqual(ordinary == ServiceLifetime.Transient, services.IsTransientServiceRegistered<IMarker>());
        Assert.AreEqual(keyed == ServiceLifetime.Singleton, services.IsKeyedSingletonServiceRegistered<IMarker>(key));
        Assert.AreEqual(keyed == ServiceLifetime.Scoped, services.IsKeyedScopedServiceRegistered<IMarker>(key));
        Assert.AreEqual(keyed == ServiceLifetime.Transient, services.IsKeyedTransientServiceRegistered<IMarker>(key));
    }

    public interface IMarker;
    public sealed class Marker : IMarker;
    public sealed class Unrelated;
    private sealed class EqualKey(int value)
    {
        private int Value => value;
        public override bool Equals(object? obj) => obj is EqualKey other && value == other.Value;
        public override int GetHashCode() => 0;
    }
}
