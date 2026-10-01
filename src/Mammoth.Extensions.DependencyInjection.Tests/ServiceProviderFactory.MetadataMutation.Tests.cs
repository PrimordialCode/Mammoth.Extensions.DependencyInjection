using Microsoft.Extensions.DependencyInjection;
namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class MetadataMutationRegressionTests
{
    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Work>();
        services.AddKeyedScoped<Work>("blue");
        return ServiceProviderFactory.CreateServiceProvider(services);
    }

    [TestMethod]
    public void ClearingPublicTypeMetadataDoesNotChangeRegistrationChecks()
    {
        using var provider = Build();
        provider.GetRequiredService<ServiceTypes>().Clear();
        Assert.IsTrue(provider.IsServiceRegistered<Work>());
        provider.GetRequiredService<ServiceTypes>().Add(typeof(string));
        Assert.IsFalse(provider.IsServiceRegistered<string>());
    }

    [TestMethod]
    public void MutatingGlobalKeyMetadataDoesNotChangeKeyChecks()
    {
        using var provider = Build();
        provider.GetRequiredService<ServiceKeys>().Clear();
        provider.GetRequiredService<ServiceKeys>().Add("invented");
        Assert.IsTrue(provider.IsKeyedServiceRegistered("blue"));
        Assert.IsFalse(provider.IsKeyedServiceRegistered("invented"));
    }

    [TestMethod]
    public void MutatingTypedKeysDoesNotChangeEnumeration()
    {
        using var provider = Build();
        provider.GetRequiredService<ServiceKeys<Work>>().Clear();
        provider.GetRequiredService<ServiceKeys<Work>>().Add("invented");
        using var scope = provider.CreateScope();
        Assert.HasCount(2, scope.ServiceProvider.GetAllServices<Work>().ToArray());
        Assert.HasCount(2, scope.ServiceProvider.GetAllServices(typeof(Work)).ToArray());
    }

    [TestMethod]
    public void MutatingPublicLifetimesDoesNotChangeProviderChecks()
    {
        using var provider = Build();
        var metadata = provider.GetRequiredService<ServiceLifetimes>();
        metadata.Add(typeof(Work), ServiceLifetime.Transient);
        metadata.Add(typeof(string), ServiceLifetime.Singleton);
        Assert.IsTrue(provider.IsSingletonServiceRegistered<Work>());
        Assert.IsFalse(provider.IsTransientServiceRegistered<Work>());
        Assert.IsFalse(provider.IsSingletonServiceRegistered<string>());
        // Legacy direct metadata mutation remains available for compatibility.
        Assert.AreEqual(ServiceLifetime.Transient, metadata.GetLifetime(typeof(Work)));
    }

    [TestMethod]
    public void LegacyPublicTypesRemainMutableAndResolvable()
    {
        using var provider = Build();
        Assert.IsInstanceOfType<HashSet<Type>>(provider.GetRequiredService<ServiceTypes>());
        Assert.IsInstanceOfType<HashSet<object>>(provider.GetRequiredService<ServiceKeys>());
        Assert.IsInstanceOfType<HashSet<object>>(provider.GetRequiredService<ServiceKeys<Work>>());
        var standalone = new ServiceLifetimes();
        standalone.Add(typeof(Work), ServiceLifetime.Singleton);
        Assert.AreEqual(ServiceLifetime.Singleton, standalone.GetLifetime(typeof(Work)));
    }

    [TestMethod]
    public void NativeProviderEnumerationRetainsLegacyBehavior()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Work>();
        services.AddKeyedSingleton<Work>("blue");
        services.AddSingleton(new ServiceKeys<Work>(["blue"]));
        using var provider = services.BuildServiceProvider();
        Assert.HasCount(2, provider.GetAllServices<Work>().ToArray());
        Assert.HasCount(2, provider.GetAllServices(typeof(Work)).ToArray());
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void PublicLifetimeMutationCannotChangeDiagnosticExemption(bool keyed, bool singleton)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<DisposableDependency>();
        var lifetime = singleton ? ServiceLifetime.Singleton : ServiceLifetime.Scoped;
        object? key = keyed ? "consumer" : null;
        services.Add(new ServiceDescriptor(typeof(Consumer), key, typeof(Consumer), lifetime));
        using var provider = ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            DetectIncorrectUsageOfTransientDisposables = true,
            AllowSingletonToResolveTransientDisposables = true
        });
        provider.GetRequiredService<ServiceLifetimes>().Add(typeof(Consumer),
            singleton ? ServiceLifetime.Scoped : ServiceLifetime.Singleton, key);
        if (singleton)
            Assert.IsNotNull(provider.GetRequiredKeyedService<Consumer>(key));
        else
            Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetRequiredKeyedService<Consumer>(key));
    }

    public sealed class DisposableDependency : IDisposable
    {
        public void Dispose() { }
    }
    public sealed class Consumer(DisposableDependency dependency)
    {
        public DisposableDependency Dependency { get; } = dependency;
    }

    public sealed class Work { }
}
