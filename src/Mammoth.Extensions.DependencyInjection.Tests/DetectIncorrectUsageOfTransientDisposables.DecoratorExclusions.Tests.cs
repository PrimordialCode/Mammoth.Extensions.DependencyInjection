using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class DecoratorDiagnosticExclusionTests
{
    public static IEnumerable<object[]> ExcludedCases()
    {
        foreach (var keyed in new[] { false, true })
        foreach (var factory in new[] { false, true })
        foreach (var depth in new[] { 0, 1, 2, 4 })
        foreach (var contract in new[] { false, true })
            yield return [keyed, factory, depth, contract];
    }

    [TestMethod]
    [DynamicData(nameof(ExcludedCases))]
    public void ExactPublicExclusionSurvivesEveryLayerAndPreservesOwnership(bool keyed, bool factory, int depth, bool contract)
    {
        IServiceCollection services = new ServiceCollection();
        var serviceType = contract ? typeof(IWork) : typeof(Work);
        object? key = keyed ? "work" : null;
        AddTransient(services, serviceType, typeof(Work), key, factory, () => new Work());
        for (var i = 0; i < depth; i++)
        {
            if (contract) services.Decorate<IWork, InterfaceDecorator>();
            else services.Decorate<Work, WorkDecorator>();
        }
        var privateLayers = services.Where(d => d.ServiceType != serviceType).ToArray();
        Assert.HasCount(depth, privateLayers);
        using var provider = Build(services, serviceType);
        var result = provider.GetRequiredKeyedService(serviceType, key);
        var layers = Layers(result).ToArray();
        Assert.HasCount(depth + 1, layers);
        Assert.IsTrue(layers.All(layer => layer.DisposeCount == 0));
        foreach (var layer in privateLayers)
        {
            Assert.IsFalse(provider.IsServiceRegistered(layer.ServiceType));
            Assert.HasCount(0, provider.GetAllServices(layer.ServiceType).ToArray());
            Assert.IsFalse(provider.GetRequiredService<ServiceTypes>().Contains(layer.ServiceType));
            Assert.IsFalse(provider.GetRequiredService<ServiceKeys>().Contains(layer.ServiceKey!));
        }
        var discovered = provider.GetAllServices(serviceType).ToArray();
        Assert.HasCount(1, discovered);
        var discoveredService = discovered[0];
        Assert.IsNotNull(discoveredService);
        Assert.AreEqual(result.GetType(), discoveredService.GetType());
        var discoveredLayers = Layers(discoveredService).ToArray();
        Assert.HasCount(depth + 1, discoveredLayers);
        Assert.AreNotSame(result, discovered[0]);
        provider.Dispose();
        Assert.IsTrue(layers.Concat(discoveredLayers).All(layer => layer.DisposeCount == 1));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task ExcludedAsyncOnlyLayersRemainContainerOwned(bool keyed, bool factory)
    {
        IServiceCollection services = new ServiceCollection();
        object? key = keyed ? "async" : null;
        AddTransient(services, typeof(IAsyncWork), typeof(AsyncWork), key, factory, () => new AsyncWork());
        for (var i = 0; i < 4; i++) services.Decorate<IAsyncWork, AsyncDecorator>();
        await using var provider = Build(services, typeof(IAsyncWork));
        var layers = new List<AsyncWork>();
        var current = provider.GetRequiredKeyedService<IAsyncWork>(key);
        while (current is AsyncDecorator decorator)
        {
            layers.Add(decorator);
            current = decorator.Inner;
        }
        layers.Add((AsyncWork)current);
        Assert.HasCount(5, layers);
        Assert.IsTrue(layers.All(layer => layer.DisposeCount == 0));
        await provider.DisposeAsync();
        Assert.IsTrue(layers.All(layer => layer.DisposeCount == 1));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExclusionDoesNotTransferCallerOwnedInstance(bool keyed)
    {
        IServiceCollection services = new ServiceCollection();
        object? key = keyed ? "instance" : null;
        using var instance = new Work();
        services.Add(new ServiceDescriptor(typeof(Work), key, instance));
        services.Decorate<Work, WorkDecorator>();
        services.Decorate<Work, WorkDecorator>();
        using var provider = Build(services, typeof(Work));
        var layers = Layers(provider.GetRequiredKeyedService<Work>(key)).ToArray();
        Assert.HasCount(3, layers);
        Assert.AreSame(instance, layers[2]);
        provider.Dispose();
        Assert.AreEqual(1, layers[0].DisposeCount);
        Assert.AreEqual(1, layers[1].DisposeCount);
        Assert.AreEqual(0, instance.DisposeCount);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void NonmatchingPublicServiceStillRejectsDisposableInner(bool keyed, bool factory)
    {
        IServiceCollection services = new ServiceCollection();
        object? key = keyed ? "work" : null;
        AddTransient(services, typeof(Work), typeof(Work), key, factory, () => new Work());
        AddTransient(services, typeof(IWork), typeof(Work), key, factory, () => new Work());
        for (var i = 0; i < 4; i++)
        {
            services.Decorate<Work, WorkDecorator>();
            // A non-disposable outer layer must not hide its unexcluded disposable inner.
            services.Decorate<IWork, NonDisposableDecorator>();
        }
        using var provider = Build(services, typeof(Work));
        Assert.IsInstanceOfType<WorkDecorator>(provider.GetRequiredKeyedService<Work>(key));
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetRequiredKeyedService<IWork>(key));
        StringAssert.Contains(error.Message, "Trying to resolve Transient Disposable service");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void ExcludedChainStillChecksUnrelatedDependencies(bool keyed, bool decoratorDependency)
    {
        IServiceCollection services = new ServiceCollection();
        object? key = keyed ? "work" : null;
        services.AddTransient<DisposableDependency>();
        services.Add(new ServiceDescriptor(typeof(Work), key,
            decoratorDependency ? typeof(Work) : typeof(WorkWithDependency), ServiceLifetime.Transient));
        if (decoratorDependency) services.Decorate<Work, DecoratorWithDependency>();
        for (var i = 0; i < 3; i++) services.Decorate<Work, WorkDecorator>();
        using var provider = Build(services, typeof(Work));
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetRequiredKeyedService<Work>(key));
        StringAssert.Contains(error.Message, $"ImplementationType: {typeof(DisposableDependency).FullName}.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MissingExclusionStillRejectsPrivateDisposableLayers(bool keyed)
    {
        IServiceCollection services = new ServiceCollection();
        object? key = keyed ? "work" : null;
        AddTransient(services, typeof(IWork), typeof(Work), key, false, () => new Work());
        services.Decorate<IWork, NonDisposableDecorator>();
        services.Decorate<IWork, NonDisposableDecorator>();
        using var provider = ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            DetectIncorrectUsageOfTransientDisposables = true
        });
        Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetRequiredKeyedService<IWork>(key));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UnwrappingStopsAtPublicClosedGenericService(bool keyed)
    {
        IServiceCollection services = new ServiceCollection();
        object? key = keyed ? "generic" : null;
        AddTransient(services, typeof(IGenericWork<Work>), typeof(GenericWork<Work>), key,
            false, () => new GenericWork<Work>());
        for (var i = 0; i < 4; i++) services.Decorate<IGenericWork<Work>, GenericDecorator<Work>>();
        using var excluded = Build(services, typeof(IGenericWork<Work>));
        Assert.IsInstanceOfType<GenericDecorator<Work>>(excluded.GetRequiredKeyedService<IGenericWork<Work>>(key));
        using var argumentOnly = Build(services, typeof(Work));
        Assert.ThrowsExactly<InvalidOperationException>(() => argumentOnly.GetRequiredKeyedService<IGenericWork<Work>>(key));
    }

    private static ServiceProvider Build(IServiceCollection services, Type excludedType) =>
        ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            DetectIncorrectUsageOfTransientDisposables = true,
            DetectIncorrectUsageOfTransientDisposablesExclusionPatterns = [$"^{Regex.Escape(excludedType.FullName!)}$"]
        });

    private static void AddTransient(IServiceCollection services, Type serviceType, Type implementationType,
        object? key, bool factory, Func<object> create)
    {
        if (!factory)
            services.Add(new ServiceDescriptor(serviceType, key, implementationType, ServiceLifetime.Transient));
        else if (key != null)
            services.Add(ServiceDescriptor.DescribeKeyed(serviceType, key, (_, actualKey) =>
            {
                Assert.AreEqual(key, actualKey);
                return create();
            }, ServiceLifetime.Transient));
        else
            services.Add(ServiceDescriptor.Describe(serviceType, _ => create(), ServiceLifetime.Transient));
    }

    private static IEnumerable<Work> Layers(object current)
    {
        while (true)
        {
            yield return (Work)current;
            if (current is WorkDecorator concrete) current = concrete.Inner;
            else if (current is InterfaceDecorator contract) current = contract.Inner;
            else yield break;
        }
    }

    public interface IWork;
    public class Work : IWork, IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
    public sealed class WorkDecorator(Work inner) : Work
    {
        public Work Inner { get; } = inner;
    }
    public sealed class InterfaceDecorator(IWork inner) : Work
    {
        public IWork Inner { get; } = inner;
    }
    public sealed class NonDisposableDecorator(IWork inner) : IWork
    {
        public IWork Inner { get; } = inner;
    }
    public interface IGenericWork<T>;
    public sealed class GenericWork<T> : Work, IGenericWork<T>;
    public sealed class GenericDecorator<T>(IGenericWork<T> inner) : IGenericWork<T>
    {
        public IGenericWork<T> Inner { get; } = inner;
    }
    public interface IAsyncWork;
    public class AsyncWork : IAsyncWork, IAsyncDisposable
    {
        public int DisposeCount { get; private set; }
        public ValueTask DisposeAsync() { DisposeCount++; return default; }
    }
    public sealed class AsyncDecorator(IAsyncWork inner) : AsyncWork
    {
        public IAsyncWork Inner { get; } = inner;
    }
    public sealed class DisposableDependency : IDisposable
    {
        public void Dispose() { }
    }
    public sealed class WorkWithDependency(DisposableDependency dependency) : Work
    {
        public DisposableDependency Dependency { get; } = dependency;
    }
    public sealed class DecoratorWithDependency(Work inner, DisposableDependency dependency) : Work
    {
        public Work Inner { get; } = inner;
        public DisposableDependency Dependency { get; } = dependency;
    }
}
