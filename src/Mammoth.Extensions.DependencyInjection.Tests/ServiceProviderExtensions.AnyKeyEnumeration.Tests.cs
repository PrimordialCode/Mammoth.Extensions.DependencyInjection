using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class AnyKeyEnumerationRegressionTests
{
    [TestMethod]
    [DataRow(false, ServiceLifetime.Transient, false)]
    [DataRow(false, ServiceLifetime.Scoped, false)]
    [DataRow(false, ServiceLifetime.Singleton, false)]
    [DataRow(true, ServiceLifetime.Transient, false)]
    [DataRow(true, ServiceLifetime.Scoped, false)]
    [DataRow(true, ServiceLifetime.Singleton, false)]
    [DataRow(false, ServiceLifetime.Transient, true)]
    [DataRow(false, ServiceLifetime.Scoped, true)]
    [DataRow(false, ServiceLifetime.Singleton, true)]
    [DataRow(true, ServiceLifetime.Transient, true)]
    [DataRow(true, ServiceLifetime.Scoped, true)]
    [DataRow(true, ServiceLifetime.Singleton, true)]
    public void ConcreteKeysAreEnumeratedOnceWithoutLosingWildcardMetadata(bool typeOverload, ServiceLifetime lifetime, bool diagnostics)
    {
        IServiceCollection services = new ServiceCollection();
        var concreteCalls = 0;
        var wildcardCalls = 0;
        services.Add(ServiceDescriptor.DescribeKeyed(typeof(IFoo), "blue", (_, key) =>
        {
            concreteCalls++;
            return new Foo(key);
        }, lifetime));
        services.AddKeyedSingleton<IFoo>(KeyedService.AnyKey, (_, key) =>
        {
            wildcardCalls++;
            return new Foo(key);
        });
        using var provider = ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            ValidateOnBuild = diagnostics,
            DetectIncorrectUsageOfTransientDisposables = diagnostics
        });
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        Assert.IsTrue(sp.IsKeyedServiceRegistered(KeyedService.AnyKey));
        Assert.IsTrue(sp.IsKeyedSingletonServiceRegistered<IFoo>(KeyedService.AnyKey));
        Assert.IsTrue(sp.GetRequiredService<ServiceKeys<IFoo>>().Contains(KeyedService.AnyKey));
        Assert.IsTrue(sp.GetRequiredService<ServiceKeys>().Contains(KeyedService.AnyKey));

        var native = sp.GetKeyedServices<IFoo>(KeyedService.AnyKey).ToArray();
        Assert.HasCount(1, native);
        Assert.AreEqual(1, concreteCalls);
        Assert.AreEqual(0, wildcardCalls);
        for (var i = 0; i < 3; i++)
        {
            var before = concreteCalls;
            var actual = Enumerate<IFoo>(sp, typeOverload);
            Assert.AreEqual(lifetime == ServiceLifetime.Transient ? 1 : 0, concreteCalls - before, "A concrete transient factory must run only once per enumeration.");
            Assert.HasCount(1, actual, "The sentinel must not enumerate concrete registrations a second time.");
            Assert.AreEqual("blue", ((Foo)actual[0]).Key);
            if (lifetime != ServiceLifetime.Transient)
                Assert.AreSame(native[0], actual[0]);
            else
                Assert.AreNotSame(native[0], actual[0]);
            Assert.AreEqual(0, wildcardCalls);
        }
        var fallback = (Foo)sp.GetRequiredKeyedService<IFoo>("unknown");
        Assert.AreEqual("unknown", fallback.Key);
        Assert.AreEqual(1, wildcardCalls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WildcardOnlyRegistrationIsNotAConcreteEnumerableEntry(bool typeOverload)
    {
        var calls = 0;
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IFoo>(KeyedService.AnyKey, (_, key) =>
        {
            calls++;
            return new Foo(key);
        });
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        Assert.HasCount(0, provider.GetKeyedServices<IFoo>(KeyedService.AnyKey).ToArray());
        Assert.HasCount(0, Enumerate<IFoo>(provider, typeOverload));
        Assert.AreEqual(0, calls);
        Assert.AreEqual("missing", ((Foo)provider.GetRequiredKeyedService<IFoo>("missing")).Key);
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, false)]
    [DataRow(false, true, true)]
    [DataRow(true, false, false)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    [DataRow(true, true, true)]
    public void DistinctRegistrationsSharingAnInstanceArePreserved(bool typeOverload, bool legacyMetadata, bool wildcard)
    {
        var shared = new Foo("shared");
        var services = new ServiceCollection();
        services.AddSingleton<IFoo>(shared);
        services.AddKeyedSingleton<IFoo>("blue", shared);
        services.AddKeyedSingleton<IFoo>("blue", shared);
        services.AddKeyedSingleton<IFoo>(42, shared);
        if (wildcard)
            services.AddKeyedSingleton<IFoo>(KeyedService.AnyKey, new Foo("fallback"));
        if (legacyMetadata)
            services.AddSingleton(new ServiceKeys<IFoo>(wildcard ? new object[] { "blue", 42, KeyedService.AnyKey } : new object[] { "blue", 42 }));
        using var provider = legacyMetadata ? services.BuildServiceProvider() : ServiceProviderFactory.CreateServiceProvider(services);
        var expected = provider.GetServices<IFoo>().Concat(provider.GetKeyedServices<IFoo>("blue"))
            .Concat(provider.GetKeyedServices<IFoo>(42)).ToArray();
        Assert.HasCount(4, expected);
        for (var i = 0; i < 3; i++)
        {
            var actual = Enumerate<IFoo>(provider, typeOverload);
            CollectionAssert.AreEquivalent(expected, actual);
            Assert.IsTrue(actual.All(x => ReferenceEquals(shared, x)), "Registration multiplicity must be preserved even for shared instances.");
        }
    }

    [TestMethod]
    [DataRow(false, 0)]
    [DataRow(false, 1)]
    [DataRow(false, 2)]
    [DataRow(true, 0)]
    [DataRow(true, 1)]
    [DataRow(true, 2)]
    public void OpenAndClosedGenericKeysAreMergedOnceWithNativeEnumerationSemantics(bool typeOverload, int wildcardRegistration)
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped(typeof(IRepository<>), "shared", typeof(Repository<>));
        services.AddKeyedScoped(typeof(IRepository<>), "open", typeof(Repository<>));
        services.AddKeyedScoped<IRepository<string>, ClosedRepository>("shared");
        services.AddKeyedScoped<IRepository<string>, ClosedRepository>("closed");
        if (wildcardRegistration != 1)
            services.AddKeyedScoped(typeof(IRepository<>), KeyedService.AnyKey, typeof(FallbackRepository<>));
        if (wildcardRegistration != 0)
            services.AddKeyedScoped<IRepository<string>, ClosedFallbackRepository>(KeyedService.AnyKey);
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var expected = new[] { "shared", "open", "closed" }
            .SelectMany(key => sp.GetKeyedServices<IRepository<string>>(key)).ToArray();
        Assert.IsTrue(expected.Length > 0);
        Assert.IsInstanceOfType<ClosedRepository>(sp.GetRequiredKeyedService<IRepository<string>>("shared"));
        for (var i = 0; i < 3; i++)
            CollectionAssert.AreEquivalent(expected, Enumerate<IRepository<string>>(sp, typeOverload));
        var fallback = sp.GetRequiredKeyedService<IRepository<string>>("unknown");
        if (wildcardRegistration == 0)
            Assert.IsInstanceOfType<FallbackRepository<string>>(fallback);
        else
            Assert.IsInstanceOfType<ClosedFallbackRepository>(fallback);
        Assert.IsTrue(sp.IsKeyedServiceRegistered(KeyedService.AnyKey));
    }

    [TestMethod]
    [DataRow(false, ServiceLifetime.Transient)]
    [DataRow(false, ServiceLifetime.Scoped)]
    [DataRow(false, ServiceLifetime.Singleton)]
    [DataRow(true, ServiceLifetime.Transient)]
    [DataRow(true, ServiceLifetime.Scoped)]
    [DataRow(true, ServiceLifetime.Singleton)]
    public void EachEnumeratedConcreteDisposableIsCreatedAndDisposedOnce(bool typeOverload, ServiceLifetime lifetime)
    {
        var created = new List<DisposableFoo>();
        IServiceCollection services = new ServiceCollection();
        services.Add(ServiceDescriptor.DescribeKeyed(typeof(IFoo), "blue", (_, key) =>
        {
            var foo = new DisposableFoo(key);
            created.Add(foo);
            return foo;
        }, lifetime));
        services.AddKeyedSingleton<IFoo>(KeyedService.AnyKey, new Foo("fallback"));
        using (var provider = ServiceProviderFactory.CreateServiceProvider(services))
        using (var scope = provider.CreateScope())
        {
            var actual = Enumerate<IFoo>(scope.ServiceProvider, typeOverload);
            Assert.HasCount(1, created);
            Assert.HasCount(1, actual);
            Assert.AreSame(created[0], actual[0]);
            Assert.AreEqual(0, created[0].DisposeCount);
        }
        Assert.AreEqual(1, created[0].DisposeCount);
    }

    private static T[] Enumerate<T>(IServiceProvider provider, bool typeOverload) => typeOverload
        ? provider.GetAllServices(typeof(T)).Cast<T>().ToArray()
        : provider.GetAllServices<T>().ToArray();

    public interface IFoo { }
    public class Foo(object? key) : IFoo
    {
        public object? Key { get; } = key;
    }
    public sealed class DisposableFoo(object? key) : Foo(key), IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
    public interface IRepository<T> { }
    public class Repository<T> : IRepository<T> { }
    public sealed class ClosedRepository : Repository<string> { }
    public sealed class FallbackRepository<T> : Repository<T> { }
    public sealed class ClosedFallbackRepository : Repository<string> { }
}
