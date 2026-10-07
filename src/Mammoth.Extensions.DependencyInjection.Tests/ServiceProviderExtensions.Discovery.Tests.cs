using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class ServiceDiscoveryRegressionTests
{
    [TestMethod]
    [DataRow("none")]
    [DataRow("exact")]
    [DataRow("open")]
    [DataRow("both")]
    public void KeyDiscoveryRemainsIsolatedWhenReturnedKeysAndPublicMetadataAreMutated(string shape)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBox<string>, Box<string>>();
        if (shape is "open" or "both")
        {
            services.AddKeyedSingleton(typeof(IBox<>), new EqualKey(42), typeof(Box<>));
            services.AddKeyedSingleton(typeof(IBox<>), "open", typeof(Box<>));
        }
        if (shape is "exact" or "both")
        {
            services.AddKeyedSingleton<IBox<string>, ClosedBox>(new EqualKey(42));
            services.AddKeyedSingleton<IBox<string>, ClosedBox>("closed");
        }
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        var snapshot = provider.GetRequiredService<ServiceProviderRegistrationSnapshot>();
        var keys = snapshot.GetKeys(typeof(IBox<string>)).ToArray();
        Assert.HasCount(shape == "none" ? 0 : shape == "both" ? 3 : 2, keys);
        var expected = provider.GetServices<IBox<string>>()
            .Concat(keys.SelectMany(key => provider.GetKeyedServices<IBox<string>>(key))).ToArray();
        Assert.IsNotEmpty(expected);
        var returnedKeys = snapshot.GetKeys(typeof(IBox<string>)) as ICollection<object>;
        Assert.IsNotNull(returnedKeys);
        if (returnedKeys.IsReadOnly)
            Assert.ThrowsExactly<NotSupportedException>(() => returnedKeys.Add("invented"));
        else
            returnedKeys.Clear(); // A two-group union is a disposable copy, never snapshot storage.
        provider.GetRequiredService<ServiceKeys>().Clear();
        var publicKeys = provider.GetRequiredService<ServiceKeys<IBox<string>>>();
        publicKeys.Clear(); publicKeys.Add("invented"); publicKeys.Add(KeyedService.AnyKey);
        for (var i = 0; i < 3; i++)
        {
            CollectionAssert.AreEquivalent(keys, snapshot.GetKeys(typeof(IBox<string>)).ToArray());
            CollectionAssert.AreEquivalent(expected, provider.GetAllServices<IBox<string>>().ToArray());
            CollectionAssert.AreEquivalent(expected, provider.GetAllServices(typeof(IBox<string>)).ToArray());
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExplicitIteratorIsConsumedEagerlyBeforeResolvingKeyedGroups(bool typeOverload)
    {
        var events = new List<string>();
        var ordinary = new Marker("ordinary");
        IEnumerable<IMarker> Stream()
        {
            events.Add("start");
            yield return ordinary;
            yield return ordinary;
            events.Add("end");
        }
        var services = new ServiceCollection();
        services.AddTransient<IEnumerable<IMarker>>(_ => Stream());
        services.AddKeyedTransient<IMarker>("blue", (_, _) => { events.Add("keyed"); return new Marker("keyed"); });
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        var first = Discover(provider, typeOverload);
        CollectionAssert.AreEqual(new[] { "start", "end", "keyed" }, events);
        var firstItems = first.ToArray();
        CollectionAssert.AreEqual(new[] { "ordinary", "ordinary", "keyed" }, firstItems.Select(x => x.Name).ToArray());
        var second = Discover(provider, typeOverload);
        CollectionAssert.AreEqual(new[] { "start", "end", "keyed", "start", "end", "keyed" }, events);
        Assert.AreNotSame(firstItems[2], second.Last());
        Assert.AreSame(ordinary, firstItems[0]);
        Assert.AreSame(ordinary, firstItems[1]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExplicitIteratorFailureStillThrowsAtTheDiscoveryCall(bool typeOverload)
    {
        IEnumerable<IMarker> Stream()
        {
            yield return new Marker("first");
            throw new InvalidOperationException("stream failed");
        }
        var services = new ServiceCollection();
        services.AddSingleton<IEnumerable<IMarker>>(_ => Stream());
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => Discover(provider, typeOverload));
        Assert.AreEqual("stream failed", error.Message);
    }

    [TestMethod]
    [DataRow(false, ServiceLifetime.Transient)]
    [DataRow(false, ServiceLifetime.Scoped)]
    [DataRow(false, ServiceLifetime.Singleton)]
    [DataRow(true, ServiceLifetime.Transient)]
    [DataRow(true, ServiceLifetime.Scoped)]
    [DataRow(true, ServiceLifetime.Singleton)]
    public void RepeatedDiscoveryKeepsNativeElementLifetimes(bool typeOverload, ServiceLifetime lifetime)
    {
        var creations = 0;
        IServiceCollection services = new ServiceCollection();
        services.Add(ServiceDescriptor.Describe(typeof(IMarker), _ => { creations++; return new Marker("ordinary"); }, lifetime));
        services.Add(ServiceDescriptor.DescribeKeyed(typeof(IMarker), "blue", (_, _) => { creations++; return new Marker("keyed"); }, lifetime));
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        using var scope = provider.CreateScope();
        var first = Discover(scope.ServiceProvider, typeOverload).ToArray();
        var second = Discover(scope.ServiceProvider, typeOverload).ToArray();
        Assert.HasCount(2, first); Assert.HasCount(2, second);
        Assert.AreEqual(lifetime == ServiceLifetime.Transient ? 4 : 2, creations);
        using var other = provider.CreateScope();
        var third = Discover(other.ServiceProvider, typeOverload).ToArray();
        Assert.HasCount(2, third);
        for (var i = 0; i < first.Length; i++)
        {
            if (lifetime == ServiceLifetime.Transient) Assert.AreNotSame(first[i], second[i]);
            else Assert.AreSame(first[i], second[i]);
            if (lifetime == ServiceLifetime.Singleton) Assert.AreSame(first[i], third[i]);
            else Assert.AreNotSame(first[i], third[i]);
        }
        Assert.AreEqual(lifetime == ServiceLifetime.Transient ? 6 : lifetime == ServiceLifetime.Scoped ? 4 : 2, creations);
    }

    private static IEnumerable<IMarker> Discover(IServiceProvider provider, bool typeOverload)
        => typeOverload ? provider.GetAllServices(typeof(IMarker)).Cast<IMarker>() : provider.GetAllServices<IMarker>();

    public interface IBox<T>;
    public class Box<T> : IBox<T>;
    public sealed class ClosedBox : Box<string>;
    public interface IMarker { string Name { get; } }
    public sealed class Marker(string name) : IMarker { public string Name { get; } = name; }
    private sealed class EqualKey(int value)
    {
        private int Value => value;
        public override bool Equals(object? obj) => obj is EqualKey other && value == other.Value;
        public override int GetHashCode() => 0;
    }
}
