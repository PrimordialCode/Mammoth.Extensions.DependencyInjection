using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class KeyedDecoratorBindingRegressionTests
{
    public static IEnumerable<object[]> BindingCases()
    {
        foreach (var lifetime in new[] { ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient })
        foreach (var registration in new[] { "type", "factory", "instance" })
        foreach (var provider in new[] { "native", "snapshot", "diagnostics" })
        foreach (var mode in new[] { "interface", "object", "inherit", "null-key" })
        foreach (var otherFirst in new[] { false, true })
            if (registration != "instance" || lifetime == ServiceLifetime.Singleton)
                yield return [lifetime, registration, provider, mode, otherFirst];
    }

    [TestMethod]
    [DynamicData(nameof(BindingCases))]
    public void KeyedDependenciesAndInnerKeepTheirIdentitiesAcrossOrderLayersAndLifetimes(
        ServiceLifetime lifetime, string registration, string kind, string mode, bool otherFirst)
    {
        var other = new Work("other", new Tracker());
        var tracker = new Tracker();
        var services = Services(lifetime, registration, mode, other, tracker);
        Decorate(services, mode, otherFirst);
        Decorate(services, mode, otherFirst);

        // Independent native resolution supplies the expected dependency and original
        // implementation under identical registrations, without decorator argument binding.
        using var native = Services(lifetime, registration, mode, other, new Tracker()).BuildServiceProvider();
        using var nativeScope = native.CreateScope();
        var expectedInner = (Work)nativeScope.ServiceProvider.GetRequiredKeyedService<IWork>("blue");
        var nativeOther = mode switch
        {
            "null-key" => nativeScope.ServiceProvider.GetRequiredService<IWork>(),
            "interface" => nativeScope.ServiceProvider.GetRequiredKeyedService<IWork>("other"),
            _ => nativeScope.ServiceProvider.GetRequiredKeyedService<object>(mode == "inherit" ? "blue" : "other")
        };
        Assert.AreSame(other, nativeOther);
        using var provider = Build(services, kind);
        Wrapper first;
        using (var scope = provider.CreateScope())
        {
            first = (Wrapper)scope.ServiceProvider.GetRequiredKeyedService<IWork>("blue");
            CheckChain(first, expectedInner, nativeOther);
            var second = (Wrapper)scope.ServiceProvider.GetRequiredKeyedService<IWork>("blue");
            CheckChain(second, expectedInner, nativeOther);
            if (lifetime == ServiceLifetime.Transient) Assert.AreNotSame(first, second);
            else Assert.AreSame(first, second);
        }
        using (var scope = provider.CreateScope())
        {
            var secondScope = (Wrapper)scope.ServiceProvider.GetRequiredKeyedService<IWork>("blue");
            CheckChain(secondScope, expectedInner, nativeOther);
            if (lifetime == ServiceLifetime.Singleton) Assert.AreSame(first, secondScope);
            else Assert.AreNotSame(first, secondScope);
        }
        Assert.IsNotEmpty(tracker.Created);
        Assert.HasCount(lifetime == ServiceLifetime.Transient ? 9 : lifetime == ServiceLifetime.Scoped ? 6 : 3, tracker.Created);
        foreach (var owned in tracker.Created)
            Assert.AreEqual(lifetime == ServiceLifetime.Singleton ? 0 : 1, owned.DisposeCount);
        provider.Dispose();
        foreach (var owned in tracker.Created)
            Assert.AreEqual(owned.CallerOwned ? 0 : 1, owned.DisposeCount);
        Assert.AreEqual(0, other.DisposeCount, "Keyed dependencies supplied as instances remain caller-owned.");
    }

    private static void CheckChain(Wrapper outer, Work expectedInner, object expectedOther)
    {
        var middle = (Wrapper)outer.Inner;
        var inner = (Work)middle.Inner;
        Assert.AreSame(expectedOther, outer.Other);
        Assert.AreSame(expectedOther, middle.Other);
        Assert.AreNotSame(outer.Other, outer.Inner);
        Assert.AreNotSame(middle.Other, middle.Inner);
        Assert.AreEqual(expectedInner.GetType(), inner.GetType());
        Assert.AreEqual(expectedInner.Key, inner.Key);
        Assert.AreEqual("blue", outer.Key);
        Assert.AreEqual("blue", middle.Key);
    }

    [TestMethod]
    [DataRow("native")]
    [DataRow("snapshot")]
    [DataRow("diagnostics")]
    public void MissingKeyedDependencyCannotConsumeInnerOrActivateAnUnkeyedSubstitute(string kind)
    {
        var tracker = new Tracker();
        var services = Services(ServiceLifetime.Scoped, "factory", "object", new Work("other", new Tracker()), tracker);
        var unkeyedCalls = 0;
        services.Insert(services.Count - 1, ServiceDescriptor.Describe(typeof(IWork), _ => { unkeyedCalls++; return new Work("unkeyed", tracker); }, ServiceLifetime.Transient));
        services.Decorate<IWork, MissingDependency>();
        using var provider = Build(services, kind);
        using (var scope = provider.CreateScope())
            Assert.ThrowsExactly<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredKeyedService<IWork>("blue"));
        Assert.AreEqual(0, unkeyedCalls);
        Assert.HasCount(1, tracker.Created);
        Assert.AreEqual(1, tracker.Created[0].DisposeCount);
    }

    [TestMethod]
    [DataRow("native")]
    [DataRow("snapshot")]
    [DataRow("diagnostics")]
    public void ConstructorWithoutAnOrdinaryInnerParameterIsRejectedWithoutResolvingItsKeyedDependency(string kind)
    {
        var tracker = new Tracker();
        var services = Services(ServiceLifetime.Scoped, "factory", "interface", new Work("other", new Tracker()), tracker);
        var otherCalls = 0;
        services.Insert(services.Count - 1, ServiceDescriptor.DescribeKeyed(typeof(IWork), "other", (_, _) => { otherCalls++; return new Work("other", tracker); }, ServiceLifetime.Transient));
        services.Decorate<IWork, NoOrdinaryInner>();
        using var provider = Build(services, kind);
        using (var scope = provider.CreateScope())
            Assert.ThrowsExactly<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredKeyedService<IWork>("blue"));
        Assert.AreEqual(0, otherCalls);
        Assert.HasCount(1, tracker.Created);
        Assert.AreEqual(1, tracker.Created[0].DisposeCount);
    }

    [TestMethod]
    [DataRow("native")]
    [DataRow("snapshot")]
    [DataRow("diagnostics")]
    public void OptionalKeyedDependencyKeepsItsDefaultAndOrdinaryParameterReceivesInner(string kind)
    {
        var tracker = new Tracker();
        var services = Services(ServiceLifetime.Scoped, "factory", "object", new Work("other", new Tracker()), tracker);
        services.Decorate<IWork, OptionalOtherFirst>();
        using var provider = Build(services, kind);
        OptionalOtherFirst result;
        using (var scope = provider.CreateScope())
        {
            result = (OptionalOtherFirst)scope.ServiceProvider.GetRequiredKeyedService<IWork>("blue");
            Assert.IsNull(result.Other);
            Assert.IsInstanceOfType<Work>(result.Inner);
            Assert.AreEqual("blue", result.Key);
        }
        Assert.AreEqual(1, result.DisposeCount);
        Assert.HasCount(1, tracker.Created);
        Assert.AreEqual(1, tracker.Created[0].DisposeCount);
    }

    private static IServiceCollection Services(ServiceLifetime lifetime, string registration, string mode, Work other, Tracker tracker)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(tracker);
        if (mode == "null-key") services.AddSingleton<IWork>(other);
        else if (mode == "interface") services.AddKeyedSingleton<IWork>("other", other);
        else services.AddKeyedSingleton<object>(mode == "inherit" ? "blue" : "other", other);
        if (registration == "instance")
        {
            var inner = new Work("blue", tracker) { CallerOwned = true };
            services.AddKeyedSingleton<IWork>("blue", inner);
        }
        else if (registration == "type")
            services.Add(ServiceDescriptor.DescribeKeyed(typeof(IWork), "blue", typeof(Work), lifetime));
        else services.Add(ServiceDescriptor.DescribeKeyed(typeof(IWork), "blue", (_, key) => new Work(key!, tracker), lifetime));
        return services;
    }

    private static void Decorate(IServiceCollection services, string mode, bool otherFirst)
    {
        if (mode == "interface")
        {
            if (otherFirst) services.Decorate<IWork, InterfaceOtherFirst>();
            else services.Decorate<IWork, InterfaceInnerFirst>();
        }
        else if (mode == "object")
        {
            if (otherFirst) services.Decorate<IWork, ObjectOtherFirst>();
            else services.Decorate<IWork, ObjectInnerFirst>();
        }
        else if (mode == "inherit")
        {
            if (otherFirst) services.Decorate<IWork, InheritedOtherFirst>();
            else services.Decorate<IWork, InheritedInnerFirst>();
        }
        else
        {
            if (otherFirst) services.Decorate<IWork, NullKeyOtherFirst>();
            else services.Decorate<IWork, NullKeyInnerFirst>();
        }
    }

    private static ServiceProvider Build(IServiceCollection services, string kind) => kind switch
    {
        "snapshot" => ServiceProviderFactory.CreateServiceProvider(services),
        "diagnostics" => ServiceProviderFactory.CreateServiceProvider(services,
            new ExtendedServiceProviderOptions { ValidateOnBuild = true, DetectIncorrectUsageOfTransientDisposables = true }),
        _ => services.BuildServiceProvider()
    };

    public interface IWork : IDisposable;
    public sealed class Tracker { public List<Tracked> Created { get; } = []; }
    public abstract class Tracked : IWork
    {
        protected Tracked(Tracker tracker) => tracker.Created.Add(this);
        public bool CallerOwned { get; set; }
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
    public sealed class Work([ServiceKey] object key, Tracker tracker) : Tracked(tracker) { public object Key { get; } = key; }
    public abstract class Wrapper(IWork inner, object other, object key, Tracker tracker) : Tracked(tracker)
    { public IWork Inner { get; } = inner; public object Other { get; } = other; public object Key { get; } = key; }
    public sealed class InterfaceOtherFirst([ServiceKey] object key, [FromKeyedServices("other")] IWork other, IWork inner, Tracker tracker) : Wrapper(inner, other, key, tracker);
    public sealed class InterfaceInnerFirst(IWork inner, [FromKeyedServices("other")] IWork other, [ServiceKey] object key, Tracker tracker) : Wrapper(inner, other, key, tracker);
    public sealed class ObjectOtherFirst([ServiceKey] object key, [FromKeyedServices("other")] object other, IWork inner, Tracker tracker) : Wrapper(inner, other, key, tracker);
    public sealed class ObjectInnerFirst(IWork inner, [FromKeyedServices("other")] object other, [ServiceKey] object key, Tracker tracker) : Wrapper(inner, other, key, tracker);
    public sealed class InheritedOtherFirst([ServiceKey] object key, [FromKeyedServices] object other, IWork inner, Tracker tracker) : Wrapper(inner, other, key, tracker);
    public sealed class InheritedInnerFirst(IWork inner, [FromKeyedServices] object other, [ServiceKey] object key, Tracker tracker) : Wrapper(inner, other, key, tracker);
    public sealed class NullKeyOtherFirst([ServiceKey] object key, [FromKeyedServices(null)] IWork other, IWork inner, Tracker tracker) : Wrapper(inner, other, key, tracker);
    public sealed class NullKeyInnerFirst(IWork inner, [FromKeyedServices(null)] IWork other, [ServiceKey] object key, Tracker tracker) : Wrapper(inner, other, key, tracker);
    public sealed class MissingDependency([FromKeyedServices("missing")] object other, IWork inner, [ServiceKey] object key) : IWork
    { public object Other { get; } = other; public IWork Inner { get; } = inner; public object Key { get; } = key; public void Dispose() { } }
    public sealed class NoOrdinaryInner([FromKeyedServices("other")] IWork other, [ServiceKey] object key) : IWork
    { public IWork Other { get; } = other; public object Key { get; } = key; public void Dispose() { } }
    public sealed class OptionalOtherFirst([FromKeyedServices("missing")] IWork? other = null, IWork? inner = null, [ServiceKey] object? key = null) : IWork
    {
        public IWork? Other { get; } = other;
        public IWork? Inner { get; } = inner;
        public object? Key { get; } = key;
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
}
