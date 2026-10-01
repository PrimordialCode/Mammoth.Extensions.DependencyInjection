using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class GenericLifetimeSnapshotIntegrationTests
{
    [TestMethod]
    [DataRow(ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient, ServiceLifetime.Singleton, false)]
    [DataRow(ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient, ServiceLifetime.Singleton, true)]
    [DataRow(ServiceLifetime.Transient, ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient, false)]
    [DataRow(ServiceLifetime.Transient, ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient, true)]
    [DataRow(ServiceLifetime.Scoped, ServiceLifetime.Transient, ServiceLifetime.Singleton, ServiceLifetime.Scoped, false)]
    [DataRow(ServiceLifetime.Scoped, ServiceLifetime.Transient, ServiceLifetime.Singleton, ServiceLifetime.Scoped, true)]
    public void GenericAndClosedIdentitiesKeepIndependentSnapshotLifetimes(
        ServiceLifetime openUnkeyed, ServiceLifetime openKeyed, ServiceLifetime closedUnkeyed,
        ServiceLifetime closedKeyed, bool diagnostics)
    {
        IServiceCollection services = new ServiceCollection();
        services.Add(new ServiceDescriptor(typeof(IRepository<>), typeof(Repository<>), openUnkeyed));
        services.Add(new ServiceDescriptor(typeof(IRepository<>), "key", typeof(Repository<>), openKeyed));
        services.Add(new ServiceDescriptor(typeof(IRepository<>), "fallback", typeof(Repository<>), openKeyed));
        services.Add(new ServiceDescriptor(typeof(IRepository<string>), typeof(Repository<string>), closedUnkeyed));
        services.Add(new ServiceDescriptor(typeof(IRepository<string>), "key", typeof(Repository<string>), closedKeyed));
        using var provider = ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            DetectIncorrectUsageOfTransientDisposables = diagnostics
        });
        var metadata = provider.GetRequiredService<ServiceLifetimes>();
        var identities = new (Type Type, object? Key, ServiceLifetime? Lifetime)[]
        {
            (typeof(IRepository<int>), null, openUnkeyed),
            (typeof(IRepository<int>), "key", openKeyed),
            (typeof(IRepository<string>), null, closedUnkeyed),
            (typeof(IRepository<string>), "key", closedKeyed),
            (typeof(IRepository<string>), "fallback", openKeyed),
            (typeof(IRepository<int>), "missing", null),
            (typeof(IRepository<string>), "missing", null)
        };
        foreach (var identity in identities)
        {
            Assert.AreEqual(identity.Lifetime, metadata.GetLifetime(identity.Type, identity.Key));
            AssertLifetime(provider, identity.Type, identity.Key, identity.Lifetime);
        }
        foreach (var type in new[] { typeof(IRepository<>), typeof(IRepository<int>), typeof(IRepository<string>) })
        {
            metadata.Add(type, ServiceLifetime.Transient);
            foreach (var key in new[] { "key", "fallback", "missing" })
                metadata.Add(type, ServiceLifetime.Singleton, key);
        }
        provider.GetRequiredService<ServiceTypes>().Clear();
        provider.GetRequiredService<ServiceKeys>().Clear();
        provider.GetRequiredService<ServiceKeys<IRepository<string>>>().Clear();
        using var scope = provider.CreateScope();
        foreach (var identity in identities)
            AssertLifetime(scope.ServiceProvider, identity.Type, identity.Key, identity.Lifetime);
        Assert.IsTrue(scope.ServiceProvider.IsServiceRegistered<IRepository<int>>());
        var first = scope.ServiceProvider.GetRequiredKeyedService<IRepository<string>>("key");
        var second = scope.ServiceProvider.GetRequiredKeyedService<IRepository<string>>("key");
        if (closedKeyed == ServiceLifetime.Transient)
            Assert.AreNotSame(first, second);
        else
            Assert.AreSame(first, second);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void GenericDiagnosticExemptionUsesActualKeyAndIgnoresPublicMutation(bool keyedSingleton, bool decorated)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<DisposableDependency>();
        var keyedLifetime = keyedSingleton ? ServiceLifetime.Singleton : ServiceLifetime.Scoped;
        var unkeyedLifetime = keyedSingleton ? ServiceLifetime.Scoped : ServiceLifetime.Singleton;
        services.Add(new ServiceDescriptor(typeof(Consumer<>), typeof(Consumer<>), keyedLifetime));
        services.Add(new ServiceDescriptor(typeof(Consumer<>), "blue", typeof(Consumer<>), unkeyedLifetime));
        // Closed registrations are tracked by diagnostics and override the opposite
        // lifetimes on the generic definitions. Native open-generic tracking stays unchanged.
        services.Add(new ServiceDescriptor(typeof(Consumer<string>), typeof(Consumer<string>), unkeyedLifetime));
        services.Add(new ServiceDescriptor(typeof(Consumer<string>), "blue", typeof(Consumer<string>), keyedLifetime));
        ServiceDescriptor? privateLayer = null;
        if (decorated)
        {
            services.Decorate<Consumer<string>, ConsumerDecorator>();
            privateLayer = services.Single(d => d.ServiceType != typeof(Consumer<>)
                && d.ServiceType != typeof(Consumer<string>) && d.ServiceType != typeof(DisposableDependency));
        }
        using var provider = ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            DetectIncorrectUsageOfTransientDisposables = true,
            AllowSingletonToResolveTransientDisposables = true
        });
        var metadata = provider.GetRequiredService<ServiceLifetimes>();
        foreach (var type in new[] { typeof(Consumer<>), typeof(Consumer<string>) })
        {
            metadata.Add(type, keyedLifetime);
            metadata.Add(type, unkeyedLifetime, "blue");
        }
        if (privateLayer != null)
        {
            metadata.Add(privateLayer.ServiceType, unkeyedLifetime, privateLayer.ServiceKey);
            AssertLifetime(provider, privateLayer.ServiceType, privateLayer.ServiceKey, keyedLifetime);
            Assert.IsFalse(provider.IsServiceRegistered(privateLayer.ServiceType));
            Assert.IsFalse(provider.IsKeyedServiceRegistered(privateLayer.ServiceKey!));
        }
        AssertLifetime(provider, typeof(Consumer<string>), "blue", keyedLifetime);
        if (keyedSingleton)
        {
            var consumer = provider.GetRequiredKeyedService<Consumer<string>>("blue");
            Assert.AreSame(consumer, provider.GetRequiredKeyedService<Consumer<string>>("blue"));
            if (decorated) Assert.IsInstanceOfType<ConsumerDecorator>(consumer);
        }
        else
            Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetRequiredKeyedService<Consumer<string>>("blue"));
    }

    private static void AssertLifetime(IServiceProvider provider, Type type, object? key, ServiceLifetime? expected)
    {
        if (key == null)
        {
            Assert.AreEqual(expected == ServiceLifetime.Transient, provider.IsTransientServiceRegistered(type));
            Assert.AreEqual(expected == ServiceLifetime.Scoped, provider.IsScopedServiceRegistered(type));
            Assert.AreEqual(expected == ServiceLifetime.Singleton, provider.IsSingletonServiceRegistered(type));
        }
        else
        {
            Assert.AreEqual(expected == ServiceLifetime.Transient, provider.IsKeyedTransientServiceRegistered(type, key));
            Assert.AreEqual(expected == ServiceLifetime.Scoped, provider.IsKeyedScopedServiceRegistered(type, key));
            Assert.AreEqual(expected == ServiceLifetime.Singleton, provider.IsKeyedSingletonServiceRegistered(type, key));
        }
    }

    public interface IRepository<T> { }
    public sealed class Repository<T> : IRepository<T> { }
    public sealed class DisposableDependency : IDisposable { public void Dispose() { } }
    public class Consumer<T>(DisposableDependency dependency)
    {
        public DisposableDependency Dependency { get; } = dependency;
    }
    public sealed class ConsumerDecorator(Consumer<string> inner) : Consumer<string>(inner.Dependency);
}
