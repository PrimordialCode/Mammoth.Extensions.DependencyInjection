using Microsoft.Extensions.DependencyInjection;
using Work = Mammoth.Extensions.DependencyInjection.Tests.DecoratorDisposalTests.Work;
using WorkDecorator = Mammoth.Extensions.DependencyInjection.Tests.DecoratorDisposalTests.WorkDecorator;
using DisposableDependency = Mammoth.Extensions.DependencyInjection.Tests.MetadataMutationRegressionTests.DisposableDependency;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class DecoratorMetadataSnapshotTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void MetadataMutationCannotExposePrivateLayersOrChangeDisposal(bool keyed, bool diagnostics)
    {
        IServiceCollection services = new ServiceCollection();
        object? key = keyed ? "work" : null;
        services.Add(new ServiceDescriptor(typeof(Work), key, typeof(Work), ServiceLifetime.Scoped));
        services.Decorate<Work, WorkDecorator>();
        services.Decorate<Work, WorkDecorator>();
        var privateLayers = services.Where(d => d.ServiceType != typeof(Work)).ToArray();
        Assert.HasCount(2, privateLayers);
        using var provider = ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            ValidateOnBuild = diagnostics,
            DetectIncorrectUsageOfTransientDisposables = diagnostics
        });
        var types = provider.GetRequiredService<ServiceTypes>();
        var keys = provider.GetRequiredService<ServiceKeys>();
        var typedKeys = provider.GetRequiredService<ServiceKeys<Work>>();
        var lifetimes = provider.GetRequiredService<ServiceLifetimes>();
        types.Clear();
        keys.Clear();
        typedKeys.Clear();
        lifetimes.Add(typeof(Work), ServiceLifetime.Singleton, key);
        foreach (var layer in privateLayers)
        {
            types.Add(layer.ServiceType);
            keys.Add(layer.ServiceKey!);
            typedKeys.Add(layer.ServiceKey!);
            lifetimes.Add(layer.ServiceType, ServiceLifetime.Singleton, layer.ServiceKey);
            Assert.IsFalse(provider.IsServiceRegistered(layer.ServiceType));
            Assert.IsFalse(provider.IsKeyedServiceRegistered(layer.ServiceKey!));
            Assert.IsTrue(provider.IsKeyedScopedServiceRegistered(layer.ServiceType, layer.ServiceKey!));
            Assert.IsFalse(provider.IsKeyedSingletonServiceRegistered(layer.ServiceType, layer.ServiceKey!));
        }
        Assert.IsTrue(provider.IsServiceRegistered<Work>());
        if (keyed)
            Assert.IsTrue(provider.IsKeyedServiceRegistered(key!));
        using var scope = provider.CreateScope();
        var outer = (WorkDecorator)scope.ServiceProvider.GetRequiredKeyedService<Work>(key);
        var middle = (WorkDecorator)outer.Inner;
        var inner = middle.Inner;
        var generic = scope.ServiceProvider.GetAllServices<Work>().ToArray();
        var nonGeneric = scope.ServiceProvider.GetAllServices(typeof(Work)).ToArray();
        Assert.HasCount(1, generic);
        Assert.HasCount(1, nonGeneric);
        Assert.AreSame(outer, generic[0]);
        Assert.AreSame(outer, nonGeneric[0]);
        scope.Dispose();
        Assert.AreEqual(1, inner.DisposeCount);
        Assert.AreEqual(1, middle.DisposeCount);
        Assert.AreEqual(1, outer.DisposeCount);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void MetadataMutationCannotChangeDecoratedDiagnosticExemption(bool keyed, bool singleton)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<DisposableDependency>();
        object? key = keyed ? "consumer" : null;
        var lifetime = singleton ? ServiceLifetime.Singleton : ServiceLifetime.Scoped;
        services.Add(new ServiceDescriptor(typeof(Consumer), key, typeof(Consumer), lifetime));
        services.Decorate<Consumer, ConsumerDecorator>();
        var privateLayer = services.Single(d => d.ServiceType != typeof(Consumer) && d.ServiceType != typeof(DisposableDependency));
        using var provider = ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            ValidateOnBuild = true,
            DetectIncorrectUsageOfTransientDisposables = true,
            AllowSingletonToResolveTransientDisposables = true
        });
        var metadata = provider.GetRequiredService<ServiceLifetimes>();
        var falseLifetime = singleton ? ServiceLifetime.Scoped : ServiceLifetime.Singleton;
        metadata.Add(typeof(Consumer), falseLifetime, key);
        metadata.Add(privateLayer.ServiceType, falseLifetime, privateLayer.ServiceKey);
        if (singleton)
            Assert.IsInstanceOfType<ConsumerDecorator>(provider.GetRequiredKeyedService<Consumer>(key));
        else
            Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetRequiredKeyedService<Consumer>(key));
    }

    public class Consumer(DisposableDependency dependency)
    {
        public DisposableDependency Dependency { get; } = dependency;
    }

    public sealed class ConsumerDecorator(Consumer inner) : Consumer(inner.Dependency);
}
