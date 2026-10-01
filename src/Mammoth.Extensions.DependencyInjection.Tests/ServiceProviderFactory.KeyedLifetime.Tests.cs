using Microsoft.Extensions.DependencyInjection;
namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class KeyedLifetimeRegressionTests
{
    [TestMethod]
    [DataRow(ServiceLifetime.Transient, ServiceLifetime.Transient, false)]
    [DataRow(ServiceLifetime.Transient, ServiceLifetime.Transient, true)]
    [DataRow(ServiceLifetime.Transient, ServiceLifetime.Scoped, false)]
    [DataRow(ServiceLifetime.Transient, ServiceLifetime.Scoped, true)]
    [DataRow(ServiceLifetime.Transient, ServiceLifetime.Singleton, false)]
    [DataRow(ServiceLifetime.Transient, ServiceLifetime.Singleton, true)]
    [DataRow(ServiceLifetime.Scoped, ServiceLifetime.Transient, false)]
    [DataRow(ServiceLifetime.Scoped, ServiceLifetime.Transient, true)]
    [DataRow(ServiceLifetime.Scoped, ServiceLifetime.Scoped, false)]
    [DataRow(ServiceLifetime.Scoped, ServiceLifetime.Scoped, true)]
    [DataRow(ServiceLifetime.Scoped, ServiceLifetime.Singleton, false)]
    [DataRow(ServiceLifetime.Scoped, ServiceLifetime.Singleton, true)]
    [DataRow(ServiceLifetime.Singleton, ServiceLifetime.Transient, false)]
    [DataRow(ServiceLifetime.Singleton, ServiceLifetime.Transient, true)]
    [DataRow(ServiceLifetime.Singleton, ServiceLifetime.Scoped, false)]
    [DataRow(ServiceLifetime.Singleton, ServiceLifetime.Scoped, true)]
    [DataRow(ServiceLifetime.Singleton, ServiceLifetime.Singleton, false)]
    [DataRow(ServiceLifetime.Singleton, ServiceLifetime.Singleton, true)]
    public void KeyedAndUnkeyedLifetimesAreIndependent(ServiceLifetime unkeyed, ServiceLifetime keyed, bool reverse)
    {
        IServiceCollection services = new ServiceCollection();
        var ordinary = new ServiceDescriptor(typeof(IFoo), typeof(Foo), unkeyed);
        var blue = new ServiceDescriptor(typeof(IFoo), "blue", typeof(Foo), keyed);
        services.Add(reverse ? blue : ordinary);
        services.Add(reverse ? ordinary : blue);
        services.Add(new ServiceDescriptor(typeof(IFoo), "other", typeof(Foo), ServiceLifetime.Transient));
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        var lifetimes = provider.GetRequiredService<ServiceLifetimes>();
        Assert.AreEqual(unkeyed, lifetimes.GetLifetime(typeof(IFoo)));
        Assert.AreEqual(keyed, lifetimes.GetLifetime(typeof(IFoo), "blue"));
        Assert.IsNull(lifetimes.GetLifetime(typeof(IFoo), "missing"));
        Assert.AreEqual(ServiceLifetime.Transient, lifetimes.GetLifetime(typeof(IFoo), "other"));
        Assert.AreEqual(keyed == ServiceLifetime.Scoped, provider.IsKeyedScopedServiceRegistered<IFoo>("blue"));
        Assert.AreEqual(keyed == ServiceLifetime.Singleton, provider.IsKeyedSingletonServiceRegistered<IFoo>("blue"));
        Assert.AreEqual(keyed == ServiceLifetime.Transient, provider.IsKeyedTransientServiceRegistered<IFoo>("blue"));
        Assert.AreEqual(keyed == ServiceLifetime.Scoped, provider.IsKeyedScopedServiceRegistered(typeof(IFoo), "blue"));
        Assert.AreEqual(keyed == ServiceLifetime.Singleton, provider.IsKeyedSingletonServiceRegistered(typeof(IFoo), "blue"));
        Assert.AreEqual(keyed == ServiceLifetime.Transient, provider.IsKeyedTransientServiceRegistered(typeof(IFoo), "blue"));
        Assert.IsFalse(provider.IsKeyedScopedServiceRegistered<IFoo>("missing"));
        Assert.IsFalse(provider.IsKeyedSingletonServiceRegistered<IFoo>("missing"));
        Assert.IsFalse(provider.IsKeyedTransientServiceRegistered<IFoo>("missing"));
        using var scope = provider.CreateScope();
        var first = scope.ServiceProvider.GetRequiredKeyedService<IFoo>("blue");
        var second = scope.ServiceProvider.GetRequiredKeyedService<IFoo>("blue");
        if (keyed == ServiceLifetime.Transient) Assert.AreNotSame(first, second);
        else Assert.AreSame(first, second);
    }

    [TestMethod]
    public void MissingIdentitiesAndDuplicateRegistrations()
    {
        var lifetimes = new ServiceLifetimes();
        Assert.IsNull(lifetimes.GetLifetime(typeof(IFoo)));
        Assert.IsNull(lifetimes.GetLifetime(typeof(IFoo), "key"));
        lifetimes.Add(typeof(IFoo), ServiceLifetime.Singleton, "key");
        Assert.IsNull(lifetimes.GetLifetime(typeof(IFoo)));
        lifetimes.Add(typeof(IFoo), ServiceLifetime.Transient, "key");
        Assert.AreEqual(ServiceLifetime.Transient, lifetimes.GetLifetime(typeof(IFoo), "key"));
        lifetimes.Add(typeof(IFoo), ServiceLifetime.Scoped);
        lifetimes.Add(typeof(IFoo), ServiceLifetime.Singleton);
        Assert.AreEqual(ServiceLifetime.Singleton, lifetimes.GetLifetime(typeof(IFoo)));
        Assert.AreEqual(ServiceLifetime.Transient, lifetimes.GetLifetime(typeof(IFoo), "key"));
        Assert.IsNull(lifetimes.GetLifetime(typeof(Foo), "key"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DiagnosticSingletonExemptionUsesActualKeyedConsumer(bool keyedSingleton)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<DisposableDependency>();
        services.Add(new ServiceDescriptor(typeof(Consumer), sp => new Consumer(sp.GetRequiredService<DisposableDependency>()),
            keyedSingleton ? ServiceLifetime.Scoped : ServiceLifetime.Singleton));
        services.Add(new ServiceDescriptor(typeof(Consumer), "blue", (sp, _) => new Consumer(sp.GetRequiredService<DisposableDependency>()),
            keyedSingleton ? ServiceLifetime.Singleton : ServiceLifetime.Scoped));
        using var provider = ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            DetectIncorrectUsageOfTransientDisposables = true,
            AllowSingletonToResolveTransientDisposables = true
        });
        if (keyedSingleton)
            Assert.IsNotNull(provider.GetRequiredKeyedService<Consumer>("blue"));
        else
            Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetRequiredKeyedService<Consumer>("blue"));
    }

    public interface IFoo { }
    public sealed class Foo : IFoo { }
    public sealed class DisposableDependency : IDisposable { public void Dispose() { } }
    public sealed class Consumer(DisposableDependency dependency) { public DisposableDependency Dependency { get; } = dependency; }
}
