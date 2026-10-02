using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class AnyKeyDecoratorTests
{
    [TestMethod]
    [DataRow(ServiceLifetime.Transient, 1, false)]
    [DataRow(ServiceLifetime.Scoped, 1, false)]
    [DataRow(ServiceLifetime.Singleton, 1, false)]
    [DataRow(ServiceLifetime.Transient, 2, false)]
    [DataRow(ServiceLifetime.Scoped, 2, false)]
    [DataRow(ServiceLifetime.Singleton, 2, false)]
    [DataRow(ServiceLifetime.Transient, 1, true)]
    [DataRow(ServiceLifetime.Scoped, 1, true)]
    [DataRow(ServiceLifetime.Singleton, 1, true)]
    [DataRow(ServiceLifetime.Transient, 2, true)]
    [DataRow(ServiceLifetime.Scoped, 2, true)]
    [DataRow(ServiceLifetime.Singleton, 2, true)]
    public void AnyKeyPreservesRequestedKeysLifetimeAndOwnership(ServiceLifetime lifetime, int layers, bool typeRegistration)
    {
        foreach (var key in new object[] { new string(['a']), 42, new EqualKey(42) })
        {
            object equalKey = key switch
            {
                string => new string(['a']),
                int => 42,
                _ => new EqualKey(42)
            };
            Assert.AreEqual(key, equalKey);
            Assert.AreNotSame(key, equalKey);
            IServiceCollection services = new ServiceCollection();
            IServiceCollection nativeServices = new ServiceCollection();
            AddWork(services, lifetime, typeRegistration);
            AddWork(nativeServices, lifetime, typeRegistration);
            for (var i = 0; i < layers; i++)
                services.Decorate<IWork, Wrapper>();

            var owned = new HashSet<Work>();
            using var native = nativeServices.BuildServiceProvider();
            using var nativeScope = native.CreateScope();
            var nativeFirst = (Work)nativeScope.ServiceProvider.GetRequiredKeyedService<IWork>(key);
            var nativeOther = (Work)nativeScope.ServiceProvider.GetRequiredKeyedService<IWork>("other");
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            try
            {
                var first = scope.ServiceProvider.GetRequiredKeyedService<IWork>(key);
                var repeated = scope.ServiceProvider.GetRequiredKeyedService<IWork>(equalKey);
                var other = scope.ServiceProvider.GetRequiredKeyedService<IWork>("other");
                var inner = Track(first, owned, layers);
                var repeatedInner = Track(repeated, owned, layers);
                var otherInner = Track(other, owned, layers);
                if (!typeRegistration)
                {
                    Assert.AreEqual(nativeFirst.Key, inner.Key, "The inner factory must receive the requested key, as native DI does.");
                    Assert.AreEqual(nativeOther.Key, otherInner.Key);
                }
                Assert.AreNotSame(inner, otherInner, "Different requested keys must not share scoped/singleton inner caches.");
                Assert.AreNotSame(nativeFirst, nativeOther);
                if (lifetime == ServiceLifetime.Transient)
                {
                    Assert.AreNotSame(first, repeated);
                    Assert.AreNotSame(inner, repeatedInner);
                }
                else
                {
                    Assert.AreSame(first, repeated, "Value-equal requested keys must share the native lifetime cache.");
                    Assert.AreSame(inner, repeatedInner);
                }
                scope.Dispose();
                foreach (var work in owned)
                    Assert.AreEqual(lifetime == ServiceLifetime.Singleton ? 0 : 1, work.DisposeCount);

                using (var nextScope = provider.CreateScope())
                {
                    var next = nextScope.ServiceProvider.GetRequiredKeyedService<IWork>(equalKey);
                    var nextInner = Track(next, owned, layers);
                    if (lifetime == ServiceLifetime.Singleton)
                        Assert.AreSame(inner, nextInner);
                    else
                        Assert.AreNotSame(inner, nextInner, "Child scopes must retain their own inner services.");
                }
            }
            finally
            {
                scope.Dispose();
                provider.Dispose();
            }
            foreach (var work in owned)
                Assert.AreEqual(1, work.DisposeCount, "Every created inner and decorator must be container-owned exactly once.");
        }
    }

    [TestMethod]
    public void WildcardLayersDoNotCollideWithConcreteKeyLayersOrOtherWildcardRegistrations()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<IWork>("blue", (_, key) => new Work { Key = key });
        services.Decorate<IWork, Wrapper>();
        services.Decorate<IWork, Wrapper>();
        services.AddKeyedScoped<IWork>(KeyedService.AnyKey, (_, key) => new Work { Key = key });
        services.Decorate<IWork, Wrapper>();
        services.Decorate<IWork, Wrapper>();
        services.AddKeyedSingleton<IWork>(KeyedService.AnyKey, (_, key) => new Work { Key = key });
        services.Decorate<IWork, Wrapper>();
        services.Decorate<IWork, Wrapper>();
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        using var scope = provider.CreateScope();
        var blue = Track(scope.ServiceProvider.GetRequiredKeyedService<IWork>("blue"), new HashSet<Work>(), 2);
        var first = Track(scope.ServiceProvider.GetRequiredKeyedService<IWork>("first"), new HashSet<Work>(), 2);
        var second = Track(scope.ServiceProvider.GetRequiredKeyedService<IWork>("second"), new HashSet<Work>(), 2);
        Assert.AreEqual("blue", blue.Key);
        Assert.AreEqual("first", first.Key);
        Assert.AreEqual("second", second.Key);
        Assert.AreNotSame(first, second);
        Assert.HasCount(1, provider.GetRequiredService<ServiceTypes>());
        CollectionAssert.AreEquivalent(new object[] { "blue", KeyedService.AnyKey }, provider.GetRequiredService<ServiceKeys<IWork>>().ToArray());
    }

    [TestMethod]
    public void WildcardCallerOwnedInstanceRemainsSharedAndUndisposed()
    {
        var inner = new Work();
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IWork>(KeyedService.AnyKey, inner);
        services.Decorate<IWork, Wrapper>();
        services.Decorate<IWork, Wrapper>();
        var owned = new HashSet<Work>();
        using (var provider = services.BuildServiceProvider())
        {
            Assert.AreSame(inner, Track(provider.GetRequiredKeyedService<IWork>("a"), owned, 2));
            Assert.AreSame(inner, Track(provider.GetRequiredKeyedService<IWork>("b"), owned, 2));
        }
        Assert.AreEqual(0, inner.DisposeCount);
        foreach (var wrapper in owned.OfType<Wrapper>())
            Assert.AreEqual(1, wrapper.DisposeCount);
    }

    [TestMethod]
    [DataRow(ServiceLifetime.Transient)]
    [DataRow(ServiceLifetime.Scoped)]
    [DataRow(ServiceLifetime.Singleton)]
    public async Task WildcardAsyncFactoryResultsRetainPerKeyOwnership(ServiceLifetime lifetime)
    {
        IServiceCollection services = new ServiceCollection();
        services.Add(ServiceDescriptor.DescribeKeyed(typeof(IAsyncWork), KeyedService.AnyKey,
            (_, key) => new AsyncWork { Key = key }, lifetime));
        services.Decorate<IAsyncWork, AsyncWrapper>();
        services.Decorate<IAsyncWork, AsyncWrapper>();
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        var first = (AsyncWrapper)scope.ServiceProvider.GetRequiredKeyedService<IAsyncWork>("a");
        var second = (AsyncWrapper)scope.ServiceProvider.GetRequiredKeyedService<IAsyncWork>("b");
        var firstMiddle = (AsyncWrapper)first.Inner;
        var secondMiddle = (AsyncWrapper)second.Inner;
        var firstInner = (AsyncWork)firstMiddle.Inner;
        var secondInner = (AsyncWork)secondMiddle.Inner;
        try
        {
            Assert.AreEqual("a", firstInner.Key);
            Assert.AreEqual("b", secondInner.Key);
            Assert.AreNotSame(firstInner, secondInner);
        }
        finally
        {
            await scope.DisposeAsync();
            await provider.DisposeAsync();
        }
        foreach (var owned in new[] { first, second, firstMiddle, secondMiddle, firstInner, secondInner })
            Assert.AreEqual(1, owned.DisposeCount);
    }

    private static void AddWork(IServiceCollection services, ServiceLifetime lifetime, bool typeRegistration)
    {
        services.Add(typeRegistration
            ? ServiceDescriptor.DescribeKeyed(typeof(IWork), KeyedService.AnyKey, typeof(Work), lifetime)
            : ServiceDescriptor.DescribeKeyed(typeof(IWork), KeyedService.AnyKey, (_, key) => new Work { Key = key }, lifetime));
    }

    private static Work Track(IWork value, HashSet<Work> owned, int layers)
    {
        for (var i = 0; i < layers; i++)
        {
            Assert.IsInstanceOfType<Wrapper>(value);
            var wrapper = (Wrapper)value;
            owned.Add(wrapper);
            value = wrapper.Inner;
        }
        Assert.IsNotInstanceOfType<Wrapper>(value);
        var inner = (Work)value;
        owned.Add(inner);
        return inner;
    }

    public interface IWork { }
    public class Work : IWork, IDisposable
    {
        public object? Key { get; set; }
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
    public sealed class Wrapper(IWork inner) : Work
    {
        public IWork Inner { get; } = inner;
    }
    public sealed class EqualKey(int value)
    {
        public override bool Equals(object? obj) => obj is EqualKey other && value == other.Value;
        private int Value => value;
        public override int GetHashCode() => value;
    }
    public interface IAsyncWork { }
    public class AsyncWork : IAsyncWork, IAsyncDisposable
    {
        public object? Key { get; set; }
        public int DisposeCount { get; private set; }
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return default;
        }
    }
    public sealed class AsyncWrapper(IAsyncWork inner) : AsyncWork
    {
        public IAsyncWork Inner { get; } = inner;
    }
}
