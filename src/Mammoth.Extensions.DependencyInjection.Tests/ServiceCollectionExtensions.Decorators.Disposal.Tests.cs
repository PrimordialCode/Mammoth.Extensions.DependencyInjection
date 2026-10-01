using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class DecoratorDisposalTests
{
    [TestMethod]
    [DataRow(ServiceLifetime.Transient, false, false)]
    [DataRow(ServiceLifetime.Scoped, false, false)]
    [DataRow(ServiceLifetime.Singleton, false, false)]
    [DataRow(ServiceLifetime.Transient, true, false)]
    [DataRow(ServiceLifetime.Scoped, true, false)]
    [DataRow(ServiceLifetime.Singleton, true, false)]
    [DataRow(ServiceLifetime.Transient, false, true)]
    [DataRow(ServiceLifetime.Scoped, false, true)]
    [DataRow(ServiceLifetime.Singleton, false, true)]
    [DataRow(ServiceLifetime.Transient, true, true)]
    [DataRow(ServiceLifetime.Scoped, true, true)]
    [DataRow(ServiceLifetime.Singleton, true, true)]
    public void ContainerOwnsInnerAndIntermediateDecorators(ServiceLifetime lifetime, bool keyed, bool typeRegistration)
    {
        IServiceCollection services = new ServiceCollection();
        object? key = keyed ? "work" : null;
        if (typeRegistration)
            services.Add(new ServiceDescriptor(typeof(Work), key, typeof(Work), lifetime));
        else if (keyed)
            services.Add(new ServiceDescriptor(typeof(Work), key, (_, actualKey) =>
            {
                Assert.AreEqual(key, actualKey);
                return new Work();
            }, lifetime));
        else
            services.Add(new ServiceDescriptor(typeof(Work), _ => new Work(), lifetime));
        services.Decorate<Work, WorkDecorator>();
        services.Decorate<Work, WorkDecorator>();
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        var outer = (WorkDecorator)scope.ServiceProvider.GetRequiredKeyedService<Work>(key);
        var middle = (WorkDecorator)outer.Inner;
        var inner = middle.Inner;
        var second = scope.ServiceProvider.GetRequiredKeyedService<Work>(key);
        if (lifetime == ServiceLifetime.Transient)
            Assert.AreNotSame(outer, second);
        else
            Assert.AreSame(outer, second);
        scope.Dispose();
        Assert.AreEqual(lifetime == ServiceLifetime.Singleton ? 0 : 1, inner.DisposeCount);
        provider.Dispose();
        Assert.AreEqual(1, inner.DisposeCount);
        Assert.AreEqual(1, middle.DisposeCount);
        Assert.AreEqual(1, outer.DisposeCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CallerOwnedInstanceIsNotDisposed(bool keyed)
    {
        IServiceCollection services = new ServiceCollection();
        var inner = new Work();
        object? key = keyed ? "work" : null;
        services.Add(new ServiceDescriptor(typeof(Work), key, inner));
        services.Decorate<Work, WorkDecorator>();
        services.Decorate<Work, WorkDecorator>();
        var provider = services.BuildServiceProvider();
        var outer = (WorkDecorator)provider.GetRequiredKeyedService<Work>(key);
        provider.Dispose();
        Assert.AreSame(inner, ((WorkDecorator)outer.Inner).Inner);
        Assert.AreEqual(0, inner.DisposeCount);
        Assert.AreEqual(1, ((WorkDecorator)outer.Inner).DisposeCount);
        Assert.AreEqual(1, outer.DisposeCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AsyncInnerRemainsOwned(bool keyed)
    {
        IServiceCollection services = new ServiceCollection();
        var inner = new AsyncWork();
        object? key = keyed ? "work" : null;
        if (keyed)
            services.AddKeyedScoped<IAsyncWork>(key, (_, _) => inner);
        else
            services.AddScoped<IAsyncWork>(_ => inner);
        services.Decorate<IAsyncWork, AsyncDecorator>();
        services.Decorate<IAsyncWork, AsyncDecorator>();
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        var outer = (AsyncDecorator)scope.ServiceProvider.GetRequiredKeyedService<IAsyncWork>(key);
        await scope.DisposeAsync();
        await provider.DisposeAsync();
        Assert.AreEqual(1, inner.DisposeCount);
        Assert.AreEqual(1, ((AsyncDecorator)outer.Inner).DisposeCount);
        Assert.AreEqual(1, outer.DisposeCount);
    }

    [TestMethod]
    public void ThrowingDecoratorDoesNotLoseInnerOwnership()
    {
        IServiceCollection services = new ServiceCollection();
        var inner = new Work();
        services.AddScoped<Work>(_ => inner);
        services.Decorate<Work, ThrowingDecorator>();
        using var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        Assert.ThrowsExactly<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<Work>());
        scope.Dispose();
        Assert.AreEqual(1, inner.DisposeCount);
    }

    [TestMethod]
    public void ImplementationTypeRegistrationDoesNotCollideWithUnrelatedRegistration()
    {
        IServiceCollection services = new ServiceCollection();
        var unrelated = new Work();
        services.AddSingleton(unrelated);
        services.AddScoped<IWork, Work>();
        services.Decorate<IWork, InterfaceDecorator>();
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        using var scope = provider.CreateScope();
        var decorated = (InterfaceDecorator)scope.ServiceProvider.GetRequiredService<IWork>();
        Assert.AreSame(unrelated, scope.ServiceProvider.GetRequiredService<Work>());
        Assert.AreNotSame(unrelated, decorated.Inner);
        Assert.HasCount(1, scope.ServiceProvider.GetAllServices<IWork>().ToArray());
    }

    [TestMethod]
    public void NativeFactoryControlDisposesInner()
    {
        var inner = new Work();
        var services = new ServiceCollection();
        services.AddScoped<Work>(_ => inner);
        using var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        Assert.AreSame(inner, scope.ServiceProvider.GetRequiredService<Work>());
        scope.Dispose();
        Assert.AreEqual(1, inner.DisposeCount);
    }

    public interface IWork { }
    public class Work : IWork, IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
    public sealed class WorkDecorator(Work inner) : Work
    {
        public Work Inner { get; } = inner;
    }
    public sealed class ThrowingDecorator : Work
    {
        public ThrowingDecorator(Work inner) => throw new InvalidOperationException("Decorator failed");
    }
    public sealed class InterfaceDecorator(IWork inner) : IWork
    {
        public IWork Inner { get; } = inner;
    }
    public interface IAsyncWork { }
    public sealed class AsyncWork : IAsyncWork, IAsyncDisposable
    {
        public int DisposeCount { get; private set; }
        public ValueTask DisposeAsync() { DisposeCount++; return default; }
    }
    public sealed class AsyncDecorator(IAsyncWork inner) : IAsyncWork, IAsyncDisposable
    {
        public IAsyncWork Inner { get; } = inner;
        public int DisposeCount { get; private set; }
        public ValueTask DisposeAsync() { DisposeCount++; return default; }
    }
}
