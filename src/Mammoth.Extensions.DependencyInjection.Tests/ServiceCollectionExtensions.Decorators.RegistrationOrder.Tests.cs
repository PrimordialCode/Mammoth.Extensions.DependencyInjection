using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class DecoratorRegistrationOrderTests
{
    [TestMethod]
    [DataRow(ServiceLifetime.Transient, false, false, false)]
    [DataRow(ServiceLifetime.Scoped, false, false, false)]
    [DataRow(ServiceLifetime.Singleton, false, false, false)]
    [DataRow(ServiceLifetime.Transient, true, false, false)]
    [DataRow(ServiceLifetime.Scoped, true, false, false)]
    [DataRow(ServiceLifetime.Singleton, true, false, false)]
    [DataRow(ServiceLifetime.Transient, false, true, false)]
    [DataRow(ServiceLifetime.Scoped, false, true, false)]
    [DataRow(ServiceLifetime.Singleton, false, true, false)]
    [DataRow(ServiceLifetime.Transient, true, true, false)]
    [DataRow(ServiceLifetime.Scoped, true, true, false)]
    [DataRow(ServiceLifetime.Singleton, true, true, false)]
    [DataRow(ServiceLifetime.Transient, false, false, true)]
    [DataRow(ServiceLifetime.Scoped, false, false, true)]
    [DataRow(ServiceLifetime.Singleton, false, false, true)]
    [DataRow(ServiceLifetime.Transient, true, false, true)]
    [DataRow(ServiceLifetime.Scoped, true, false, true)]
    [DataRow(ServiceLifetime.Singleton, true, false, true)]
    [DataRow(ServiceLifetime.Transient, false, true, true)]
    [DataRow(ServiceLifetime.Scoped, false, true, true)]
    [DataRow(ServiceLifetime.Singleton, false, true, true)]
    [DataRow(ServiceLifetime.Transient, true, true, true)]
    [DataRow(ServiceLifetime.Scoped, true, true, true)]
    [DataRow(ServiceLifetime.Singleton, true, true, true)]
    public void LastOccurrencePreservesOrderLifetimeAndOwnership(
        ServiceLifetime lifetime, bool keyed, bool factory, bool reuseDescriptor)
    {
        IServiceCollection services = new ServiceCollection();
        object? key = keyed ? "work" : null;
        object? otherKey = keyed ? null : "other";
        var first = CreateDescriptor(lifetime, key, factory);
        var last = reuseDescriptor ? first : CreateDescriptor(lifetime, key, factory);
        var other = new Work();
        var otherDescriptor = new ServiceDescriptor(typeof(Work), otherKey, other);
        var unrelated = ServiceDescriptor.Singleton(new object());
        services.Add(first);
        services.Add(otherDescriptor);
        services.Add(last);
        services.Add(unrelated);

        services.Decorate<Work, Wrapper>();

        Assert.AreSame(first, services[0], "An earlier occurrence must remain untouched.");
        Assert.AreSame(otherDescriptor, services[1]);
        Assert.AreNotSame(last, services[2]);
        Assert.AreEqual(lifetime, services[2].Lifetime);
        Assert.AreEqual(key, services[2].ServiceKey);
        Assert.AreSame(unrelated, services[3]);
        var owned = new HashSet<Work>();
        using (var provider = services.BuildServiceProvider())
        {
            using (var scope = provider.CreateScope())
            {
                var values = scope.ServiceProvider.GetKeyedServices<Work>(key).ToArray();
                Assert.HasCount(2, values);
                Assert.AreEqual(typeof(Work), values[0].GetType());
                owned.Add(values[0]);
                var inner = TrackWrapper(values[1], owned);
                Assert.AreNotSame(values[0], inner, "Each occurrence owns its own activation.");
                var single = scope.ServiceProvider.GetRequiredKeyedService<Work>(key);
                var singleInner = TrackWrapper(single, owned);
                if (lifetime == ServiceLifetime.Transient)
                {
                    Assert.AreNotSame(values[1], single);
                    Assert.AreNotSame(inner, singleInner);
                }
                else
                {
                    Assert.AreSame(values[1], single);
                    Assert.AreSame(inner, singleInner);
                }
                Assert.AreSame(other, scope.ServiceProvider.GetRequiredKeyedService<Work>(otherKey));
                foreach (var work in owned)
                    Assert.AreEqual(0, work.DisposeCount);
            }
            foreach (var work in owned)
                Assert.AreEqual(lifetime == ServiceLifetime.Singleton ? 0 : 1, work.DisposeCount);
        }
        foreach (var work in owned)
            Assert.AreEqual(1, work.DisposeCount, "Every created service and decorator is disposed exactly once.");
        Assert.AreEqual(0, other.DisposeCount);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void RepeatedDecorationWrapsTheLastOccurrenceOnly(bool keyed, bool reuseDescriptor)
    {
        IServiceCollection services = new ServiceCollection();
        object? key = keyed ? "work" : null;
        var first = CreateDescriptor(ServiceLifetime.Scoped, key, factory: false);
        services.Add(first);
        services.Add(reuseDescriptor ? first : CreateDescriptor(ServiceLifetime.Scoped, key, factory: false));
        services.Decorate<Work, Wrapper>();
        var firstDecoration = services[1];
        services.Decorate<Work, OuterWrapper>();

        Assert.AreSame(first, services[0]);
        Assert.AreNotSame(firstDecoration, services[1]);
        var owned = new HashSet<Work>();
        using (var provider = ServiceProviderFactory.CreateServiceProvider(services))
        using (var scope = provider.CreateScope())
        {
            var values = scope.ServiceProvider.GetKeyedServices<Work>(key).ToArray();
            Assert.HasCount(2, values);
            Assert.AreEqual(typeof(Work), values[0].GetType());
            owned.Add(values[0]);
            Assert.IsInstanceOfType<OuterWrapper>(values[1]);
            var outer = (OuterWrapper)values[1];
            owned.Add(outer);
            TrackWrapper(outer.Inner, owned);
            Assert.AreSame(outer, scope.ServiceProvider.GetRequiredKeyedService<Work>(key));
            CollectionAssert.AreEqual(values, scope.ServiceProvider.GetAllServices<Work>().ToArray());
        }
        foreach (var work in owned)
            Assert.AreEqual(1, work.DisposeCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReusedCallerOwnedInstanceIsNotDisposed(bool keyed)
    {
        IServiceCollection services = new ServiceCollection();
        object? key = keyed ? "work" : null;
        var instance = new Work();
        var descriptor = new ServiceDescriptor(typeof(Work), key, instance);
        services.Add(descriptor);
        services.Add(descriptor);
        services.Decorate<Work, Wrapper>();
        services.Decorate<Work, OuterWrapper>();
        Wrapper middle;
        OuterWrapper outer;
        using (var provider = services.BuildServiceProvider())
        {
            var values = provider.GetKeyedServices<Work>(key).ToArray();
            Assert.HasCount(2, values);
            Assert.AreSame(instance, values[0]);
            Assert.IsInstanceOfType<OuterWrapper>(values[1]);
            outer = (OuterWrapper)values[1];
            Assert.IsInstanceOfType<Wrapper>(outer.Inner);
            middle = (Wrapper)outer.Inner;
            Assert.AreSame(instance, middle.Inner);
            Assert.AreSame(outer, provider.GetRequiredKeyedService<Work>(key));
        }
        Assert.AreEqual(0, instance.DisposeCount);
        Assert.AreEqual(1, middle.DisposeCount);
        Assert.AreEqual(1, outer.DisposeCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MissingExactServiceThrowsWithoutChangingRegistrations(bool addUnrelated)
    {
        IServiceCollection services = new ServiceCollection();
        if (addUnrelated)
            services.AddTransient<DerivedWork>();
        var before = services.ToArray();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => services.Decorate<Work, Wrapper>());

        Assert.AreEqual("Service type Work not registered.", exception.Message);
        CollectionAssert.AreEqual(before, services.ToArray());
    }

    [TestMethod]
    public void NullCollectionRetainsArgumentNullException()
    {
        IServiceCollection services = null!;

        Assert.ThrowsExactly<ArgumentNullException>(() => services.Decorate<Work, Wrapper>());
    }

    private static ServiceDescriptor CreateDescriptor(ServiceLifetime lifetime, object? key, bool factory)
    {
        if (!factory)
            return new ServiceDescriptor(typeof(Work), key, typeof(Work), lifetime);
        if (key == null)
            return new ServiceDescriptor(typeof(Work), _ => new Work(), lifetime);
        return new ServiceDescriptor(typeof(Work), key, (_, actualKey) =>
        {
            Assert.AreEqual(key, actualKey);
            return new Work();
        }, lifetime);
    }

    private static Work TrackWrapper(Work value, HashSet<Work> owned)
    {
        Assert.IsInstanceOfType<Wrapper>(value);
        var wrapper = (Wrapper)value;
        Assert.AreEqual(typeof(Work), wrapper.Inner.GetType());
        owned.Add(wrapper);
        owned.Add(wrapper.Inner);
        return wrapper.Inner;
    }

    public class Work : IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }

    public sealed class Wrapper(Work inner) : Work
    {
        public Work Inner { get; } = inner;
    }

    public sealed class OuterWrapper(Work inner) : Work
    {
        public Work Inner { get; } = inner;
    }

    public sealed class DerivedWork : Work;
}
