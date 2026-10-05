using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class TransientDisposableDuplicateLifetimeTests
{
    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, false)]
    [DataRow(false, true, true)]
    [DataRow(true, false, false)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    [DataRow(true, true, true)]
    public void RootEnumerationRejectsTransientDisposableRegardlessOfDuplicateLifetime(bool keyed, bool factory, bool singletonFirst)
    {
        var services = new ServiceCollection();
        AddPair<DisposableWork, PlainWork>(services, keyed, factory, singletonFirst,
            _ => new DisposableWork(), _ => new PlainWork());
        using var provider = Build(services);
        AssertMetadata(provider, keyed, singletonFirst);
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(provider, keyed));
        StringAssert.Contains(error.Message, "Trying to resolve Transient Disposable service");
        StringAssert.Contains(error.Message, "ServiceType: " + typeof(IWork).FullName);
        Assert.HasCount(0, ResolutionContext.CurrentStack);
        using var scope = provider.CreateScope();
        var scoped = Resolve(scope.ServiceProvider, keyed);
        Assert.HasCount(2, scoped);
        Assert.HasCount(1, scoped.OfType<DisposableWork>().ToArray());
        Assert.HasCount(1, scoped.OfType<PlainWork>().ToArray());
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
    public void SingletonConsumerKeepsExemptionRegardlessOfDuplicateLifetime(bool keyed, bool factory, bool singletonFirst)
    {
        var services = new ServiceCollection();
        services.AddTransient<Resource>();
        AddPair<PlainWork, SingletonConsumer>(services, keyed, factory, singletonFirst,
            _ => new PlainWork(), sp => new SingletonConsumer(sp.GetRequiredService<Resource>()));
        using var provider = Build(services);
        AssertMetadata(provider, keyed, singletonFirst);
        var first = Resolve(provider, keyed);
        var second = Resolve(provider, keyed);
        Assert.HasCount(2, first);
        var consumer = first.OfType<SingletonConsumer>().Single();
        Assert.AreSame(consumer, second.OfType<SingletonConsumer>().Single());
        Assert.AreNotSame(first.OfType<PlainWork>().Single(), second.OfType<PlainWork>().Single());
        Assert.AreEqual(0, consumer.Resource.DisposeCount);
        Assert.HasCount(0, ResolutionContext.CurrentStack);
        // A previous singleton resolution must not exempt a later root request.
        Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetRequiredService<Resource>());
        provider.Dispose();
        Assert.AreEqual(1, consumer.Resource.DisposeCount);
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
    public void SingletonConsumerStillRejectsDependencyWhenExemptionDisabled(bool keyed, bool factory, bool singletonFirst)
    {
        var services = new ServiceCollection();
        services.AddTransient<Resource>();
        AddPair<PlainWork, SingletonConsumer>(services, keyed, factory, singletonFirst,
            _ => new PlainWork(), sp => new SingletonConsumer(sp.GetRequiredService<Resource>()));
        using var provider = Build(services, allowSingleton: false);
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(provider, keyed));
        StringAssert.Contains(error.Message, "Trying to resolve Transient Disposable service");
        StringAssert.Contains(error.Message, "ServiceType: " + typeof(Resource).FullName);
        Assert.HasCount(0, ResolutionContext.CurrentStack);
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
    public void TransientConsumerCannotBorrowDuplicateSingletonExemption(bool keyed, bool factory, bool singletonFirst)
    {
        var services = new ServiceCollection();
        services.AddTransient<Resource>();
        AddPair<SingletonConsumer, PlainWork>(services, keyed, factory, singletonFirst,
            sp => new SingletonConsumer(sp.GetRequiredService<Resource>()), _ => new PlainWork());
        using var provider = Build(services);
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(provider, keyed));
        StringAssert.Contains(error.Message, "Trying to resolve Transient Disposable service");
        StringAssert.Contains(error.Message, "ServiceType: " + typeof(Resource).FullName);
        Assert.HasCount(0, ResolutionContext.CurrentStack);
    }

    private static void AddPair<TTransient, TSingleton>(IServiceCollection services, bool keyed, bool factory,
        bool singletonFirst, Func<IServiceProvider, TTransient> transientFactory, Func<IServiceProvider, TSingleton> singletonFactory)
        where TTransient : class, IWork where TSingleton : class, IWork
    {
        ServiceDescriptor Create<T>(ServiceLifetime lifetime, Func<IServiceProvider, T> create) where T : class, IWork =>
            keyed
                ? factory
                    ? ServiceDescriptor.DescribeKeyed(typeof(IWork), "work", (sp, _) => create(sp), lifetime)
                    : ServiceDescriptor.DescribeKeyed(typeof(IWork), "work", typeof(T), lifetime)
                : factory
                    ? ServiceDescriptor.Describe(typeof(IWork), sp => create(sp), lifetime)
                    : ServiceDescriptor.Describe(typeof(IWork), typeof(T), lifetime);
        var transient = Create(ServiceLifetime.Transient, transientFactory);
        var singleton = Create(ServiceLifetime.Singleton, singletonFactory);
        services.Add(singletonFirst ? singleton : transient);
        services.Add(singletonFirst ? transient : singleton);
    }

    private static ServiceProvider Build(IServiceCollection services, bool allowSingleton = true) =>
        ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            DetectIncorrectUsageOfTransientDisposables = true,
            AllowSingletonToResolveTransientDisposables = allowSingleton
        });

    private static IWork[] Resolve(IServiceProvider provider, bool keyed) => keyed
        ? provider.GetKeyedServices<IWork>("work").ToArray()
        : provider.GetServices<IWork>().ToArray();

    private static void AssertMetadata(IServiceProvider provider, bool keyed, bool singletonFirst)
    {
        Assert.AreEqual(!singletonFirst, keyed
            ? provider.IsKeyedSingletonServiceRegistered(typeof(IWork), "work")
            : provider.IsSingletonServiceRegistered(typeof(IWork)));
        Assert.AreEqual(singletonFirst, keyed
            ? provider.IsKeyedTransientServiceRegistered(typeof(IWork), "work")
            : provider.IsTransientServiceRegistered(typeof(IWork)));
    }

    public interface IWork;
    public sealed class PlainWork : IWork;
    public sealed class DisposableWork : IWork, IDisposable
    {
        public void Dispose() { }
    }
    public sealed class Resource : IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
    public sealed class SingletonConsumer(Resource resource) : IWork
    {
        public Resource Resource { get; } = resource;
    }
}
