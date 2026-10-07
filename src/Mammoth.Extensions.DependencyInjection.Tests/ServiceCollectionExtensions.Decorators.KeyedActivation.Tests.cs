using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class KeyedDecoratorActivationEdgeTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void ServiceKeyBeforeInnerRetainsKeyIdentityAndInnerBinding(bool wildcard, bool diagnostic)
    {
        var key = new EqualKey(42);
        var tracker = new Tracker();
        var registeredKey = wildcard ? KeyedService.AnyKey : new EqualKey(42);
        var services = Services(ServiceLifetime.Scoped, registeredKey, tracker);
        var expectedKey = diagnostic && !wildcard ? registeredKey : key;
        services.Decorate<IWork, KeyBeforeInnerWrapper>();
        services.Decorate<IWork, KeyBeforeInnerWrapper>();
        using var provider = Build(services, diagnostic);
        KeyBeforeInnerWrapper outer;
        KeyBeforeInnerWrapper middle;
        Work inner;
        using (var scope = provider.CreateScope())
        {
            outer = (KeyBeforeInnerWrapper)scope.ServiceProvider.GetRequiredKeyedService<IWork>(key);
            middle = (KeyBeforeInnerWrapper)outer.Inner;
            inner = (Work)middle.Inner;
            Assert.AreSame(expectedKey, outer.Key);
            Assert.AreSame(expectedKey, middle.Key);
            Assert.AreSame(expectedKey, inner.Key);
            Assert.AreSame(outer, scope.ServiceProvider.GetRequiredKeyedService<IWork>(new EqualKey(42)));
            Assert.HasCount(1, tracker.Created);
        }
        Assert.AreEqual(1, outer.DisposeCount);
        Assert.AreEqual(1, middle.DisposeCount);
        Assert.AreEqual(1, inner.DisposeCount);
        provider.Dispose();
        Assert.AreEqual(1, inner.DisposeCount);
    }

    [TestMethod]
    [DataRow(ServiceLifetime.Transient, false, false)]
    [DataRow(ServiceLifetime.Transient, false, true)]
    [DataRow(ServiceLifetime.Transient, true, false)]
    [DataRow(ServiceLifetime.Transient, true, true)]
    [DataRow(ServiceLifetime.Scoped, false, false)]
    [DataRow(ServiceLifetime.Scoped, false, true)]
    [DataRow(ServiceLifetime.Scoped, true, false)]
    [DataRow(ServiceLifetime.Scoped, true, true)]
    [DataRow(ServiceLifetime.Singleton, false, false)]
    [DataRow(ServiceLifetime.Singleton, false, true)]
    [DataRow(ServiceLifetime.Singleton, true, false)]
    [DataRow(ServiceLifetime.Singleton, true, true)]
    public void ThrowingKeyedDecoratorRetainsInnerOwnership(ServiceLifetime lifetime, bool wildcard, bool diagnostic)
    {
        var key = new EqualKey(42);
        var tracker = new Tracker();
        var registeredKey = wildcard ? KeyedService.AnyKey : new EqualKey(42);
        var services = Services(lifetime, registeredKey, tracker);
        var expectedKey = diagnostic && !wildcard ? registeredKey : key;
        services.Decorate<IWork, ThrowingWrapper>();
        var provider = Build(services, diagnostic);
        var scope = provider.CreateScope();
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var error = Assert.ThrowsExactly<ArgumentException>(() => scope.ServiceProvider.GetRequiredKeyedService<IWork>(key));
                Assert.AreSame(tracker.Failure, error);
                Assert.AreSame(expectedKey, tracker.DecoratorKey);
                Assert.IsInstanceOfType<Work>(tracker.CapturedInner);
                Assert.AreSame(expectedKey, ((Work)tracker.CapturedInner!).Key);
            }
            Assert.HasCount(lifetime == ServiceLifetime.Transient ? 2 : 1, tracker.Created);
            foreach (var inner in tracker.Created) Assert.AreEqual(0, inner.DisposeCount);
            scope.Dispose();
            foreach (var inner in tracker.Created) Assert.AreEqual(lifetime == ServiceLifetime.Singleton ? 0 : 1, inner.DisposeCount);
        }
        finally { scope.Dispose(); provider.Dispose(); }
        foreach (var inner in tracker.Created) Assert.AreEqual(1, inner.DisposeCount);
    }

    private static IServiceCollection Services(ServiceLifetime lifetime, object key, Tracker tracker)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddSingleton<object>(new object());
        services.Add(ServiceDescriptor.DescribeKeyed(typeof(IWork), key, typeof(Work), lifetime));
        return services;
    }
    private static ServiceProvider Build(IServiceCollection services, bool diagnostic) => ServiceProviderFactory.CreateServiceProvider(services,
        new ExtendedServiceProviderOptions { ValidateOnBuild = diagnostic, DetectIncorrectUsageOfTransientDisposables = diagnostic });
    public interface IWork : IDisposable { }
    public sealed class Work : IWork
    {
        public Work([ServiceKey] object key, Tracker tracker) { Key = key; tracker.Created.Add(this); }
        public object Key { get; }
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
    public sealed class KeyBeforeInnerWrapper([ServiceKey] object key, IWork inner) : IWork
    {
        public object Key { get; } = key;
        public IWork Inner { get; } = inner;
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
    public sealed class ThrowingWrapper : IWork
    {
        public ThrowingWrapper([ServiceKey] object key, IWork inner, Tracker tracker)
        { tracker.DecoratorKey = key; tracker.CapturedInner = inner; throw tracker.Failure; }
        public void Dispose() { }
    }
    public sealed class Tracker
    {
        public List<Work> Created { get; } = [];
        public object? DecoratorKey { get; set; }
        public IWork? CapturedInner { get; set; }
        public ArgumentException Failure { get; } = new("Decorator failed after capturing its inner service.");
    }
    public sealed class EqualKey(int value)
    { private int Value => value; public override bool Equals(object? obj) => obj is EqualKey other && value == other.Value; public override int GetHashCode() => value; }
}
