using Mammoth.DependencyInjection.Regression;
using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class NativeHotCacheEnumerationRegressionTests
{
    [TestMethod]
    public void NativeOnlyEnumerationRetainsBothUnkeyedImplementationsAfterCompilation()
    {
        IServiceCollection services = new ServiceCollection();
        var ledger = new Ledger();
        services.AddSingleton(ledger);
        services.AddScoped(typeof(IRepository<>), typeof(DefaultRepository<>));
        services.AddSingleton<IRepository<string>, ClosedRepository>();
        services.AddKeyedTransient(typeof(IRepository<>), "key", typeof(KeyRepository<>));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var before = sp.GetServices<IRepository<string>>().ToArray();
        AssertMarkers(before, 0, 1);
        AssertMarkers(sp.GetKeyedServices<IRepository<string>>("key").ToArray(), 2);
        var compilation = NativeResolverCompilation.Observe<IRepository<string>>(provider, "key");
        sp.GetKeyedServices<IRepository<string>>("key").ToArray();
        compilation.WaitForReplacement();
        for (int i = 0; i < 100; i++)
        {
            var current = sp.GetServices<IRepository<string>>().ToArray();
            AssertMarkers(current, 0, 1);
            AssertMarkers(sp.GetKeyedServices<IRepository<string>>("key").ToArray(), 2);
            Assert.AreSame(before[0], current[0]);
            Assert.AreSame(before[1], current[1]);
        }
    }

    [TestMethod]
    [DataRow(ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient, ServiceLifetime.Singleton, false)]
    [DataRow(ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient, ServiceLifetime.Singleton, true)]
    [DataRow(ServiceLifetime.Transient, ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient, false)]
    [DataRow(ServiceLifetime.Transient, ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient, true)]
    [DataRow(ServiceLifetime.Scoped, ServiceLifetime.Transient, ServiceLifetime.Singleton, ServiceLifetime.Scoped, false)]
    [DataRow(ServiceLifetime.Scoped, ServiceLifetime.Transient, ServiceLifetime.Singleton, ServiceLifetime.Scoped, true)]
    public void ReusedProviderKeepsOriginalMixedRegistrationSequenceAndLifetimes(
        ServiceLifetime openUnkeyed, ServiceLifetime openKeyed, ServiceLifetime closedUnkeyed,
        ServiceLifetime closedKeyed, bool diagnostics)
    {
        IServiceCollection services = new ServiceCollection();
        var ledger = new Ledger();
        services.AddSingleton(ledger);
        services.Add(new ServiceDescriptor(typeof(IRepository<>), typeof(DefaultRepository<>), openUnkeyed));
        services.Add(new ServiceDescriptor(typeof(IRepository<>), "key", typeof(KeyRepository<>), openKeyed));
        services.Add(new ServiceDescriptor(typeof(IRepository<>), "fallback", typeof(FallbackRepository<>), openKeyed));
        services.Add(new ServiceDescriptor(typeof(IRepository<string>), typeof(ClosedRepository), closedUnkeyed));
        services.Add(new ServiceDescriptor(typeof(IRepository<string>), "key", typeof(ClosedKeyRepository), closedKeyed));
        using var provider = ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            ValidateOnBuild = diagnostics,
            DetectIncorrectUsageOfTransientDisposables = diagnostics
        });
        foreach (var type in new[] { typeof(IRepository<>), typeof(IRepository<int>), typeof(IRepository<string>) })
        {
            provider.GetRequiredService<ServiceLifetimes>().Add(type, ServiceLifetime.Transient);
            foreach (var key in new[] { "key", "fallback", "missing" })
                provider.GetRequiredService<ServiceLifetimes>().Add(type, ServiceLifetime.Singleton, key);
        }
        provider.GetRequiredService<ServiceTypes>().Clear();
        provider.GetRequiredService<ServiceKeys>().Clear();
        provider.GetRequiredService<ServiceKeys<IRepository<string>>>().Clear();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        Assert.IsFalse(sp.IsKeyedSingletonServiceRegistered<IRepository<string>>("missing"));
        var direct = sp.GetRequiredKeyedService<IRepository<string>>("key");
        AssertReuse(direct, sp.GetRequiredKeyedService<IRepository<string>>("key"), closedKeyed, sameScope: true);
        var cold = NativeAll(sp);
        AssertMarkers(cold, 0, 1, 2, 3, 4);
        var compilation = NativeResolverCompilation.Observe<IRepository<string>>(provider, "key");
        var unkeyedCompilation = NativeResolverCompilation.Observe<IRepository<string>>(provider, null);
        // Singleton-only fallback enumerables use the root cache immediately.
        var fallbackCompilation = openKeyed == ServiceLifetime.Singleton ? null :
            NativeResolverCompilation.Observe<IRepository<string>>(provider, "fallback");
        sp.GetKeyedServices<IRepository<string>>("key").ToArray();
        compilation.WaitForReplacement();
        AssertMarkers(sp.GetServices<IRepository<string>>().ToArray(), 0, 1);
        unkeyedCompilation.WaitForReplacement();
        if (fallbackCompilation != null)
        {
            sp.GetKeyedServices<IRepository<string>>("fallback").ToArray();
            fallbackCompilation.WaitForReplacement();
        }
        ServiceLifetime[] lifetimes = [openUnkeyed, closedUnkeyed, openKeyed, closedKeyed, openKeyed];
        for (int iteration = 0; iteration < 100; iteration++)
        {
            // Retain the original native -> generic helper -> Type helper order
            // on the same provider after actual native resolver replacement.
            var native = NativeAll(sp);
            var generic = sp.GetAllServices<IRepository<string>>().ToArray();
            var typed = sp.GetAllServices(typeof(IRepository<string>)).Cast<IRepository<string>>().ToArray();
            AssertMarkers(native, 0, 1, 2, 3, 4);
            AssertAllMarkers(generic);
            AssertAllMarkers(typed);
            for (int i = 0; i < native.Length; i++)
            {
                AssertReuse(cold[i], native[i], lifetimes[i], sameScope: true);
                AssertReuse(native[i], generic.Single(x => x.Marker == i), lifetimes[i], sameScope: true);
                AssertReuse(native[i], typed.Single(x => x.Marker == i), lifetimes[i], sameScope: true);
            }
            // Compare consecutive enumerations too: a transient must stay fresh.
            cold = native;
            Assert.AreEqual(openUnkeyed == ServiceLifetime.Singleton, sp.IsSingletonServiceRegistered<IRepository<int>>());
            Assert.AreEqual(closedKeyed == ServiceLifetime.Singleton, sp.IsKeyedSingletonServiceRegistered<IRepository<string>>("key"));
        }
        using var otherScope = provider.CreateScope();
        var other = NativeAll(otherScope.ServiceProvider);
        AssertMarkers(other, 0, 1, 2, 3, 4);
        for (int i = 0; i < other.Length; i++) AssertReuse(cold[i], other[i], lifetimes[i], sameScope: false);
        scope.Dispose();
        otherScope.Dispose();
        foreach (var instance in ledger.Instances)
            Assert.AreEqual(lifetimes[instance.Marker] == ServiceLifetime.Singleton ? 0 : 1, instance.DisposeCount);
        provider.Dispose();
        foreach (var instance in ledger.Instances) Assert.AreEqual(1, instance.DisposeCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void HotEnumerationPreservesDependsOnDecoratorsAndCallerOwnership(bool diagnostics)
    {
        IServiceCollection services = new ServiceCollection();
        var ledger = new Ledger();
        services.AddSingleton(ledger);
        services.AddScoped(typeof(IRepository<>), typeof(DefaultRepository<>));
        services.AddKeyedTransient(typeof(IRepository<>), "key", typeof(KeyRepository<>));
        services.AddSingleton<IRepository<string>, ClosedRepository>();
        services.AddKeyedScoped<IConsumer, Consumer>("owned", [Dependency.OnValue("label", "configured"),
            Parameter.ForKey("repository").Eq("key")]);
        services.Decorate<IConsumer, ConsumerDecorator>();
        services.Decorate<IConsumer, ConsumerDecorator>();
        var caller = new Consumer("caller", new DefaultRepository<string>(ledger));
        services.AddKeyedSingleton<IConsumer>("caller", caller);
        services.Decorate<IConsumer, ConsumerDecorator>();
        using var provider = ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            ValidateOnBuild = diagnostics,
            DetectIncorrectUsageOfTransientDisposables = diagnostics
        });
        provider.GetRequiredService<ServiceTypes>().Clear();
        provider.GetRequiredService<ServiceKeys>().Clear();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        AssertMarkers(sp.GetServices<IRepository<string>>().ToArray(), 0, 1);
        sp.GetKeyedServices<IRepository<string>>("key").ToArray();
        var compilation = NativeResolverCompilation.Observe<IRepository<string>>(provider, "key");
        sp.GetKeyedServices<IRepository<string>>("key").ToArray();
        compilation.WaitForReplacement();
        var outer = (ConsumerDecorator)sp.GetRequiredKeyedService<IConsumer>("owned");
        var middle = (ConsumerDecorator)outer.Inner;
        var owned = (Consumer)middle.Inner;
        Assert.AreEqual("configured", owned.Label);
        Assert.AreEqual(2, owned.Repository.Marker);
        Assert.AreEqual("short", owned.Selected);
        for (int i = 0; i < 100; i++)
        {
            Assert.AreSame(outer, sp.GetRequiredKeyedService<IConsumer>("owned"));
            AssertMarkers(sp.GetServices<IRepository<string>>().ToArray(), 0, 1);
            AssertAllMarkers(sp.GetAllServices<IRepository<string>>().ToArray(), 0, 1, 2);
            AssertAllMarkers(sp.GetAllServices(typeof(IRepository<string>)).Cast<IRepository<string>>().ToArray(), 0, 1, 2);
        }
        var callerOuter = (ConsumerDecorator)sp.GetRequiredKeyedService<IConsumer>("caller");
        Assert.AreSame(caller, callerOuter.Inner);
        scope.Dispose();
        Assert.AreEqual(1, owned.DisposeCount);
        Assert.AreEqual(1, middle.DisposeCount);
        Assert.AreEqual(1, outer.DisposeCount);
        Assert.AreEqual(1, owned.Repository.DisposeCount);
        provider.Dispose();
        Assert.AreEqual(0, caller.DisposeCount);
        Assert.AreEqual(0, caller.Repository.DisposeCount);
        Assert.AreEqual(1, callerOuter.DisposeCount);
    }

    private static IRepository<string>[] NativeAll(IServiceProvider provider) =>
        provider.GetServices<IRepository<string>>().Concat(provider.GetKeyedServices<IRepository<string>>("key"))
            .Concat(provider.GetKeyedServices<IRepository<string>>("fallback")).ToArray();
    private static void AssertMarkers(IRepository<string>[] instances, params int[] markers) =>
        CollectionAssert.AreEqual(markers, instances.Select(x => x.Marker).ToArray());
    private static void AssertAllMarkers(IRepository<string>[] instances, params int[] markers) =>
        CollectionAssert.AreEqual(markers.Length == 0 ? new[] { 0, 1, 2, 3, 4 } : markers,
            instances.Select(x => x.Marker).OrderBy(x => x).ToArray());
    private static void AssertReuse(IRepository<string> before, IRepository<string> after, ServiceLifetime lifetime, bool sameScope)
    {
        if (lifetime == ServiceLifetime.Singleton || (lifetime == ServiceLifetime.Scoped && sameScope)) Assert.AreSame(before, after);
        else Assert.AreNotSame(before, after);
    }
    public sealed class Ledger { public ConcurrentBag<TrackedRepository<string>> Instances { get; } = []; }
    public interface IRepository<T> : IDisposable { int Marker { get; } int DisposeCount { get; } }
    public abstract class TrackedRepository<T> : IRepository<T>
    {
        public abstract int Marker { get; }
        public int DisposeCount { get; private set; }
        protected TrackedRepository(Ledger ledger)
        {
            if (this is TrackedRepository<string> instance) ledger.Instances.Add(instance);
        }
        public void Dispose() => DisposeCount++;
    }
    public sealed class DefaultRepository<T>(Ledger ledger) : TrackedRepository<T>(ledger) { public override int Marker => 0; }
    public sealed class ClosedRepository(Ledger ledger) : TrackedRepository<string>(ledger) { public override int Marker => 1; }
    public sealed class KeyRepository<T>(Ledger ledger) : TrackedRepository<T>(ledger) { public override int Marker => 2; }
    public sealed class ClosedKeyRepository(Ledger ledger) : TrackedRepository<string>(ledger) { public override int Marker => 3; }
    public sealed class FallbackRepository<T>(Ledger ledger) : TrackedRepository<T>(ledger) { public override int Marker => 4; }
    public interface IConsumer : IDisposable { }
    public sealed class Missing { }
    public sealed class Consumer : IConsumer
    {
        public string Selected { get; } = "short";
        public string Label { get; }
        public IRepository<string> Repository { get; }
        public int DisposeCount { get; private set; }
        public Consumer(string label, IRepository<string> repository) { Label = label; Repository = repository; }
        public Consumer(string label, IRepository<string> repository, Missing missing) : this(label, repository) => Selected = "long";
        public void Dispose() => DisposeCount++;
    }
    public sealed class ConsumerDecorator(IConsumer inner) : IConsumer
    {
        public IConsumer Inner { get; } = inner;
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
}
