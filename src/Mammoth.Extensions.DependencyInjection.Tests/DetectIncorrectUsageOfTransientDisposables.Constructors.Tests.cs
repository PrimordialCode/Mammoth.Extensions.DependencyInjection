using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class DiagnosticConstructorParityTests
{
    public static IEnumerable<object[]> ValidCases()
    {
        foreach (var type in new[] { typeof(Preferred), typeof(MultiplePreferred), typeof(UnavailablePreferred),
            typeof(Permuted), typeof(Subset), typeof(Repeated), typeof(Optional), typeof(RejectLonger) })
        foreach (var lifetime in new[] { ServiceLifetime.Transient, ServiceLifetime.Scoped, ServiceLifetime.Singleton })
        foreach (var keyed in new[] { false, true })
        foreach (var validate in new[] { false, true })
            yield return [type, lifetime, keyed, validate];
    }

    [TestMethod]
    [DynamicData(nameof(ValidCases))]
    public void OrdinaryTypeRegistrationsKeepNativeSelectionAndLifetime(Type type, ServiceLifetime lifetime, bool keyed, bool validate)
    {
        string? nativeChoice = null;
        foreach (var mode in new[] { "native", "disabled", "enabled" })
        {
            var counts = new Counts();
            var services = Services(counts);
            Add(services, type, lifetime, keyed);
            using var provider = Build(services, mode, validate);
            Assert.AreEqual(0, counts.Dependencies, "Building must not activate dependencies.");
            using var scope = provider.CreateScope();
            var first = Resolve(scope.ServiceProvider, type, keyed);
            var again = Resolve(scope.ServiceProvider, type, keyed);
            using var other = provider.CreateScope();
            var acrossScopes = Resolve(other.ServiceProvider, type, keyed);
            nativeChoice ??= first.Selected;
            Assert.AreEqual(nativeChoice, first.Selected, mode);
            Assert.AreEqual(0, counts.Rejected, "Rejected candidates must not activate their dependencies.");
            if (lifetime == ServiceLifetime.Transient) Assert.AreNotSame(first, again);
            else Assert.AreSame(first, again);
            if (lifetime == ServiceLifetime.Singleton) Assert.AreSame(first, acrossScopes);
            else Assert.AreNotSame(first, acrossScopes);
        }
    }

    public static IEnumerable<object[]> AmbiguousCases()
    {
        foreach (var type in new[] { typeof(DifferentParameters), typeof(EqualLengthDifferentParameters) })
        foreach (var lifetime in new[] { ServiceLifetime.Transient, ServiceLifetime.Scoped, ServiceLifetime.Singleton })
        foreach (var keyed in new[] { false, true })
        foreach (var validate in new[] { false, true })
            yield return [type, lifetime, keyed, validate];
    }

    [TestMethod]
    [DynamicData(nameof(AmbiguousCases))]
    public void OrdinaryTypeRegistrationsRejectNativeAmbiguityBeforeActivation(Type type, ServiceLifetime lifetime, bool keyed, bool validate)
    {
        foreach (var mode in new[] { "native", "disabled", "enabled" })
        {
            var counts = new Counts();
            var services = Services(counts);
            Add(services, type, lifetime, keyed);
            if (validate || mode == "enabled")
            {
                var error = Assert.ThrowsExactly<AggregateException>(() => Build(services, mode, validate));
                StringAssert.Contains(error.ToString(), "ambiguous");
            }
            else
            {
                using var provider = Build(services, mode, validate);
                using var scope = provider.CreateScope();
                var error = Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(scope.ServiceProvider, type, keyed), mode);
                StringAssert.Contains(error.Message, "ambiguous");
            }
            Assert.AreEqual(0, counts.Dependencies, mode);
            Assert.AreEqual(0, counts.Constructors, mode);
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void ContextualAttributesPreserveNativeSelectionAndRequestedKey(bool wildcard, bool validate)
    {
        foreach (var mode in new[] { "native", "disabled", "enabled" })
        {
            var services = new ServiceCollection();
            var ordinary = new A();
            var inherited = new A();
            var explicitKey = new A();
            services.AddSingleton(ordinary);
            services.AddKeyedSingleton("blue", inherited);
            services.AddKeyedSingleton("fixed", explicitKey);
            services.AddKeyedTransient<Contextual>(wildcard ? KeyedService.AnyKey : "blue");
            var requestedKey = new string("blue".ToCharArray());
            using var native = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = validate || mode == "enabled" });
            var nativeKey = native.GetRequiredKeyedService<Contextual>(requestedKey).Key;
            using var provider = Build(services, mode, validate);
            var result = provider.GetRequiredKeyedService<Contextual>(requestedKey);
            Assert.AreEqual(requestedKey, result.Key, mode);
            Assert.AreSame(nativeKey, result.Key, mode);
            Assert.AreSame(inherited, result.Inherited, mode);
            Assert.AreSame(explicitKey, result.Explicit, mode);
            Assert.AreSame(ordinary, result.Ordinary, mode);
            Assert.AreEqual("long", result.Selected, mode);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UnkeyedServiceKeyAttributeUsesOrdinaryResolution(bool validate)
    {
        foreach (var mode in new[] { "native", "disabled", "enabled" })
        {
            var services = new ServiceCollection();
            services.AddSingleton("ordinary");
            services.AddTransient<UnkeyedContext>();
            using var provider = Build(services, mode, validate);
            Assert.AreEqual("ordinary", provider.GetRequiredService<UnkeyedContext>().Key, mode);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OptionalValuesAndRegisteredNullResultsMatchNative(bool keyed)
    {
        foreach (var mode in new[] { "native", "disabled", "enabled" })
        {
            IServiceCollection services = new ServiceCollection();
            services.AddTransient<A>(_ => null!);
            services.AddKeyedTransient<A>("missing", (_, _) => null!);
            Add(services, typeof(DefaultValues), ServiceLifetime.Transient, keyed);
            using var provider = Build(services, mode, validate: true);
            var result = (DefaultValues)Resolve(provider, typeof(DefaultValues), keyed);
            Assert.IsNull(result.Ordinary, mode);
            Assert.IsNull(result.Keyed, mode);
            Assert.AreEqual(7, result.Count, mode);
            Assert.AreEqual(default(DateTime), result.Date, mode);
            Assert.AreEqual(DayOfWeek.Friday, result.Day, mode);
            Assert.IsNull(result.Optional, mode);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DisposableTypePathsKeepSelectionAndDisposalOwnership(bool keyed)
    {
        foreach (var mode in new[] { "native", "disabled", "enabled" })
        {
            var counts = new Counts();
            var services = Services(counts);
            Add(services, typeof(DisposablePreferred), ServiceLifetime.Transient, keyed);
            DisposablePreferred result;
            using (var provider = Build(services, mode, validate: true))
            {
                using (var scope = provider.CreateScope())
                {
                    result = (DisposablePreferred)Resolve(scope.ServiceProvider, typeof(DisposablePreferred), keyed);
                    Assert.AreEqual("AB", result.Selected, mode);
                    Assert.AreEqual(0, result.Disposals, mode);
                }
                Assert.AreEqual(1, result.Disposals, mode);
            }
            Assert.AreEqual(1, result.Disposals, mode);
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void NonemptyDependsOnRetainsItsPreferredConstructorContract(bool keyed, bool diagnostics)
    {
        var services = Services(new Counts());
        Dependency[] map = [Dependency.OnValue("a", new A())];
        if (keyed) services.AddKeyedTransient<Preferred>("blue", map);
        else services.AddTransient<Preferred>(map);
        using var provider = Build(services, diagnostics ? "enabled" : "disabled", validate: true);
        Assert.AreEqual("A", Resolve(provider, typeof(Preferred), keyed).Selected);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void KeyedBuiltInsRequireAnExplicitRegistration(bool registered)
    {
        foreach (var mode in new[] { "native", "disabled", "enabled" })
        {
            var services = new ServiceCollection();
            var explicitProvider = new EmptyProvider();
            if (registered) services.AddKeyedSingleton<IServiceProvider>("blue", explicitProvider);
            services.AddKeyedTransient<KeyedBuiltIn>("blue");
            using var provider = Build(services, mode, validate: false);
            var result = provider.GetRequiredKeyedService<KeyedBuiltIn>("blue");
            Assert.AreEqual(registered ? "provider" : "empty", result.Selected, mode);
            Assert.AreSame(registered ? explicitProvider : null, result.Provider, mode);
        }
    }

    [TestMethod]
    public void InheritedOpenGenericWildcardDependencyParticipatesInSelection()
    {
        foreach (var mode in new[] { "native", "disabled", "enabled" })
        {
            var services = new ServiceCollection();
            services.AddKeyedTransient(typeof(IOpen<>), KeyedService.AnyKey, typeof(Open<>));
            services.AddKeyedTransient<InheritedGeneric>("blue");
            using var provider = Build(services, mode, validate: true);
            Assert.AreEqual("generic", provider.GetRequiredKeyedService<InheritedGeneric>("blue").Selected, mode);
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void UnselectedGenericCandidatesValidateConstraintsWithoutActivation(bool wildcard, bool closedOverride)
    {
        foreach (var mode in new[] { "native", "disabled", "enabled" })
        {
            var counts = new Counts();
            var services = Services(counts);
            services.AddTransient<IOpen<int>>(_ => { counts.Dependencies++; return new Open<int>(); });
            services.AddKeyedTransient(typeof(IOpen<>), wildcard ? KeyedService.AnyKey : "bad", typeof(ReferenceOnly<>));
            if (closedOverride) services.AddKeyedTransient<IOpen<int>, Open<int>>("bad");
            services.AddTransient<ConstrainedCandidate>();
            if (!closedOverride && mode == "enabled")
            {
                var error = Assert.ThrowsExactly<AggregateException>(() => Build(services, mode, validate: true));
                StringAssert.Contains(error.ToString(), nameof(ReferenceOnly<object>));
                Assert.AreEqual(0, counts.Dependencies, mode);
                Assert.AreEqual(0, counts.Constructors, mode);
                continue;
            }
            using var provider = Build(services, mode, validate: false);
            if (closedOverride)
                Assert.AreEqual("long", provider.GetRequiredService<ConstrainedCandidate>().Selected, mode);
            else
            {
                Assert.ThrowsExactly<ArgumentException>(() => provider.GetRequiredService<ConstrainedCandidate>(), mode);
                Assert.AreEqual(0, counts.Dependencies, mode);
                Assert.AreEqual(0, counts.Constructors, mode);
            }
        }
    }

    [TestMethod]
    public void OptionalStructDefaultsDoNotRunUserDefinedConstructors()
    {
        foreach (var mode in new[] { "native", "disabled", "enabled" })
        {
            var services = new ServiceCollection();
            services.AddTransient<StructDefault>();
            using var provider = Build(services, mode, validate: false);
            Assert.AreEqual(0, provider.GetRequiredService<StructDefault>().Value.Marker, mode);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ConstructorExceptionsRetainTheirOriginalType(bool keyed)
    {
        foreach (var mode in new[] { "native", "disabled", "enabled" })
        {
            IServiceCollection services = new ServiceCollection();
            Add(services, typeof(Throwing), ServiceLifetime.Transient, keyed);
            using var provider = Build(services, mode, validate: false);
            Assert.ThrowsExactly<ArgumentException>(() => Resolve(provider, typeof(Throwing), keyed), mode);
        }
    }

    private static ServiceCollection Services(Counts counts)
    {
        var services = new ServiceCollection();
        services.AddSingleton(counts);
        services.AddTransient<A>(_ => { counts.Dependencies++; return new A(); });
        services.AddTransient<B>(_ => { counts.Dependencies++; return new B(); });
        services.AddTransient<C>(_ => { counts.Dependencies++; return new C(); });
        services.AddTransient<Rejected>(_ => { counts.Rejected++; return new Rejected(); });
        return services;
    }

    private static void Add(IServiceCollection services, Type type, ServiceLifetime lifetime, bool keyed)
        => services.Add(keyed ? ServiceDescriptor.DescribeKeyed(type, "blue", type, lifetime)
            : ServiceDescriptor.Describe(type, type, lifetime));

    private static IChoice Resolve(IServiceProvider provider, Type type, bool keyed)
        => (IChoice)(keyed ? provider.GetRequiredKeyedService(type, "blue") : provider.GetRequiredService(type));

    private static ServiceProvider Build(IServiceCollection services, string mode, bool validate)
        => mode == "native" ? services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = validate })
            : ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
            { DetectIncorrectUsageOfTransientDisposables = mode == "enabled", ValidateOnBuild = validate || mode == "enabled" });

    public interface IChoice { string Selected { get; } }
    public sealed class Counts { public int Dependencies; public int Rejected; public int Constructors; }
    public sealed class A { }
    public sealed class B { }
    public sealed class C { }
    public sealed class Missing { }
    public sealed class Rejected { }
    public sealed class Preferred : IChoice
    {
        public string Selected { get; }
        [ActivatorUtilitiesConstructor] public Preferred(A a) => Selected = "A";
        public Preferred(A a, B b) => Selected = "AB";
    }
    public sealed class MultiplePreferred : IChoice
    {
        public string Selected { get; }
        [ActivatorUtilitiesConstructor] public MultiplePreferred(A a) => Selected = "A";
        [ActivatorUtilitiesConstructor] public MultiplePreferred(A a, B b) => Selected = "AB";
    }
    public sealed class UnavailablePreferred : IChoice
    {
        public string Selected { get; }
        [ActivatorUtilitiesConstructor] public UnavailablePreferred(Missing missing) => Selected = "missing";
        public UnavailablePreferred(A a) => Selected = "A";
    }
    public sealed class Permuted : IChoice
    {
        public string Selected { get; }
        public Permuted(A a, B b) => Selected = "AB";
        public Permuted(B b, A a) => Selected = "BA";
    }
    public sealed class Subset : IChoice
    {
        public string Selected { get; }
        public Subset(A a) => Selected = "A";
        public Subset(A a, B b) => Selected = "AB";
    }
    public sealed class Repeated : IChoice
    {
        public string Selected { get; }
        public Repeated(A a) => Selected = "A";
        public Repeated(A first, A second) => Selected = "AA";
    }
    public sealed class Optional : IChoice
    {
        public string Selected { get; }
        public Optional(A a) => Selected = "A";
        public Optional(A a, int count = 7) => Selected = "A:" + count;
    }
    public sealed class RejectLonger : IChoice
    {
        public string Selected { get; }
        public RejectLonger(A a) => Selected = "A";
        public RejectLonger(Rejected rejected, Missing missing) => Selected = "missing";
    }
    public sealed class DifferentParameters : IChoice
    {
        public string Selected => "unused";
        public DifferentParameters(Counts counts, A a, B b) => counts.Constructors++;
        public DifferentParameters(Counts counts, C c) => counts.Constructors++;
    }
    public sealed class EqualLengthDifferentParameters : IChoice
    {
        public string Selected => "unused";
        [ActivatorUtilitiesConstructor] public EqualLengthDifferentParameters(Counts counts, A a) => counts.Constructors++;
        public EqualLengthDifferentParameters(Counts counts, B b) => counts.Constructors++;
    }
    public sealed class Contextual : IChoice
    {
        public string Selected { get; }
        public object Key { get; }
        public A? Inherited { get; }
        public A? Explicit { get; }
        public A? Ordinary { get; }
        [ActivatorUtilitiesConstructor]
        public Contextual([ServiceKey] object key) { Key = key; Selected = "short"; }
        public Contextual([ServiceKey] object key, [FromKeyedServices] A inherited,
            [FromKeyedServices("fixed")] A explicitKey, [FromKeyedServices(null)] A ordinary)
        { Key = key; Inherited = inherited; Explicit = explicitKey; Ordinary = ordinary; Selected = "long"; }
    }
    public sealed class UnkeyedContext([ServiceKey] string key) { public string Key { get; } = key; }
    public sealed class DefaultValues(A? ordinary, [FromKeyedServices("missing")] A? keyed,
        int count = 7, DateTime date = default, DayOfWeek? day = DayOfWeek.Friday, Missing? optional = null) : IChoice
    {
        public string Selected => "defaults";
        public A? Ordinary { get; } = ordinary;
        public A? Keyed { get; } = keyed;
        public int Count { get; } = count;
        public DateTime Date { get; } = date;
        public DayOfWeek? Day { get; } = day;
        public Missing? Optional { get; } = optional;
    }
    public sealed class DisposablePreferred : IChoice, IDisposable
    {
        public string Selected { get; }
        public int Disposals { get; private set; }
        [ActivatorUtilitiesConstructor] public DisposablePreferred(A a) => Selected = "A";
        public DisposablePreferred(A a, B b) => Selected = "AB";
        public void Dispose() => Disposals++;
    }
    public sealed class EmptyProvider : IServiceProvider { public object? GetService(Type serviceType) => null; }
    public sealed class KeyedBuiltIn : IChoice
    {
        public string Selected { get; }
        public IServiceProvider? Provider { get; }
        public KeyedBuiltIn() => Selected = "empty";
        public KeyedBuiltIn([FromKeyedServices("blue")] IServiceProvider provider)
        { Provider = provider; Selected = "provider"; }
    }
    public interface IOpen<T> { }
    public sealed class Open<T> : IOpen<T> { }
    public sealed class InheritedGeneric : IChoice
    {
        public string Selected { get; }
        public InheritedGeneric() => Selected = "empty";
        public InheritedGeneric([FromKeyedServices] IOpen<int> dependency) => Selected = "generic";
    }
    public sealed class ReferenceOnly<T> : IOpen<T> where T : class { }
    public sealed class ConstrainedCandidate : IChoice
    {
        public string Selected { get; }
        public ConstrainedCandidate(Counts counts, IOpen<int> generic, B b)
        { counts.Constructors++; Selected = "long"; }
        public ConstrainedCandidate(Counts counts, [FromKeyedServices("bad")] IOpen<int> generic)
        { counts.Constructors++; Selected = "short"; }
    }
    public readonly struct CustomDefault
    {
        public int Marker { get; }
        public CustomDefault() => Marker = 42;
    }
    public sealed class StructDefault(CustomDefault value = default) { public CustomDefault Value { get; } = value; }
    public sealed class Throwing : IChoice
    {
        public string Selected => "unused";
        public Throwing() => throw new ArgumentException("constructor failed");
    }
}
