using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class OpenGenericSnapshotIntegrationTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void KeyedGenericSnapshotResistsMutationAndPreservesClosedPrecedence(bool diagnostics)
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped(typeof(IRepository<>), "shared", typeof(Repository<>));
        services.AddKeyedScoped(typeof(IRepository<>), "open", typeof(Repository<>));
        services.AddKeyedSingleton<IRepository<string>, ClosedRepository>("shared");
        services.AddKeyedSingleton<IRepository<string>, ClosedRepository>("closed");
        using var provider = Build(services, diagnostics);
        Tamper(provider);
        var lifetimes = provider.GetRequiredService<ServiceLifetimes>();
        lifetimes.Add(typeof(IRepository<>), ServiceLifetime.Transient, "open");
        lifetimes.Add(typeof(IRepository<string>), ServiceLifetime.Transient, "shared");
        Assert.IsTrue(provider.IsServiceRegistered<IRepository<int>>());
        Assert.IsTrue(provider.IsKeyedScopedServiceRegistered<IRepository<int>>("shared"));
        Assert.IsTrue(provider.IsKeyedScopedServiceRegistered<IRepository<string>>("open"));
        Assert.IsTrue(provider.IsKeyedSingletonServiceRegistered<IRepository<string>>("shared"));
        Assert.IsFalse(provider.IsServiceRegistered<string>());
        Assert.IsTrue(provider.IsKeyedServiceRegistered("open"));
        Assert.IsFalse(provider.IsKeyedServiceRegistered("invented"));
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        Assert.IsInstanceOfType<ClosedRepository>(sp.GetRequiredKeyedService<IRepository<string>>("shared"));
        Assert.IsInstanceOfType<Repository<int>>(sp.GetRequiredKeyedService<IRepository<int>>("shared"));
        var expected = new[] { "shared", "open", "closed" }
            .SelectMany(key => sp.GetKeyedServices<IRepository<string>>(key)).Cast<object>().ToArray();
        for (int i = 0; i < 3; i++)
        {
            CollectionAssert.AreEquivalent(expected, sp.GetAllServices<IRepository<string>>().Cast<object>().ToArray());
            CollectionAssert.AreEquivalent(expected, sp.GetAllServices(typeof(IRepository<string>)).ToArray());
            Assert.HasCount(2, sp.GetAllServices<IRepository<int>>().ToArray());
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OpenSiblingAndDecoratedClosedGenericKeepPrivateLayersHidden(bool diagnostics)
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped(typeof(IRepository<>), "open", typeof(Repository<>));
        services.AddKeyedScoped<IRepository<string>, DisposableRepository>("decorated");
        services.Decorate<IRepository<string>, RepositoryDecorator>();
        services.Decorate<IRepository<string>, RepositoryDecorator>();
        var layers = services.Where(d => d.ServiceType != typeof(IRepository<>) && d.ServiceType != typeof(IRepository<string>)).ToArray();
        Assert.HasCount(2, layers);
        using var provider = Build(services, diagnostics);
        Tamper(provider);
        foreach (var layer in layers)
        {
            provider.GetRequiredService<ServiceTypes>().Add(layer.ServiceType);
            provider.GetRequiredService<ServiceKeys>().Add(layer.ServiceKey!);
            provider.GetRequiredService<ServiceKeys<IRepository<string>>>().Add(layer.ServiceKey!);
            Assert.IsFalse(provider.IsServiceRegistered(layer.ServiceType));
            Assert.IsFalse(provider.IsKeyedServiceRegistered(layer.ServiceKey!));
            Assert.IsTrue(provider.IsKeyedScopedServiceRegistered(layer.ServiceType, layer.ServiceKey!));
        }
        using var scope = provider.CreateScope();
        var outer = (RepositoryDecorator)scope.ServiceProvider.GetRequiredKeyedService<IRepository<string>>("decorated");
        var middle = (RepositoryDecorator)outer.Inner;
        var inner = (DisposableRepository)middle.Inner;
        var generic = scope.ServiceProvider.GetAllServices<IRepository<string>>().ToArray();
        var nonGeneric = scope.ServiceProvider.GetAllServices(typeof(IRepository<string>)).ToArray();
        Assert.HasCount(2, generic);
        Assert.HasCount(2, nonGeneric);
        Assert.HasCount(1, generic.Where(service => ReferenceEquals(service, outer)).ToArray());
        Assert.HasCount(1, nonGeneric.Where(service => ReferenceEquals(service, outer)).ToArray());
        scope.Dispose();
        Assert.AreEqual(1, inner.DisposeCount);
        Assert.AreEqual(1, middle.DisposeCount);
        Assert.AreEqual(1, outer.DisposeCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UnkeyedClosedAndGenericDiscoveryUsesIndependentSnapshot(bool diagnostics)
    {
        var services = new ServiceCollection();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddKeyedScoped(typeof(IRepository<>), "open", typeof(Repository<>));
        services.AddSingleton<IRepository<string>, ClosedRepository>();
        using var provider = Build(services, diagnostics);
        Tamper(provider);
        provider.GetRequiredService<ServiceLifetimes>().Add(typeof(IRepository<>), ServiceLifetime.Transient);
        Assert.IsTrue(provider.IsServiceRegistered<IRepository<int>>());
        Assert.IsTrue(provider.IsScopedServiceRegistered<IRepository<int>>());
        Assert.IsTrue(provider.IsSingletonServiceRegistered<IRepository<string>>());
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var expected = sp.GetServices<IRepository<string>>()
            .Concat(sp.GetKeyedServices<IRepository<string>>("open")).Cast<object>().ToArray();
        CollectionAssert.AreEquivalent(expected, sp.GetAllServices<IRepository<string>>().Cast<object>().ToArray());
        CollectionAssert.AreEquivalent(expected, sp.GetAllServices(typeof(IRepository<string>)).ToArray());
        Assert.HasCount(2, sp.GetAllServices<IRepository<int>>().ToArray());
    }

    private static ServiceProvider Build(IServiceCollection services, bool diagnostics)
    {
        var factory = new ServiceProviderFactory(new ExtendedServiceProviderOptions
        {
            DetectIncorrectUsageOfTransientDisposables = diagnostics
        });
        var builder = factory.CreateBuilder(services);
        return (ServiceProvider)((IServiceProviderFactory<IServiceCollection>)factory).CreateServiceProvider(builder);
    }

    private static void Tamper(ServiceProvider provider)
    {
        provider.GetRequiredService<ServiceTypes>().Clear();
        provider.GetRequiredService<ServiceTypes>().Add(typeof(string));
        provider.GetRequiredService<ServiceKeys>().Clear();
        provider.GetRequiredService<ServiceKeys>().Add("invented");
        provider.GetRequiredService<ServiceKeys<IRepository<string>>>().Clear();
        provider.GetRequiredService<ServiceKeys<IRepository<string>>>().Add("invented");
    }

    public interface IRepository<T> { }
    public class Repository<T> : IRepository<T> { }
    public sealed class ClosedRepository : Repository<string> { }
    public sealed class DisposableRepository : Repository<string>, IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
    public sealed class RepositoryDecorator(IRepository<string> inner) : IRepository<string>, IDisposable
    {
        public IRepository<string> Inner { get; } = inner;
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
}
