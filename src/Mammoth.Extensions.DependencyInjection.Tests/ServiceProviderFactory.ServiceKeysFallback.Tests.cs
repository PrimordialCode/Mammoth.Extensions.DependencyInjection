using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class ServiceKeysFallbackTests
{
    [TestMethod]
    [DataRow(ServiceLifetime.Transient, false)]
    [DataRow(ServiceLifetime.Singleton, false)]
    [DataRow(ServiceLifetime.Transient, true)]
    [DataRow(ServiceLifetime.Singleton, true)]
    public void FallbackMetadataDoesNotActivateApplicationObjects(ServiceLifetime lifetime, bool diagnostics)
    {
        IServiceCollection services = new ServiceCollection();
        var calls = 0;
        services.Add(new ServiceDescriptor(typeof(object), _ => { calls++; return new object(); }, lifetime));
        services.AddKeyedSingleton(typeof(IRepository<>), "open", typeof(Repository<>));
        using var provider = Build(services, diagnostics);

        var emptyKeys = provider.GetRequiredService<ServiceKeys<Marker>>();
        var openKeys = provider.GetRequiredService<ServiceKeys<IRepository<int>>>();

        Assert.AreEqual(0, calls);
        Assert.HasCount(0, emptyKeys);
        Assert.HasCount(0, openKeys);
        var repository = provider.GetRequiredKeyedService<IRepository<int>>("open");
        CollectionAssert.AreEqual(new[] { repository }, provider.GetAllServices<IRepository<int>>().ToArray());
        CollectionAssert.AreEqual(new object[] { repository }, provider.GetAllServices(typeof(IRepository<int>)).ToArray());
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ScopedObjectDoesNotPreventFallbackResolutionFromAValidScope(bool diagnostics)
    {
        var services = new ServiceCollection();
        var calls = 0;
        services.AddScoped<object>(_ => { calls++; return new object(); });
        using var provider = Build(services, diagnostics);
        using var scope = provider.CreateScope();

        var keys = scope.ServiceProvider.GetRequiredService<ServiceKeys<Marker>>();

        Assert.HasCount(0, keys);
        Assert.AreSame(keys, provider.GetRequiredService<ServiceKeys<Marker>>());
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MetadataConsumerPassesBuildAndScopeValidation(bool diagnostics)
    {
        var services = new ServiceCollection();
        var calls = 0;
        services.AddScoped<object>(_ => { calls++; return new object(); });
        services.AddTransient<MetadataConsumer>();
        using var provider = Build(services, diagnostics, validateOnBuild: true);
        using var scope = provider.CreateScope();

        var consumer = scope.ServiceProvider.GetRequiredService<MetadataConsumer>();

        Assert.HasCount(0, consumer.Keys);
        Assert.AreSame(consumer.Keys, provider.GetRequiredService<ServiceKeys<Marker>>());
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ClosedAndFallbackMetadataStayMutableWithoutChangingSnapshotDiscovery(bool diagnostics)
    {
        var services = new ServiceCollection();
        var calls = 0;
        services.AddScoped<object>(_ => { calls++; return new object(); });
        services.AddKeyedSingleton(typeof(IRepository<>), "open", typeof(Repository<>));
        services.AddKeyedSingleton<IRepository<string>, Repository<string>>("closed");
        using var provider = Build(services, diagnostics, validateOnBuild: true);

        var closedKeys = provider.GetRequiredService<ServiceKeys<IRepository<string>>>();
        CollectionAssert.AreEqual(new object[] { "closed" }, closedKeys.ToArray());
        var allMetadata = provider.GetServices<ServiceKeys<IRepository<string>>>().ToArray();
        Assert.HasCount(2, allMetadata);
        Assert.AreSame(closedKeys, allMetadata[0]);
        Assert.HasCount(0, allMetadata[1]);
        var openKeys = provider.GetRequiredService<ServiceKeys<IRepository<int>>>();
        Assert.HasCount(0, openKeys);

        closedKeys.Clear();
        closedKeys.Add("invented-closed");
        allMetadata[1].Add("invented-fallback");
        openKeys.Add("invented-open");
        CollectionAssert.AreEqual(new object[] { "invented-closed" }, closedKeys.ToArray());
        CollectionAssert.AreEqual(new object[] { "invented-fallback" }, allMetadata[1].ToArray());
        CollectionAssert.AreEqual(new object[] { "invented-open" }, openKeys.ToArray());

        var expected = new[]
        {
            provider.GetRequiredKeyedService<IRepository<string>>("open"),
            provider.GetRequiredKeyedService<IRepository<string>>("closed")
        };
        CollectionAssert.AreEquivalent(expected, provider.GetAllServices<IRepository<string>>().ToArray());
        CollectionAssert.AreEquivalent(expected.Cast<object>().ToArray(), provider.GetAllServices(typeof(IRepository<string>)).ToArray());
        var openRepository = provider.GetRequiredKeyedService<IRepository<int>>("open");
        CollectionAssert.AreEqual(new[] { openRepository }, provider.GetAllServices<IRepository<int>>().ToArray());
        CollectionAssert.AreEqual(new object[] { openRepository }, provider.GetAllServices(typeof(IRepository<int>)).ToArray());
        Assert.IsTrue(provider.IsKeyedSingletonServiceRegistered<IRepository<int>>("open"));
        Assert.IsTrue(provider.IsKeyedSingletonServiceRegistered<IRepository<string>>("closed"));
        Assert.IsFalse(provider.IsKeyedServiceRegistered("invented-open"));
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FallbackIsASingletonPerClosedTypeAndProvider(bool diagnostics)
    {
        var services = new ServiceCollection();
        using var first = Build(services, diagnostics);
        using var second = Build(services, diagnostics);
        using var scope = first.CreateScope();
        var keys = first.GetRequiredService<ServiceKeys<Marker>>();

        Assert.HasCount(0, keys);
        Assert.AreSame(keys, scope.ServiceProvider.GetRequiredService<ServiceKeys<Marker>>());
        keys.Add("local");
        Assert.AreNotSame(keys, second.GetRequiredService<ServiceKeys<Marker>>());
        Assert.HasCount(0, second.GetRequiredService<ServiceKeys<Marker>>());
        Assert.HasCount(0, first.GetRequiredService<ServiceKeys<IRepository<int>>>());
        Assert.IsFalse(first.IsKeyedServiceRegistered("local"));
    }

    [TestMethod]
    public void PublicCollectionConstructorPreservesMutableCompatibility()
    {
        var source = new object[] { "first", "first", "second" };
        var keys = new ServiceKeys<Marker>(source);

        CollectionAssert.AreEquivalent(new object[] { "first", "second" }, keys.ToArray());
        source[0] = "changed";
        Assert.IsTrue(keys.Remove("first"));
        Assert.IsTrue(keys.Add("third"));
        CollectionAssert.AreEquivalent(new object[] { "second", "third" }, keys.ToArray());
    }

    private static ServiceProvider Build(IServiceCollection services, bool diagnostics, bool validateOnBuild = false)
    {
        return ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            DetectIncorrectUsageOfTransientDisposables = diagnostics,
            ValidateScopes = true,
            ValidateOnBuild = validateOnBuild
        });
    }

    public sealed class Marker { }
    public interface IRepository<T> { }
    public sealed class Repository<T> : IRepository<T> { }
    public sealed class MetadataConsumer(ServiceKeys<Marker> keys)
    {
        public ServiceKeys<Marker> Keys { get; } = keys;
    }
}
