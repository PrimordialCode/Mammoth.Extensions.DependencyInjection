using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static Mammoth.Extensions.DependencyInjection.Tests.DiagnosticConstructorParityTests;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class DecoratedNativeConstructorRegressionTests
{
    public static IEnumerable<object[]> ConstructorCases()
    {
        foreach (var type in new[] { typeof(Preferred), typeof(MultiplePreferred), typeof(UnavailablePreferred),
            typeof(Permuted), typeof(Subset), typeof(Repeated), typeof(Optional), typeof(RejectLonger),
            typeof(DifferentParameters), typeof(EqualLengthDifferentParameters) })
        foreach (var kind in new[] { "native", "snapshot", "diagnostics" })
        foreach (var lifetime in new[] { ServiceLifetime.Transient, ServiceLifetime.Scoped, ServiceLifetime.Singleton })
        foreach (var keyed in new[] { false, true })
            yield return [type, kind, lifetime, keyed];
    }

    [TestMethod]
    [DynamicData(nameof(ConstructorCases))]
    public void OriginalTypeKeepsNativeConstructorRulesAcrossLayersAndLifetimes(
        Type type, string kind, ServiceLifetime lifetime, bool keyed)
    {
        var nativeCounts = new Counts();
        var counts = new Counts();
        var nativeServices = Services(nativeCounts);
        var services = Services(counts);
        Add(nativeServices, type, lifetime, keyed);
        Add(services, type, lifetime, keyed);
        services.Decorate<IChoice, Forwarder>();
        services.Decorate<IChoice, Forwarder>();
        if (kind == "diagnostics" && (type == typeof(DifferentParameters) || type == typeof(EqualLengthDifferentParameters)))
        {
            var nativeError = Assert.ThrowsExactly<AggregateException>(() => nativeServices.BuildServiceProvider(
                new ServiceProviderOptions { ValidateOnBuild = true }));
            var error = Assert.ThrowsExactly<AggregateException>(() => Build(services, kind));
            StringAssert.Contains(nativeError.ToString(), "ambiguous");
            StringAssert.Contains(error.ToString(), "ambiguous");
            Assert.AreEqual(0, nativeCounts.Dependencies);
            Assert.AreEqual(0, counts.Dependencies);
            Assert.AreEqual(0, counts.Constructors);
            return;
        }
        using var native = nativeServices.BuildServiceProvider();
        using var provider = Build(services, kind);
        Assert.AreEqual(0, counts.Dependencies);
        using var nativeScope = native.CreateScope();
        using var scope = provider.CreateScope();
        if (type == typeof(DifferentParameters) || type == typeof(EqualLengthDifferentParameters))
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(nativeScope.ServiceProvider, keyed));
            var error = Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(scope.ServiceProvider, keyed));
            StringAssert.Contains(error.Message, "ambiguous");
            Assert.AreEqual(0, nativeCounts.Dependencies);
            Assert.AreEqual(0, counts.Dependencies);
            Assert.AreEqual(0, counts.Constructors);
            return;
        }
        var nativeResult = Resolve(nativeScope.ServiceProvider, keyed);
        var first = Resolve(scope.ServiceProvider, keyed);
        var firstInner = Original(first);
        Assert.AreEqual(type, firstInner.GetType());
        Assert.AreEqual(nativeResult.Selected, first.Selected);
        var again = Resolve(scope.ServiceProvider, keyed);
        using var other = provider.CreateScope();
        var acrossScopes = Resolve(other.ServiceProvider, keyed);
        Assert.AreEqual(nativeResult.Selected, again.Selected);
        Assert.AreEqual(nativeResult.Selected, acrossScopes.Selected);
        if (lifetime == ServiceLifetime.Transient)
        {
            Assert.AreNotSame(first, again);
            Assert.AreNotSame(firstInner, Original(again));
        }
        else
        {
            Assert.AreSame(first, again);
            Assert.AreSame(firstInner, Original(again));
        }
        if (lifetime == ServiceLifetime.Singleton) Assert.AreSame(first, acrossScopes);
        else Assert.AreNotSame(first, acrossScopes);
        Assert.AreEqual(0, counts.Rejected, "A rejected constructor must not activate its dependencies.");
    }

    [TestMethod]
    [DataRow("native", false)]
    [DataRow("native", true)]
    [DataRow("snapshot", false)]
    [DataRow("snapshot", true)]
    [DataRow("diagnostics", false)]
    [DataRow("diagnostics", true)]
    public void ContextualOriginalKeepsRequestedKeyAndAttributeBindings(string kind, bool wildcard)
    {
        IServiceCollection services = new ServiceCollection();
        var ordinary = new A();
        var inherited = new A();
        var explicitKey = new A();
        services.AddSingleton(ordinary);
        services.AddKeyedSingleton("blue", inherited);
        services.AddKeyedSingleton("fixed", explicitKey);
        services.AddKeyedTransient<IChoice, Contextual>(wildcard ? KeyedService.AnyKey : "blue");
        using var native = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = kind == "diagnostics" });
        var key = new string("blue".ToCharArray());
        var nativeResult = (Contextual)native.GetRequiredKeyedService<IChoice>(key);
        services.Decorate<IChoice, Forwarder>();
        services.Decorate<IChoice, Forwarder>();
        using var provider = Build(services, kind);
        var result = (Contextual)Original(provider.GetRequiredKeyedService<IChoice>(key));
        Assert.AreEqual(nativeResult.Selected, result.Selected);
        // Build validation can cache the registration's equal key before the first
        // request. Compare key identity under the same native validation setting.
        Assert.AreSame(nativeResult.Key, result.Key);
        Assert.AreSame(ordinary, result.Ordinary);
        Assert.AreSame(inherited, result.Inherited);
        Assert.AreSame(explicitKey, result.Explicit);
    }

    public static IEnumerable<object[]> MetadataCases()
    {
        foreach (var kind in new[] { "native", "snapshot", "diagnostics" })
        foreach (var keyed in new[] { false, true })
        foreach (var scenario in new[] { "builtin-missing", "builtin-exact", "builtin-wildcard",
            "generic-invalid", "generic-closed", "generic-wildcard-closed", "generic-inherited" })
            if (scenario != "generic-inherited" || keyed) yield return [kind, keyed, scenario];
    }

    [TestMethod]
    [DynamicData(nameof(MetadataCases))]
    public void NativeProvidersPreserveBuiltInAvailabilityAndGenericConstraintChecks(
        string kind, bool keyed, string scenario)
    {
        var nativeCounts = new Counts();
        var counts = new Counts();
        var explicitProvider = new EmptyProvider();
        var nativeServices = Configure(nativeCounts);
        var services = Configure(counts);
        using var native = nativeServices.BuildServiceProvider();
        services.Decorate<IChoice, Forwarder>();
        if (kind == "diagnostics" && scenario == "generic-invalid")
        {
            Assert.ThrowsExactly<AggregateException>(() => nativeServices.BuildServiceProvider(
                new ServiceProviderOptions { ValidateOnBuild = true }));
            var error = Assert.ThrowsExactly<AggregateException>(() => Build(services, kind));
            Assert.IsInstanceOfType<ArgumentException>(error.InnerExceptions[0].InnerException);
            Assert.AreEqual(0, nativeCounts.Dependencies);
            Assert.AreEqual(0, counts.Dependencies);
            Assert.AreEqual(0, counts.Constructors);
            return;
        }
        using var provider = Build(services, kind);
        // Mutating the caller collection must not replace a built provider's metadata.
        services.AddKeyedTransient<IOpen<int>, Open<int>>("bad");
        services.AddKeyedSingleton<IServiceProvider>("blue", explicitProvider);
        if (scenario == "generic-invalid")
        {
            Assert.ThrowsExactly<ArgumentException>(() => Resolve(native, keyed));
            Assert.ThrowsExactly<ArgumentException>(() => Resolve(provider, keyed));
            Assert.AreEqual(0, nativeCounts.Dependencies);
            Assert.AreEqual(0, counts.Dependencies);
            Assert.AreEqual(0, counts.Constructors);
        }
        else
        {
            var nativeResult = Resolve(native, keyed);
            var result = Original(Resolve(provider, keyed), layers: 1);
            Assert.AreEqual(nativeResult.Selected, result.Selected);
            if (result is KeyedBuiltIn builtIn)
                Assert.AreSame(scenario == "builtin-missing" ? null : explicitProvider, builtIn.Provider);
        }

        IServiceCollection Configure(Counts tracker)
        {
            var collection = Services(tracker);
            Type target;
            if (scenario.StartsWith("builtin", StringComparison.Ordinal))
            {
                target = typeof(KeyedBuiltIn);
                if (scenario != "builtin-missing") collection.AddKeyedSingleton<IServiceProvider>(
                    scenario == "builtin-exact" ? "blue" : KeyedService.AnyKey, explicitProvider);
            }
            else if (scenario == "generic-inherited")
            {
                target = typeof(InheritedGeneric);
                collection.AddKeyedTransient(typeof(IOpen<>), KeyedService.AnyKey, typeof(Open<>));
            }
            else
            {
                target = typeof(ConstrainedCandidate);
                collection.AddTransient<IOpen<int>>(_ => { tracker.Dependencies++; return new Open<int>(); });
                collection.AddKeyedTransient(typeof(IOpen<>), "bad", typeof(ReferenceOnly<>));
                if (scenario != "generic-invalid") collection.AddKeyedTransient<IOpen<int>, Open<int>>(
                    scenario == "generic-closed" ? "bad" : KeyedService.AnyKey);
            }
            Add(collection, target, ServiceLifetime.Transient, keyed);
            return collection;
        }
    }

    public static IEnumerable<object[]> OwnershipCases()
    {
        foreach (var kind in new[] { "native", "snapshot", "diagnostics" })
        foreach (var keyed in new[] { false, true })
        foreach (var registration in new[] { "type", "factory", "instance" })
        foreach (var lifetime in new[] { ServiceLifetime.Transient, ServiceLifetime.Scoped, ServiceLifetime.Singleton })
            if (registration != "instance" || lifetime == ServiceLifetime.Singleton)
                yield return [kind, keyed, registration, lifetime];
    }

    [TestMethod]
    [DynamicData(nameof(OwnershipCases))]
    public void RepeatedLayersPreserveFactoryInstanceLifetimesAndDisposalOwnership(
        string kind, bool keyed, string registration, ServiceLifetime lifetime)
    {
        var tracker = new Tracker();
        var services = Services(new Counts());
        services.AddSingleton(tracker);
        var borrowed = new DisposablePreferred(new A());
        var factoryCalls = 0;
        services.Add(registration switch
        {
            "instance" => keyed ? ServiceDescriptor.KeyedSingleton<IChoice>("blue", borrowed) : ServiceDescriptor.Singleton<IChoice>(borrowed),
            "factory" => keyed ? ServiceDescriptor.DescribeKeyed(typeof(IChoice), "blue", (_, _) => Create(), lifetime)
                : ServiceDescriptor.Describe(typeof(IChoice), _ => Create(), lifetime),
            _ => keyed ? ServiceDescriptor.DescribeKeyed(typeof(IChoice), "blue", typeof(DisposablePreferred), lifetime)
                : ServiceDescriptor.Describe(typeof(IChoice), typeof(DisposablePreferred), lifetime)
        });
        services.Decorate<IChoice, OwnedForwarder>();
        services.Decorate<IChoice, OwnedForwarder>();
        using var provider = Build(services, kind);
        var originals = new HashSet<DisposablePreferred>();
        IChoice? first = null;
        for (var scopeIndex = 0; scopeIndex < 2; scopeIndex++)
        {
            using (var scope = provider.CreateScope())
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    var result = Resolve(scope.ServiceProvider, keyed);
                    var inner = (DisposablePreferred)Original(result);
                    originals.Add(inner);
                    Assert.AreEqual(registration == "type" ? "AB" : "A", result.Selected);
                    if (registration == "instance") Assert.AreSame(borrowed, inner);
                    if (first == null) first = result;
                    else if (lifetime == ServiceLifetime.Singleton || (lifetime == ServiceLifetime.Scoped && scopeIndex == 0))
                        Assert.AreSame(first, result);
                    else Assert.AreNotSame(first, result);
                }
            foreach (var inner in originals)
                Assert.AreEqual(registration == "instance" || lifetime == ServiceLifetime.Singleton ? 0 : 1, inner.Disposals);
        }
        Assert.IsNotEmpty(originals);
        Assert.IsNotEmpty(tracker.Created);
        var expectedInstances = lifetime == ServiceLifetime.Transient ? 4 : lifetime == ServiceLifetime.Scoped ? 2 : 1;
        Assert.HasCount(expectedInstances, originals);
        Assert.HasCount(expectedInstances * 2, tracker.Created);
        Assert.AreEqual(registration == "factory" ? expectedInstances : 0, factoryCalls);
        provider.Dispose();
        foreach (var wrapper in tracker.Created) Assert.AreEqual(1, wrapper.Disposals);
        foreach (var inner in originals) Assert.AreEqual(registration == "instance" ? 0 : 1, inner.Disposals);

        DisposablePreferred Create() { factoryCalls++; return new DisposablePreferred(new A()); }
    }

    [TestMethod]
    [DataRow("native", false)]
    [DataRow("native", true)]
    [DataRow("snapshot", false)]
    [DataRow("snapshot", true)]
    [DataRow("diagnostics", false)]
    [DataRow("diagnostics", true)]
    public void NonemptyMapsAndNewDecoratorsKeepTheirPreferredConstructorPolicy(string kind, bool keyed)
    {
        var services = Services(new Counts());
        Dependency[] map = [Dependency.OnValue("a", new A())];
        if (keyed) services.AddKeyedTransient<IChoice, Preferred>("blue", map);
        else services.AddTransient<IChoice, Preferred>(map);
        services.Decorate<IChoice, PolicyForwarder>();
        using var provider = Build(services, kind);
        var wrapper = (PolicyForwarder)Resolve(provider, keyed);
        Assert.AreEqual("short", wrapper.Policy);
        Assert.AreEqual("A", wrapper.Inner.Selected);
    }

    [TestMethod]
    [DataRow("native", false)]
    [DataRow("native", true)]
    [DataRow("snapshot", false)]
    [DataRow("snapshot", true)]
    [DataRow("diagnostics", false)]
    [DataRow("diagnostics", true)]
    public void OriginalConstructorExceptionsKeepTheirType(string kind, bool keyed)
    {
        var services = Services(new Counts());
        Add(services, typeof(Throwing), ServiceLifetime.Transient, keyed);
        services.Decorate<IChoice, Forwarder>();
        using var provider = Build(services, kind);
        Assert.ThrowsExactly<ArgumentException>(() => Resolve(provider, keyed));
    }

    private static IServiceCollection Services(Counts counts)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(counts);
        services.AddTransient<A>(_ => { counts.Dependencies++; return new A(); });
        services.AddTransient<B>(_ => { counts.Dependencies++; return new B(); });
        services.AddTransient<C>(_ => { counts.Dependencies++; return new C(); });
        services.AddTransient<Rejected>(_ => { counts.Rejected++; return new Rejected(); });
        return services;
    }
    private static void Add(IServiceCollection services, Type type, ServiceLifetime lifetime, bool keyed)
        => services.Add(keyed ? ServiceDescriptor.DescribeKeyed(typeof(IChoice), "blue", type, lifetime)
            : ServiceDescriptor.Describe(typeof(IChoice), type, lifetime));
    private static IChoice Resolve(IServiceProvider provider, bool keyed) => keyed
        ? provider.GetRequiredKeyedService<IChoice>("blue") : provider.GetRequiredService<IChoice>();
    private static IChoice Original(IChoice choice, int layers = 2)
    {
        for (var index = 0; index < layers; index++) choice = ((Forwarder)choice).Inner;
        return choice;
    }
    private static ServiceProvider Build(IServiceCollection services, string kind) => kind switch
    {
        "snapshot" => ServiceProviderFactory.CreateServiceProvider(services),
        "diagnostics" => ServiceProviderFactory.CreateServiceProvider(services,
            new ExtendedServiceProviderOptions { ValidateOnBuild = true, DetectIncorrectUsageOfTransientDisposables = true }),
        _ => services.BuildServiceProvider()
    };
    public class Forwarder(IChoice inner) : IChoice
    {
        public IChoice Inner { get; } = inner;
        public string Selected => Inner.Selected;
    }
    public sealed class Tracker { public List<OwnedForwarder> Created { get; } = []; }
    public sealed class OwnedForwarder : Forwarder, IDisposable
    {
        public int Disposals { get; private set; }
        public OwnedForwarder(IChoice inner, Tracker tracker) : base(inner) => tracker.Created.Add(this);
        public void Dispose() => Disposals++;
    }
    public sealed class PolicyForwarder : Forwarder
    {
        public string Policy { get; }
        [ActivatorUtilitiesConstructor] public PolicyForwarder(IChoice inner) : base(inner) => Policy = "short";
        public PolicyForwarder(IChoice inner, A a) : base(inner) => Policy = "long";
    }
}
