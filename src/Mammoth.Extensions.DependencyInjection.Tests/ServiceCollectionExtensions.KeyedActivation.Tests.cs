using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class KeyedActivationRegressionTests
{
    public static IEnumerable<object[]> ActivationCases()
    {
        foreach (var lifetime in new[] { ServiceLifetime.Transient, ServiceLifetime.Scoped, ServiceLifetime.Singleton })
        foreach (var layers in new[] { 0, 1, 2 })
        foreach (var diagnostic in new[] { false, true })
        foreach (var wildcard in new[] { false, true })
            yield return new object[] { lifetime, layers, diagnostic, wildcard };
    }

    [TestMethod]
    [DynamicData(nameof(ActivationCases))]
    public void TypeActivationPreservesContextLifetimeAndDisposal(ServiceLifetime lifetime, int layers, bool diagnostic, bool wildcard)
    {
        var services = Services();
        AddType(services, lifetime, wildcard);
        // Native DI is the control for key injection and lookup modes.
        using (var native = services.BuildServiceProvider())
        using (var scope = native.CreateScope())
            Check(scope.ServiceProvider.GetRequiredKeyedService<IWork>("blue"), "blue", 0);
        for (var i = 0; i < layers; i++) services.Decorate<IWork, Wrapper>();
        if (diagnostic && wildcard && layers == 0)
        {
            // Native build validation inspects inherited dependencies under AnyKey.
            var native = Assert.ThrowsExactly<AggregateException>(() => services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateOnBuild = true }));
            var error = Assert.ThrowsExactly<AggregateException>(() => Build(services, diagnostic));
            StringAssert.Contains(native.ToString(), nameof(Part));
            StringAssert.Contains(error.ToString(), nameof(Part));
            return;
        }
        var owned = new HashSet<Snapshot>();
        var provider = Build(services, diagnostic);
        try
        {
            using var scope = provider.CreateScope();
            var blue = scope.ServiceProvider.GetRequiredKeyedService<IWork>("blue");
            var equalBlue = scope.ServiceProvider.GetRequiredKeyedService<IWork>(new string("blue".ToCharArray()));
            var red = scope.ServiceProvider.GetRequiredKeyedService<IWork>("red");
            Track(blue, owned); Track(equalBlue, owned); Track(red, owned);
            Check(blue, "blue", layers); Check(red, "red", wildcard ? layers : 0);
            Assert.AreNotSame(blue, red);
            if (lifetime == ServiceLifetime.Transient) Assert.AreNotSame(blue, equalBlue);
            else Assert.AreSame(blue, equalBlue);
            using var other = provider.CreateScope();
            var otherBlue = other.ServiceProvider.GetRequiredKeyedService<IWork>("blue");
            Track(otherBlue, owned); Check(otherBlue, "blue", layers);
            if (lifetime == ServiceLifetime.Singleton) Assert.AreSame(blue, otherBlue);
            else Assert.AreNotSame(blue, otherBlue);
        }
        finally { provider.Dispose(); }
        foreach (var item in owned) Assert.AreEqual(1, item.DisposeCount);
    }

    public static IEnumerable<object[]> DependsOnCases()
    {
        foreach (var lifetime in new[] { ServiceLifetime.Transient, ServiceLifetime.Scoped, ServiceLifetime.Singleton })
        foreach (var tryAdd in new[] { false, true })
        foreach (var interfaceRegistration in new[] { false, true })
        foreach (var wildcard in new[] { false, true })
        foreach (var diagnostic in new[] { false, true })
            yield return new object[] { lifetime, tryAdd, interfaceRegistration, wildcard, diagnostic };
    }

    [TestMethod]
    [DynamicData(nameof(DependsOnCases))]
    public void DependsOnHonorsLookupModesAndNamedOverrides(ServiceLifetime lifetime, bool tryAdd, bool interfaceRegistration, bool wildcard, bool diagnostic)
    {
        var services = Services();
        Dependency[] map = [Dependency.OnValue("label", "configured")];
        object registrationKey = wildcard ? KeyedService.AnyKey : "blue";
        RegisterDependsOn(services, lifetime, tryAdd, interfaceRegistration, registrationKey, map);
        using var provider = Build(services, diagnostic);
        using var scope = provider.CreateScope();
        IWork result = interfaceRegistration
            ? scope.ServiceProvider.GetRequiredKeyedService<IWork>("blue")
            : scope.ServiceProvider.GetRequiredKeyedService<Work>("blue");
        Check(result, "blue", 0, "configured");
        var again = interfaceRegistration
            ? scope.ServiceProvider.GetRequiredKeyedService<IWork>(new string("blue".ToCharArray()))
            : scope.ServiceProvider.GetRequiredKeyedService<Work>(new string("blue".ToCharArray()));
        if (lifetime == ServiceLifetime.Transient) Assert.AreNotSame(result, again);
        else Assert.AreSame(result, again);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DependsOnInheritedDependencyIsSatisfiableWithoutOrdinaryRegistration(bool diagnostic)
    {
        var services = new ServiceCollection();
        var blue = new Part("blue");
        services.AddKeyedSingleton("blue", blue);
        services.AddKeyedTransient<OnlyInherited>("blue", [Dependency.OnValue("label", "configured")]);
        using var provider = Build(services, diagnostic);
        using var scope = provider.CreateScope();
        Assert.AreSame(blue, scope.ServiceProvider.GetRequiredKeyedService<OnlyInherited>("blue").Part);
    }

    [TestMethod]
    public void ExplicitNamedOverridesTakePriorityOverKeyAttributes()
    {
        var services = Services();
        services.AddKeyedTransient<Work>("blue", [Dependency.OnValue("key", "override"),
            Dependency.OnValue("label", "configured"), Parameter.ForKey("inherited").Eq("fixed")]);
        using var provider = services.BuildServiceProvider();
        var work = provider.GetRequiredKeyedService<Work>("blue");
        Assert.AreEqual("override", work.Key);
        Assert.AreEqual("fixed", work.Inherited.Name);
        Assert.AreEqual("ordinary", work.Ordinary.Name);
        Assert.AreEqual("configured", work.Label);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void KeyedDecoratorUsesContextForFactoryAndCallerOwnedInstance(bool callerOwned, bool diagnostic)
    {
        var services = Services();
        var inner = new PlainWork();
        if (callerOwned) services.AddKeyedSingleton<IWork>(KeyedService.AnyKey, inner);
        else services.AddKeyedScoped<IWork>(KeyedService.AnyKey, (_, _) => new PlainWork());
        services.Decorate<IWork, Wrapper>(); services.Decorate<IWork, Wrapper>();
        var provider = Build(services, diagnostic);
        Wrapper first;
        using (var scope = provider.CreateScope())
        {
            first = (Wrapper)scope.ServiceProvider.GetRequiredKeyedService<IWork>("blue");
            Assert.AreEqual("blue", first.Key);
            Assert.AreEqual("blue", first.Inherited.Name);
            var middle = (Wrapper)first.Inner;
            Assert.AreEqual("blue", middle.Key);
            if (callerOwned) Assert.AreSame(inner, middle.Inner);
            inner = (PlainWork)middle.Inner;
        }
        provider.Dispose();
        Assert.AreEqual(1, first.DisposeCount);
        Assert.AreEqual(1, ((Wrapper)first.Inner).DisposeCount);
        Assert.AreEqual(callerOwned ? 0 : 1, inner.DisposeCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void KeyAwareConstructorSelectionPreservesDefaultsAndNativePreference(bool diagnostic)
    {
        var services = new ServiceCollection();
        services.AddKeyedTransient<OptionalWork>("blue");
        services.AddKeyedTransient<PreferredWork>("blue");
        services.AddKeyedTransient<AlternativeWork>("blue");
        using var provider = Build(services, diagnostic);
        using var scope = provider.CreateScope();
        var optional = scope.ServiceProvider.GetRequiredKeyedService<OptionalWork>("blue");
        Assert.AreEqual("blue", optional.Key); Assert.AreEqual(7, optional.Count); Assert.IsNull(optional.Part);
        var preferred = scope.ServiceProvider.GetRequiredKeyedService<PreferredWork>("blue");
        Assert.AreEqual("blue", preferred.Key); Assert.AreEqual("long", preferred.Selected);
        var alternative = scope.ServiceProvider.GetRequiredKeyedService<AlternativeWork>("blue");
        Assert.AreEqual("blue", alternative.Key); Assert.AreEqual("short", alternative.Selected);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NullLookupModesUseNativeUnkeyedServiceKeyResolution(bool diagnostic)
    {
        var services = Services();
        services.AddKeyedTransient<Work>(null);
        using (var native = services.BuildServiceProvider()) Assert.AreEqual("ordinary-key", native.GetRequiredService<Work>().Key);
        services.AddKeyedTransient<Work>(null, [Dependency.OnValue("label", "configured")]);
        using var provider = Build(services, diagnostic);
        using var scope = provider.CreateScope();
        var work = scope.ServiceProvider.GetRequiredService<Work>();
        Assert.AreEqual("ordinary-key", work.Key, "Null-key DependsOn uses ordinary registration resolution, as native DI does.");
        Assert.AreEqual("ordinary", work.Inherited.Name);
        Assert.AreEqual("ordinary", work.Ordinary.Name);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void StrongKeyDoesNotReplaceAnOrdinaryParameterOfTheSameType(int path)
    {
        var services = Services();
        if (path == 3) services.AddKeyedTransient<IStrongWork, StrongWork>(KeyedService.AnyKey, [Dependency.OnValue("label", "configured")]);
        else services.AddKeyedTransient<IStrongWork, StrongWork>(KeyedService.AnyKey);
        if (path == 1) { services.Decorate<IStrongWork, StrongWrapper>(); services.Decorate<IStrongWork, StrongWrapper>(); }
        using var provider = Build(services, path == 2);
        using var scope = provider.CreateScope();
        var work = scope.ServiceProvider.GetRequiredKeyedService<IStrongWork>("blue");
        Assert.AreEqual("blue", work.Key);
        Assert.AreEqual(path == 3 ? "configured" : "ordinary-label", work.Label);
        if (work is StrongWrapper outer)
        {
            var middle = (StrongWrapper)outer.Inner;
            Assert.AreEqual("blue", middle.Key); Assert.AreEqual("ordinary-label", middle.Label);
            Assert.AreEqual("blue", middle.Inner.Key); Assert.AreEqual("ordinary-label", middle.Inner.Label);
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void EqualKeyObjectsPreserveNativeEffectiveKeyIdentity(bool decorate, bool diagnostic)
    {
        var registeredKey = new EqualKey(42);
        var requestedKey = new EqualKey(42);
        var services = Services();
        services.AddKeyedSingleton(registeredKey, new Part("equal"));
        services.AddKeyedScoped<IWork, Work>(registeredKey);
        if (decorate) { services.Decorate<IWork, Wrapper>(); services.Decorate<IWork, Wrapper>(); }
        // Native build validation caches explicit-key call sites before the request.
        var expectedKey = diagnostic ? registeredKey : requestedKey;
        using var provider = Build(services, diagnostic);
        using var scope = provider.CreateScope();
        IWork work = scope.ServiceProvider.GetRequiredKeyedService<IWork>(requestedKey);
        while (true)
        {
            Assert.AreSame(expectedKey, ((Snapshot)work).Key);
            Assert.AreEqual("equal", ((Snapshot)work).Inherited.Name);
            if (work is not Wrapper wrapper) break;
            work = wrapper.Inner;
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WrongStrongKeyTypeIsRejectedWithoutOrdinaryFallback(bool diagnostic)
    {
        var services = Services();
        services.AddKeyedTransient<IStrongWork, StrongWork>(42);
        if (!diagnostic) services.Decorate<IStrongWork, StrongWrapper>();
        if (diagnostic)
        {
            var native = Assert.ThrowsExactly<AggregateException>(() => services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateOnBuild = true }));
            var error = Assert.ThrowsExactly<AggregateException>(() => Build(services, diagnostic));
            StringAssert.Contains(native.ToString(), "ServiceKey");
            StringAssert.Contains(error.ToString(), "ServiceKey");
            return;
        }
        using var provider = Build(services, diagnostic);
        using var scope = provider.CreateScope();
        Assert.ThrowsExactly<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredKeyedService<IStrongWork>(42));
    }

    [TestMethod]
    public void KeyAwareActivationRetainsConstructorExceptionType()
    {
        var services = Services();
        services.AddKeyedScoped<ThrowingWork>("blue");
        using var provider = Build(services, true);
        using var scope = provider.CreateScope();
        Assert.ThrowsExactly<ArgumentException>(() => scope.ServiceProvider.GetRequiredKeyedService<ThrowingWork>("blue"));
    }
    [TestMethod]
    [DataRow(ServiceLifetime.Transient, false)]
    [DataRow(ServiceLifetime.Transient, true)]
    [DataRow(ServiceLifetime.Scoped, false)]
    [DataRow(ServiceLifetime.Scoped, true)]
    [DataRow(ServiceLifetime.Singleton, false)]
    [DataRow(ServiceLifetime.Singleton, true)]
    public async Task KeyAwareAsyncLayersRemainOwnedExactlyOnce(ServiceLifetime lifetime, bool diagnostic)
    {
        var services = Services();
        ((IServiceCollection)services).Add(ServiceDescriptor.DescribeKeyed(typeof(IAsyncWork), KeyedService.AnyKey, typeof(AsyncWork), lifetime));
        services.Decorate<IAsyncWork, AsyncWrapper>(); services.Decorate<IAsyncWork, AsyncWrapper>();
        var provider = Build(services, diagnostic);
        var scope = provider.CreateAsyncScope();
        var owned = new HashSet<AsyncWork>();
        try
        {
            foreach (var key in new[] { "blue", "red" })
            {
                IAsyncWork item = scope.ServiceProvider.GetRequiredKeyedService<IAsyncWork>(key);
                while (true)
                {
                    var current = (AsyncWork)item;
                    owned.Add(current);
                    Assert.AreEqual(key, current.Key); Assert.AreEqual(key, current.Part.Name);
                    if (current is not AsyncWrapper wrapper) break;
                    item = wrapper.Inner;
                }
            }
        }
        finally { await scope.DisposeAsync(); await provider.DisposeAsync(); }
        foreach (var item in owned) Assert.AreEqual(1, item.DisposeCount);
    }
    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<object>("ordinary-key");
        services.AddSingleton("ordinary-label");
        services.AddSingleton(new Part("ordinary"));
        foreach (var key in new[] { "blue", "red", "fixed" }) services.AddKeyedSingleton(key, new Part(key));
        return services;
    }
    private static ServiceProvider Build(IServiceCollection services, bool diagnostic) => ServiceProviderFactory.CreateServiceProvider(services,
        new ExtendedServiceProviderOptions { ValidateOnBuild = diagnostic, DetectIncorrectUsageOfTransientDisposables = diagnostic });
    private static void AddType(IServiceCollection services, ServiceLifetime lifetime, bool wildcard)
    {
        foreach (object key in wildcard ? new object[] { KeyedService.AnyKey } : new object[] { "red", "blue" })
            services.Add(ServiceDescriptor.DescribeKeyed(typeof(IWork), key, typeof(Work), lifetime));
    }
    private static void RegisterDependsOn(IServiceCollection services, ServiceLifetime lifetime, bool tryAdd, bool asInterface, object key, Dependency[] map)
    {
        if (asInterface)
        {
            switch (lifetime)
            {
                case ServiceLifetime.Transient: if (tryAdd) services.TryAddKeyedTransient<IWork, Work>(key, map); else services.AddKeyedTransient<IWork, Work>(key, map); break;
                case ServiceLifetime.Scoped: if (tryAdd) services.TryAddKeyedScoped<IWork, Work>(key, map); else services.AddKeyedScoped<IWork, Work>(key, map); break;
                case ServiceLifetime.Singleton: if (tryAdd) services.TryAddKeyedSingleton<IWork, Work>(key, map); else services.AddKeyedSingleton<IWork, Work>(key, map); break;
            }
        }
        else
        {
            switch (lifetime)
            {
                case ServiceLifetime.Transient: if (tryAdd) services.TryAddKeyedTransient<Work>(key, map); else services.AddKeyedTransient<Work>(key, map); break;
                case ServiceLifetime.Scoped: if (tryAdd) services.TryAddKeyedScoped<Work>(key, map); else services.AddKeyedScoped<Work>(key, map); break;
                case ServiceLifetime.Singleton: if (tryAdd) services.TryAddKeyedSingleton<Work>(key, map); else services.AddKeyedSingleton<Work>(key, map); break;
            }
        }
    }
    private static void Check(IWork work, string key, int layers, string label = "ordinary-label")
    {
        var current = work;
        for (var i = 0; i <= layers; i++)
        {
            var item = (Snapshot)current;
            Assert.AreEqual(key, item.Key); Assert.AreEqual(label, item.Label);
            Assert.AreEqual(key, item.Inherited.Name); Assert.AreEqual("fixed", item.Explicit.Name); Assert.AreEqual("ordinary", item.Ordinary.Name);
            if (i < layers) current = ((Wrapper)current).Inner;
        }
    }
    private static void Track(IWork work, HashSet<Snapshot> owned)
    {
        owned.Add((Snapshot)work);
        if (work is Wrapper wrapper) Track(wrapper.Inner, owned);
    }
    public interface IWork { }
    public interface IAsyncWork { }
    public class AsyncWork([ServiceKey] object key, [FromKeyedServices] Part part) : IAsyncWork, IAsyncDisposable
    {
        public object Key { get; } = key;
        public Part Part { get; } = part;
        public int DisposeCount { get; private set; }
        public ValueTask DisposeAsync() { DisposeCount++; return default; }
    }
    public sealed class AsyncWrapper(IAsyncWork inner, [ServiceKey] object key, [FromKeyedServices] Part part) : AsyncWork(key, part)
    { public IAsyncWork Inner { get; } = inner; }
    public interface IStrongWork { string Key { get; } string Label { get; } }
    public sealed class StrongWork(string label, [ServiceKey] string key) : IStrongWork
    { public string Key { get; } = key; public string Label { get; } = label; }
    public sealed class StrongWrapper(string label, IStrongWork inner, [ServiceKey] string key) : IStrongWork
    { public string Key { get; } = key; public string Label { get; } = label; public IStrongWork Inner { get; } = inner; }
    public sealed class EqualKey(int value)
    { private int Value => value; public override bool Equals(object? obj) => obj is EqualKey key && key.Value == value; public override int GetHashCode() => value; }
    public sealed class ThrowingWork
    { public ThrowingWork([ServiceKey] string key) => throw new ArgumentException(key); }
    public sealed class Part(string name) { public string Name { get; } = name; }
    public abstract class Snapshot(object key, string label, Part inherited, Part explicitPart, Part ordinary) : IWork, IDisposable
    {
        public object Key { get; } = key;
        public string Label { get; } = label;
        public Part Inherited { get; } = inherited;
        public Part Explicit { get; } = explicitPart;
        public Part Ordinary { get; } = ordinary;
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
    public sealed class Work([ServiceKey] object key, string label, [FromKeyedServices] Part inherited,
        [FromKeyedServices("fixed")] Part explicitPart, [FromKeyedServices(null)] Part ordinary) : Snapshot(key, label, inherited, explicitPart, ordinary);
    public sealed class Wrapper(IWork inner, [ServiceKey] object key, string label, [FromKeyedServices] Part inherited,
        [FromKeyedServices("fixed")] Part explicitPart, [FromKeyedServices(null)] Part ordinary) : Snapshot(key, label, inherited, explicitPart, ordinary)
    { public IWork Inner { get; } = inner; }
    public sealed class PlainWork : IWork, IDisposable
    { public int DisposeCount { get; private set; } public void Dispose() => DisposeCount++; }
    public sealed class OnlyInherited(string label, [FromKeyedServices] Part part)
    { public string Label { get; } = label; public Part Part { get; } = part; }
    public sealed class OptionalWork([ServiceKey] string key, [FromKeyedServices] Part? part = null, int count = 7)
    { public string Key { get; } = key; public Part? Part { get; } = part; public int Count { get; } = count; }
    public sealed class PreferredWork
    {
        public string Key { get; }
        public string Selected { get; }
        [ActivatorUtilitiesConstructor]
        public PreferredWork([ServiceKey] string key) { Key = key; Selected = "preferred"; }
        public PreferredWork([ServiceKey] string key, [FromKeyedServices] Part? part = null) { Key = key; Selected = "long"; }
    }
    public sealed class AlternativeWork
    {
        public string Key { get; }
        public string Selected { get; }
        public AlternativeWork([ServiceKey] string key) { Key = key; Selected = "short"; }
        public AlternativeWork([ServiceKey] string key, [FromKeyedServices] Part part) { Key = key; Selected = "long"; }
    }
}
